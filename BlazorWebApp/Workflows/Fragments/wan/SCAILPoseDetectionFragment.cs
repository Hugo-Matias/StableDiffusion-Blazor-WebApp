using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that performs DW pose detection for SCAIL workflows.
/// Pipeline: OnnxDetectionModelLoader -> PoseDetectionVitPoseToDWPose for video and reference images.
/// Requires registry: video_frames, ref_image.
/// Registers dw_poses, ref_dw_pose.
/// </summary>
public class SCAILPoseDetectionFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "scail_pose_detection",
        Type = FragmentType.Conditioning,
        Title = "SCAIL Pose Detection",
        Component = "SCAILPoseDetectionForm",
        IsHidden = false
    };

    public class Parameters
    {
        public string ImageRefName { get; set; } = "video_frames";
        public bool DetectHand { get; set; } = true;
        public string VitposeModel { get; set; } = "vitpose-l-wholebody.onnx";
        public string YoloModel { get; set; } = "yolov10m.onnx";
        public string OnnxDevice { get; set; } = "CUDAExecutionProvider";
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
            ImageRefName = fragment?.GetString("image_ref_name", "video_frames") ?? "video_frames",
            DetectHand = fragment?.GetBool("detect_hand", true) ?? true,
            VitposeModel = fragment?.GetString("vitpose_model", "vitpose-l-wholebody.onnx") ?? "vitpose-l-wholebody.onnx",
            YoloModel = fragment?.GetString("yolo_model", "yolov10m.onnx") ?? "yolov10m.onnx",
            OnnxDevice = fragment?.GetString("onnx_device", "CUDAExecutionProvider") ?? "CUDAExecutionProvider"
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
        var imageRef = registry.GetRef(p.ImageRefName);
        var onnxLoaderId = $"{scope}onnx_detection_loader";
        var vitposeDetectId = $"{scope}vitpose_detect";
        var refVitposeDetectId = $"{scope}ref_vitpose_detect";

        builder.AddNode(onnxLoaderId, node => node
            .Type("OnnxDetectionModelLoader")
            .Title($"{scopeTitle}Load ONNX Detection Models")
            .Input("vitpose_model", p.VitposeModel)
            .Input("yolo_model", p.YoloModel)
            .Input("onnx_device", p.OnnxDevice));

        builder.AddNode(vitposeDetectId, node => node
            .Type("PoseDetectionVitPoseToDWPose")
            .Title($"{scopeTitle}VitPose Detection (Video)")
            .InputFromNode("vitpose_model", onnxLoaderId, 0)
            .InputRef("images", imageRef));

        registry.Register($"{scope}dw_poses", vitposeDetectId, 0);

        var refImageRef = registry.GetRef("ref_image");
        builder.AddNode(refVitposeDetectId, node => node
            .Type("PoseDetectionVitPoseToDWPose")
            .Title($"{scopeTitle}VitPose Detection (Ref Image)")
            .InputFromNode("vitpose_model", onnxLoaderId, 0)
            .InputRef("images", refImageRef));

        registry.Register($"{scope}ref_dw_pose", refVitposeDetectId, 0);
    }
}
