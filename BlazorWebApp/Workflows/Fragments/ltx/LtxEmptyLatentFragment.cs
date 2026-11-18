using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that creates empty video and audio latents for LTX.
/// Nodes: EmptyLTXVLatentVideo + LTXVEmptyLatentAudio.
/// Registers: video_latent, audio_latent.
/// </summary>
public class LtxEmptyLatentFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_empty_latent",
        Type = FragmentType.Latent,
        Title = "LTX Empty Latent",
        IsHidden = true
    };

    public class Parameters
    {
        public int Width { get; set; } = 640;
        public int Height { get; set; } = 360;
        public int Length { get; set; } = 126;
        public int BatchSize { get; set; } = 1;
        public int FrameRate { get; set; } = 25;

        /// <summary>
        /// When <c>true</c>, the empty audio latent node is omitted and only the video
        /// latent is registered. Workflows that supply a real audio track
        /// (e.g. <c>LtxAudioVaeEncodeFragment</c>) should set this to avoid emitting
        /// an unused empty audio latent that needs the audio VAE.
        /// </summary>
        public bool SkipAudio { get; set; } = false;
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
        var videoLatentId = $"{scope}empty_ltx_latent_video";
        var audioLatentId = $"{scope}empty_ltx_latent_audio";

        // EmptyLTXVLatentVideo
        builder.AddNode(videoLatentId, node => node
            .Type("EmptyLTXVLatentVideo")
            .Title($"{scopeTitle}EmptyLTXVLatentVideo")
            .Input("width", p.Width)
            .Input("height", p.Height)
            .Input("length", p.Length)
            .Input("batch_size", p.BatchSize));

        registry.Register($"{scope}video_latent", videoLatentId, 0);

        if (p.SkipAudio)
        {
            return;
        }

        var audioVaeRef = registry.GetRef($"{scope}audio_vae_output");

        // LTXVEmptyLatentAudio
        builder.AddNode(audioLatentId, node => node
            .Type("LTXVEmptyLatentAudio")
            .Title($"{scopeTitle}LTXV Empty Latent Audio")
            .Input("frames_number", p.Length)
            .Input("frame_rate", p.FrameRate)
            .Input("batch_size", p.BatchSize)
            .InputRef("audio_vae", audioVaeRef));

        registry.Register($"{scope}audio_latent", audioLatentId, 0);
    }
}
