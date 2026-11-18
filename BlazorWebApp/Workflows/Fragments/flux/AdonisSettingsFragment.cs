using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// UI-only fragment exposing the Adonis upscale workflow's tunables:
/// input image scaling (megapixels + multiple-of) and per-LoRA strength + sampler
/// targeting (base / refine / both) for the two hardcoded Adonis LoRAs.
/// Parameters are consumed by the workflow's Build() method; no nodes are emitted here.
/// </summary>
public class AdonisSettingsFragment : IFragmentBuilder
{
    public const string TargetBase = "base";
    public const string TargetRefine = "refine";
    public const string TargetBoth = "both";

    public FragmentMetadata Metadata => new()
    {
        Id = "adonis_settings",
        Type = FragmentType.Settings,
        Title = "Adonis Settings",
        Component = "AdonisSettingsForm",
        Icon = "fa-solid fa-wand-magic-sparkles",
        Order = 20,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "megapixels",
                Label = "Output Megapixels",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 8.0,
                Step = 0.1,
                DefaultValue = 1.7
            },
            new FragmentParameter
            {
                Name = "multiple_of",
                Label = "Multiple Of",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 64,
                Step = 1,
                DefaultValue = 16
            },
            new FragmentParameter
            {
                Name = "adonis_base_strength",
                Label = "Adonis Base Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.05,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "adonis_base_target",
                Label = "Adonis Base Target",
                Type = ParameterType.Select,
                Options = [TargetBase, TargetRefine, TargetBoth],
                DefaultValue = TargetBase
            },
            new FragmentParameter
            {
                Name = "adonis_refine_strength",
                Label = "Adonis Refine Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.05,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "adonis_refine_target",
                Label = "Adonis Refine Target",
                Type = ParameterType.Select,
                Options = [TargetBase, TargetRefine, TargetBoth],
                DefaultValue = TargetRefine
            }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // UI-only fragment - parameters are consumed by the workflow's Build() method.
    }
}
