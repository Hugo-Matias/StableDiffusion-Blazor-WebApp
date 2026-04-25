using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads a model patch (ControlNet) for application to the main model.
/// Registers model_patch_output in the node registry.
/// </summary>
public class ModelPatchLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "model_patch_loader",
        Type = FragmentType.Loader,
        Title = "Model Patch Loader",
        IsHidden = true // Utility fragment, no UI
    };

    /// <summary>
    /// Parameters for the model patch loader fragment.
    /// </summary>
    public class Parameters
    {
        public string PatchName { get; set; } = "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var patchName = GetParameterValue(parameters, "ControlNet", "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors");

        BuildInternal(builder, registry, new Parameters
        {
            PatchName = patchName
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
        var patchNodeId = $"{scope}model_patch_loader";

        builder.AddNode(patchNodeId, node => node
            .Type("ModelPatchLoader")
            .Title($"{scopeTitle}Load Model Patch")
            .Input("name", p.PatchName));

        registry.Register($"{scope}model_patch_output", patchNodeId, 0);
    }

    private static string GetParameterValue(GenerationParameters parameters, string key, string defaultValue)
    {
        var assetValue = parameters.Assets?.GetValueOrDefault(key);
        if (!string.IsNullOrEmpty(assetValue))
            return assetValue;
        return defaultValue;
    }
}
