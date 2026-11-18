using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Fired when a Scheduler <see cref="Job"/> begins executing.
    /// </summary>
    public class JobStartedEventArgs : EventArgs
    {
        public Guid JobId { get; }
        public string JobName { get; }
        public int TotalIterations { get; }
        public DateTime StartedAt { get; }

        public JobStartedEventArgs(Guid jobId, string jobName, int totalIterations, DateTime startedAt)
        {
            JobId = jobId;
            JobName = jobName;
            TotalIterations = totalIterations;
            StartedAt = startedAt;
        }
    }
}
