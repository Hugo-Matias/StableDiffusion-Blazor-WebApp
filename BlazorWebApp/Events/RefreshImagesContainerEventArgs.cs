namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for requesting ImagesContainer refresh.
    /// Published when the images container needs to refresh its display.
    /// This is an invoke-only pattern - no subscribers expected, just signals UI refresh needed.
    /// </summary>
    public class RefreshImagesContainerEventArgs : EventArgs
    {
        /// <summary>
        /// Reason for the refresh request.
        /// </summary>
        public RefreshReason Reason { get; }

        /// <summary>
        /// Optional context information about what changed.
        /// </summary>
        public string? Context { get; }

        public RefreshImagesContainerEventArgs(RefreshReason reason = RefreshReason.DataChanged, string? context = null)
        {
            Reason = reason;
            Context = context;
        }
    }

    /// <summary>
    /// Reason for images container refresh.
    /// </summary>
    public enum RefreshReason
    {
        /// <summary>Underlying data changed</summary>
        DataChanged,
        /// <summary>Filter or sort criteria changed</summary>
        FilterChanged,
        /// <summary>Page changed</summary>
        PageChanged,
        /// <summary>Images were added or deleted</summary>
        ImagesModified,
        /// <summary>General refresh requested</summary>
        General
    }
}
