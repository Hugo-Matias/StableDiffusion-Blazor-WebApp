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

        // LTXVLatentUpsampler - upscale video latent 2x
        builder.AddNode(upsampleId, node => node
            .Type("LTXVLatentUpsampler")
            .Title($"{scopeTitle}LTXVLatentUpsampler")
            .InputFromNode("samples", separateId, 0)
            .InputRef("upscale_model", upscaleModelRef)
            .InputRef("vae", vaeRef));

        // Separated video (pre-upsampled) for CropGuides
        registry.Register($"{scope}pass1_video_latent", separateId, 0);
        // Upsampled video for pass 2
        registry.Register($"{scope}video_latent", upsampleId, 0);
        // Audio from pass 1 separation for pass 2
        registry.Register($"{scope}audio_latent", separateId, 1);
    }
}
