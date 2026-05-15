using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that saves generated images to disk.
/// This is a terminal node with no outputs.
/// </summary>
public class SaveFragment : IFragmentBuilder
{
    public const string StandardFilenamePrefix = "tmp/img";

    public FragmentMetadata Metadata => new()
    {
        Id = "save",
        Type = Workflows.Models.FragmentType.Output,
        Title = "Save Image",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the save fragment.
    /// </summary>
    public class Parameters
    {
        public string FilenamePrefix { get; set; } = StandardFilenamePrefix;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry)
    {
        var imageRef = registry.GetRef("image_output");

        builder.AddNode("save", node => node
            .Type("SaveImage")
            .Title("Save Image")
            .Input("filename_prefix", StandardFilenamePrefix)
            .InputRef("images", imageRef));

        // Register output for potential chaining (though typically terminal)
        registry.Register("save_node", "save", 0);
    }
}
