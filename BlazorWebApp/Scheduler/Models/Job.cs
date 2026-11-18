using BlazorWebApp.Models;

namespace BlazorWebApp.Scheduler.Models
{
    /// <summary>
    /// Top-level Scheduler aggregate. A job owns a base parameter set and an ordered list of
    /// <see cref="JobAction"/>s that execute sequentially to produce a deterministic batch of generations.
    /// </summary>
    public sealed class Job
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>User-visible name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional free-form description.</summary>
        public string? Description { get; set; }

        /// <summary>Creation timestamp.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Timestamp of the most recent run start, if any.</summary>
        public DateTime? LastRunAt { get; set; }

        /// <summary>Current lifecycle status.</summary>
        public JobStatus Status { get; set; } = JobStatus.Draft;

        /// <summary>
        /// Identifier of the workflow this job is bound to. A job is pinned to a single workflow;
        /// multi-workflow chaining is a future extension.
        /// </summary>
        public Guid? WorkflowId { get; set; }

        /// <summary>
        /// Snapshot of generation parameters used as the starting point for every action iteration.
        /// </summary>
        public GenerationParameters BaseParameters { get; set; } = new();

        /// <summary>Default output routing for all actions.</summary>
        public JobOutputConfig OutputConfig { get; set; } = new();

        /// <summary>Ordered actions.</summary>
        public List<JobAction> Actions { get; set; } = new();

        /// <summary>Last-known run state, used to resume paused/interrupted jobs.</summary>
        public JobRunState RunState { get; set; } = new();

        /// <summary>
        /// Monotonic counter used to assign <see cref="Run.RunNumber"/> to newly created runs.
        /// Incremented whenever a fresh run is triggered and never decremented, so deleting
        /// older runs does not cause numbering collisions.
        /// </summary>
        public int RunCounter { get; set; }

        /// <summary>
        /// Immutable history of executions for this job. Each entry is a frozen snapshot of the
        /// base parameters and actions that were in effect when the run was triggered, plus
        /// runtime progress metadata. Newest runs are appended to the end of the list.
        /// </summary>
        public List<Run> Runs { get; set; } = new();
    }
}
