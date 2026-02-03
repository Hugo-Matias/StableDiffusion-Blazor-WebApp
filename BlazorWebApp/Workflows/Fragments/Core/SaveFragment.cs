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
        public string FilenamePrefix { get; set; } = "tmp/img";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var filenamePrefix = fragment?.GetString("filename_prefix", "tmp/img") ?? "tmp/img";

        BuildInternal(builder, registry, filenamePrefix);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams.FilenamePrefix);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        string filenamePrefix)
    {
        var imageRef = registry.GetRef("image_output");

        builder.AddNode("save", node => node
            .Type("SaveImage")
            .Title("Save Image")
            .Input("filename_prefix", filenamePrefix)
            .InputRef("images", imageRef));

        // Register output for potential chaining (though typically terminal)
        registry.Register("save_node", "save", 0);
    }
}
