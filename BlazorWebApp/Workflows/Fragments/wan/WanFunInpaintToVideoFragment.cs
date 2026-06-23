using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanFunInpaintToVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_fun_inpaint_to_video",
        Type = FragmentType.Conditioning,
        Title = "Wan Fun Inpaint To Video",
        IsHidden = true
    };

    public class Parameters
    {
        public int Length { get; set; } = 81;
        public int BatchSize { get; set; } = 1;
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
        var nodeId = $"{scope}wan_fun_inpaint_to_video";

        builder.AddNode(nodeId, node => node
            .Type("WanFunInpaintToVideo")
            .Title($"{scopeTitle}Wan Fun Inpaint To Video")
            .InputRef("positive", registry.GetRef("positive_output"))
            .InputRef("negative", registry.GetRef("negative_output"))
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height"))
            .Input("length", fragmentParams.Length)
            .Input("batch_size", fragmentParams.BatchSize)
            .InputRef("start_image", registry.GetRef("start_image_output"))
            .InputRef("end_image", registry.GetRef("end_image_output")));

        registry.Register("positive_output", nodeId, 0);
        registry.Register("negative_output", nodeId, 1);
        registry.Register("latent_output", nodeId, 2);
    }
}
