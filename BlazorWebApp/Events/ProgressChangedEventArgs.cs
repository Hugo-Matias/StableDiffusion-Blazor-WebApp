namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when progress changes (generation, download, etc.).
    /// Subscribe to this event to update progress displays.
    /// </summary>
    public class ProgressChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Current progress value (0-1 or 0-100 depending on context)
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Optional message describing current operation
        /// </summary>
        public string? Message { get; set; }

        public ProgressChangedEventArgs(float value = 0, string? message = null)
        {
            Value = value;
            Message = message;
        }
    }
}
