using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that concatenates video and audio latents into a combined AV latent.
/// Node: LTXVConcatAVLatent.
/// Reads: video_latent, audio_latent.
/// Registers: av_latent_output.
/// </summary>
public class LtxConcatAVLatentFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_concat_av",
        Type = FragmentType.Utility,
        Title = "LTX Concat AV Latent",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "ltx_concat_av";
        public string VideoLatentInputName { get; set; } = "video_latent";
        public string AudioLatentInputName { get; set; } = "audio_latent";
        public string Title { get; set; } = "LTXVConcatAVLatent";
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
        var videoRef = registry.GetRef($"{scope}{p.VideoLatentInputName}");
        var audioRef = registry.GetRef($"{scope}{p.AudioLatentInputName}");

        builder.AddNode(nodeId, node => node
            .Type("LTXVConcatAVLatent")
            .Title($"{scopeTitle}{p.Title}")
            .InputRef("video_latent", videoRef)
            .InputRef("audio_latent", audioRef));

        registry.Register($"{scope}av_latent_output", nodeId, 0);
    }
}
