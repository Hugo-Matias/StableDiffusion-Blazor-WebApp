using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that gets the size and frame count from an image batch.
/// Registers image_size_info, width, height, and num_frames outputs.
/// </summary>
public class GetImageSizeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "get_image_size",
        Type = FragmentType.Utility,
        Title = "Get Image Size",
        IsHidden = true
    };

    public class Parameters
    {
        public string ImageRefName { get; set; } = "video_frames";
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
        var nodeId = $"{scope}get_size";
        var imageRef = registry.GetRef(p.ImageRefName);

        builder.AddNode(nodeId, node => node
            .Type("GetImageSizeAndCount")
            .Title($"{scopeTitle}Get Image Size & Count")
            .InputRef("image", imageRef));

        registry.Register($"{scope}image_size_info", nodeId, 0);
        registry.Register($"{scope}width", nodeId, 1);
        registry.Register($"{scope}height", nodeId, 2);
        registry.Register($"{scope}num_frames", nodeId, 3);
    }
}
