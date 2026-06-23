using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads video frames using VHS_LoadVideo.
/// Registers video_frames, frame_count, and audio outputs.
/// </summary>
public class LoadVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_video",
        Type = FragmentType.Input,
        Title = "Load Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string Video { get; set; } = "";
        public double ForceRate { get; set; } = 16;
        public int CustomWidth { get; set; } = 480;
        public int CustomHeight { get; set; } = 832;
        public int FrameLoadCap { get; set; } = 176;
        public int SkipFirstFrames { get; set; } = 0;
        public int SelectEveryNth { get; set; } = 1;
        public string Format { get; set; } = "AnimateDiff";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        BuildInternal(builder, registry, new Parameters
        {
            Video = fragment?.GetString("video", "") ?? "",
            ForceRate = fragment?.GetDouble("force_rate", 16) ?? 16,
            CustomWidth = fragment?.GetInt("custom_width", 480) ?? 480,
            CustomHeight = fragment?.GetInt("custom_height", 832) ?? 832,
            FrameLoadCap = fragment?.GetInt("frame_load_cap", 176) ?? 176,
            SkipFirstFrames = fragment?.GetInt("skip_first_frames", 0) ?? 0,
            SelectEveryNth = fragment?.GetInt("select_every_nth", 1) ?? 1
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
        var nodeId = $"{scope}load_video";

        builder.AddNode(nodeId, node => node
            .Type("VHS_LoadVideo")
            .Title($"{scopeTitle}Load Video")
            .Input("video", p.Video)
            .Input("force_rate", p.ForceRate)
            .Input("custom_width", p.CustomWidth)
            .Input("custom_height", p.CustomHeight)
            .Input("frame_load_cap", p.FrameLoadCap)
            .Input("skip_first_frames", p.SkipFirstFrames)
            .Input("select_every_nth", p.SelectEveryNth)
            .Input("format", p.Format));

        registry.Register($"{scope}video_frames", nodeId, 0);
        registry.Register($"{scope}frame_count", nodeId, 1);
        registry.Register($"{scope}audio", nodeId, 2);
    }
}
