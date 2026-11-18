using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Encodes a loaded source video (image batch) into a video latent via
/// <c>VAEEncode</c>.
///
/// Reads: <c>{scope}loaded_video_images</c>, <c>{scope}vae_output</c>.
/// Registers: <c>{scope}video_latent</c> (overrides the empty video latent
/// so the existing AV concat / sampler chain consumes the source-video
/// latent unchanged).
/// </summary>
public class LtxVaeEncodeVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_vae_encode_video",
        Type = FragmentType.Latent,
        Title = "LTX VAE Encode Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string ImagesInputName { get; set; } = "loaded_video_images";
        public string VaeInputName { get; set; } = "vae_output";
        public string OutputName { get; set; } = "video_latent";
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
        var encodeId = $"{scope}ltx_video_vae_encode";
        var imagesRef = registry.GetRef($"{scope}{p.ImagesInputName}");
        var vaeRef = registry.GetRef($"{scope}{p.VaeInputName}");

        builder.AddNode(encodeId, node => node
            .Type("VAEEncode")
            .Title($"{scopeTitle}VAE Encode (Video)")
            .InputRef("pixels", imagesRef)
            .InputRef("vae", vaeRef));

        registry.Register($"{scope}{p.OutputName}", encodeId, 0);
    }
}
