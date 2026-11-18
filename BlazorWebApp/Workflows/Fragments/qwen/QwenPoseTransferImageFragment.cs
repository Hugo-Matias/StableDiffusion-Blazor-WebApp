using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenPoseTransferImageFragment : IFragmentBuilder
{
    public const string TargetImageOutput = "target_image_output";
    public const string PoseImageOutput = "pose_image_output";

    public FragmentMetadata Metadata => new()
    {
        Id = "qwen_pose_transfer_images",
        Type = FragmentType.Input,
        Title = "Qwen Pose Transfer Images",
        IsHidden = true
    };

    public class Parameters
    {
        public string TargetImage { get; set; } = string.Empty;
        public string PoseReferenceImage { get; set; } = string.Empty;
        public string PoseCheckpoint { get; set; } = "SDPose/sdpose_wholebody_fp16.safetensors";
        public int ScaleLength { get; set; } = 1280;
        public int PoseBatchSize { get; set; } = 16;
        public bool DrawBody { get; set; } = true;
        public bool DrawHands { get; set; } = true;
        public bool DrawFace { get; set; }
        public bool DrawFeet { get; set; }
        public int StickWidth { get; set; } = 4;
        public int FacePointSize { get; set; } = 2;
        public double ScoreThreshold { get; set; } = 0.3;
    }

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
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        builder.AddNode("target_image_loader", node => node
            .Type("LoadImage")
            .Title("Target Image")
            .Input("image", fragmentParams.TargetImage));

        AddAspectScale(builder, "target_image_scale", "Target Image Scale", "target_image_loader", fragmentParams.ScaleLength);
        registry.Register(TargetImageOutput, "target_image_scale", 0);

        builder.AddNode("pose_reference_loader", node => node
            .Type("LoadImage")
            .Title("Pose Reference Image")
            .Input("image", fragmentParams.PoseReferenceImage));

        AddAspectScale(builder, "pose_reference_scale", "Pose Reference Scale", "pose_reference_loader", fragmentParams.ScaleLength);

        builder.AddNode("pose_checkpoint_loader", node => node
            .Type("Checkpoint Loader (Simple)")
            .Title("Load SDPose Checkpoint")
            .Input("ckpt_name", fragmentParams.PoseCheckpoint));

        builder.AddNode("pose_keypoint_extractor", node => node
            .Type("SDPoseKeypointExtractor")
            .Title("SDPose Keypoint Extractor")
            .Input("batch_size", fragmentParams.PoseBatchSize)
            .InputFromNode("model", "pose_checkpoint_loader", 0)
            .InputFromNode("vae", "pose_checkpoint_loader", 2)
            .InputFromNode("image", "pose_reference_scale", 0));

        builder.AddNode("pose_keypoint_draw", node => node
            .Type("SDPoseDrawKeypoints")
            .Title("Draw Pose Keypoints")
            .Input("draw_body", fragmentParams.DrawBody)
            .Input("draw_hands", fragmentParams.DrawHands)
            .Input("draw_face", fragmentParams.DrawFace)
            .Input("draw_feet", fragmentParams.DrawFeet)
            .Input("stick_width", fragmentParams.StickWidth)
            .Input("face_point_size", fragmentParams.FacePointSize)
            .Input("score_threshold", fragmentParams.ScoreThreshold)
            .InputFromNode("keypoints", "pose_keypoint_extractor", 0));

        AddAspectScale(builder, "pose_image_scale", "Pose Image Scale", "pose_keypoint_draw", fragmentParams.ScaleLength);
        registry.Register(PoseImageOutput, "pose_image_scale", 0);
    }

    private static void AddAspectScale(
        ComfyWorkflowBuilder builder,
        string nodeId,
        string title,
        string imageNodeId,
        int scaleLength)
    {
        builder.AddNode(nodeId, node => node
            .Type("LayerUtility: ImageScaleByAspectRatio V2")
            .Title(title)
            .Input("aspect_ratio", "original")
            .Input("proportional_width", 1)
            .Input("proportional_height", 1)
            .Input("fit", "crop")
            .Input("method", "lanczos")
            .Input("round_to_multiple", "8")
            .Input("scale_to_side", "longest")
            .Input("scale_to_length", scaleLength)
            .Input("background_color", "#000000")
            .InputFromNode("image", imageNodeId, 0));
    }
}
