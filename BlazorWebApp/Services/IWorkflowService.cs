using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing workflow templates and rendering.
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
        /// Composes a workflow from a template using Txt2Img parameters.
        /// </summary>
        string ComposeWorkflowFromTemplate(Workflow template, Txt2ImgComfyUI param);

        /// <summary>
        /// Composes a workflow from a template using Img2Img parameters.
        /// </summary>
        string ComposeWorkflowFromTemplate(Workflow template, Img2ImgComfyUI param);

        /// <summary>
        /// Composes a workflow from a template using Img2Vid parameters.
        /// </summary>
        string ComposeWorkflowFromTemplate(Workflow template, Img2VidComfyUI param);

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
        /// Renders a workflow fragment with the given context.
        /// </summary>
        (string rendered, Dictionary<string, (string nodeId, int index)> outputs) RenderFragment(
            string fragmentText, 
            SubgraphContext context, 
            Dictionary<string, object> globalParams, 
            Func<string, Task<string>>? loraPathResolver = null);

        /// <summary>
        /// Parses the UI schema from a fragment's #meta block.
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
        /// Clears the fragment schema cache.
        /// Call after fragments are modified.
        /// </summary>
        void ClearSchemaCache();
    }
}
