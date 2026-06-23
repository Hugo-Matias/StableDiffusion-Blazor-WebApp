using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxFrameGuidesFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_frame_guides",
        Type = FragmentType.Conditioning,
        Title = "Frame Guides",
        Component = "LtxFrameGuidesForm",
        Icon = "fa-solid fa-images",
        Order = 40,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter { Name = "first_strength", Label = "First Frame Strength", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = Defaults.FirstStrength },
            new FragmentParameter { Name = "last_strength", Label = "Last Frame Strength", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = Defaults.LastStrength }
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
        public double FirstStrength { get; set; } = 1.0;
        public double LastStrength { get; set; } = 1.0;
    }
}