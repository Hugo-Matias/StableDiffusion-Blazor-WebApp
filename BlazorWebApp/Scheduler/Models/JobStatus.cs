namespace BlazorWebApp.Scheduler.Models
{
    /// <summary>
    /// Lifecycle state of a <see cref="Job"/>.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>Draft, editable in the Scheduler Editor. Never executed.</summary>
        Draft,

        /// <summary>Queued for execution but not yet started.</summary>
        Queued,

        /// <summary>Currently running.</summary>
        Running,

        /// <summary>User-paused; may be resumed.</summary>
        Paused,

        /// <summary>All iterations completed successfully or skipped.</summary>
        Completed,

        /// <summary>Aborted due to an error.</summary>
        Failed,

        /// <summary>Cancelled by the user before completion.</summary>
        Cancelled
    }
}
