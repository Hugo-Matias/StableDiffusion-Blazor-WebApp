using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// LTX 2.3 split-safetensors loader using the AV-aware text encoder
/// <c>LTXAVTextEncoderLoader</c> (instead of <c>DualCLIPLoader</c>) for the
/// CLIP slot. Required by V2V Just-Talk and other AV / lip-sync workflows
/// that ship audio prompt features through the AV text projection.
///
/// Pipeline:
///   <c>UNETLoader</c>, <c>LTXAVTextEncoderLoader</c>, <c>VAELoader</c>,
///   <c>LTXVAudioVAELoader</c>, <c>LatentUpscaleModelLoader</c>,
///   then always-on <c>LTXVChunkFeedForward</c> +
///   <c>LTX2SamplingPreviewOverride</c> patches.
///
/// Registers: <c>{scope}model_output</c>, <c>{scope}clip_output</c>,
/// <c>{scope}vae_output</c>, <c>{scope}audio_vae_output</c>,
/// <c>{scope}upscale_model_output</c>.
/// </summary>
public class LtxLoadSplitAvFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_load_split_av",
        Type = FragmentType.Loader,
        Title = "LTX Load (Split, AV)",
        IsHidden = true
    };

    public class Parameters
    {
        public string UnetName { get; set; } = "ltx-2.3-22b-distilled-bf16.safetensors";
        public string UnetWeightDtype { get; set; } = "default";
        public string ClipName { get; set; } = "gemma_3_12B_it_fpmixed.safetensors";
        // LTXAVTextEncoderLoader.ckpt_name reads /models/checkpoints (verified
        // via ComfyUI /object_info), independent from the UNet diffusion model.
        // Upstream V2V_Just_Talk default: "VIDEO/LTX/LTX-2/ltx-2.3_text_projection_bf16.safetensors".
        public string AvProjectionCkpt { get; set; } = "ltx-2.3_text_projection_bf16.safetensors";
        public string VaeName { get; set; } = "ltx-2.3_video_vae.safetensors";
        // Audio VAE loader — see LtxLoadSplitFragment.Parameters for the
        // VAELoaderKJ vs LTXVAudioVAELoader distinction.
        public string AudioVaeName { get; set; } = "LTX23_audio_vae_bf16_KJ.safetensors";
        public string AudioVaeNodeType { get; set; } = "VAELoaderKJ";
        public string AudioVaeWeightDtype { get; set; } = "bf16";
        public string UpscaleModelName { get; set; } = "ltx-2-spatial-upscaler-x2-1.0.safetensors";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var p = new Parameters();
        if (parameters.Assets is not null)
        {
            if (parameters.Assets.TryGetValue("UNet", out var unet) && !string.IsNullOrWhiteSpace(unet))
                p.UnetName = unet;
            if (parameters.Assets.TryGetValue("Clip", out var clip) && !string.IsNullOrWhiteSpace(clip))
                p.ClipName = clip;
            if (parameters.Assets.TryGetValue("AvProjectionCkpt", out var avProj) && !string.IsNullOrWhiteSpace(avProj))
                p.AvProjectionCkpt = avProj;
            if (parameters.Assets.TryGetValue("Vae", out var vae) && !string.IsNullOrWhiteSpace(vae))
                p.VaeName = vae;
            if (parameters.Assets.TryGetValue("AudioVae", out var avae) && !string.IsNullOrWhiteSpace(avae))
                p.AudioVaeName = avae;
            if (parameters.Assets.TryGetValue("UpscaleModel", out var upscale) && !string.IsNullOrWhiteSpace(upscale))
                p.UpscaleModelName = upscale;
        }
        BuildInternal(builder, registry, p, scope, scopeTitle);
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
        var unetId = $"{scope}ltx_unet_loader";
        var clipId = $"{scope}ltx_av_text_encoder_loader";
        var vaeId = $"{scope}ltx_vae_loader";
        var audioVaeId = $"{scope}ltx_audio_vae_loader";
        var upscaleId = $"{scope}ltx_upscale_model_loader";
        var ffnPatchId = $"{scope}ltx_chunk_ffn_patch";
        var previewPatchId = $"{scope}ltx_preview_override";

        builder.AddNode(unetId, node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}Load Diffusion Model")
            .Input("unet_name", p.UnetName)
            .Input("weight_dtype", p.UnetWeightDtype));

        builder.AddNode(clipId, node => node
            .Type("LTXAVTextEncoderLoader")
            .Title($"{scopeTitle}LTXV Audio Text Encoder Loader")
            .Input("text_encoder", p.ClipName)
            .Input("ckpt_name", p.AvProjectionCkpt)
            .Input("device", "default"));

        builder.AddNode(vaeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        builder.AddNode(audioVaeId, node => BuildAudioVaeNode(node, p, scopeTitle));

        builder.AddNode(upscaleId, node => node
            .Type("LatentUpscaleModelLoader")
            .Title($"{scopeTitle}Load Latent Upscale Model")
            .Input("model_name", p.UpscaleModelName));

        // Always-on model patches mirror LtxLoadSplitFragment.
        builder.AddNode(ffnPatchId, node => node
            .Type("LTXVChunkFeedForward")
            .Title($"{scopeTitle}LTXV Chunk Feed Forward")
            .InputFromNode("model", unetId, 0));

        builder.AddNode(previewPatchId, node => node
            .Type("LTX2SamplingPreviewOverride")
            .Title($"{scopeTitle}LTX2 Sampling Preview Override")
            .InputFromNode("model", ffnPatchId, 0));

        registry.Register($"{scope}model_output", previewPatchId, 0);
        registry.Register($"{scope}clip_output", clipId, 0);
        registry.Register($"{scope}vae_output", vaeId, 0);
        registry.Register($"{scope}audio_vae_output", audioVaeId, 0);
        registry.Register($"{scope}upscale_model_output", upscaleId, 0);
    }

    private static void BuildAudioVaeNode(NodeBuilder node, Parameters p, string scopeTitle)
    {
        if (string.Equals(p.AudioVaeNodeType, "LTXVAudioVAELoader", StringComparison.Ordinal))
        {
            node.Type("LTXVAudioVAELoader")
                .Title($"{scopeTitle}LTXV Audio VAE Loader")
                .Input("ckpt_name", p.AudioVaeName);
            return;
        }

        node.Type("VAELoaderKJ")
            .Title($"{scopeTitle}VAELoader KJ (audio VAE)")
            .Input("vae_name", p.AudioVaeName)
            .Input("device", "main_device")
            .Input("weight_dtype", p.AudioVaeWeightDtype);
    }
}
