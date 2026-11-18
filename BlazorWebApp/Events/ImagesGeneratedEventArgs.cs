namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when image or video generation completes.
    /// Published after a generation operation finishes (success or failure).
    /// </summary>
    public class ImagesGeneratedEventArgs : EventArgs
    {
        /// <summary>
        /// True if images/videos were successfully generated.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of images or videos generated.
        /// </summary>
        public int Count { get; set; }

        public ImagesGeneratedEventArgs(bool success = true, int count = 0)
        {
            Success = success;
            Count = count;
        }
    }
}
