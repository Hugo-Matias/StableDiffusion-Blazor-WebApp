using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxVhsDecodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_vhs_decode",
        Type = FragmentType.Output,
        Title = "LTX VHS Decode",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var separateId = $"{scope}ltx_final_separate_av";
        var cropId = $"{scope}ltx_crop_guides_pass2";
        var decodeId = $"{scope}ltx_final_vae_decode_tiled";
        var audioDecodeId = $"{scope}ltx_final_audio_decode";
        var saveId = $"{scope}ltx_vhs_video_combine";

        builder.AddNode(separateId, node => node
            .Type("LTXVSeparateAVLatent")
            .Title($"{scopeTitle}LTXVSeparateAVLatent")
            .InputRef("av_latent", registry.GetRef($"{scope}{p.AvLatentInputName}")));

        // LTXVCropGuides - optional, removes the conditioning-frame padding injected
        // by stage-2 LTXVAddGuide nodes. The reference Comfy graph crops before decode
        // (node 1194:1179). Without this, the guide image bleeds into the final frames
        // of the decoded video (image-burn-at-end artifact for I2V).
        var hasCrop = !string.IsNullOrEmpty(p.PositiveInputName) && !string.IsNullOrEmpty(p.NegativeInputName);
        var latentSourceId = separateId;
        var latentSourceSlot = 0;
        if (hasCrop)
        {
            var positiveRef = registry.GetRef($"{scope}{p.PositiveInputName}");
            var negativeRef = registry.GetRef($"{scope}{p.NegativeInputName}");
            builder.AddNode(cropId, node => node
                .Type("LTXVCropGuides")
                .Title($"{scopeTitle}LTXVCropGuides (Pass 2)")
                .InputRef("positive", positiveRef)
                .InputRef("negative", negativeRef)
                .InputFromNode("latent", separateId, 0));
            latentSourceId = cropId;
            latentSourceSlot = 2;
        }

        builder.AddNode(decodeId, node => node
            .Type("VAEDecodeTiled")
            .Title($"{scopeTitle}VAEDecodeTiled")
            .InputFromNode("samples", latentSourceId, latentSourceSlot)
            .InputRef("vae", registry.GetRef($"{scope}vae_output"))
            .Input("tile_size", p.TileSize)
            .Input("overlap", p.Overlap)
            .Input("temporal_size", p.TemporalSize)
            .Input("temporal_overlap", p.TemporalOverlap));

        builder.AddNode(audioDecodeId, node => node
            .Type("LTXVAudioVAEDecode")
            .Title($"{scopeTitle}LTXVAudioVAEDecode")
            .InputFromNode("samples", separateId, 1)
            .InputRef("audio_vae", registry.GetRef($"{scope}audio_vae_output")));

        registry.Register($"{scope}image_output", decodeId, 0);
        registry.Register($"{scope}audio_output", audioDecodeId, 0);

        if (!p.SaveVideo)
        {
            return;
        }

        builder.AddNode(saveId, node => node
            .Type("VHS_VideoCombine")
            .Title($"{scopeTitle}VHS Video Combine")
            .InputRef("images", registry.GetRef($"{scope}image_output"))
            .InputRef("audio", registry.GetRef($"{scope}audio_output"))
            .Input("frame_rate", p.FrameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", p.FilenamePrefix)
            .Input("format", p.Format)
            .Input("pingpong", false)
            .Input("save_output", true)
            .Input("pix_fmt", p.PixelFormat)
            .Input("crf", p.Crf)
            .Input("save_metadata", true)
            .Input("trim_to_audio", false));
    }

    public void BuildSave(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var saveId = $"{scope}ltx_vhs_video_combine";

        builder.AddNode(saveId, node => node
            .Type("VHS_VideoCombine")
            .Title($"{scopeTitle}VHS Video Combine")
            .InputRef("images", registry.GetRef($"{scope}image_output"))
            .InputRef("audio", registry.GetRef($"{scope}audio_output"))
            .Input("frame_rate", p.FrameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", p.FilenamePrefix)
            .Input("format", p.Format)
            .Input("pingpong", false)
            .Input("save_output", true)
            // VHS format-specific inputs are flat top-level keys per the live schema
            // (object_info/VHS_VideoCombine.input.required.format.formats[<fmt>]).
            // Dotted keys ("format.pix_fmt" etc.) cause the "Missing input for p/c/s/t"
            // warnings and silent default fallback.
            .Input("pix_fmt", p.PixelFormat)
            .Input("crf", p.Crf)
            .Input("save_metadata", true)
            .Input("trim_to_audio", false));
    }

    public class Parameters
    {
        public string AvLatentInputName { get; set; } = "av_latent_output";

        /// <summary>
        /// When both are set, inserts an <c>LTXVCropGuides</c> node between the final
        /// AV separate and the VAE decode. Required for I2V / A2V — strips the
        /// conditioning-frame padding that <c>LTXVAddGuide</c> injects, matching the
        /// reference Comfy graph (node 1194:1179).
        /// </summary>
        public string? PositiveInputName { get; set; }
        public string? NegativeInputName { get; set; }
        public int FrameRate { get; set; } = 25;
        public int TileSize { get; set; } = 512;
        public int Overlap { get; set; } = 64;
        public int TemporalSize { get; set; } = 4096;
        public int TemporalOverlap { get; set; } = 8;
        public string FilenamePrefix { get; set; } = "tmp/video";
        public string Format { get; set; } = "video/h264-mp4";
        public string PixelFormat { get; set; } = "yuv420p";
        public int Crf { get; set; } = 19;
        public bool SaveVideo { get; set; } = true;
    }
}