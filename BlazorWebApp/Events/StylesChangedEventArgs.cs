namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when prompt styles change (added, removed, or updated).
    /// Used by components that display or depend on available styles.
    /// </summary>
    public class StylesChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Optional information about what changed.
        /// </summary>
        public string? ChangeType { get; set; }
    }
}
