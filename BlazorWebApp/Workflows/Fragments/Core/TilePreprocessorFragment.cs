using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that runs AIO Preprocessor with TilePreprocessor to generate tile guidance map.
/// Registers tile_map_output in the node registry.
/// </summary>
public class TilePreprocessorFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "tile_preprocessor",
        Type = FragmentType.Utility,
        Title = "Tile Preprocessor",
        IsHidden = true // Utility fragment, no UI
    };

    /// <summary>
    /// Parameters for the tile preprocessor fragment.
    /// </summary>
    public class Parameters
    {
        public int Resolution { get; set; } = 1024;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters
        {
            Resolution = 1024
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
        var preprocessorNodeId = $"{scope}tile_preprocessor";

        // Get image_output reference from Stage 1
        var imageRef = registry.GetRef("image_output");

        builder.AddNode(preprocessorNodeId, node => node
            .Type("AIO_Preprocessor")
            .Title($"{scopeTitle}Tile Preprocessor")
            .Input("preprocessor", "TilePreprocessor")
            .Input("resolution", p.Resolution)
            .InputRef("image", imageRef));

        registry.Register($"{scope}tile_map_output", preprocessorNodeId, 0);
    }
}
