using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler.Persistence
{
    /// <summary>
    /// Persistence abstraction for Scheduler <see cref="Job"/> aggregates.
    /// Implementations manage the backing <see cref="BlazorWebApp.Data.Entities.JobEntity"/>
    /// row and keep denormalized projection columns (Name, Status, timestamps) in sync with
    /// the JSON body on every save.
    /// </summary>
    public interface IJobRepository
    {
        /// <summary>Persists a new job. Returns the supplied job unchanged (identity is caller-owned).</summary>
        Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default);

        /// <summary>Overwrites an existing job. Creates the row if one with the same Id does not exist.</summary>
        Task UpdateAsync(Job job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Editor-safe update path. Persists only user-authored "definition" fields
        /// (<see cref="Job.Name"/>, <see cref="Job.Description"/>, <see cref="Job.WorkflowId"/>,
        /// <see cref="Job.BaseParameters"/>, <see cref="Job.Actions"/>, <see cref="Job.OutputConfig"/>)
        /// and preserves runtime-owned state (<see cref="Job.Runs"/>, <see cref="Job.RunCounter"/>,
        /// <see cref="Job.RunState"/>, <see cref="Job.LastRunAt"/>, and <see cref="Job.Status"/> while
        /// the job is Running/Paused) read from the currently persisted row. Use this from the Job
        /// editor so saving edits mid-run cannot wipe the run history or reset the run counter.
        /// </summary>
        Task UpdateDefinitionAsync(Job job, CancellationToken cancellationToken = default);

        /// <summary>Deletes a job by id. No-op when the job does not exist.</summary>
        Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>Fetches a single job by id, or <c>null</c> when missing.</summary>
        Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>Returns all jobs ordered by most recently updated first.</summary>
        Task<List<Job>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns jobs whose status is <see cref="JobStatus.Running"/> or <see cref="JobStatus.Paused"/>.
        /// Used at startup to resume interrupted work.
        /// </summary>
        Task<List<Job>> GetRunningOrPausedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically persists only the <see cref="Job.RunState"/> and denormalized columns
        /// (<see cref="BlazorWebApp.Data.Entities.JobEntity.LastRunAt"/>, <c>UpdatedAt</c>, <c>Status</c>),
        /// rewriting the JSON body. Prefer this over <see cref="UpdateAsync"/> during execution to make
        /// progress updates cheap.
        /// </summary>
        Task SaveRunStateAsync(Guid jobId, JobRunState runState, JobStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the <see cref="Run"/> with <paramref name="runId"/> from the owning job's
        /// <see cref="Job.Runs"/> history. The job's <see cref="Job.RunCounter"/> is left untouched
        /// so numbering of future runs never collides with a previously deleted entry.
        /// No-op when the run or job is missing.
        /// </summary>
        Task DeleteRunAsync(Guid jobId, Guid runId, CancellationToken cancellationToken = default);
    }
}
