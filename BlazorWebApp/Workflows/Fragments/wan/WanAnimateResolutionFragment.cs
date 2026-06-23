using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// UI-only resolution ceiling for Wan Animate. The workflow uses the larger side
/// as the max longer edge and resolves final width/height from the reference image.
/// </summary>
public class WanAnimateResolutionFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_animate_resolution";

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
                DefaultValue = 832
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Height Ceiling",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 8,
                DefaultValue = 480
            },
            new FragmentParameter
            {
                Name = "batch_size",
                Label = "Batch Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 4,
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
    }
}