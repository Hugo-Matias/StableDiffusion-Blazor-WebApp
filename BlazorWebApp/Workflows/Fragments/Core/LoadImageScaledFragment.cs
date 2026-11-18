using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads a source image and scales it to a target megapixel count.
/// Used by Img2Img workflows to normalize source image resolution.
/// Registers image_input in the node registry.
/// </summary>
public class LoadImageScaledFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_image_scaled",
        Type = FragmentType.Input,
        Title = "Load Image",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the load image scaled fragment.
    /// </summary>
    public class Parameters
    {
        public string Image { get; set; } = "";
        public string UpscaleMethod { get; set; } = "lanczos";
        public double Megapixels { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        BuildInternal(builder, registry,
            fragment?.GetString("image", "") ?? "",
            fragment?.GetString("upscale_method", "lanczos") ?? "lanczos",
            fragment?.GetDouble("megapixels", 1) ?? 1);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry,
            fragmentParams.Image,
            fragmentParams.UpscaleMethod,
            fragmentParams.Megapixels);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string image,
        string upscaleMethod,
        double megapixels)
    {
        builder.AddNode("image_loader", node => node
            .Type("LoadImage")
            .Title("Load Image")
            .Input("image", image));

        builder.AddNode("image_scale", node => node
            .Type("ImageScaleToTotalPixels")
            .Title("Scale Image")
            .Input("upscale_method", upscaleMethod)
            .Input("megapixels", megapixels)
            .InputFromNode("image", "image_loader", 0));

        registry.Register("image_input", "image_scale", 0);
    }
}
