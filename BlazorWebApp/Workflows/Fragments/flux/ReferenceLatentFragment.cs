using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// Fragment that processes a reference image for Flux2 image editing workflows.
/// Per image: LoadImage -> ImageScaleToTotalPixels -> VAEEncode -> ReferenceLatent(pos) + ReferenceLatent(neg).
/// Each call chains by overwriting positive_output and negative_output with reference-wrapped conditioning.
/// The first image also registers image_size_width and image_size_height for downstream use.
/// </summary>
public class ReferenceLatentFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "reference_latent",
        Type = FragmentType.Utility,
        Title = "Reference Latent",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for a single reference image in the chain.
    /// </summary>
    public class Parameters
    {
        public string Image { get; set; } = "";
        public int Index { get; set; }
        public string UpscaleMethod { get; set; } = "lanczos";
        public double Megapixels { get; set; } = 1;
        public int ResolutionSteps { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // Not used directly from GenerationParameters; called with explicit Parameters from workflow.
    }

    /// <summary>
    /// Builds a single reference image in the chain.
    /// Reads positive_output and negative_output, wraps them with ReferenceLatent, and overwrites.
    /// On the first call (index 0), also registers image_size_width and image_size_height.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        var i = fragmentParams.Index;
        var loaderId = $"ref_image_loader_{i}";
        var scaleId = $"ref_image_scale_{i}";
        var vaeEncodeId = $"ref_vae_encode_{i}";
        var refPositiveId = $"ref_latent_positive_{i}";
        var refNegativeId = $"ref_latent_negative_{i}";

        var vaeRef = registry.GetRef("vae_output");
        var positiveRef = registry.GetRef("positive_output");
        var negativeRef = registry.GetRef("negative_output");

        // LoadImage
        builder.AddNode(loaderId, node => node
            .Type("LoadImage")
            .Title($"Load Reference Image {i + 1}")
            .Input("image", fragmentParams.Image));

        // ImageScaleToTotalPixels
        builder.AddNode(scaleId, node => node
            .Type("ImageScaleToTotalPixels")
            .Title($"Scale Reference Image {i + 1}")
            .Input("upscale_method", fragmentParams.UpscaleMethod)
            .Input("megapixels", fragmentParams.Megapixels)
            .Input("resolution_steps", fragmentParams.ResolutionSteps)
            .InputFromNode("image", loaderId, 0));

        // On the first reference image, register GetImageSize outputs for EmptyLatent and Scheduler
        if (i == 0)
        {
            var getSizeId = "ref_get_image_size";
            builder.AddNode(getSizeId, node => node
                .Type("GetImageSize")
                .Title("Get Image Size")
                .InputFromNode("image", scaleId, 0));

            registry.Register("image_size_width", getSizeId, 0);
            registry.Register("image_size_height", getSizeId, 1);
        }

        // VAEEncode
        builder.AddNode(vaeEncodeId, node => node
            .Type("VAEEncode")
            .Title($"VAE Encode Reference {i + 1}")
            .InputFromNode("pixels", scaleId, 0)
            .InputRef("vae", vaeRef));

        // ReferenceLatent for positive conditioning
        builder.AddNode(refPositiveId, node => node
            .Type("ReferenceLatent")
            .Title($"Reference Latent Positive {i + 1}")
            .InputRef("conditioning", positiveRef)
            .InputFromNode("latent", vaeEncodeId, 0));

        // ReferenceLatent for negative conditioning
        builder.AddNode(refNegativeId, node => node
            .Type("ReferenceLatent")
            .Title($"Reference Latent Negative {i + 1}")
            .InputRef("conditioning", negativeRef)
            .InputFromNode("latent", vaeEncodeId, 0));

        // Overwrite conditioning outputs so the next image or sampler chains from these
        registry.Register("positive_output", refPositiveId, 0);
        registry.Register("negative_output", refNegativeId, 0);
    }
}
