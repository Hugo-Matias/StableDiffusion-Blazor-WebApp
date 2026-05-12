using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanVaceClipJoinerSettingsFragment : IFragmentBuilder
{
    public const string FragmentId = "vace_clip_joiner_settings";
    public const string ContextFramesParameter = "context_frames";
    public const string ReplaceFramesParameter = "replace_frames";
    public const string NewFramesParameter = "new_frames";
    public const string HighSpeedLoraStrengthParameter = "high_speed_lora_strength";
    public const string LowSpeedLoraStrengthParameter = "low_speed_lora_strength";

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Settings,
        Title = "VACE Join Settings",
        Component = "WanVaceClipJoinerSettingsForm",
        Icon = "fa-solid fa-film",
        Order = 30,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = ContextFramesParameter,
                Label = "Context Frames",
                Type = ParameterType.Number,
                Min = 1,
                Max = 128,
                Step = 1,
                DefaultValue = Defaults.ContextFrames
            },
            new FragmentParameter
            {
                Name = ReplaceFramesParameter,
                Label = "Replace Frames",
                Type = ParameterType.Number,
                Min = 0,
                Max = 128,
                Step = 1,
                DefaultValue = Defaults.ReplaceFrames
            },
            new FragmentParameter
            {
                Name = NewFramesParameter,
                Label = "New Frames",
                Type = ParameterType.Number,
                Min = 0,
                Max = 128,
                Step = 1,
                DefaultValue = Defaults.NewFrames
            },
            new FragmentParameter
            {
                Name = HighSpeedLoraStrengthParameter,
                Label = "High Speed LoRA Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.05,
                DefaultValue = Defaults.HighSpeedLoraStrength
            },
            new FragmentParameter
            {
                Name = LowSpeedLoraStrengthParameter,
                Label = "Low Speed LoRA Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.05,
                DefaultValue = Defaults.LowSpeedLoraStrength
            }
        ]
    };

    public class Parameters
    {
        public int ContextFrames { get; set; } = 8;
        public int ReplaceFrames { get; set; } = 8;
        public int NewFrames { get; set; }
        public double HighSpeedLoraStrength { get; set; } = 1;
        public double LowSpeedLoraStrength { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        Build(builder, registry, new Parameters
        {
            ContextFrames = fragment?.GetInt(ContextFramesParameter, Defaults.ContextFrames) ?? Defaults.ContextFrames,
            ReplaceFrames = fragment?.GetInt(ReplaceFramesParameter, Defaults.ReplaceFrames) ?? Defaults.ReplaceFrames,
            NewFrames = fragment?.GetInt(NewFramesParameter, Defaults.NewFrames) ?? Defaults.NewFrames,
            HighSpeedLoraStrength = fragment?.GetDouble(HighSpeedLoraStrengthParameter, Defaults.HighSpeedLoraStrength) ?? Defaults.HighSpeedLoraStrength,
            LowSpeedLoraStrength = fragment?.GetDouble(LowSpeedLoraStrengthParameter, Defaults.LowSpeedLoraStrength) ?? Defaults.LowSpeedLoraStrength
        }, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}vace_prep";

        builder.AddNode(nodeId, node => node
            .Type("WanVACEPrep")
            .Title($"{scopeTitle}Wan VACE Prep")
            .InputRef("video_1", registry.GetRef("video_1_images"))
            .InputRef("video_2", registry.GetRef("video_2_images"))
            .Input("context_frames", fragmentParams.ContextFrames)
            .Input("replace_frames", fragmentParams.ReplaceFrames)
            .Input("new_frames", fragmentParams.NewFrames));

        registry.Register("control_video", nodeId, 0);
        registry.Register("control_mask", nodeId, 1);
        registry.Register("width", nodeId, 2);
        registry.Register("height", nodeId, 3);
        registry.Register("length", nodeId, 4);
        registry.Register("start_images", nodeId, 5);
        registry.Register("end_images", nodeId, 6);
    }
}