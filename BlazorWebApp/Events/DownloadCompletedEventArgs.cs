namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when a download completes (e.g., Civitai resource download).
    /// Subscribe to this event to refresh resource displays and lists.
    /// </summary>
    public class DownloadCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Optional message describing the download result
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Resource type that was downloaded (optional)
        /// </summary>
        public string? ResourceType { get; set; }

        public DownloadCompletedEventArgs(string? message = null, string? resourceType = null)
        {
            Message = message;
            ResourceType = resourceType;
        }
    }
}
