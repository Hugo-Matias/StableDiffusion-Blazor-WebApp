using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing per-workflow generation parameter state.
    /// Handles loading, saving, and caching of workflow-specific parameters.
    /// </summary>
    public interface IWorkflowStateService
    {
        /// <summary>
        /// Loads saved parameters for a workflow from the database.
        /// Returns null if no saved state exists for this workflow.
        /// </summary>
        /// <param name="workflowId">The workflow's unique identifier</param>
        /// <returns>Saved parameters or null if none exist</returns>
        Task<GenerationParameters?> LoadWorkflowStateAsync(Guid workflowId);

        /// <summary>
        /// Saves the current parameters for a workflow to the database.
        /// Creates a new record if none exists, otherwise updates the existing one.
        /// </summary>
        /// <param name="workflowId">The workflow's unique identifier</param>
        /// <param name="parameters">The parameters to save</param>
        Task SaveWorkflowStateAsync(Guid workflowId, GenerationParameters parameters);

        /// <summary>
        /// Deletes saved state for a workflow.
        /// </summary>
        /// <param name="workflowId">The workflow's unique identifier</param>
        Task DeleteWorkflowStateAsync(Guid workflowId);

        /// <summary>
        /// Gets metadata about all saved workflow states.
        /// Useful for showing "recently used" workflows.
        /// </summary>
        /// <returns>List of workflow states ordered by last modified</returns>
        Task<List<WorkflowState>> GetAllWorkflowStatesAsync();

        /// <summary>
        /// Gets the most recently used workflow states.
        /// </summary>
        /// <param name="count">Maximum number of states to return</param>
        /// <returns>List of workflow states ordered by last modified descending</returns>
        Task<List<WorkflowState>> GetRecentWorkflowStatesAsync(int count = 10);

        /// <summary>
        /// Checks if a saved state exists for the given workflow.
        /// </summary>
        /// <param name="workflowId">The workflow's unique identifier</param>
        Task<bool> HasSavedStateAsync(Guid workflowId);
    }
}
