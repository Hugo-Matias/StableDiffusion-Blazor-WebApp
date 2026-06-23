using BlazorWebApp.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event args for canvas image data changes.
    /// </summary>
    public class CanvasImageDataChangedEventArgs : EventArgs
    {
        public string ImageData { get; init; } = string.Empty;
    }

    /// <summary>
    /// Event args for image editor state changes.
    /// </summary>
    public class ImageEditorStateChangedEventArgs : EventArgs
    {
        public ImageEditorState EditorState { get; init; } = new();
    }

    /// <summary>
    /// Event args for session videos changes.
    /// </summary>
    public class SessionVideosChangedEventArgs : EventArgs
    {
        public int VideoCount { get; init; }
        public SessionVideoAction Action { get; init; }
    }

    /// <summary>
    /// Event args for pending source media changes (Send To).
    /// </summary>
    public class PendingSourceMediaChangedEventArgs : EventArgs
    {
    }

    /// <summary>
    /// Actions that can occur on session videos collection.
    /// </summary>
    public enum SessionVideoAction
    {
        Added,
        Removed,
        Cleared
    }
}
