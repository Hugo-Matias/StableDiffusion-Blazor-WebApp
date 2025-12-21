using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Stores per-workflow generation parameters.
    /// Each workflow can have its own saved state that persists across sessions.
    /// </summary>
    public class WorkflowState
    {
        /// <summary>
        /// Primary key.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The workflow this state belongs to.
        /// Links to Workflow.Id (deterministic GUID based on title/base/mode).
        /// </summary>
        public Guid WorkflowId { get; set; }

        /// <summary>
        /// When this state was last modified.
        /// Used for "recently used" sorting and cleanup.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// The saved generation parameters for this workflow.
        /// Stored as JSON in the database.
        /// </summary>
        public GenerationParameters? Parameters { get; set; }
    }
}
