using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Emits <c>LTXAddVideoICLoRAGuide</c> which encodes a reference image
/// batch through the VAE, optionally dilates the latent by the
/// <c>latent_downscale_factor</c> exposed by
/// <see cref="LtxIcLoraLoaderFragment"/>, and injects the result as
/// IC-LoRA conditioning into the video latent / positive / negative
/// conditioning chain.
///
/// Reads:
/// <c>{scope}{PositiveInputName}</c>, <c>{scope}{NegativeInputName}</c>,
/// <c>{scope}{VaeInputName}</c>, <c>{scope}{LatentInputName}</c>,
/// <c>{scope}{ImageInputName}</c>,
/// <c>{scope}{LatentDownscaleFactorInputName}</c>.
///
/// Re-registers (under the same names by default):
/// positive, negative, latent.
/// </summary>
public class LtxAddVideoIcLoraGuideFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_add_video_iclora_guide",
        Type = FragmentType.Conditioning,
        Title = "LTX Add Video IC-LoRA Guide",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_add_video_iclora_guide";
        public int FrameIdx { get; set; } = 0;
        public double Strength { get; set; } = 0.7;
        public string Crop { get; set; } = "disabled";
        public bool UseTiledEncode { get; set; } = false;
        public int TileSize { get; set; } = 256;
        public int TileOverlap { get; set; } = 64;
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        public string VaeInputName { get; set; } = "vae_output";
        public string LatentInputName { get; set; } = "video_latent";
        public string ImageInputName { get; set; } = "control_image";
        public string LatentDownscaleFactorInputName { get; set; } = "latent_downscale_factor";
        public string? PositiveOutputName { get; set; }
        public string? NegativeOutputName { get; set; }
        public string? LatentOutputName { get; set; }
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
        var nodeId = $"{scope}{p.NodeId}";
        var positiveRef = registry.GetRef($"{scope}{p.PositiveInputName}");
        var negativeRef = registry.GetRef($"{scope}{p.NegativeInputName}");
        var vaeRef = registry.GetRef($"{scope}{p.VaeInputName}");
        var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");
        var imageRef = registry.GetRef($"{scope}{p.ImageInputName}");
        var downscaleRef = registry.GetRef($"{scope}{p.LatentDownscaleFactorInputName}");

        builder.AddNode(nodeId, node => node
            .Type("LTXAddVideoICLoRAGuide")
            .Title($"{scopeTitle}Add Video IC-LoRA Guide")
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", vaeRef)
            .InputRef("latent", latentRef)
            .InputRef("image", imageRef)
            .Input("frame_idx", p.FrameIdx)
            .Input("strength", p.Strength)
            .InputRef("latent_downscale_factor", downscaleRef)
            .Input("crop", p.Crop)
            .Input("use_tiled_encode", p.UseTiledEncode)
            .Input("tile_size", p.TileSize)
            .Input("tile_overlap", p.TileOverlap));

        registry.Register($"{scope}{p.PositiveOutputName ?? p.PositiveInputName}", nodeId, 0);
        registry.Register($"{scope}{p.NegativeOutputName ?? p.NegativeInputName}", nodeId, 1);
        registry.Register($"{scope}{p.LatentOutputName ?? p.LatentInputName}", nodeId, 2);
    }
}
