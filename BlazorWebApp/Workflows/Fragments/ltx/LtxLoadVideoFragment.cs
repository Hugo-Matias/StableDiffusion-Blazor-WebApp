using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Loads a user-supplied source video and prepares the supporting handles
/// the V2V pipeline needs: a resized image batch, the resolved width /
/// height / frame count, and the first / last frames extracted as
/// single-frame batches.
///
/// Pipeline:
///   <c>VHS_LoadVideoFFmpeg</c>
///   -&gt; <c>ResizeImagesByLongerEdge</c>
///   -&gt; <c>GetImageSizeAndCount</c>
///   -&gt; <c>GetImageRangeFromBatch</c> (first frame)
///   -&gt; <c>GetImageRangeFromBatch</c> (last frame).
///
/// Reads <c>parameters.Sources["source_video"]</c> for the filename
/// (falls back to <c>FilePath</c>).
///
/// Registers:
/// <list type="bullet">
///   <item><c>{scope}loaded_video_images</c> - resized image batch.</item>
///   <item><c>{scope}loaded_video_first_frame</c></item>
///   <item><c>{scope}loaded_video_last_frame</c></item>
///   <item><c>{scope}loaded_video_width</c></item>
///   <item><c>{scope}loaded_video_height</c></item>
///   <item><c>{scope}loaded_video_frame_count</c></item>
/// </list>
/// </summary>
public class LtxLoadVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_load_video",
        Type = FragmentType.Input,
        Title = "LTX Load Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string VideoPath { get; set; } = "";
        public int ForceRate { get; set; } = 0;
        public int FrameLoadCap { get; set; } = 0;
        public double StartTime { get; set; } = 0.0;
        public string Format { get; set; } = "LTXV";
        public int MaxLongerEdge { get; set; } = 1024;
        public string SourceKey { get; set; } = "source_video";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var p = new Parameters();
        if (parameters.Sources is not null
            && parameters.Sources.TryGetValue(p.SourceKey, out var src) && src is not null)
        {
            p.VideoPath = src.Filename ?? src.FilePath ?? "";
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
        var loadId = $"{scope}ltx_load_video";
        var resizeId = $"{scope}ltx_video_resize";
        var sizeId = $"{scope}ltx_video_size";
        var firstId = $"{scope}ltx_video_first_frame";
        var lastId = $"{scope}ltx_video_last_frame";

        builder.AddNode(loadId, node => node
            .Type("VHS_LoadVideoFFmpeg")
            .Title($"{scopeTitle}Load Video (FFmpeg)")
            .Input("video", p.VideoPath)
            .Input("force_rate", p.ForceRate)
            .Input("custom_width", 0)
            .Input("custom_height", 0)
            .Input("frame_load_cap", p.FrameLoadCap)
            .Input("start_time", p.StartTime)
            .Input("format", p.Format));

        builder.AddNode(resizeId, node => node
            .Type("ResizeImagesByLongerEdge")
            .Title($"{scopeTitle}Resize Video (Longer Edge)")
            .InputFromNode("images", loadId, 0)
            .Input("max_size", p.MaxLongerEdge));

        builder.AddNode(sizeId, node => node
            .Type("GetImageSizeAndCount")
            .Title($"{scopeTitle}Video Size + Count")
            .InputFromNode("image", resizeId, 0));

        builder.AddNode(firstId, node => node
            .Type("GetImageRangeFromBatch")
            .Title($"{scopeTitle}First Frame")
            .InputFromNode("images", resizeId, 0)
            .Input("start_index", 0)
            .Input("num_frames", 1));

        builder.AddNode(lastId, node => node
            .Type("GetImageRangeFromBatch")
            .Title($"{scopeTitle}Last Frame")
            .InputFromNode("images", resizeId, 0)
            .Input("start_index", -1)
            .Input("num_frames", 1));

        registry.Register($"{scope}loaded_video_images", resizeId, 0);
        // GetImageSizeAndCount outputs: image[0], width[1], height[2], count[3].
        registry.Register($"{scope}loaded_video_width", sizeId, 1);
        registry.Register($"{scope}loaded_video_height", sizeId, 2);
        registry.Register($"{scope}loaded_video_frame_count", sizeId, 3);
        registry.Register($"{scope}loaded_video_first_frame", firstId, 0);
        registry.Register($"{scope}loaded_video_last_frame", lastId, 0);
    }
}
