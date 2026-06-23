using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment emitting <c>LTXVImgToVideoConditionOnly</c>. Used in I2V multi-pass
/// flows to re-inject a reference image into the current latent slot between
/// passes (without altering the latent shape, unlike <c>LTXVImgToVideoInplace</c>).
///
/// Inputs: positive, negative, vae, latent, image. Widgets: [strength, bypass].
/// Outputs: positive (0), negative (1), latent (2).
///
/// Not used by T2V (which has no ref image); available as a building block for
/// later I2V / audio / FML variants that need it.
/// </summary>
public class LtxImgToVideoConditionOnlyFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_img_to_video_condition_only",
        Type = FragmentType.Conditioning,
        Title = "LTX Img To Video Condition Only",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_i2v_cond_only";
        public double Strength { get; set; } = 1.0;
        public bool Bypass { get; set; } = false;
        public string ImageInputName { get; set; } = "preprocessed_image";
        public string LatentInputName { get; set; } = "video_latent";
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        /// <summary>Optional override; defaults to <see cref="LatentInputName"/> so the
        /// output overwrites the input latent slot transparently.</summary>
        public string? LatentOutputName { get; set; }
        /// <summary>Optional override; defaults to <see cref="PositiveInputName"/>.</summary>
        public string? PositiveOutputName { get; set; }
        /// <summary>Optional override; defaults to <see cref="NegativeInputName"/>.</summary>
        public string? NegativeOutputName { get; set; }
        public string Title { get; set; } = "LTXVImgToVideoConditionOnly";
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
        var vaeRef = registry.GetRef($"{scope}vae_output");
        var imageRef = registry.GetRef($"{scope}{p.ImageInputName}");
        var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");
        var positiveRef = registry.GetRef($"{scope}{p.PositiveInputName}");
        var negativeRef = registry.GetRef($"{scope}{p.NegativeInputName}");

        builder.AddNode(nodeId, node => node
            .Type("LTXVImgToVideoConditionOnly")
            .Title($"{scopeTitle}{p.Title}")
            .Input("strength", p.Strength)
            .Input("bypass", p.Bypass)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", vaeRef)
            .InputRef("latent", latentRef)
            .InputRef("image", imageRef));

        var posOut = p.PositiveOutputName ?? p.PositiveInputName;
        var negOut = p.NegativeOutputName ?? p.NegativeInputName;
        var latOut = p.LatentOutputName ?? p.LatentInputName;

        registry.Register($"{scope}{posOut}", nodeId, 0);
        registry.Register($"{scope}{negOut}", nodeId, 1);
        registry.Register($"{scope}{latOut}", nodeId, 2);
    }
}
