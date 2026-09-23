using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// UI-only fragment exposing resolution (Manual or Aspect Ratio + Megapixels) and duration
/// settings for MiniMax H3. No ComfyUI nodes are created here - the workflow's Build() method
/// reads these values directly and, when a first/last frame source is present, derives the
/// aspect ratio from that image instead of the aspect_ratio dropdown (megapixels still applies).
/// </summary>
public class MiniMaxVideoSettingsFragment : IFragmentBuilder
{
    public Defaults DefaultValues { get; init; } = new();

    public class Defaults
    {
        public string ResolutionMode { get; init; } = "calculated";
        public string AspectRatio { get; init; } = "16:9 (Widescreen)";
        public double Megapixels { get; init; } = 0.45;
        public int MultipleOf { get; init; } = 32;
        public int Width { get; init; } = 1344;
        public int Height { get; init; } = 768;
        public double Duration { get; init; } = 10;
    }

    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_video_settings",
        Type = FragmentType.Latent,
        Title = "Resolution & Duration",
        Component = "MiniMaxVideoSettingsForm",
        Icon = "fa-solid fa-expand",
        Order = 20,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "resolution_mode",
                Label = "Resolution Mode",
                Type = ParameterType.Select,
                Options = ["manual", "calculated"],
                DefaultValue = DefaultValues.ResolutionMode
            },
            new FragmentParameter
            {
                Name = "aspect_ratio",
                Label = "Aspect Ratio",
                Type = ParameterType.Select,
                Options = BlazorWebApp.Models.ResolutionSelectorMath.AspectRatios,
                DefaultValue = DefaultValues.AspectRatio
            },
            new FragmentParameter
            {
                Name = "megapixels",
                Label = "Megapixels",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 5.0,
                Step = 0.1,
                DefaultValue = DefaultValues.Megapixels
            },
            new FragmentParameter
            {
                Name = "multiple_of",
                Label = "Multiple Of",
                Type = ParameterType.Slider,
                Min = 8,
                Max = 128,
                Step = 4,
                DefaultValue = DefaultValues.MultipleOf
            },
            new FragmentParameter
            {
                Name = "width",
                Label = "Width",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 4096,
                Step = 8,
                DefaultValue = DefaultValues.Width
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 4096,
                Step = 8,
                DefaultValue = DefaultValues.Height
            },
            new FragmentParameter
            {
                Name = "duration",
                Label = "Duration (seconds)",
                Type = ParameterType.Slider,
                Min = 0.5,
                Max = 15,
                Step = 0.5,
                DefaultValue = DefaultValues.Duration
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
        // UI-only fragment - values are consumed directly by the workflow's Build() method.
    }
}
