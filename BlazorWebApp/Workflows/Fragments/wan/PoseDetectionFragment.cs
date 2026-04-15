using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that performs pose detection and drawing for SteadyDancer.
/// Pipeline: OnnxDetectionModelLoader -> PoseAndFaceDetection -> DrawViTPose -> ImageResizeKJv2
/// Requires registry: image_size_info, width, height.
/// Registers pose_images.
/// </summary>
public class PoseDetectionFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "pose_detection",
        Type = FragmentType.Conditioning,
        Title = "Pose Detection",
        IsHidden = true
    };

    public class Parameters
    {
        public string VitposeModel { get; set; } = "onnx\\vitpose_h_wholebody_model.onnx";
        public string YoloModel { get; set; } = "onnx\\yolov10m.onnx";
        public string OnnxDevice { get; set; } = "CUDAExecutionProvider";
        public int RetargetPadding { get; set; } = 16;
        public int BodyStickWidth { get; set; } = -1;
        public int HandStickWidth { get; set; } = -1;
        public bool DrawHead { get; set; } = true;
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
        var onnxLoaderId = $"{scope}onnx_loader";
        var poseDetectionId = $"{scope}pose_detection";
        var drawPoseId = $"{scope}draw_pose";
        var resizePoseId = $"{scope}resize_pose";

        var widthRef = registry.GetRef("width");
        var heightRef = registry.GetRef("height");
        var imageSizeRef = registry.GetRef("image_size_info");

        // ONNX Detection Model Loader
        builder.AddNode(onnxLoaderId, node => node
            .Type("OnnxDetectionModelLoader")
            .Title($"{scopeTitle}ONNX Detection Model Loader")
            .Input("vitpose_model", p.VitposeModel)
            .Input("yolo_model", p.YoloModel)
            .Input("onnx_device", p.OnnxDevice));

        // Pose and Face Detection
        builder.AddNode(poseDetectionId, node => node
            .Type("PoseAndFaceDetection")
            .Title($"{scopeTitle}Pose and Face Detection")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .InputFromNode("model", onnxLoaderId, 0)
            .InputRef("images", imageSizeRef));

        // Draw ViT Pose
        builder.AddNode(drawPoseId, node => node
            .Type("DrawViTPose")
            .Title($"{scopeTitle}Draw ViT Pose")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .Input("retarget_padding", p.RetargetPadding)
            .Input("body_stick_width", p.BodyStickWidth)
            .Input("hand_stick_width", p.HandStickWidth)
            .Input("draw_head", p.DrawHead)
            .InputFromNode("pose_data", poseDetectionId, 0));

        // Resize Pose Images
        builder.AddNode(resizePoseId, node => node
            .Type("ImageResizeKJv2")
            .Title($"{scopeTitle}Resize Pose Images")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", drawPoseId, 0));

        registry.Register($"{scope}pose_images", resizePoseId, 0);
    }
}
