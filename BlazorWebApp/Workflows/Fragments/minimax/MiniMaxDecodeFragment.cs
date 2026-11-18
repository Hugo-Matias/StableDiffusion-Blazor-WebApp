using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Decodes the joint MiniMax H3 latent into video frames (VAEDecode) and audio (VAEDecodeAudio)
/// from the same latent_output, then muxes and saves the result. Pipeline:
/// VAEDecode + VAEDecodeAudio -> CreateVideo -> SaveVideo.
/// </summary>
public class MiniMaxDecodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_decode",
        Type = FragmentType.Output,
        Title = "Decode And Save Video",
        IsHidden = true
    };

    public class Parameters
    {
        public double Fps { get; set; } = 24;
        public string FilenamePrefix { get; set; } = "tmp/video";
        public string Format { get; set; } = "auto";
        public string Codec { get; set; } = "auto";

        /// <summary>
        /// When false, skips CreateVideo/SaveVideo so an enhancement fragment (e.g. RTX Super
        /// Resolution) can overwrite image_output first. Call <see cref="BuildSave"/> afterward.
        /// </summary>
        public bool SaveVideo { get; set; } = true;
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
        var videoDecodeId = $"{scope}minimax_vae_decode";
        var audioDecodeId = $"{scope}minimax_vae_decode_audio";

        var latentRef = registry.GetRef($"{scope}latent_output");

        builder.AddNode(videoDecodeId, node => node
            .Type("VAEDecode")
            .Title($"{scopeTitle}VAE Decode")
            .InputRef("samples", latentRef)
            .InputRef("vae", registry.GetRef($"{scope}vae_output")));

        builder.AddNode(audioDecodeId, node => node
            .Type("VAEDecodeAudio")
            .Title($"{scopeTitle}VAE Decode Audio")
            .InputRef("samples", latentRef)
            .InputRef("vae", registry.GetRef($"{scope}audio_vae_output")));

        registry.Register($"{scope}image_output", videoDecodeId, 0);
        registry.Register($"{scope}audio_output", audioDecodeId, 0);

        if (!p.SaveVideo)
        {
            return;
        }

        BuildSave(builder, registry, p, scope, scopeTitle);
    }

    /// <summary>
    /// Builds CreateVideo + SaveVideo, reading images/audio from the registry so an
    /// enhancement fragment (e.g. RTX Super Resolution) can overwrite image_output first.
    /// </summary>
    public static void BuildSave(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var createVideoId = $"{scope}minimax_create_video";
        var saveVideoId = $"{scope}minimax_save_video";

        builder.AddNode(createVideoId, node => node
            .Type("CreateVideo")
            .Title($"{scopeTitle}Create Video")
            .InputRef("images", registry.GetRef($"{scope}image_output"))
            .InputRef("audio", registry.GetRef($"{scope}audio_output"))
            .Input("fps", p.Fps));

        builder.AddNode(saveVideoId, node => node
            .Type("SaveVideo")
            .Title($"{scopeTitle}Save Video")
            .InputFromNode("video", createVideoId, 0)
            .Input("filename_prefix", p.FilenamePrefix)
            .Input("format", p.Format)
            .Input("codec", p.Codec));
    }
}
