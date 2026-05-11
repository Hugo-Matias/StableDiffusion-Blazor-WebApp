namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupIndexingQueue
    {
        int PendingCount { get; }

        void EnqueueImage(
            int imageId,
            CleanupIndexingQueueReason reason = CleanupIndexingQueueReason.Manual);
    }
}