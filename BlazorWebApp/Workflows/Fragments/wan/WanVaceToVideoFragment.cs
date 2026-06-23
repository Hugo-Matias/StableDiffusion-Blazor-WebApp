using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanVaceToVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_vace_to_video",
        Type = FragmentType.Conditioning,
        Title = "Wan VACE To Video",
        IsHidden = true
    };

    public class Parameters
    {
        public int BatchSize { get; set; } = 1;
        public double Strength { get; set; } = 1;
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
        var nodeId = $"{scope}wan_vace_to_video";

        builder.AddNode(nodeId, node => node
            .Type("WanVaceToVideo")
            .Title($"{scopeTitle}Wan VACE To Video")
            .InputRef("positive", registry.GetRef("positive_output"))
            .InputRef("negative", registry.GetRef("negative_output"))
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputRef("control_video", registry.GetRef("control_video"))
            .InputRef("control_masks", registry.GetRef("control_mask"))
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height"))
            .InputRef("length", registry.GetRef("length"))
            .Input("batch_size", fragmentParams.BatchSize)
            .Input("strength", fragmentParams.Strength));

        registry.Register("positive_output", nodeId, 0);
        registry.Register("negative_output", nodeId, 1);
        registry.Register("latent_output", nodeId, 2);
        registry.Register("trim_latent", nodeId, 3);
    }
}