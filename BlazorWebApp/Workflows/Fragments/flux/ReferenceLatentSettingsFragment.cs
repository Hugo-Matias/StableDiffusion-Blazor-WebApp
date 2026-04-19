using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// UI-only fragment that exposes reference latent scaling settings.
/// Parameters are consumed by the workflow's Build() method.
/// No nodes are created by this fragment.
/// </summary>
public class ReferenceLatentSettingsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "reference_latent_settings",
        Type = FragmentType.Settings,
        Title = "Reference Image Settings",
        Component = "ReferenceLatentSettingsForm",
        Icon = "fa-solid fa-image",
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
                Max = 4.0,
                Step = 0.1,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "resolution_steps",
                Label = "Resolution Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 64,
                Step = 1,
                DefaultValue = 1
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
        // UI-only fragment - parameters are consumed by the workflow's Build() method
    }
}
