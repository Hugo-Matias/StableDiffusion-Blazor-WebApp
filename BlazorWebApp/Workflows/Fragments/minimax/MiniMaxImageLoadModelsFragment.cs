using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Loads the MiniMax H3 image model stack: UNETLoader (diffusion), CLIPLoader (type=minimax) and
/// VAELoader. Mirrors the source workflow's nodes 29/30/31 exactly.
/// </summary>
public class MiniMaxImageLoadModelsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_image_load_models",
        Type = FragmentType.Loader,
        Title = "Load MiniMax H3 Image Models",
        IsHidden = true
    };

    public class Parameters
    {
        public string UnetName { get; set; } = "";
        public string ClipName { get; set; } = "";
        public string VaeName { get; set; } = "";
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("UNet") ?? "",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? ""
        }, scope, scopeTitle);
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters fragmentParams, string scope = "", string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters p, string scope, string scopeTitle)
    {
        var unetNodeId = $"{scope}minimax_image_unet_loader";
        var clipNodeId = $"{scope}minimax_image_clip_loader";
        var vaeNodeId = $"{scope}minimax_image_vae_loader";

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

        builder.AddNode(vaeNodeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        registry.Register($"{scope}model_output", unetNodeId, 0);
        registry.Register($"{scope}clip_output", clipNodeId, 0);
        registry.Register($"{scope}vae_output", vaeNodeId, 0);
    }
}
