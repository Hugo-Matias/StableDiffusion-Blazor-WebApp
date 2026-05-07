using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// UI-only settings for Flux2 Klein grid-reference scaling.
/// </summary>
public class FluxKleinGridRefSettingsFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "grid_ref_settings",
        Type = FragmentType.Settings,
        Title = "Grid References",
        Component = "FluxKleinGridRefSettingsForm",
        Icon = "fa-solid fa-images",
        Order = 20,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "grid_tile_megapixels",
                Label = "Grid Tile MP",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 4.0,
                Step = 0.1,
                DefaultValue = Defaults.GridTileMegapixels
            },
            new FragmentParameter
            {
                Name = "grid_megapixels",
                Label = "Grid MP",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 8.0,
                Step = 0.1,
                DefaultValue = Defaults.GridMegapixels
            },
            new FragmentParameter
            {
                Name = "base_megapixels",
                Label = "Base Image MP",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 4.0,
                Step = 0.1,
                DefaultValue = Defaults.BaseMegapixels
            }
        ]
    };

    public class Parameters
    {
        public double GridTileMegapixels { get; set; } = 1.0;
        public double GridMegapixels { get; set; } = 4.0;
        public double BaseMegapixels { get; set; } = 1.0;
        public int ResolutionSteps { get; set; } = 1;
        public string UpscaleMethod { get; set; } = "lanczos";
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