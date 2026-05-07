using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that loads LTX model components:
/// CheckpointLoaderSimple (model + VAE), LTXAVTextEncoderLoader (CLIP),
/// LTXVAudioVAELoader (audio VAE), LatentUpscaleModelLoader (spatial upscaler),
/// and LTX2SamplingPreviewOverride with a lightweight preview VAE.
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
        // CheckpointLoaderSimple.ckpt_name resolves against /models/checkpoints
        // (verified via ComfyUI /object_info).
        public string CheckpointName { get; set; } = "ltx-2.3-22b-dev-fp8.safetensors";
        public string TextEncoderName { get; set; } = "gemma_3_12B_it_fp4_mixed.safetensors";
        // LTXAVTextEncoderLoader.ckpt_name reads /models/checkpoints — distinct
        // slot from CheckpointLoaderSimple; upstream I2V pairs it with the same
        // LTX 2.3 checkpoint by default.
        public string AvProjectionCkpt { get; set; } = "ltx-2.3-22b-dev-fp8.safetensors";
        // Audio VAE loader — upstream I2V uses LTXVAudioVAELoader, which reads
        // ckpt_name from /models/checkpoints. VAELoaderKJ remains supported.
        public string AudioVaeName { get; set; } = "ltx-2.3-22b-dev-fp8.safetensors";
        public string AudioVaeNodeType { get; set; } = "LTXVAudioVAELoader";
        public string AudioVaeWeightDtype { get; set; } = "bf16";
        public string UpscaleModelName { get; set; } = "ltx-2.3-spatial-upscaler-x2-1.0.safetensors";
        public string PreviewVaeName { get; set; } = "taeltx2_3.safetensors";
        public int PreviewRate { get; set; } = 24;
        public bool EnableSamplingPreviewOverride { get; set; } = true;
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
            if (parameters.Assets.TryGetValue("Checkpoint", out var ckpt) && !string.IsNullOrWhiteSpace(ckpt))
                p.CheckpointName = ckpt;
            if (parameters.Assets.TryGetValue("Clip", out var clip) && !string.IsNullOrWhiteSpace(clip))
                p.TextEncoderName = clip;
            if (parameters.Assets.TryGetValue("AvProjectionCkpt", out var avProj) && !string.IsNullOrWhiteSpace(avProj))
                p.AvProjectionCkpt = avProj;
            if (parameters.Assets.TryGetValue("AudioVae", out var avae) && !string.IsNullOrWhiteSpace(avae))
                p.AudioVaeName = avae;
            if (parameters.Assets.TryGetValue("UpscaleModel", out var upscale) && !string.IsNullOrWhiteSpace(upscale))
                p.UpscaleModelName = upscale;
            if (parameters.Assets.TryGetValue("PreviewVae", out var previewVae) && !string.IsNullOrWhiteSpace(previewVae))
                p.PreviewVaeName = previewVae;
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
        var checkpointId = $"{scope}checkpoint_loader";
        var textEncoderId = $"{scope}text_encoder_loader";
        var audioVaeId = $"{scope}audio_vae_loader";
        var upscaleModelId = $"{scope}upscale_model_loader";
        var previewVaeId = $"{scope}preview_vae_loader";
        var previewOverrideId = $"{scope}ltx_preview_override";

        // CheckpointLoaderSimple -> model[0], clip[1] (unused), vae[2]
        builder.AddNode(checkpointId, node => node
            .Type("CheckpointLoaderSimple")
            .Title($"{scopeTitle}Load Checkpoint")
            .Input("ckpt_name", p.CheckpointName));

        // LTXAVTextEncoderLoader -> clip[0]. ckpt_name reads /models/checkpoints.
        builder.AddNode(textEncoderId, node => node
            .Type("LTXAVTextEncoderLoader")
            .Title($"{scopeTitle}LTXV Audio Text Encoder Loader")
            .Input("text_encoder", p.TextEncoderName)
            .Input("ckpt_name", p.AvProjectionCkpt)
            .Input("device", "default"));

        // Audio VAE loader. LTXVAudioVAELoader reads checkpoints; VAELoaderKJ reads VAEs.
        builder.AddNode(audioVaeId, node => BuildAudioVaeNode(node, p, scopeTitle));

        // LatentUpscaleModelLoader -> upscale_model[0]
        builder.AddNode(upscaleModelId, node => node
            .Type("LatentUpscaleModelLoader")
            .Title($"{scopeTitle}Load Latent Upscale Model")
            .Input("model_name", p.UpscaleModelName));

        if (p.EnableSamplingPreviewOverride)
        {
            builder.AddNode(previewVaeId, node => node
                .Type("VAELoader")
                .Title($"{scopeTitle}Load Preview VAE")
                .Input("vae_name", p.PreviewVaeName));

            builder.AddNode(previewOverrideId, node => node
                .Type("LTX2SamplingPreviewOverride")
                .Title($"{scopeTitle}LTX2 Sampling Preview Override")
                .InputFromNode("model", checkpointId, 0)
                .InputFromNode("vae", previewVaeId, 0)
                .Input("preview_rate", p.PreviewRate));
        }

        registry.Register($"{scope}model_output", p.EnableSamplingPreviewOverride ? previewOverrideId : checkpointId, 0);
        registry.Register($"{scope}clip_output", textEncoderId, 0);
        registry.Register($"{scope}vae_output", checkpointId, 2);
        registry.Register($"{scope}audio_vae_output", audioVaeId, 0);
        registry.Register($"{scope}upscale_model_output", upscaleModelId, 0);
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
