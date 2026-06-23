namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for backend availability changes.
    /// Fired when the ComfyUI backend becomes available or unavailable.
    /// </summary>
    public class BackendAvailabilityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the backend is currently available
        /// </summary>
        public bool IsAvailable { get; init; }

        /// <summary>
        /// The name/type of the backend (e.g., "ComfyUI")
        /// </summary>
        public string BackendName { get; init; } = "ComfyUI";

        /// <summary>
        /// Optional error message if backend became unavailable
        /// </summary>
        public string? ErrorMessage { get; init; }
    }
}
