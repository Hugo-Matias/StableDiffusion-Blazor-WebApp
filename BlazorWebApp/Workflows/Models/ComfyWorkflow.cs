using BlazorWebApp.Workflows.Builders;

namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Result of building a ComfyUI workflow.
/// Contains the JSON string and the node registry for tracking outputs.
/// </summary>
public class ComfyWorkflow
{
    /// <summary>
    /// The ComfyUI workflow JSON string ready to be sent to the API.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// The node registry containing all registered outputs (for debugging/validation).
    /// </summary>
    public NodeRegistry? Registry { get; init; }
}
