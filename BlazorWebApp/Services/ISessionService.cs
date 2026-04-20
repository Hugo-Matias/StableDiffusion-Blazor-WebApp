using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Represents a source image to be loaded into a specific workflow source slot.
    /// </summary>
    public record PendingSourceImage(string SourceKey, string Data, string? FilePath, string? Label = null, bool IsNewSlot = false);

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

        // Input Images
        string Img2ImgInputImage { get; set; }
        string Img2VidInputImage { get; set; }

        /// <summary>
        /// Pending source images to load into specific workflow source slots.
        /// Consumed by Generate page on workflow initialization.
        /// </summary>
        List<PendingSourceImage> PendingSourceImages { get; }

        /// <summary>
        /// Publishes an event to notify subscribers that pending source images are available.
        /// Call this after adding images to PendingSourceImages.
        /// </summary>
        void NotifyPendingSourceImages();

        // Image Editor
        ImageEditorState ImageEditorState { get; set; }
        void ResetImageEditorState();
        void SetImg2ImgInputImage(string imageData, bool resetEditorState);

        // Session Videos
        GeneratedVideos SessionGeneratedVideos { get; }
        void AddSessionVideo(GeneratedVideo video);
        void AddSessionVideos(IEnumerable<GeneratedVideo> videos);
        void RemoveSessionVideo(GeneratedVideo video);
        void ClearSessionVideos();
    }
}
