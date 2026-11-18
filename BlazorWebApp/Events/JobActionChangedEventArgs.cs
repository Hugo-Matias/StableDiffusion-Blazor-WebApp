using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Fired when the Scheduler transitions between actions within a <see cref="Job"/>.
    /// </summary>
    public class JobActionChangedEventArgs : EventArgs
    {
        public Guid JobId { get; }
        public int ActionIndex { get; }
        public int ActionIterationCount { get; }
        public string? ActionLabel { get; }

        public JobActionChangedEventArgs(Guid jobId, int actionIndex, int actionIterationCount, string? actionLabel)
        {
            JobId = jobId;
            ActionIndex = actionIndex;
            ActionIterationCount = actionIterationCount;
            ActionLabel = actionLabel;
        }
    }
}
