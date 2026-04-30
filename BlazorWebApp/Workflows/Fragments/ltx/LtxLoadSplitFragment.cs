using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that loads LTX 2.3 distilled split safetensors:
/// UNETLoader + DualCLIPLoader (clip_type "ltxv") + VAELoader + LTXVAudioVAELoader
/// + LatentUpscaleModelLoader, plus always-on model patches LTXVChunkFeedForward and
/// LTX2SamplingPreviewOverride. Mirrors the loader cluster of the upstream
/// LTX-2.3 - I2V_T2V_Basic.json workflow.
///
/// Registers (final patched outputs):
///   {scope}model_output, {scope}clip_output, {scope}vae_output,
///   {scope}audio_vae_output, {scope}upscale_model_output.
/// </summary>
public class LtxLoadSplitFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_load_split",
        Type = FragmentType.Loader,
        Title = "LTX Load Split Model",
        IsHidden = true
    };

    public class Parameters
    {
        public string UnetName { get; set; } = "ltx-2.3-22b-distilled-bf16.safetensors";
        public string ClipName { get; set; } = "gemma_3_12B_it_fp4_mixed.safetensors";
        public string VaeName { get; set; } = "ltx-2.3_video_vae.safetensors";
        public string AudioVaeName { get; set; } = "ltx-2.3_audio_vae.safetensors";
        public string UpscaleModelName { get; set; } = "ltx-2.3-spatial-upscaler-x2-1.1.safetensors";
        public string UnetWeightDtype { get; set; } = "default";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var assets = parameters.Assets;
        var p = new Parameters
        {
            UnetName = assets?.GetValueOrDefault("UNet") ?? new Parameters().UnetName,
            ClipName = assets?.GetValueOrDefault("Clip") ?? new Parameters().ClipName,
            VaeName = assets?.GetValueOrDefault("Vae") ?? new Parameters().VaeName,
            AudioVaeName = assets?.GetValueOrDefault("AudioVae") ?? new Parameters().AudioVaeName,
            UpscaleModelName = assets?.GetValueOrDefault("UpscaleModel") ?? new Parameters().UpscaleModelName
        };
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
        var clipId = $"{scope}ltx_dual_clip_loader";
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
            .Type("DualCLIPLoader")
            .Title($"{scopeTitle}DualCLIPLoader")
            .Input("clip_name1", p.ClipName)
            .Input("clip_name2", p.ClipName)
            .Input("type", "ltxv")
            .Input("device", "default"));

        builder.AddNode(vaeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        builder.AddNode(audioVaeId, node => node
            .Type("LTXVAudioVAELoader")
            .Title($"{scopeTitle}LTXV Audio VAE Loader")
            .Input("ckpt_name", p.AudioVaeName));

        builder.AddNode(upscaleId, node => node
            .Type("LatentUpscaleModelLoader")
            .Title($"{scopeTitle}Load Latent Upscale Model")
            .Input("model_name", p.UpscaleModelName));

        // Always-on model patches: chunked feed-forward + sampling preview override.
        // Both reduce VRAM / improve preview quality on distilled LTX 2.3 and have no
        // user-tunable parameters in the upstream workflow.
        builder.AddNode(ffnPatchId, node => node
            .Type("LTXVChunkFeedForward")
            .Title($"{scopeTitle}LTXV Chunk Feed Forward")
            .InputFromNode("model", unetId, 0));

        builder.AddNode(previewPatchId, node => node
            .Type("LTX2SamplingPreviewOverride")
            .Title($"{scopeTitle}LTX2 Sampling Preview Override")
            .InputFromNode("model", ffnPatchId, 0));

        // Final patched model output. NAG / SageAttention enhancements (if active) chain
        // additional patches downstream of this and re-register {scope}model_output.
        registry.Register($"{scope}model_output", previewPatchId, 0);
        registry.Register($"{scope}clip_output", clipId, 0);
        registry.Register($"{scope}vae_output", vaeId, 0);
        registry.Register($"{scope}audio_vae_output", audioVaeId, 0);
        registry.Register($"{scope}upscale_model_output", upscaleId, 0);
    }
}
