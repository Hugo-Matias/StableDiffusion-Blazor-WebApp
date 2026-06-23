using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Fired when a Scheduler <see cref="Job"/> finishes (completed, canceled, failed, or paused).
    /// </summary>
    public class JobCompletedEventArgs : EventArgs
    {
        public Guid JobId { get; }
        public JobStatus FinalStatus { get; }
        public int CompletedImages { get; }
        public int FailedImages { get; }
        public int TotalIterations { get; }
        public string? ErrorMessage { get; }
        public DateTime CompletedAt { get; }

        public JobCompletedEventArgs(
            Guid jobId,
            JobStatus finalStatus,
            int completedImages,
            int failedImages,
            int totalIterations,
            string? errorMessage,
            DateTime completedAt)
        {
            JobId = jobId;
            FinalStatus = finalStatus;
            CompletedImages = completedImages;
            FailedImages = failedImages;
            TotalIterations = totalIterations;
            ErrorMessage = errorMessage;
            CompletedAt = completedAt;
        }
    }
}
