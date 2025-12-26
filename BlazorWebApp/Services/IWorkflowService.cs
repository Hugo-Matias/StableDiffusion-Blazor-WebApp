using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Represents a parsed pipeline step from a workflow template.
    /// Contains all data needed to initialize a fragment in GenerationParameters.
    /// </summary>
    /// <param name="Id">The unique step ID (from "id" field in Pipeline)</param>
    /// <param name="Fragment">The fragment file path (from "fragment" field)</param>
    /// <param name="DefaultValues">Default parameter values extracted from the step's parameters block</param>
    /// <param name="Order">The order of this step in the pipeline</param>
    public record ParsedPipelineStep(
        string Id,
        string Fragment,
        Dictionary<string, object?> DefaultValues,
        int Order
    );

    /// <summary>
    /// Service for managing workflow templates and rendering using Fluid template engine.
    /// </summary>
    public interface IWorkflowService
    {
        /// <summary>
        /// Gets all available workflows from template files.
        /// </summary>
        List<Workflow> GetWorkflows();

        /// <summary>
        /// Refreshes workflows from disk template files and attempts to preserve the current selection.
        /// </summary>
        /// <param name="currentWorkflowBase">The currently selected workflow base (to preserve selection)</param>
        /// <param name="currentWorkflowId">The currently selected workflow ID (to preserve selection)</param>
        /// <returns>A tuple containing: (workflows list, suggested workflow base, suggested workflow ID)</returns>
        (List<Workflow> workflows, ModelBase? suggestedBase, Guid? suggestedId) RefreshWorkflows(
            ModelBase? currentWorkflowBase = null,
            Guid? currentWorkflowId = null);

        /// <summary>
        /// Composes a workflow from a template using the unified GenerationParameters model.
        /// Uses Fluid template engine for rendering.
        /// </summary>
        /// <param name="template">The workflow template to compose</param>
        /// <param name="parameters">The unified generation parameters containing all fragment values</param>
        /// <returns>The rendered workflow JSON ready for ComfyUI</returns>
        Task<string> ComposeWorkflowFromGenerationParametersAsync(Workflow template, GenerationParameters parameters);

        /// <summary>
        /// Saves the current asset values as defaults in the workflow template file.
        /// </summary>
        /// <param name="workflow">The workflow whose template should be updated</param>
        /// <param name="assetValues">Dictionary of asset parameter names to their current values</param>
        /// <param name="allWorkflows">Optional: The full list of workflows to update in-memory</param>
        /// <returns>True if the file was updated successfully, false otherwise</returns>
        bool SaveAssetDefaults(Workflow workflow, Dictionary<string, string> assetValues, List<Workflow>? allWorkflows = null);

        /// <summary>
        /// Loads a workflow template from a file path.
        /// </summary>
        Workflow LoadWorkflowTemplate(string path);

        /// <summary>
        /// Renders a workflow fragment using the Fluid template engine.
        /// </summary>
        Task<(string rendered, Dictionary<string, (string nodeId, int index)> outputs)> RenderFragmentAsync(
            string fragmentText,
            SubgraphContext context,
            Dictionary<string, object> globalParams);

        /// <summary>
        /// Parses the UI schema from a fragment's meta block.
        /// Returns null if no UI schema is defined.
        /// </summary>
        FragmentSchema? ParseFragmentSchema(string fragmentText);

        /// <summary>
        /// Gets the UI schema for a fragment file.
        /// Uses cached schemas when available.
        /// </summary>
        FragmentSchema? GetFragmentSchema(string fragmentFile);

        /// <summary>
        /// Gets all fragment schemas for a workflow's pipeline.
        /// Returns a dictionary keyed by fragment ID.
        /// </summary>
        Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow);

        /// <summary>
        /// Gets a workflow by its ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID to find</param>
        /// <returns>The workflow, or null if not found</returns>
        Workflow? GetWorkflowById(Guid workflowId);

        /// <summary>
        /// Parses pipeline steps from a workflow's RawJson.
        /// Extracts step IDs, fragment files, and default parameter values.
        /// </summary>
        /// <param name="rawJson">The workflow's RawJson content</param>
        /// <returns>List of parsed pipeline steps with all extracted data</returns>
        List<ParsedPipelineStep> ParsePipelineSteps(string rawJson);

        /// <summary>
        /// Gets cached pipeline steps for a workflow.
        /// Parses and caches on first access; returns cached result on subsequent calls.
        /// </summary>
        /// <param name="workflow">The workflow to get pipeline steps for</param>
        /// <returns>List of parsed pipeline steps</returns>
        List<ParsedPipelineStep> GetPipelineSteps(Workflow workflow);

        /// <summary>
        /// Clears the pipeline step cache.
        /// Called automatically when workflows are refreshed.
        /// </summary>
        void ClearPipelineCache();

        /// <summary>
        /// Clears the fragment schema cache.
        /// Call after fragments are modified.
        /// </summary>
        void ClearSchemaCache();
    }
}
