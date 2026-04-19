using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that decodes the final AV latent into video and saves it.
/// Pipeline: LTXVSeparateAVLatent -> VAEDecodeTiled + LTXVAudioVAEDecode -> CreateVideo -> SaveVideo.
/// Reads: av_latent_output, vae_output, audio_vae_output.
/// Registers: image_output.
/// </summary>
public class LtxDecodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_decode",
        Type = FragmentType.Output,
        Title = "LTX Decode",
        IsHidden = true
    };

    public class Parameters
    {
        public int FrameRate { get; set; } = 25;
        public int TileSize { get; set; } = 768;
        public int Overlap { get; set; } = 64;
        public int TemporalSize { get; set; } = 4096;
        public int TemporalOverlap { get; set; } = 4;
        public string FilenamePrefix { get; set; } = "tmp/vid";
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
        var separateId = $"{scope}ltx_separate_pass2";
        var vaeDecodeId = $"{scope}vae_decode_tiled";
        var audioDecodeId = $"{scope}audio_decode";
        var createVideoId = $"{scope}create_video";
        var saveVideoId = $"{scope}save_video";

        var avLatentRef = registry.GetRef($"{scope}{p.AvLatentInputName}");
        var vaeRef = registry.GetRef($"{scope}vae_output");
        var audioVaeRef = registry.GetRef($"{scope}audio_vae_output");

        // LTXVSeparateAVLatent - split final AV output
        builder.AddNode(separateId, node => node
            .Type("LTXVSeparateAVLatent")
            .Title($"{scopeTitle}LTXVSeparateAVLatent")
            .InputRef("av_latent", avLatentRef));

        // VAEDecodeTiled - decode video frames
        builder.AddNode(vaeDecodeId, node => node
            .Type("VAEDecodeTiled")
            .Title($"{scopeTitle}VAE Decode (Tiled)")
            .Input("tile_size", p.TileSize)
            .Input("overlap", p.Overlap)
            .Input("temporal_size", p.TemporalSize)
            .Input("temporal_overlap", p.TemporalOverlap)
            .InputFromNode("samples", separateId, 0)
            .InputRef("vae", vaeRef));

        // LTXVAudioVAEDecode - decode audio
        builder.AddNode(audioDecodeId, node => node
            .Type("LTXVAudioVAEDecode")
            .Title($"{scopeTitle}LTXV Audio VAE Decode")
            .InputFromNode("samples", separateId, 1)
            .InputRef("audio_vae", audioVaeRef));

        // CreateVideo - combine video frames + audio
        builder.AddNode(createVideoId, node => node
            .Type("CreateVideo")
            .Title($"{scopeTitle}Create Video")
            .Input("fps", p.FrameRate)
            .InputFromNode("images", vaeDecodeId, 0)
            .InputFromNode("audio", audioDecodeId, 0));

        // SaveVideo - save to file
        builder.AddNode(saveVideoId, node => node
            .Type("SaveVideo")
            .Title($"{scopeTitle}Save Video")
            .Input("filename_prefix", p.FilenamePrefix)
            .Input("format", "auto")
            .Input("codec", "auto")
            .InputFromNode("video", createVideoId, 0));

        registry.Register($"{scope}image_output", vaeDecodeId, 0);
    }
}
