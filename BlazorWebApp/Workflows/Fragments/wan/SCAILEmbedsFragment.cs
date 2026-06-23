using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that builds the SCAIL embeds chain for video multi-character motion transfer.
/// Pipeline: EmptyEmbeds -> AddSCAILReferenceEmbeds -> AddSCAILPoseEmbeds
/// Requires registry: vae, ref_image, clip_embeds, pose_images, width, height, num_frames
/// Registers image_embeds (final output for sampler).
/// </summary>
public class SCAILEmbedsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "scail_embeds",
        Type = FragmentType.Conditioning,
        Title = "SCAIL Embeds Chain",
        Component = "SCAILEmbedsForm",
        IsHidden = false
    };

    public class Parameters
    {
        // Reference embeds
        public float RefStrength { get; set; } = 1.0f;
        public float RefStartPercent { get; set; } = 0.0f;
        public float RefEndPercent { get; set; } = 1.0f;

        // Pose embeds
        public float PoseStrength { get; set; } = 1.0f;
        public float PoseStartPercent { get; set; } = 0.0f;
        public float PoseEndPercent { get; set; } = 0.5f;
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
            RefStrength = fragment?.GetFloat("ref_strength", 1.0f) ?? 1.0f,
            RefStartPercent = fragment?.GetFloat("ref_start_percent", 0.0f) ?? 0.0f,
            RefEndPercent = fragment?.GetFloat("ref_end_percent", 1.0f) ?? 1.0f,
            PoseStrength = fragment?.GetFloat("pose_strength", 1.0f) ?? 1.0f,
            PoseStartPercent = fragment?.GetFloat("pose_start_percent", 0.0f) ?? 0.0f,
            PoseEndPercent = fragment?.GetFloat("pose_end_percent", 0.5f) ?? 0.5f
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
        var emptyEmbedsId = $"{scope}empty_embeds";
        var refEmbedsId = $"{scope}ref_embeds";
        var poseEmbedsId = $"{scope}pose_embeds";

        var vaeRef = registry.GetRef("vae");
        var refImageRef = registry.GetRef("ref_image");
        var clipEmbedsRef = registry.GetRef("clip_embeds");
        var poseImagesRef = registry.GetRef($"{scope}pose_images");
        var widthRef = registry.GetRef("width");
        var heightRef = registry.GetRef("height");
        var numFramesRef = registry.GetRef("num_frames");

        // 1. WanVideoEmptyEmbeds - creates empty embeds with correct dimensions
        builder.AddNode(emptyEmbedsId, node => node
            .Type("WanVideoEmptyEmbeds")
            .Title($"{scopeTitle}Empty Embeds")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .InputRef("num_frames", numFramesRef));

        // 2. WanVideoAddSCAILReferenceEmbeds - adds reference image conditioning
        builder.AddNode(refEmbedsId, node => node
            .Type("WanVideoAddSCAILReferenceEmbeds")
            .Title($"{scopeTitle}Add SCAIL Reference Embeds")
            .Input("strength", p.RefStrength)
            .Input("start_percent", p.RefStartPercent)
            .Input("end_percent", p.RefEndPercent)
            .InputFromNode("embeds", emptyEmbedsId, 0)
            .InputRef("vae", vaeRef)
            .InputRef("ref_image", refImageRef)
            .InputRef("clip_embeds", clipEmbedsRef));

        // 3. WanVideoAddSCAILPoseEmbeds - adds pose conditioning
        builder.AddNode(poseEmbedsId, node => node
            .Type("WanVideoAddSCAILPoseEmbeds")
            .Title($"{scopeTitle}Add SCAIL Pose Embeds")
            .Input("strength", p.PoseStrength)
            .Input("start_percent", p.PoseStartPercent)
            .Input("end_percent", p.PoseEndPercent)
            .InputFromNode("embeds", refEmbedsId, 0)
            .InputRef("vae", vaeRef)
            .InputRef("pose_images", poseImagesRef));

        registry.Register($"{scope}image_embeds", poseEmbedsId, 0);
    }
}
