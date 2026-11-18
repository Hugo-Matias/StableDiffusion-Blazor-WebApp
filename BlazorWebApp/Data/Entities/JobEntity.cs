using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Database row for a persisted Scheduler <see cref="Job"/>.
    /// The complete job payload is stored as JSON in <see cref="Body"/>; the remaining columns are
    /// denormalized projections of the body kept in sync by <c>JobRepository</c> so that list/filter
    /// queries don't require deserializing every row.
    /// </summary>
    public class JobEntity
    {
        /// <summary>Database primary key (auto-increment).</summary>
        public int Id { get; set; }

        /// <summary>
        /// Stable domain identifier matching <see cref="Job.Id"/>. Indexed and unique.
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>Denormalized job name, in sync with <see cref="Body"/>.Name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Denormalized lifecycle status, in sync with <see cref="Body"/>.Status.</summary>
        public JobStatus Status { get; set; }

        /// <summary>Denormalized workflow identifier, in sync with <see cref="Body"/>.WorkflowId.</summary>
        public Guid? WorkflowId { get; set; }

        /// <summary>Creation timestamp (UTC).</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Timestamp of the most recent run start, or <c>null</c> if the job has never run.</summary>
        public DateTime? LastRunAt { get; set; }

        /// <summary>Timestamp of the most recent change to the row.</summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Full job payload serialized via <see cref="BlazorWebApp.Scheduler.SchedulerJsonOptions.Compact"/>.
        /// Includes <see cref="Job.RunState"/> so a single column holds the resumable state.
        /// </summary>
        public Job Body { get; set; } = default!;
    }
}
