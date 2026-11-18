using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanVaceImageBatchJoinFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_vace_image_batch_join",
        Type = FragmentType.Output,
        Title = "Wan VACE Image Batch Join",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var firstBatchId = $"{scope}batch_start_generated";
        var finalBatchId = $"{scope}batch_joined_video";

        builder.AddNode(firstBatchId, node => node
            .Type("ImageBatch")
            .Title($"{scopeTitle}Batch Start And Generated")
            .InputRef("image1", registry.GetRef("start_images"))
            .InputRef("image2", registry.GetRef("image_output")));

        builder.AddNode(finalBatchId, node => node
            .Type("ImageBatch")
            .Title($"{scopeTitle}Batch Joined Video")
            .InputFromNode("image1", firstBatchId, 0)
            .InputRef("image2", registry.GetRef("end_images")));

        registry.Register("image_output", finalBatchId, 0);
    }
}