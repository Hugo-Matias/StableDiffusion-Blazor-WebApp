using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing workflow templates and composition.
    /// Uses C# IWorkflowBuilder implementations exclusively.
    /// </summary>
    public interface IWorkflowService
    {
        #region Workflow Builder Access

        /// <summary>
        /// Gets a C# workflow builder by ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID to find</param>
        /// <returns>The workflow builder, or null if not found</returns>
        IWorkflowBuilder? GetWorkflowBuilder(Guid workflowId);

        /// <summary>
        /// Gets all discovered C# workflow builders.
        /// </summary>
        /// <returns>Dictionary of workflow ID to builder instance</returns>
        IReadOnlyDictionary<Guid, IWorkflowBuilder> GetWorkflowBuilders();

        /// <summary>
        /// Checks if a workflow builder exists for the given ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID to check</param>
        /// <returns>True if a builder exists</returns>
        bool HasWorkflowBuilder(Guid workflowId);

        #endregion

        #region Workflow Discovery

        /// <summary>
        /// Gets all available workflows.
        /// </summary>
        List<Workflow> GetWorkflows();

        /// <summary>
        /// Gets a workflow by its ID.
        /// </summary>
        /// <param name="workflowId">The workflow ID to find</param>
        /// <returns>The workflow, or null if not found</returns>
        Workflow? GetWorkflowById(Guid workflowId);

        /// <summary>
        /// Refreshes workflows, attempting to preserve the current selection.
        /// </summary>
        /// <param name="currentWorkflowBase">The currently selected workflow base</param>
        /// <param name="currentWorkflowId">The currently selected workflow ID</param>
        /// <returns>Tuple of (workflows, suggested base, suggested ID)</returns>
        (List<Workflow> workflows, ModelBase? suggestedBase, Guid? suggestedId) RefreshWorkflows(
            ModelBase? currentWorkflowBase = null,
            Guid? currentWorkflowId = null);

        #endregion

        #region Workflow Composition

        /// <summary>
        /// Composes a workflow from generation parameters.
        /// </summary>
        /// <param name="template">The workflow template</param>
        /// <param name="parameters">The generation parameters</param>
        /// <returns>The rendered workflow JSON for ComfyUI</returns>
        string ComposeWorkflowFromGenerationParameters(Workflow template, GenerationParameters parameters);

        #endregion

        #region Schema Access

        /// <summary>
        /// Gets all fragment schemas for a workflow.
        /// </summary>
        /// <param name="workflow">The workflow</param>
        /// <returns>Dictionary of fragment ID to schema</returns>
        Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow);

        #endregion
    }
}
