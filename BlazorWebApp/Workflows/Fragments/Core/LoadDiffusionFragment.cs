using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads the core diffusion model components: UNet, CLIP, and VAE.
/// Registers model_output, clip_output, and vae_output in the node registry.
/// </summary>
public class LoadDiffusionFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_diffusion",
        Type = FragmentType.Loader,
        Title = "Load Diffusion Model",
        IsHidden = true // Utility fragment, no UI
    };

    /// <summary>
    /// Parameters for the load diffusion fragment.
    /// </summary>
    public class Parameters
    {
        public string UnetName { get; set; } = "";
        public string ClipName { get; set; } = "";
        public string ClipType { get; set; } = "stable_diffusion";
        public string VaeName { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // Get fragment-specific parameters from GenerationParameters
        var fragment = parameters.GetFragment(Metadata.Id);
        
        // Extract parameters - these come from workflow pipeline configuration
        var unetName = GetParameterValue(parameters, "Model", "");
        var clipName = GetParameterValue(parameters, "Clip", "");
        var clipType = GetParameterValue(parameters, "clip_type", "stable_diffusion");
        var vaeName = GetParameterValue(parameters, "Vae", "");

        BuildInternal(builder, registry, new Parameters
        {
            UnetName = unetName,
            ClipName = clipName,
            ClipType = clipType,
            VaeName = vaeName
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters (for direct construction without GenerationParameters lookup).
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var unetNodeId = $"{scope}unet_loader";
        var clipNodeId = $"{scope}clip_loader";
        var vaeNodeId = $"{scope}vae_loader";

        builder.AddNode(unetNodeId, node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}Load Diffusion Model")
            .Input("unet_name", p.UnetName)
            .Input("weight_dtype", "default"));

        builder.AddNode(clipNodeId, node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", p.ClipType)
            .Input("device", "default"));

        builder.AddNode(vaeNodeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        registry.Register($"{scope}model_output", unetNodeId, 0);
        registry.Register($"{scope}clip_output", clipNodeId, 0);
        registry.Register($"{scope}vae_output", vaeNodeId, 0);
    }

    private static string GetParameterValue(GenerationParameters parameters, string key, string defaultValue)
    {
        var assetValue = parameters.Assets?.GetValueOrDefault(key);
        if (!string.IsNullOrEmpty(assetValue))
            return assetValue;
        return defaultValue;
    }
}
