using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// Applies the Flux2 Klein grid-reference latent wiring.
/// With a base image, the base latent anchors positive and negative conditioning, then the grid reference wraps positive conditioning.
/// Without a base image, the grid reference wraps positive conditioning only and faux resolution is supplied by the workflow latent fragment.
/// </summary>
public class FluxKleinGridRefConditioningFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "grid_ref_conditioning",
        Type = FragmentType.Utility,
        Title = "Grid Reference Conditioning",
        IsHidden = true
    };

    public class Parameters
    {
        public string? BaseImage { get; init; }
        public double BaseMegapixels { get; init; } = 1.0;
        public string UpscaleMethod { get; init; } = "lanczos";
        public int ResolutionSteps { get; init; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public bool Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        var hasBaseImage = !string.IsNullOrWhiteSpace(fragmentParams.BaseImage);
        var vaeRef = registry.GetRef("vae_output");

        builder.AddNode("negative_zeroed", node => node
            .Type("ConditioningZeroOut")
            .Title("Negative (zeroed positive)")
            .InputRef("conditioning", registry.GetRef("positive_output")));
        registry.Register("negative_output", "negative_zeroed", 0);

        if (hasBaseImage)
        {
            builder.AddNode("base_image_loader", node => node
                .Type("LoadImage")
                .Title("Load Base Image")
                .Input("image", fragmentParams.BaseImage!));

            builder.AddNode("base_image_scale", node => node
                .Type("ImageScaleToTotalPixels")
                .Title("Scale Base Image")
                .Input("upscale_method", fragmentParams.UpscaleMethod)
                .Input("megapixels", fragmentParams.BaseMegapixels)
                .Input("resolution_steps", fragmentParams.ResolutionSteps)
                .InputFromNode("image", "base_image_loader", 0));

            builder.AddNode("base_get_image_size", node => node
                .Type("GetImageSize")
                .Title("Get Base Image Size")
                .InputFromNode("image", "base_image_scale", 0));
            registry.Register("base_image_width", "base_get_image_size", 0);
            registry.Register("base_image_height", "base_get_image_size", 1);

            builder.AddNode("base_vae_encode", node => node
                .Type("VAEEncode")
                .Title("VAE Encode Base Image")
                .InputFromNode("pixels", "base_image_scale", 0)
                .InputRef("vae", vaeRef));

            builder.AddNode("base_ref_positive", node => node
                .Type("ReferenceLatent")
                .Title("Reference Latent Base Positive")
                .InputRef("conditioning", registry.GetRef("positive_output"))
                .InputFromNode("latent", "base_vae_encode", 0));
            registry.Register("positive_output", "base_ref_positive", 0);

            builder.AddNode("base_ref_negative", node => node
                .Type("ReferenceLatent")
                .Title("Reference Latent Base Negative")
                .InputRef("conditioning", registry.GetRef("negative_output"))
                .InputFromNode("latent", "base_vae_encode", 0));
            registry.Register("negative_output", "base_ref_negative", 0);
        }

        builder.AddNode("grid_ref_vae_encode", node => node
            .Type("VAEEncode")
            .Title("VAE Encode Grid Reference")
            .InputRef("pixels", registry.GetRef("grid_reference_image"))
            .InputRef("vae", vaeRef));

        builder.AddNode("grid_ref_positive", node => node
            .Type("ReferenceLatent")
            .Title("Reference Latent Grid Positive")
            .InputRef("conditioning", registry.GetRef("positive_output"))
            .InputFromNode("latent", "grid_ref_vae_encode", 0));
        registry.Register("positive_output", "grid_ref_positive", 0);

        return hasBaseImage;
    }
}