using BlazorWebApp.Models;

namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Defines the contract for fragment builders that generate portions of ComfyUI workflows.
/// Fragments are reusable components that can be composed into complete workflows.
/// </summary>
public interface IFragmentBuilder
{
    /// <summary>
    /// Gets the metadata describing this fragment (ID, title, parameters, UI schema, etc.).
    /// </summary>
    FragmentMetadata Metadata { get; }

    /// <summary>
    /// Builds this fragment's nodes and registers outputs in the workflow.
    /// </summary>
    /// <param name="builder">The workflow builder to add nodes to.</param>
    /// <param name="parameters">The generation parameters containing fragment values.</param>
    /// <param name="registry">The node registry for tracking and referencing outputs.</param>
    void Build(BlazorWebApp.Workflows.Builders.ComfyWorkflowBuilder builder, GenerationParameters parameters, BlazorWebApp.Workflows.Builders.NodeRegistry registry);
}
