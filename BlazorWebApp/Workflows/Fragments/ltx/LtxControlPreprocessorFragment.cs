using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Generates an IC-LoRA control image batch from a reference video by
/// running <c>DWPreprocessor</c> (mode <c>dwpose</c>) on each frame and
/// then multiply-blending the resulting pose skeleton against the
/// original frames. The blended output combines the strong pose signal
/// with low-frequency luminance from the source video, which is the
/// pattern used by the upstream
/// <c>LTX-2.3 - IV2V_TV2V_transfer_body_movements_IC-Union-Control-lora_DWPose.json</c>
/// workflow.
///
/// Pipeline:
///   <c>DWPreprocessor</c> -&gt; <c>ImageBlend</c> (pose, source, multiply, 0.5).
///
/// Reads <c>{scope}{ImagesInputName}</c> (default
/// <c>loaded_video_images</c>).
/// Registers <c>{scope}control_image</c> (pose-blended frames) and
/// <c>{scope}control_pose</c> (raw pose skeleton).
/// </summary>
public class LtxControlPreprocessorFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_control_preprocessor",
        Type = FragmentType.Conditioning,
        Title = "LTX Control Preprocessor",
        IsHidden = true
    };

    public class Parameters
    {
        public string ImagesInputName { get; set; } = "loaded_video_images";
        public string DetectHand { get; set; } = "enable";
        public string DetectBody { get; set; } = "enable";
        public string DetectFace { get; set; } = "enable";
        public int Resolution { get; set; } = 576;
        public string BboxDetector { get; set; } = "yolox_l.onnx";
        public string PoseEstimator { get; set; } = "dw-ll_ucoco_384_bs5.torchscript.pt";
        public string ScaleStickForXinsr { get; set; } = "disable";
        public double BlendFactor { get; set; } = 0.5;
        public string BlendMode { get; set; } = "multiply";
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
        var dwId = $"{scope}ltx_dwpose";
        var blendId = $"{scope}ltx_control_blend";
        var imagesRef = registry.GetRef($"{scope}{p.ImagesInputName}");

        builder.AddNode(dwId, node => node
            .Type("DWPreprocessor")
            .Title($"{scopeTitle}DWPose Preprocessor")
            .InputRef("image", imagesRef)
            .Input("detect_hand", p.DetectHand)
            .Input("detect_body", p.DetectBody)
            .Input("detect_face", p.DetectFace)
            .Input("resolution", p.Resolution)
            .Input("bbox_detector", p.BboxDetector)
            .Input("pose_estimator", p.PoseEstimator)
            .Input("scale_stick_for_xinsr_cn", p.ScaleStickForXinsr));

        builder.AddNode(blendId, node => node
            .Type("ImageBlend")
            .Title($"{scopeTitle}Pose x Source (multiply)")
            .InputFromNode("image1", dwId, 0)
            .InputRef("image2", imagesRef)
            .Input("blend_factor", p.BlendFactor)
            .Input("blend_mode", p.BlendMode));

        registry.Register($"{scope}control_pose", dwId, 0);
        registry.Register($"{scope}control_image", blendId, 0);
    }
}
