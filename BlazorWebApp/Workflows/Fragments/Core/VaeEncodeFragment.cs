using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that encodes an image to latent space using the VAE.
/// Used by Img2Img workflows to convert a source image into a latent representation.
/// Registers latent_output in the node registry.
/// </summary>
public class VaeEncodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "vae_encode",
        Type = FragmentType.Latent,
        Title = "VAE Encode",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the VAE encode fragment.
    /// </summary>
    public class Parameters
    {
        public string ImageInputName { get; set; } = "image_input";
        public string Scope { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, "image_input", scope);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams.ImageInputName, fragmentParams.Scope);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string imageInputName,
        string scope)
    {
        var imageRef = registry.GetRef(imageInputName);
        var vaeRef = registry.GetRef($"{scope}vae_output");

        builder.AddNode("vae_encoder", node => node
            .Type("VAEEncode")
            .Title("VAE Encode")
            .InputRef("pixels", imageRef)
            .InputRef("vae", vaeRef));

        registry.Register("latent_output", "vae_encoder", 0);
    }
}
