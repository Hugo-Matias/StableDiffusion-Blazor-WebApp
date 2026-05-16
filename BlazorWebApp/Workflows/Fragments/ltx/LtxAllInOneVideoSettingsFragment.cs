using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxAllInOneVideoSettingsFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_all_in_one_video_settings",
        Type = FragmentType.Latent,
        Title = "Video Settings",
        Component = "LtxVideoSettingsForm",
        Icon = "fa-solid fa-video",
        Order = 30,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter { Name = "width", Label = "Width", Type = ParameterType.Slider, Min = 256, Max = 2048, Step = 32, DefaultValue = Defaults.Width },
            new FragmentParameter { Name = "height", Label = "Height", Type = ParameterType.Slider, Min = 256, Max = 2048, Step = 32, DefaultValue = Defaults.Height },
            new FragmentParameter { Name = "duration", Label = "Duration (seconds)", Type = ParameterType.Slider, Min = 1, Max = 20, Step = 1, DefaultValue = Defaults.Duration },
            new FragmentParameter { Name = "frame_rate", Label = "Frame Rate", Type = ParameterType.Slider, Min = 8, Max = 30, Step = 1, DefaultValue = Defaults.FrameRate },
            new FragmentParameter { Name = "img_compression", Label = "Image Compression", Type = ParameterType.Slider, Min = 0, Max = 100, Step = 1, DefaultValue = Defaults.ImgCompression },
            new FragmentParameter { Name = "i2v_strength", Label = "I2V Strength", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = Defaults.I2vStrength }
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
        public int Width { get; set; } = 1728;
        public int Height { get; set; } = 1152;
        public int Duration { get; set; } = 10;
        public int FrameRate { get; set; } = 25;
        public int ImgCompression { get; set; } = 0;
        public double I2vStrength { get; set; } = 1.0;
    }
}