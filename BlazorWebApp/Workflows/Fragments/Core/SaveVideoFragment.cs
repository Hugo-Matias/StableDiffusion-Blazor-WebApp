using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that saves video frames using VHS_VideoCombine.
/// This is a terminal node for video workflows.
/// </summary>
public class SaveVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "save_video",
        Type = FragmentType.Output,
        Title = "Save Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "video_save";
        public int FrameRate { get; set; } = 16;
        public string ImageInputName { get; set; } = "image_output";
        public string FilenamePrefix { get; set; } = "tmp/vid";
        public string Format { get; set; } = "video/h264-mp4";
        public string PixFmt { get; set; } = "yuv420p";
        public int Crf { get; set; } = 19;
        public string Title { get; set; } = "Video Combine";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var frameRate = fragment?.GetInt("frame_rate", 16) ?? 16;
        var imageInputName = fragment?.GetString("image_input_name", "image_output") ?? "image_output";
        var filenamePrefix = fragment?.GetString("filename_prefix", "tmp/vid") ?? "tmp/vid";

        BuildInternal(builder, registry, new Parameters
        {
            FrameRate = frameRate,
            ImageInputName = imageInputName,
            FilenamePrefix = filenamePrefix
        }, scope, scopeTitle);
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
        var imageRef = registry.GetRef(p.ImageInputName);

        builder.AddNode(nodeId, node => node
            .Type("VHS_VideoCombine")
            .Title($"{scopeTitle}{p.Title}")
            .Input("frame_rate", p.FrameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", p.FilenamePrefix)
            .Input("format", p.Format)
            .Input("pix_fmt", p.PixFmt)
            .Input("crf", p.Crf)
            .Input("save_metadata", true)
            .Input("trim_to_audio", false)
            .Input("pingpong", false)
            .Input("save_output", true)
            .InputRef("images", imageRef));

        registry.Register($"{scope}video_save_node", nodeId, 0);
    }
}
