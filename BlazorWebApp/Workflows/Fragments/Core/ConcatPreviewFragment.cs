using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that creates a side-by-side preview concatenation.
/// Combines output + (source stacked with pose) using ImageConcatMulti.
/// Requires registry: image_output, resized_image, pose_images.
/// Registers preview_concat.
/// </summary>
public class ConcatPreviewFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "concat_preview",
        Type = FragmentType.Utility,
        Title = "Preview Concatenation",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope,
        string scopeTitle)
    {
        var concatSourcePoseId = $"{scope}concat_source_pose";
        var concatFinalId = $"{scope}concat_final";

        var resizedImageRef = registry.GetRef("resized_image");
        var poseImagesRef = registry.GetRef("pose_images");
        var imageOutputRef = registry.GetRef("image_output");

        // Concat Source + Pose (stacked vertically)
        builder.AddNode(concatSourcePoseId, node => node
            .Type("ImageConcatMulti")
            .Title($"{scopeTitle}Concat Source + Pose")
            .Input("inputcount", 2)
            .Input("direction", "down")
            .Input("match_image_size", false)
            .Input("Update inputs", "")
            .InputRef("image_1", resizedImageRef)
            .InputRef("image_2", poseImagesRef));

        // Concat Output + Preview (side by side)
        builder.AddNode(concatFinalId, node => node
            .Type("ImageConcatMulti")
            .Title($"{scopeTitle}Concat Output + Preview")
            .Input("inputcount", 2)
            .Input("direction", "left")
            .Input("match_image_size", true)
            .Input("Update inputs", "")
            .InputRef("image_1", imageOutputRef)
            .InputFromNode("image_2", concatSourcePoseId, 0));

        registry.Register($"{scope}preview_concat", concatFinalId, 0);
    }
}
