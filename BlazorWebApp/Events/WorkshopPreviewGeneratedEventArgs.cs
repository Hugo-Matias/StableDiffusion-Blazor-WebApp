namespace BlazorWebApp.Events
{
    /// <summary>
    /// Lifecycle status of a Workshop preview generation. Phase 8.6 Step 6 added the
    /// <see cref="Queued"/> status so consumers can render a spinner the moment a
    /// preview is enqueued (before any image exists).
    /// </summary>
    public enum WorkshopPreviewStatus
    {
        Queued = 0,
        Completed = 1,
        Failed = 2,
    }

    /// <summary>
    /// Published when a Workshop preview image transitions through queueing / completion / failure for a node.
    /// Distinct from <see cref="ImagesGeneratedEventArgs"/> so the gallery / Results tab does not refresh.
    /// </summary>
    public class WorkshopPreviewGeneratedEventArgs : EventArgs
    {
        public int NodeId { get; }
        public int ImageId { get; }
        public bool Success { get; }
        public WorkshopPreviewStatus Status { get; }
        public string? ErrorMessage { get; }

        public WorkshopPreviewGeneratedEventArgs(int nodeId, int imageId, bool success = true)
            : this(nodeId, imageId, success ? WorkshopPreviewStatus.Completed : WorkshopPreviewStatus.Failed, null)
        {
        }

        public WorkshopPreviewGeneratedEventArgs(int nodeId, int imageId, WorkshopPreviewStatus status, string? errorMessage = null)
        {
            NodeId = nodeId;
            ImageId = imageId;
            Status = status;
            Success = status == WorkshopPreviewStatus.Completed;
            ErrorMessage = errorMessage;
        }
    }
}
