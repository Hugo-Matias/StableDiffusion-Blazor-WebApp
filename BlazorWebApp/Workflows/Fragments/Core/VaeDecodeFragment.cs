using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that decodes latent images to pixel space using the VAE.
/// Registers image_output in the node registry.
/// </summary>
public class VaeDecodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "vae_decode",
        Type = FragmentType.Output,
        Title = "VAE Decode",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, scope);
    }

    /// <summary>
    /// Builds the fragment with explicit scope.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        string scope = "")
    {
        BuildInternal(builder, registry, scope);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        string scope)
    {
        // Get references - latent_output doesn't use scope, vae_output does
        var latentRef = registry.GetRef("latent_output");
        var vaeRef = registry.GetRef($"{scope}vae_output");

        builder.AddNode("vae_decoder", node => node
            .Type("VAEDecode")
            .Title("VAE Decode")
            .InputRef("samples", latentRef)
            .InputRef("vae", vaeRef));

        registry.Register("image_output", "vae_decoder", 0);
    }
}
