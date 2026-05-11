using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Represents source media to be loaded into a specific workflow source slot.
    /// </summary>
    public record PendingSourceMedia(string SourceKey, string SourceType, string? Data, string? FilePath, string? Label = null, bool IsNewSlot = false);

    /// <summary>
    /// Service for managing session-scoped UI state (canvas, image editor, videos).
    /// This service is scoped per browser tab and does not persist to database.
    /// Session state is cleared when the browser tab is closed.
    /// </summary>
    public interface ISessionService
    {
        // Canvas State
        string CanvasImageData { get; set; }
        string? CanvasMaskData { get; set; }
        string UpscaleImageData { get; set; }
        List<string> CanvasStates { get; }

        /// <summary>
        /// Pending source media to load into specific workflow source slots.
        /// Consumed by Generate page on workflow initialization.
        /// </summary>
        List<PendingSourceMedia> PendingSourceMedia { get; }

        /// <summary>
        /// Publishes an event to notify subscribers that pending source media is available.
        /// Call this after adding media to PendingSourceMedia.
        /// </summary>
        void NotifyPendingSourceMedia();

        // Image Editor
        ImageEditorState ImageEditorState { get; set; }
        void ResetImageEditorState();

        // Session Videos
        GeneratedVideos SessionGeneratedVideos { get; }
        void AddSessionVideo(GeneratedVideo video);
        void AddSessionVideos(IEnumerable<GeneratedVideo> videos);
        void RemoveSessionVideo(GeneratedVideo video);
        void ClearSessionVideos();
    }
}
