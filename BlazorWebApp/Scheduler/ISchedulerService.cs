using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler
{
    /// <summary>
    /// Orchestrates end-to-end execution of a Scheduler <see cref="Job"/>:
    /// walks its actions, materializes variations, applies directives, invokes generations,
    /// publishes lifecycle events and persists run state for resume.
    /// Only one job runs at a time per service instance.
    /// </summary>
    public interface ISchedulerService
    {
        /// <summary>Identifier of the job currently running, if any.</summary>
        Guid? RunningJobId { get; }

        /// <summary>Current status of the running job, or null if none.</summary>
        JobStatus? RunningJobStatus { get; }

        /// <summary>
        /// Runs a job from scratch. Resets <see cref="Job.RunState"/> to zero and sets status to Running.
        /// Returns when the job reaches a terminal state (Completed/Canceled/Failed/Paused).
        /// </summary>
        Task RunAsync(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes a previously paused job from its persisted <see cref="JobRunState"/>.
        /// </summary>
        Task ResumeAsync(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>Pauses the currently running job at the next iteration boundary.</summary>
        Task PauseAsync();

        /// <summary>Stops the currently running job; job status becomes Canceled.</summary>
        Task StopAsync();

        /// <summary>Skips the currently in-flight iteration and proceeds to the next one.</summary>
        Task SkipCurrentAsync();
    }
}
