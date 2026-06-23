using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxAddGuideFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_add_guide",
        Type = FragmentType.Conditioning,
        Title = "LTX Add Guide",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}{p.NodeId}";

        builder.AddNode(nodeId, node => node
            .Type("LTXVAddGuide")
            .Title($"{scopeTitle}{p.Title}")
            .InputRef("positive", registry.GetRef($"{scope}{p.PositiveInputName}"))
            .InputRef("negative", registry.GetRef($"{scope}{p.NegativeInputName}"))
            .InputRef("vae", registry.GetRef($"{scope}{p.VaeInputName}"))
            .InputRef("latent", registry.GetRef($"{scope}{p.LatentInputName}"))
            .InputRef("image", registry.GetRef($"{scope}{p.ImageInputName}"))
            .Input("frame_idx", p.FrameIndex)
            .Input("strength", p.Strength));

        registry.Register($"{scope}{p.PositiveOutputName ?? p.PositiveInputName}", nodeId, 0);
        registry.Register($"{scope}{p.NegativeOutputName ?? p.NegativeInputName}", nodeId, 1);
        registry.Register($"{scope}{p.LatentOutputName ?? p.LatentInputName}", nodeId, 2);
    }

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_add_guide";
        public string Title { get; set; } = "LTXVAddGuide";
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        public string VaeInputName { get; set; } = "vae_output";
        public string LatentInputName { get; set; } = "video_latent";
        public string ImageInputName { get; set; } = "first_frame_image";
        public string? PositiveOutputName { get; set; }
        public string? NegativeOutputName { get; set; }
        public string? LatentOutputName { get; set; }
        public int FrameIndex { get; set; } = 0;
        public double Strength { get; set; } = 1.0;
    }
}