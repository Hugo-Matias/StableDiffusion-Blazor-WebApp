using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that renders NLF poses for SCAIL workflows.
/// Pipeline: DownloadAndLoadNLFModel -> NLFPredict -> RenderNLFPoses
/// Requires registry: video_frames, dw_poses (from SCAILPoseDetectionFragment), ref_dw_pose.
/// Registers pose_images.
/// </summary>
public class SCAILPoseRenderingFragment : IFragmentBuilder
{
    private const string DefaultNlfUrl = "https://github.com/isarandi/nlf/releases/download/v0.3.2/nlf_l_multi_0.3.2.torchscript";

    public FragmentMetadata Metadata => new()
    {
        Id = "scail_pose_rendering",
        Type = FragmentType.Conditioning,
        Title = "SCAIL Pose Rendering",
        IsHidden = true
    };

    public class Parameters
    {
        public string NlfUrl { get; set; } = DefaultNlfUrl;
        public int PerBatch { get; set; } = -1;
        public bool DrawFace { get; set; } = true;
        public bool DrawHands { get; set; } = true;
        public bool ScaleHands { get; set; } = true;
        public string RenderDevice { get; set; } = "gpu";
        public string RenderBackend { get; set; } = "taichi";
        public bool UseDwPoseAlignment { get; set; } = true;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var poseDetectionFragment = parameters.GetFragment("scail_pose_detection");
        var detectHands = poseDetectionFragment?.GetBool("detect_hand", true) ?? true;

        BuildInternal(builder, registry, new Parameters
        {
            NlfUrl = fragment?.GetString("nlf_url", DefaultNlfUrl) ?? DefaultNlfUrl,
            PerBatch = fragment?.GetInt("per_batch", -1) ?? -1,
            DrawFace = fragment?.GetBool("draw_face", true) ?? true,
            DrawHands = detectHands && (fragment?.GetBool("draw_hands", true) ?? true),
            ScaleHands = detectHands && (fragment?.GetBool("scale_hands", true) ?? true),
            RenderDevice = fragment?.GetString("render_device", "gpu") ?? "gpu",
            RenderBackend = fragment?.GetString("render_backend", "taichi") ?? "taichi",
            UseDwPoseAlignment = fragment?.GetBool("use_dw_pose_alignment", true) ?? true
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
        var nlfModelId = $"{scope}nlf_model";
        var nlfPredictId = $"{scope}nlf_predict";
        var renderPosesId = $"{scope}render_poses";

        var videoFramesRef = registry.GetRef("video_frames");
        var widthRef = registry.HasOutput("render_width") ? registry.GetRef("render_width") : registry.GetRef("width");
        var heightRef = registry.HasOutput("render_height") ? registry.GetRef("render_height") : registry.GetRef("height");

        // DownloadAndLoadNLFModel - loads the NLF model for pose prediction
        builder.AddNode(nlfModelId, node => node
            .Type("DownloadAndLoadNLFModel")
            .Title($"{scopeTitle}Load NLF Model")
            .Input("url", p.NlfUrl)
            .Input("warmup", true));

        // NLFPredict - runs non-linear flow prediction on video frames
        builder.AddNode(nlfPredictId, node => node
            .Type("NLFPredict")
            .Title($"{scopeTitle}NLF Predict")
            .Input("per_batch", p.PerBatch)
            .InputFromNode("model", nlfModelId, 0)
            .InputRef("images", videoFramesRef));

        // RenderNLFPoses - renders pose images from NLF predictions + DW poses
        builder.AddNode(renderPosesId, node =>
        {
            node.Type("RenderNLFPoses")
                .Title($"{scopeTitle}Render NLF Poses")
                .Input("draw_face", p.DrawFace)
                .Input("draw_hands", p.DrawHands)
                .Input("render_device", p.RenderDevice)
                .Input("scale_hands", p.ScaleHands)
                .Input("render_backend", p.RenderBackend)
                .InputFromNode("nlf_poses", nlfPredictId, 0)
                .InputRef("width", widthRef)
                .InputRef("height", heightRef);

            if (p.UseDwPoseAlignment)
            {
                node.InputRef("dw_poses", registry.GetRef($"{scope}dw_poses"))
                    .InputRef("ref_dw_pose", registry.GetRef($"{scope}ref_dw_pose"));
            }
        });

        registry.Register($"{scope}pose_images", renderPosesId, 0);
        registry.Register($"{scope}pose_mask", renderPosesId, 1);
    }
}
