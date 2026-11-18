using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Emits <c>LTXVAudioVideoMask</c> (KJNodes) which builds time-range noise
/// masks on both the video and audio latents, marking the regions the
/// sampler is allowed to repaint.
///
/// Reads:
/// <c>{scope}{VideoLatentInputName}</c>,
/// <c>{scope}{AudioLatentInputName}</c>.
///
/// Re-registers:
/// video_latent and audio_latent under their input names by default.
/// </summary>
public class LtxAudioVideoMaskFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_audio_video_mask",
        Type = FragmentType.Latent,
        Title = "LTX Audio/Video Mask",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_audio_video_mask";
        public double VideoFps { get; set; } = 24.0;
        public double VideoStartTime { get; set; } = 0.0;
        public double VideoEndTime { get; set; } = 15.0;
        public double AudioStartTime { get; set; } = 0.0;
        public double AudioEndTime { get; set; } = 10000.0;
        public string MaxLength { get; set; } = "pad";
        public string ExistingMaskMode { get; set; } = "add";
        public string VideoLatentInputName { get; set; } = "video_latent";
        public string AudioLatentInputName { get; set; } = "audio_latent";
        public string? VideoLatentOutputName { get; set; }
        public string? AudioLatentOutputName { get; set; }
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
        var nodeId = $"{scope}{p.NodeId}";
        var videoLatentRef = registry.GetRef($"{scope}{p.VideoLatentInputName}");
        var audioLatentRef = registry.GetRef($"{scope}{p.AudioLatentInputName}");

        builder.AddNode(nodeId, node => node
            .Type("LTXVAudioVideoMask")
            .Title($"{scopeTitle}LTXV Audio/Video Mask")
            .InputRef("video_latent", videoLatentRef)
            .InputRef("audio_latent", audioLatentRef)
            .Input("video_fps", p.VideoFps)
            .Input("video_start_time", p.VideoStartTime)
            .Input("video_end_time", p.VideoEndTime)
            .Input("audio_start_time", p.AudioStartTime)
            .Input("audio_end_time", p.AudioEndTime)
            .Input("max_length", p.MaxLength)
            .Input("existing_mask_mode", p.ExistingMaskMode));

        registry.Register($"{scope}{p.VideoLatentOutputName ?? p.VideoLatentInputName}", nodeId, 0);
        registry.Register($"{scope}{p.AudioLatentOutputName ?? p.AudioLatentInputName}", nodeId, 1);
    }
}
