using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that loads LTX model components:
/// CheckpointLoaderSimple (model + VAE), LTXAVTextEncoderLoader (CLIP),
/// LTXVAudioVAELoader (audio VAE), LatentUpscaleModelLoader (spatial upscaler).
/// Registers: model_output, clip_output, vae_output, audio_vae_output, upscale_model_output.
/// </summary>
public class LtxLoadModelFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_load_model",
        Type = FragmentType.Loader,
        Title = "LTX Load Model",
        IsHidden = true
    };

    public class Parameters
    {
        public string CheckpointName { get; set; } = "ltx-2.3-22b-dev-fp8.safetensors";
        public string TextEncoderName { get; set; } = "gemma_3_12B_it_fp4_mixed.safetensors";
        public string UpscaleModelName { get; set; } = "ltx-2.3-spatial-upscaler-x2-1.1.safetensors";
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
        var checkpointId = $"{scope}checkpoint_loader";
        var textEncoderId = $"{scope}text_encoder_loader";
        var audioVaeId = $"{scope}audio_vae_loader";
        var upscaleModelId = $"{scope}upscale_model_loader";

        // CheckpointLoaderSimple -> model[0], clip[1] (unused), vae[2]
        builder.AddNode(checkpointId, node => node
            .Type("CheckpointLoaderSimple")
            .Title($"{scopeTitle}Load Checkpoint")
            .Input("ckpt_name", p.CheckpointName));

        // LTXAVTextEncoderLoader -> clip[0]
        builder.AddNode(textEncoderId, node => node
            .Type("LTXAVTextEncoderLoader")
            .Title($"{scopeTitle}LTXV Audio Text Encoder Loader")
            .Input("text_encoder", p.TextEncoderName)
            .Input("ckpt_name", p.CheckpointName)
            .Input("device", "default"));

        // LTXVAudioVAELoader -> audio_vae[0]
        builder.AddNode(audioVaeId, node => node
            .Type("LTXVAudioVAELoader")
            .Title($"{scopeTitle}LTXV Audio VAE Loader")
            .Input("ckpt_name", p.CheckpointName));

        // LatentUpscaleModelLoader -> upscale_model[0]
        builder.AddNode(upscaleModelId, node => node
            .Type("LatentUpscaleModelLoader")
            .Title($"{scopeTitle}Load Latent Upscale Model")
            .Input("model_name", p.UpscaleModelName));

        registry.Register($"{scope}model_output", checkpointId, 0);
        registry.Register($"{scope}clip_output", textEncoderId, 0);
        registry.Register($"{scope}vae_output", checkpointId, 2);
        registry.Register($"{scope}audio_vae_output", audioVaeId, 0);
        registry.Register($"{scope}upscale_model_output", upscaleModelId, 0);
    }
}
