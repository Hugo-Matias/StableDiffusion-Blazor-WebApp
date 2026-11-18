using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenPoseTransferSettingsFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "pose_transfer_settings",
        Type = FragmentType.Settings,
        Title = "Pose Transfer",
        Component = "QwenPoseTransferSettingsForm",
        Icon = "fa-solid fa-person-running",
        Order = 30,
        Collapsible = true,
        Parameters =
        [
            new() { Name = "scale_length", Label = "Long Side", Type = ParameterType.Slider, Min = 512, Max = 2048, Step = 8, DefaultValue = Defaults.ScaleLength },
            new() { Name = "pose_batch_size", Label = "Pose Batch", Type = ParameterType.Number, Min = 1, Max = 64, Step = 1, DefaultValue = Defaults.PoseBatchSize },
            new() { Name = "draw_body", Label = "Draw Body", Type = ParameterType.Checkbox, DefaultValue = Defaults.DrawBody },
            new() { Name = "draw_hands", Label = "Draw Hands", Type = ParameterType.Checkbox, DefaultValue = Defaults.DrawHands },
            new() { Name = "draw_face", Label = "Draw Face", Type = ParameterType.Checkbox, DefaultValue = Defaults.DrawFace },
            new() { Name = "draw_feet", Label = "Draw Feet", Type = ParameterType.Checkbox, DefaultValue = Defaults.DrawFeet },
            new() { Name = "stick_width", Label = "Stick Width", Type = ParameterType.Slider, Min = 1, Max = 16, Step = 1, DefaultValue = Defaults.StickWidth },
            new() { Name = "face_point_size", Label = "Face Point Size", Type = ParameterType.Slider, Min = 1, Max = 16, Step = 1, DefaultValue = Defaults.FacePointSize },
            new() { Name = "score_threshold", Label = "Score Threshold", Type = ParameterType.Slider, Min = 0, Max = 1, Step = 0.01, DefaultValue = Defaults.ScoreThreshold },
            new() { Name = "lightning_lora_strength", Label = "Lightning LoRA", Type = ParameterType.Slider, Min = -2, Max = 2, Step = 0.05, DefaultValue = Defaults.LightningLoraStrength },
            new() { Name = "consistency_lora_strength", Label = "Consistency LoRA", Type = ParameterType.Slider, Min = -2, Max = 2, Step = 0.05, DefaultValue = Defaults.ConsistencyLoraStrength }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public class Parameters
    {
        public int ScaleLength { get; set; } = 1280;
        public int PoseBatchSize { get; set; } = 16;
        public bool DrawBody { get; set; } = true;
        public bool DrawHands { get; set; } = true;
        public bool DrawFace { get; set; }
        public bool DrawFeet { get; set; }
        public int StickWidth { get; set; } = 4;
        public int FacePointSize { get; set; } = 2;
        public double ScoreThreshold { get; set; } = 0.3;
        public double LightningLoraStrength { get; set; } = 1.0;
        public double ConsistencyLoraStrength { get; set; } = 0.6;
    }
}
