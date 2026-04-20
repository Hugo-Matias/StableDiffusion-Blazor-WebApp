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
    }
}
