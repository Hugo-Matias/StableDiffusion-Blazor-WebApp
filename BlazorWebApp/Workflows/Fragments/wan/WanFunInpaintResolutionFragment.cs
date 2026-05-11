using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanFunInpaintResolutionFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_fun_inpaint_resolution";

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Latent,
        Title = "Resolution Ceiling",
        Component = "LatentForm",
        Icon = "fa-solid fa-expand",
        Order = 20,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "width",
                Label = "Width Ceiling",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 8,
                DefaultValue = Defaults.Width
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Height Ceiling",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 8,
                DefaultValue = Defaults.Height
            },
            new FragmentParameter
            {
                Name = "batch_size",
                Label = "Batch Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 4,
                Step = 1,
                DefaultValue = Defaults.BatchSize
            }
        ]
    };

    public class Parameters
    {
        public int Width { get; set; } = 640;
        public int Height { get; set; } = 640;
        public int BatchSize { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }
}
