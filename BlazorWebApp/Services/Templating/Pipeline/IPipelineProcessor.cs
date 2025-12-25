using System.Text.Json;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Templating.Pipeline;

/// <summary>
/// Interface for pipeline step processors that handle special markers
/// like $foreach, $if, etc.
/// </summary>
public interface IPipelineProcessor
{
    /// <summary>
    /// Determines if this processor can handle the given pipeline step.
    /// </summary>
    /// <param name="step">The JSON element representing the pipeline step.</param>
    /// <returns>True if this processor should handle the step.</returns>
    bool CanProcess(JsonElement step);

    /// <summary>
    /// Processes the pipeline step and returns expanded steps.
    /// </summary>
    /// <param name="step">The JSON element representing the pipeline step.</param>
    /// <param name="parameters">The generation parameters.</param>
    /// <param name="computeRegistry">Registry for computed values.</param>
    /// <returns>Zero or more expanded pipeline steps.</returns>
    IEnumerable<ExpandedPipelineStep> Process(
        JsonElement step,
        GenerationParameters parameters,
        ComputeRegistry computeRegistry);
}

/// <summary>
/// Represents an expanded pipeline step ready for fragment rendering.
/// </summary>
public class ExpandedPipelineStep
{
    /// <summary>
    /// The step identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The fragment file path (e.g., "lora-loader.liquid").
    /// </summary>
    public string Fragment { get; set; } = string.Empty;

    /// <summary>
    /// Parameters to pass to the fragment, with all markers resolved.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Original step order in the pipeline (for sorting).
    /// </summary>
    public int Order { get; set; }
}
