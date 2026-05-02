using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that removes the background from the generated image and outputs a transparent PNG.
/// Requires the comfyui-inspyrenet-rembg custom node pack to be installed in ComfyUI.
/// Overwrites image_output in the node registry with the RGBA result.
/// Conditional: Only builds when background_removal.IsActive is true.
/// </summary>
public class BackgroundRemovalFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "background_removal",
        Type = FragmentType.Enhancement,
        Title = "Remove Background (Inspyrenet)",
        Icon = "fa-solid fa-eraser",
        Order = 95,
        Collapsible = true,
        DefaultCollapsed = true,
        Component = "BackgroundRemovalForm",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "torchscript_jit",
                Label = "TorchScript JIT",
                Type = ParameterType.Select,
                Options = ["default", "on"],
                DefaultValue = "default"
            }
        ]
    };

    public class Parameters
    {
        public string TorchscriptJit { get; init; } = "default";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        if (fragment?.IsActive != true)
            return;

        var p = new Parameters
        {
            TorchscriptJit = fragment.GetString("torchscript_jit", "default") ?? "default"
        };

        BuildInternal(builder, registry, p);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        var imageRef = registry.GetRef("image_output");

        builder.AddNode("background_removal", node => node
            .Type("InspyrenetRembg")
            .Title("Background Removal")
            .Input("torchscript_jit", p.TorchscriptJit)
            .InputRef("image", imageRef));

        // Output 0 is the RGBA image with background removed.
        // SaveImage handles 4-channel tensors natively and writes transparent PNG.
        registry.Register("image_output", "background_removal", 0);
    }
}
