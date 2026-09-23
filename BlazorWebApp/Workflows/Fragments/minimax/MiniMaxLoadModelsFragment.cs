using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Fragment that loads the MiniMax H3 diffusion model, CLIP (type="minimax"), video VAE, and audio VAE.
/// Registers model_output, clip_output, vae_output, and audio_vae_output in the node registry.
/// </summary>
public class MiniMaxLoadModelsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_load_models",
        Type = FragmentType.Loader,
        Title = "Load MiniMax H3 Models",
        IsHidden = true
    };

    public class Parameters
    {
        public string UnetName { get; set; } = "";
        public string ClipName { get; set; } = "";
        public string VideoVaeName { get; set; } = "";
        public string AudioVaeName { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("UNet") ?? "",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "",
            VideoVaeName = parameters.Assets?.GetValueOrDefault("VideoVae") ?? "",
            AudioVaeName = parameters.Assets?.GetValueOrDefault("AudioVae") ?? ""
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
        var unetNodeId = $"{scope}minimax_unet_loader";
        var clipNodeId = $"{scope}minimax_clip_loader";
        var videoVaeNodeId = $"{scope}minimax_video_vae_loader";
        var audioVaeNodeId = $"{scope}minimax_audio_vae_loader";

        builder.AddNode(unetNodeId, node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}Load Diffusion Model")
            .Input("unet_name", p.UnetName)
            .Input("weight_dtype", "default"));

        builder.AddNode(clipNodeId, node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", "minimax")
            .Input("device", "default"));

        builder.AddNode(videoVaeNodeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load Video VAE")
            .Input("vae_name", p.VideoVaeName));

        builder.AddNode(audioVaeNodeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load Audio VAE")
            .Input("vae_name", p.AudioVaeName));

        registry.Register($"{scope}model_output", unetNodeId, 0);
        registry.Register($"{scope}clip_output", clipNodeId, 0);
        registry.Register($"{scope}vae_output", videoVaeNodeId, 0);
        registry.Register($"{scope}audio_vae_output", audioVaeNodeId, 0);
    }
}
