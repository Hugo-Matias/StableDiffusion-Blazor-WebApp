using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// LTXVAddGuideMulti - injects 2 or 3 frame anchors (first / [middle] / last)
/// into the conditioning + latent. Mirrors the upstream FML / FLF guide cluster.
///
/// Reads (defaults):
///   {scope}ltx_positive_output, {scope}ltx_negative_output,
///   {scope}vae_output, {scope}video_latent,
///   plus the image refs supplied via <see cref="Parameters.GuideImageInputs"/>.
///
/// Re-registers (overwrites the pre-guide entries):
///   {scope}ltx_positive_output, {scope}ltx_negative_output,
///   {scope}video_latent.
///
/// Widget convention follows upstream LTX 2.3 FML2V Custom Audio:
///   3-guide: <c>num_guides="3", frame_idx_1=0, strength_1=1,
///            frame_idx_2=&lt;mid&gt;, strength_2=0.5,
///            frame_idx_3=-1, strength_3=1</c>.
///   2-guide: <c>num_guides="2", frame_idx_1=0, strength_1=1,
///            frame_idx_2=-1, strength_2=1</c>.
/// </summary>
public class LtxAddGuideMultiFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_add_guide_multi",
        Type = FragmentType.Conditioning,
        Title = "LTX Add Guide Multi",
        IsHidden = true
    };

    public class GuideEntry
    {
        /// <summary>Registry key for the image to use as a guide (resolved with scope prefix).</summary>
        public string ImageInputName { get; set; } = "";
        /// <summary>Frame index in the latent (0 = first, -1 = last, otherwise the absolute frame index).</summary>
        public int FrameIndex { get; set; }
        /// <summary>Guide strength (1.0 for hard anchors, 0.5 for the mid frame in upstream).</summary>
        public double Strength { get; set; } = 1.0;
    }

    public class Parameters
    {
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        public string VaeInputName { get; set; } = "vae_output";
        public string LatentInputName { get; set; } = "video_latent";

        /// <summary>
        /// Optional override for the registry key the latent output is registered under.
        /// Defaults to <see cref="LatentInputName"/> (overwrites the input key) so the
        /// guided latent flows through the rest of the pipeline transparently.
        /// </summary>
        public string? LatentOutputName { get; set; }

        /// <summary>Two or three guides, in order (first, [middle,] last).</summary>
        public List<GuideEntry> Guides { get; set; } = [];
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // No state-driven path; this fragment is always invoked with explicit parameters.
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
        if (p.Guides.Count is < 2 or > 3)
        {
            throw new InvalidOperationException(
                $"LtxAddGuideMultiFragment requires 2 or 3 guides; got {p.Guides.Count}.");
        }

        var nodeId = $"{scope}ltx_add_guide_multi";
        var positiveRef = registry.GetRef($"{scope}{p.PositiveInputName}");
        var negativeRef = registry.GetRef($"{scope}{p.NegativeInputName}");
        var vaeRef = registry.GetRef($"{scope}{p.VaeInputName}");
        var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");

        var guideCount = p.Guides.Count;

        builder.AddNode(nodeId, node =>
        {
            node.Type("LTXVAddGuideMulti")
                .Title($"{scopeTitle}LTXVAddGuideMulti")
                .Input("num_guides", guideCount.ToString())
                .InputRef("positive", positiveRef)
                .InputRef("negative", negativeRef)
                .InputRef("vae", vaeRef)
                .InputRef("latent", latentRef);

            for (int i = 0; i < guideCount; i++)
            {
                var idx = i + 1;
                var guide = p.Guides[i];
                var imageRef = registry.GetRef($"{scope}{guide.ImageInputName}");
                node.InputRef($"image_{idx}", imageRef)
                    .Input($"frame_idx_{idx}", guide.FrameIndex)
                    .Input($"strength_{idx}", guide.Strength);
            }
        });

        registry.Register($"{scope}ltx_positive_output", nodeId, 0);
        registry.Register($"{scope}ltx_negative_output", nodeId, 1);

        var latentOutput = p.LatentOutputName ?? p.LatentInputName;
        registry.Register($"{scope}{latentOutput}", nodeId, 2);
    }
}
