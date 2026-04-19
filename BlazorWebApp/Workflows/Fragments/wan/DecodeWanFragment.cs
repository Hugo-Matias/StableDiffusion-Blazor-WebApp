using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that decodes WanVideo latents using WanVideoDecode.
/// Requires registry: vae_output, latent_output.
/// Registers image_output.
/// </summary>
public class DecodeWanFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "decode_wan",
        Type = FragmentType.Output,
        Title = "WanVideo Decode",
        IsHidden = true
    };

    public class Parameters
    {
        public bool EnableVaeTiling { get; set; } = false;
        public int TileX { get; set; } = 272;
        public int TileY { get; set; } = 272;
        public int TileStrideX { get; set; } = 144;
        public int TileStrideY { get; set; } = 128;
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
        var nodeId = $"{scope}decode";

        var vaeRef = registry.GetRef($"{scope}vae_output");
        var latentRef = registry.GetRef("latent_output");

        builder.AddNode(nodeId, node => node
            .Type("WanVideoDecode")
            .Title($"{scopeTitle}WanVideo Decode")
            .Input("enable_vae_tiling", p.EnableVaeTiling)
            .Input("tile_x", p.TileX)
            .Input("tile_y", p.TileY)
            .Input("tile_stride_x", p.TileStrideX)
            .Input("tile_stride_y", p.TileStrideY)
            .Input("normalization", "default")
            .InputRef("vae", vaeRef)
            .InputRef("samples", latentRef));

        registry.Register($"{scope}image_output", nodeId, 0);
    }
}
