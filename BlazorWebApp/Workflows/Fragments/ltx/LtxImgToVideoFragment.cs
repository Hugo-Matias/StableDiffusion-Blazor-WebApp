using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that injects a source image into the video latent using LTXVImgToVideoInplace.
/// Called twice per workflow: pass 1 (reduced strength) and pass 2 (full strength).
/// Reads: vae_output, preprocessed_image, video_latent.
/// Overwrites: video_latent.
/// </summary>
public class LtxImgToVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_img_to_video",
        Type = FragmentType.Conditioning,
        Title = "LTX Img To Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_i2v";
        public double Strength { get; set; } = 0.7;
        public bool Bypass { get; set; } = false;
        public string ImageInputName { get; set; } = "preprocessed_image";
        public string LatentInputName { get; set; } = "video_latent";
        public string Title { get; set; } = "LTXVImgToVideoInplace";
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
        var nodeId = $"{scope}{p.NodeId}";
        var vaeRef = registry.GetRef($"{scope}vae_output");
        var imageRef = registry.GetRef($"{scope}{p.ImageInputName}");
        var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");

        builder.AddNode(nodeId, node => node
            .Type("LTXVImgToVideoInplace")
            .Title($"{scopeTitle}{p.Title}")
            .Input("strength", p.Strength)
            .Input("bypass", p.Bypass)
            .InputRef("vae", vaeRef)
            .InputRef("image", imageRef)
            .InputRef("latent", latentRef));

        registry.Register($"{scope}video_latent", nodeId, 0);
    }
}
