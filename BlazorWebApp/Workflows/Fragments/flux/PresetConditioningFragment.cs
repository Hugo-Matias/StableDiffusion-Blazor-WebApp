using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// Blends anchor instructions into the positive conditioning using ConditioningAverage.
///
/// Roles (inverted from naive "base + instructions" approach):
///   conditioning_to   = main positive prompt (already encoded by PromptsFragment)
///   conditioning_from = anchor instructions encoded alone
///
/// When preset_strength = 1.0: 100% main prompt; instructions have zero effect (default).
/// When preset_strength = 0.5: 50/50 blend of main prompt and anchor instructions.
/// When preset_strength = 0.0: 100% anchor instructions only.
/// Optional timing limits the blend to a sampling window and keeps the normal prompt outside it.
///
/// This keeps users in full control of the main prompt. Reducing strength pulls the
/// conditioning toward the anchor without changing the prompt field or generation params.
///
/// Overrides registry positive_output so downstream nodes automatically use the blend.
///
/// Usage in a workflow Build():
///   1. Build PromptsFragment first (registers positive_output from main text).
///   2. Call PresetConditioningFragment.Build(). Empty preset_text is a no-op only at the neutral strength.
/// </summary>
public class PresetConditioningFragment : IFragmentBuilder
{
    public const string TimingFull = "Full";
    public const string TimingEarly = "Early";
    public const string TimingMiddle = "Middle";
    public const string TimingLate = "Late";
    public const string TimingCustom = "Custom";

    public FragmentMetadata Metadata => new()
    {
        Id = "preset_conditioning",
        Type = FragmentType.Conditioning,
        Title = "Conditioning Control",
        Icon = "fa-solid fa-sliders",
        Collapsible = true,
        Description = "Control how strongly the main prompt, anchor instructions, and optional timing shape the positive conditioning.",
        Order = 55,
        Component = "PresetConditioningForm",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "preset_text",
                Label = "Anchor Instructions",
                Type = ParameterType.TextArea,
                DefaultValue = ""
            },
            new FragmentParameter
            {
                Name = "preset_strength",
                Label = "Prompt Strength",
                Type = ParameterType.Slider,
                DefaultValue = 1.0,
                Min = 0.0,
                Max = 1.0,
                Step = 0.01
            },
            new FragmentParameter
            {
                Name = "timing_mode",
                Label = "Timing",
                Type = ParameterType.Select,
                DefaultValue = TimingFull,
                Options = [TimingFull, TimingEarly, TimingMiddle, TimingLate, TimingCustom]
            },
            new FragmentParameter
            {
                Name = "timing_start",
                Label = "Start",
                Type = ParameterType.Slider,
                DefaultValue = 0.0,
                Min = 0.0,
                Max = 1.0,
                Step = 0.01
            },
            new FragmentParameter
            {
                Name = "timing_end",
                Label = "End",
                Type = ParameterType.Slider,
                DefaultValue = 1.0,
                Min = 0.0,
                Max = 1.0,
                Step = 0.01
            }
        ]
    };

    /// <summary>
    /// Standard fragment build entry. Reads parameters directly from the fragment data.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (fragment?.IsActive != true) return;

        var presetText = fragment.GetString("preset_text", "");
        var presetStrength = fragment.GetDouble("preset_strength", 1.0);
        var timingMode = fragment.GetString("timing_mode", TimingFull);
        var timingStart = fragment.GetDouble("timing_start", 0.0);
        var timingEnd = fragment.GetDouble("timing_end", 1.0);

        BuildInternal(builder, registry, presetText, presetStrength, timingMode, timingStart, timingEnd);
    }

    /// <summary>
    /// Explicit build overload used by the workflow when it has already resolved the text values.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string presetText,
        double presetStrength,
        string timingMode = TimingFull,
        double timingStart = 0.0,
        double timingEnd = 1.0)
    {
        BuildInternal(builder, registry, presetText, presetStrength, timingMode, timingStart, timingEnd);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string presetText,
        double presetStrength,
        string timingMode,
        double timingStart,
        double timingEnd)
    {
        presetStrength = Math.Clamp(presetStrength, 0.0, 1.0);

        if (Math.Abs(presetStrength - 1.0) < 0.0001) return;

        var clipRef = registry.GetRef("clip_output");
        var mainConditioningRef = registry.GetRef("positive_output");

        builder.AddNode("instructions_encode", node => node
            .Type("CLIPTextEncode")
            .Title(string.IsNullOrWhiteSpace(presetText)
                ? "CLIP Text Encode - Null Anchor"
                : "CLIP Text Encode - Anchor Instructions")
            .Input("text", presetText ?? "")
            .InputRef("clip", clipRef));

        // Blend: conditioning_to = main prompt, conditioning_from = anchor instructions
        // conditioning_to_strength = 1.0 means 100% main prompt; 0.0 means 100% anchor only.
        builder.AddNode("preset_blend", node => node
            .Type("ConditioningAverage")
            .Title("Prompt + Anchor Blend")
            .InputRef("conditioning_to", mainConditioningRef)
            .InputRef("conditioning_from", ("instructions_encode", 0))
            .Input("conditioning_to_strength", presetStrength));

        var (start, end) = ResolveTimingRange(timingMode, timingStart, timingEnd);
        if (IsFullRange(start, end))
        {
            registry.Register("positive_output", "preset_blend", 0);
            return;
        }

        RegisterTimedBlend(builder, registry, mainConditioningRef, ("preset_blend", 0), start, end);
    }

    private static (double Start, double End) ResolveTimingRange(
        string timingMode,
        double timingStart,
        double timingEnd)
    {
        return timingMode switch
        {
            TimingEarly => (0.0, 0.35),
            TimingMiddle => (0.25, 0.75),
            TimingLate => (0.65, 1.0),
            TimingCustom => NormalizeRange(timingStart, timingEnd),
            _ => (0.0, 1.0)
        };
    }

    private static (double Start, double End) NormalizeRange(double start, double end)
    {
        start = Math.Clamp(start, 0.0, 1.0);
        end = Math.Clamp(end, 0.0, 1.0);

        if (end < start)
        {
            (start, end) = (end, start);
        }

        if (Math.Abs(end - start) < 0.001)
        {
            end = Math.Min(1.0, start + 0.001);
            if (Math.Abs(end - start) < 0.001)
            {
                start = Math.Max(0.0, end - 0.001);
            }
        }

        return (start, end);
    }

    private static bool IsFullRange(double start, double end)
    {
        return start <= 0.0001 && end >= 0.9999;
    }

    private static void RegisterTimedBlend(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        (string nodeId, int outputIndex) mainConditioningRef,
        (string nodeId, int outputIndex) blendedConditioningRef,
        double start,
        double end)
    {
        builder.AddNode("preset_blend_range", node => node
            .Type("ConditioningSetTimestepRange")
            .Title("Conditioning Control - Timed Blend")
            .Input("start", start)
            .Input("end", end)
            .InputRef("conditioning", blendedConditioningRef));

        var activeRef = (nodeId: "preset_blend_range", outputIndex: 0);

        if (start > 0.0001)
        {
            builder.AddNode("preset_main_before_range", node => node
                .Type("ConditioningSetTimestepRange")
                .Title("Conditioning Control - Main Before")
                .Input("start", 0.0)
                .Input("end", start)
                .InputRef("conditioning", mainConditioningRef));

            builder.AddNode("preset_timed_combine_before", node => node
                .Type("ConditioningCombine")
                .Title("Conditioning Control - Combine Before")
                .InputRef("conditioning_1", ("preset_main_before_range", 0))
                .InputRef("conditioning_2", activeRef));

            activeRef = ("preset_timed_combine_before", 0);
        }

        if (end < 0.9999)
        {
            builder.AddNode("preset_main_after_range", node => node
                .Type("ConditioningSetTimestepRange")
                .Title("Conditioning Control - Main After")
                .Input("start", end)
                .Input("end", 1.0)
                .InputRef("conditioning", mainConditioningRef));

            builder.AddNode("preset_timed_combine_after", node => node
                .Type("ConditioningCombine")
                .Title("Conditioning Control - Combine After")
                .InputRef("conditioning_1", activeRef)
                .InputRef("conditioning_2", ("preset_main_after_range", 0)));

            activeRef = ("preset_timed_combine_after", 0);
        }

        registry.Register("positive_output", activeRef.nodeId, activeRef.outputIndex);
    }
}
