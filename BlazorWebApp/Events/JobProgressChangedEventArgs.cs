using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Fired after each iteration within a Scheduler <see cref="Job"/> completes (success or failure).
    /// </summary>
    public class JobProgressChangedEventArgs : EventArgs
    {
        public Guid JobId { get; }
        public int CurrentActionIndex { get; }
        public int CurrentIterationIndex { get; }
        public int CompletedImages { get; }
        public int FailedImages { get; }
        public int TotalIterations { get; }

        public JobProgressChangedEventArgs(
            Guid jobId,
            int currentActionIndex,
            int currentIterationIndex,
            int completedImages,
            int failedImages,
            int totalIterations)
        {
            JobId = jobId;
            CurrentActionIndex = currentActionIndex;
            CurrentIterationIndex = currentIterationIndex;
            CompletedImages = completedImages;
            FailedImages = failedImages;
            TotalIterations = totalIterations;
        }
    }
}
