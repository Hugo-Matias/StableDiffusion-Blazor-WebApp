using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads two VAEs and merges them with a weighted sum.
/// Used in Z-Image to ZIT workflow to combine Z-image-ae and Ultra_flux_For_Z-image-vae.
/// Registers vae_output in the node registry.
/// </summary>
public class VaeMergeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "vae_merge",
        Type = FragmentType.Loader,
        Title = "VAE Merge",
        IsHidden = true // Utility fragment, no UI
    };

    /// <summary>
    /// Parameters for the VAE merge fragment.
    /// </summary>
    public class Parameters
    {
        public string VaeAName { get; set; } = "Z-image-ae.safetensors";
        public string VaeBName { get; set; } = "Ultra_flux_For_Z-image-vae.safetensors";
        public float Ratio { get; set; } = 0.3f;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // Extract parameters from workflow assets
        var vaeAName = GetParameterValue(parameters, "VaeMergeA", "Z-image-ae.safetensors");
        var vaeBName = GetParameterValue(parameters, "VaeMergeB", "Ultra_flux_For_Z-image-vae.safetensors");

        BuildInternal(builder, registry, new Parameters
        {
            VaeAName = vaeAName,
            VaeBName = vaeBName,
            Ratio = 0.3f
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var vaeANodeId = $"{scope}vae_loader_a";
        var vaeBNodeId = $"{scope}vae_loader_b";
        var vaeMergeNodeId = $"{scope}vae_merge";

        // Load first VAE
        builder.AddNode(vaeANodeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE A")
            .Input("vae_name", p.VaeAName));

        // Load second VAE
        builder.AddNode(vaeBNodeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE B")
            .Input("vae_name", p.VaeBName));

        // Merge VAEs with weighted sum
        builder.AddNode(vaeMergeNodeId, node => node
            .Type("VAE Merge")
            .Title($"{scopeTitle}Merge VAEs")
            .InputRef("vae_a", registry.GetRef($"{vaeANodeId}"))
            .InputRef("vae_b", registry.GetRef($"{vaeBNodeId}"))
            .Input("method", "weighted_sum")
            .Input("ratio", p.Ratio));

        registry.Register($"{scope}vae_output", vaeMergeNodeId, 0);
    }

    private static string GetParameterValue(GenerationParameters parameters, string key, string defaultValue)
    {
        var assetValue = parameters.Assets?.GetValueOrDefault(key);
        if (!string.IsNullOrEmpty(assetValue))
            return assetValue;
        return defaultValue;
    }
}
