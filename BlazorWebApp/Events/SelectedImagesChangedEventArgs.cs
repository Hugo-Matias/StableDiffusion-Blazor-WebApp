namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when selected images collection changes (add, remove, clear).
    /// Subscribe to this event to refresh UI when image selection state changes.
    /// </summary>
    public class SelectedImagesChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Type of selection change (Add, Remove, Clear, Replace)
        /// </summary>
        public string ChangeType { get; set; }

        /// <summary>
        /// Number of selected images after the change
        /// </summary>
        public int SelectedCount { get; set; }

        public SelectedImagesChangedEventArgs(string changeType = "Changed", int selectedCount = 0)
        {
            ChangeType = changeType;
            SelectedCount = selectedCount;
        }
    }
}
