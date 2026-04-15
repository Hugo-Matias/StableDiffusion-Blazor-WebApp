using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that creates SteadyDancer embeds combining pose latents and CLIP vision.
/// Pipeline: WanVideoEncode (pose) + GetImageRangeFromBatch + WanVideoClipVisionEncode (pose) -> WanVideoAddSteadyDancerEmbeds
/// Requires registry: vae_output, pose_images, clip_vision_output, image_embeds.
/// Registers steadydancer_embeds.
/// </summary>
public class SteadyDancerEmbedsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "steadydancer_embeds",
        Type = FragmentType.Conditioning,
        Title = "SteadyDancer Embeds",
        IsHidden = true
    };

    public class Parameters
    {
        public double PoseLatentStrength { get; set; } = 1;
        public double PoseClipStrength { get; set; } = 1;
        public double PoseStrengthSpatial { get; set; } = 1;
        public double PoseStrengthTemporal { get; set; } = 1;
        public double PoseStartPercent { get; set; } = 0;
        public double PoseEndPercent { get; set; } = 1;
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
        var poseEncodeId = $"{scope}pose_encode";
        var getFirstPoseId = $"{scope}get_first_pose";
        var poseClipVisionId = $"{scope}pose_clip_vision";
        var addSteadydancerId = $"{scope}add_steadydancer";

        var vaeRef = registry.GetRef($"{scope}vae_output");
        var poseImagesRef = registry.GetRef("pose_images");
        var clipVisionRef = registry.GetRef($"{scope}clip_vision_output");
        var imageEmbedsRef = registry.GetRef("image_embeds");

        // Pose Encode (WanVideoEncode)
        builder.AddNode(poseEncodeId, node => node
            .Type("WanVideoEncode")
            .Title($"{scopeTitle}Pose Encode")
            .Input("enable_vae_tiling", false)
            .Input("tile_x", 272)
            .Input("tile_y", 272)
            .Input("tile_stride_x", 144)
            .Input("tile_stride_y", 128)
            .Input("noise_aug_strength", 0.0)
            .Input("latent_strength", p.PoseLatentStrength)
            .InputRef("vae", vaeRef)
            .InputRef("image", poseImagesRef));

        // Get First Pose Frame
        builder.AddNode(getFirstPoseId, node => node
            .Type("GetImageRangeFromBatch")
            .Title($"{scopeTitle}Get First Pose Frame")
            .Input("start_index", 0)
            .Input("num_frames", 1)
            .InputRef("images", poseImagesRef));

        // CLIP Vision Encode (Pose)
        builder.AddNode(poseClipVisionId, node => node
            .Type("WanVideoClipVisionEncode")
            .Title($"{scopeTitle}CLIP Vision Encode (Pose)")
            .Input("strength_1", p.PoseClipStrength)
            .Input("strength_2", 1.0)
            .Input("crop", "center")
            .Input("combine_embeds", "average")
            .Input("force_offload", true)
            .Input("tiles", 0)
            .Input("ratio", 0.2)
            .InputRef("clip_vision", clipVisionRef)
            .InputFromNode("image_1", getFirstPoseId, 0));

        // Add SteadyDancer Embeds
        builder.AddNode(addSteadydancerId, node => node
            .Type("WanVideoAddSteadyDancerEmbeds")
            .Title($"{scopeTitle}Add SteadyDancer Embeds")
            .Input("pose_strength_spatial", p.PoseStrengthSpatial)
            .Input("pose_strength_temporal", p.PoseStrengthTemporal)
            .Input("start_percent", p.PoseStartPercent)
            .Input("end_percent", p.PoseEndPercent)
            .InputRef("embeds", imageEmbedsRef)
            .InputFromNode("pose_latents_positive", poseEncodeId, 0)
            .InputFromNode("clip_vision_embeds", poseClipVisionId, 0));

        registry.Register($"{scope}steadydancer_embeds", addSteadydancerId, 0);
    }
}
