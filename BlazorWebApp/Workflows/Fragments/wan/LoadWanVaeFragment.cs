using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that loads a WanVideo VAE using WanVideoVAELoader.
/// Registers vae_output.
/// </summary>
public class LoadWanVaeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_wan_vae",
        Type = FragmentType.Loader,
        Title = "Load WanVideo VAE",
        IsHidden = true
    };

    public class Parameters
    {
        public string VaeName { get; set; } = "wan_2.1_vae.safetensors";
        public string Precision { get; set; } = "bf16";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var nodeId = $"{scope}vae_loader";

        builder.AddNode(nodeId, node => node
            .Type("WanVideoVAELoader")
            .Title($"{scopeTitle}WanVideo VAE Loader")
            .Input("model_name", p.VaeName)
            .Input("precision", p.Precision)
            .Input("use_cpu_cache", false));

        registry.Register($"{scope}vae_output", nodeId, 0);
    }
}
