using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// UI-only settings for the H3 Image Edit workflow: megapixel budget applied to every source image
/// before conditioning. The multiple-of-32 snap and "area" scale method are implied by the source
/// preset and not exposed. Build() is a no-op - the workflow reads the values directly.
/// </summary>
public class MiniMaxH3ImageSettingsFragment : IFragmentBuilder
{
    public Defaults DefaultValues { get; init; } = new();

    public class Defaults
    {
        public double Megapixels { get; init; } = 4.0;
    }

    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_h3_image_settings",
        // Latent type routes this UI-only fragment to the primary latent slot in Generate.razor,
        // which renders it flush (no expansion panel) immediately below the sources panel - same
        // mechanism as MiniMaxVideoSettingsFragment's "Resolution & Duration".
        Type = FragmentType.Latent,
        Title = "Resolution",
        Component = "MiniMaxH3ImageSettingsForm",
        Icon = "fa-solid fa-expand",
        Order = 20,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "megapixels",
                Label = "Megapixels",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 8.0,
                Step = 0.1,
                DefaultValue = DefaultValues.Megapixels
            }
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        // UI-only fragment; the workflow reads its values directly.
    }
}
