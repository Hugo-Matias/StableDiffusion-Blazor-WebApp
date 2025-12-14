using System;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when the current workflow changes.
    /// Published when SetCurrentWorkflow() or SetCurrentWorkflowAsync() is called.
    /// </summary>
    public class WorkflowChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The ID of the newly selected workflow.
        /// </summary>
        public Guid WorkflowId { get; }

        /// <summary>
        /// The type of change that occurred.
        /// </summary>
        public string ChangeType { get; }

        public WorkflowChangedEventArgs(Guid workflowId, string changeType)
        {
            WorkflowId = workflowId;
            ChangeType = changeType;
        }
    }
}
