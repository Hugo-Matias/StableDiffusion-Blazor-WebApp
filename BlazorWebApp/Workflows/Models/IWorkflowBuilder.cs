using BlazorWebApp.Models;

namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Defines the contract for workflow builders that generate ComfyUI workflows.
/// Each workflow implementation provides metadata and a Build method to compose the workflow from parameters.
/// </summary>
public interface IWorkflowBuilder
{
    /// <summary>
    /// Gets the metadata describing this workflow (title, base model, assets, etc.).
    /// </summary>
    WorkflowMetadata Metadata { get; }

    /// <summary>
    /// Builds a ComfyUI workflow from the provided generation parameters.
    /// </summary>
    /// <param name="parameters">The generation parameters containing fragment values, assets, and sources.</param>
    /// <returns>A ComfyWorkflow containing the generated JSON and node registry.</returns>
    ComfyWorkflow Build(GenerationParameters parameters);

    /// <summary>
    /// Gets all fragments used by this workflow.
    /// Used for initializing fragment parameters and discovering UI components.
    /// </summary>
    /// <returns>Collection of fragment builders used by this workflow.</returns>
    IEnumerable<IFragmentBuilder> GetFragments();
}
