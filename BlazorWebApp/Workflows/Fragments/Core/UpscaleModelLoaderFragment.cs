using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads an upscale model for pixel-space upscaling.
/// Registers upscale_model_output in the node registry.
/// </summary>
public class UpscaleModelLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "upscale_model_loader",
        Type = FragmentType.Loader,
        Title = "Upscale Model Loader",
        IsHidden = true // Utility fragment, no UI
    };

    /// <summary>
    /// Parameters for the upscale model loader fragment.
    /// </summary>
    public class Parameters
    {
        public string ModelName { get; set; } = "x1_ITF_SkinDiffDetail_Lite_v1.pth";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var modelName = GetParameterValue(parameters, "UpscaleModel", "x1_ITF_SkinDiffDetail_Lite_v1.pth");

        BuildInternal(builder, registry, new Parameters
        {
            ModelName = modelName
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
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
        var upscaleLoaderNodeId = $"{scope}upscale_model_loader";

        builder.AddNode(upscaleLoaderNodeId, node => node
            .Type("UpscaleModelLoader")
            .Title($"{scopeTitle}Load Upscale Model")
            .Input("model_name", p.ModelName));

        registry.Register($"{scope}upscale_model_output", upscaleLoaderNodeId, 0);
    }

    private static string GetParameterValue(GenerationParameters parameters, string key, string defaultValue)
    {
        var assetValue = parameters.Assets?.GetValueOrDefault(key);
        if (!string.IsNullOrEmpty(assetValue))
            return assetValue;
        return defaultValue;
    }
}
