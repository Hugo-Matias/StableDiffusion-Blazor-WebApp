namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments published when the active LLM Tools view changes via the sidebar nav menu.
    /// </summary>
    public class LLMViewChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The identifier of the previously active view.
        /// </summary>
        public string PreviousViewId { get; init; } = string.Empty;

        /// <summary>
        /// The identifier of the newly active view.
        /// </summary>
        public string ViewId { get; init; } = string.Empty;
    }
}
