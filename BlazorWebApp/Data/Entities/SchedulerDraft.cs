using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Single-row persistence of the Scheduler editor's unsaved draft.
    /// Only the row with <see cref="Id"/> = 1 is used; a null body indicates no draft.
    /// Follows the JSON-backed entity pattern used by <see cref="JobEntity"/>.
    /// </summary>
    public class SchedulerDraft
    {
        /// <summary>Fixed primary key (always 1). Guarantees a single draft slot.</summary>
        public int Id { get; set; } = 1;

        /// <summary>
        /// Stable domain identifier of the job being edited. <c>null</c> when the draft
        /// represents a brand new unsaved job.
        /// </summary>
        public Guid? EditingJobId { get; set; }

        /// <summary>Timestamp of the last write.</summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Full draft payload serialized via <see cref="BlazorWebApp.Scheduler.SchedulerJsonOptions.Compact"/>.
        /// </summary>
        public Job Body { get; set; } = default!;
    }
}
