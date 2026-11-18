using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Fired after each successful image save within a Scheduler <see cref="Job"/> iteration.
    /// </summary>
    public class JobImageGeneratedEventArgs : EventArgs
    {
        public Guid JobId { get; }
        public int ActionIndex { get; }
        public int IterationIndex { get; }
        public ImagesDto Images { get; }

        public JobImageGeneratedEventArgs(Guid jobId, int actionIndex, int iterationIndex, ImagesDto images)
        {
            JobId = jobId;
            ActionIndex = actionIndex;
            IterationIndex = iterationIndex;
            Images = images;
        }
    }
}
