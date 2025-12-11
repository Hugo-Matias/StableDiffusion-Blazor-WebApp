namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event args for folder change events.
    /// </summary>
    public class FolderChangedEventArgs : EventArgs
    {
        public int FolderId { get; init; }
        public string FolderName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Event args for project change events.
    /// </summary>
    public class ProjectChangedEventArgs : EventArgs
    {
        public int ProjectId { get; init; }
        public string ProjectName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Event args for image selection change events.
    /// </summary>
    public class ImageSelectionChangedEventArgs : EventArgs
    {
        public int SelectedCount { get; init; }
        public List<int> SelectedImageIds { get; init; } = new();
    }
}
