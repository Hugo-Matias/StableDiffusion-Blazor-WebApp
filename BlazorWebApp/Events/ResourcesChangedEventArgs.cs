namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when resources state changes (e.g., resource enabled/disabled, template activated).
    /// Subscribe to this event to refresh resource displays and lists.
    /// </summary>
    public class ResourcesChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Optional message describing the change
        /// </summary>
        public string? Message { get; set; }

        public ResourcesChangedEventArgs(string? message = null)
        {
            Message = message;
        }
    }
}
