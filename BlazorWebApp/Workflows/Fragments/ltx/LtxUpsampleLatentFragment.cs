using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that separates pass 1 AV output and upsamples the video latent 2x.
/// Nodes: LTXVSeparateAVLatent -> LTXVLatentUpsampler.
/// Reads: av_latent_output, upscale_model_output, vae_output.
/// Registers: pass1_video_latent (pre-upsampled, for CropGuides),
///            video_latent (upsampled), audio_latent (from separation).
/// </summary>
public class LtxUpsampleLatentFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_upsample_latent",
        Type = FragmentType.Utility,
        Title = "LTX Upsample Latent",
        IsHidden = true
    };

    public class Parameters
    {
        public string AvLatentInputName { get; set; } = "av_latent_output";

        /// <summary>
        /// When set, inserts an <c>LTXVCropGuides</c> node between Separate and Upsampler
        /// to strip the conditioning-frame padding added by <c>LTXVAddGuide</c>. The cond
        /// inputs are pulled from the named registry refs and the cropped cond outputs
        /// are re-registered under those same names so downstream stage-2 frame guides
        /// pick them up. Required for I2V / A2V — without it the guide image bleeds
        /// into the last frames of the decoded video.
        /// </summary>
        public string? PositiveInputName { get; set; }
        public string? NegativeInputName { get; set; }
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
        var separateId = $"{scope}ltx_separate_pass1";
        var upsampleId = $"{scope}ltx_upsample";

        var avLatentRef = registry.GetRef($"{scope}{p.AvLatentInputName}");
        var upscaleModelRef = registry.GetRef($"{scope}upscale_model_output");
        var vaeRef = registry.GetRef($"{scope}vae_output");

        // LTXVSeparateAVLatent - split into video[0] and audio[1]
        builder.AddNode(separateId, node => node
            .Type("LTXVSeparateAVLatent")
            .Title($"{scopeTitle}LTXVSeparateAVLatent")
            .InputRef("av_latent", avLatentRef));

        // LTXVCropGuides - optional, strips the conditioning-frame padding that
        // LTXVAddGuide injects ahead of the temporal axis. Required for I2V / A2V.
        // The cropped cond is re-registered so downstream stage-2 frame guides
        // and ConditioningZeroOut nodes pull the cleaned version instead of the
        // padded one (which is what the reference Comfy graph does via 389:1050).
        var hasCrop = !string.IsNullOrEmpty(p.PositiveInputName) && !string.IsNullOrEmpty(p.NegativeInputName);
        var latentSourceId = separateId;
        var latentSourceSlot = 0;
        if (hasCrop)
        {
            var cropId = $"{scope}ltx_crop_guides_pass1";
            var positiveRef = registry.GetRef($"{scope}{p.PositiveInputName}");
            var negativeRef = registry.GetRef($"{scope}{p.NegativeInputName}");
            builder.AddNode(cropId, node => node
                .Type("LTXVCropGuides")
                .Title($"{scopeTitle}LTXVCropGuides (Pass 1)")
                .InputRef("positive", positiveRef)
                .InputRef("negative", negativeRef)
                .InputFromNode("latent", separateId, 0));

            // Re-register cond refs so downstream consumers pick up the cropped cond.
            registry.Register($"{scope}{p.PositiveInputName!}", cropId, 0);
            registry.Register($"{scope}{p.NegativeInputName!}", cropId, 1);
            latentSourceId = cropId;
            latentSourceSlot = 2;
        }

        // LTXVLatentUpsampler - upscale video latent 2x
        builder.AddNode(upsampleId, node => node
            .Type("LTXVLatentUpsampler")
            .Title($"{scopeTitle}LTXVLatentUpsampler")
            .InputFromNode("samples", latentSourceId, latentSourceSlot)
            .InputRef("upscale_model", upscaleModelRef)
            .InputRef("vae", vaeRef));

        // Separated video (pre-upsampled) for CropGuides
        registry.Register($"{scope}pass1_video_latent", latentSourceId, latentSourceSlot);
        // Upsampled video for pass 2
        registry.Register($"{scope}video_latent", upsampleId, 0);
        // Audio from pass 1 separation for pass 2
        registry.Register($"{scope}audio_latent", separateId, 1);
    }
}
