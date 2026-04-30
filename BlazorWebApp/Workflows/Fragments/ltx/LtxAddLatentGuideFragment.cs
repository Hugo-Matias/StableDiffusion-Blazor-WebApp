using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Emits <c>LTXVAddLatentGuide</c> which injects a single image (typically
/// the last frame of the source video) into the video latent at a given
/// frame index, so the sampler converges back to that frame and the
/// generated clip ends on the source's terminal frame.
///
/// Reads:
/// <c>{scope}{PositiveInputName}</c>, <c>{scope}{NegativeInputName}</c>,
/// <c>{scope}{VaeInputName}</c>, <c>{scope}{LatentInputName}</c>,
/// <c>{scope}{ImageInputName}</c>.
///
/// Re-registers (under the same names by default):
/// positive, negative, latent.
/// </summary>
public class LtxAddLatentGuideFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_add_latent_guide",
        Type = FragmentType.Conditioning,
        Title = "LTX Add Latent Guide",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_add_latent_guide";
        public int FrameIdx { get; set; } = -1;
        public double Strength { get; set; } = 0.7;
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        public string VaeInputName { get; set; } = "vae_output";
        public string LatentInputName { get; set; } = "video_latent";
        public string GuidingLatentInputName { get; set; } = "loaded_video_last_frame_latent";
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
        var guidingLatentRef = registry.GetRef($"{scope}{p.GuidingLatentInputName}");

        builder.AddNode(nodeId, node => node
            .Type("LTXVAddLatentGuide")
            .Title($"{scopeTitle}LTXV Add Latent Guide")
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", vaeRef)
            .InputRef("latent", latentRef)
            .InputRef("guiding_latent", guidingLatentRef)
            .Input("latent_idx", p.FrameIdx)
            .Input("strength", p.Strength));

        registry.Register($"{scope}{p.PositiveOutputName ?? p.PositiveInputName}", nodeId, 0);
        registry.Register($"{scope}{p.NegativeOutputName ?? p.NegativeInputName}", nodeId, 1);
        registry.Register($"{scope}{p.LatentOutputName ?? p.LatentInputName}", nodeId, 2);
    }
}
