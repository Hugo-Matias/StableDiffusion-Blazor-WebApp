using BlazorWebApp.Events;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing session-scoped UI state (canvas, image editor, videos).
    /// This service is a singleton and maintains state across the application.
    /// Session state is transient and does not persist to database.
    /// </summary>
    public class SessionService : ISessionService
    {
        private readonly IEventService _events;
        private string _canvasImageData = string.Empty;
        private ImageEditorState _imageEditorState = new();

        public SessionService(IEventService events)
        {
            _events = events;
        }

        #region Canvas State

        public string CanvasImageData
        {
            get => _canvasImageData;
            set
            {
                _canvasImageData = value;
                _events.Publish(new CanvasImageDataChangedEventArgs { ImageData = value });
            }
        }

        public string? CanvasMaskData { get; set; }

        public string UpscaleImageData { get; set; } = string.Empty;

        public List<string> CanvasStates { get; } = new();

        #endregion

        #region Source Media

        public List<PendingSourceMedia> PendingSourceMedia { get; } = new();

        public void NotifyPendingSourceMedia()
        {
            _events.Publish(new PendingSourceMediaChangedEventArgs());
        }

        #endregion

        #region Image Editor

        public ImageEditorState ImageEditorState
        {
            get => _imageEditorState;
            set
            {
                _imageEditorState = value;
                _events.Publish(new ImageEditorStateChangedEventArgs { EditorState = value });
            }
        }

        /// <summary>
        /// Resets the image editor state, clearing all edits and layers.
        /// Call this when the user manually changes the input image (not when editor outputs a result).
        /// </summary>
        public void ResetImageEditorState()
        {
            _imageEditorState = new ImageEditorState();
            _events.Publish(new ImageEditorStateChangedEventArgs { EditorState = _imageEditorState });
        }

        #endregion

        #region Session Videos

        public GeneratedVideos SessionGeneratedVideos { get; } = new();

        public void AddSessionVideo(GeneratedVideo video)
        {
            SessionGeneratedVideos.Videos.Insert(0, video);
            _events.Publish(new SessionVideosChangedEventArgs
            {
                VideoCount = SessionGeneratedVideos.Videos.Count,
                Action = SessionVideoAction.Added
            });
        }

        public void AddSessionVideos(IEnumerable<GeneratedVideo> videos)
        {
            foreach (var video in videos)
            {
                SessionGeneratedVideos.Videos.Insert(0, video);
            }
            _events.Publish(new SessionVideosChangedEventArgs
            {
                VideoCount = SessionGeneratedVideos.Videos.Count,
                Action = SessionVideoAction.Added
            });
        }

        public void RemoveSessionVideo(GeneratedVideo video)
        {
            SessionGeneratedVideos.Videos.Remove(video);
            _events.Publish(new SessionVideosChangedEventArgs
            {
                VideoCount = SessionGeneratedVideos.Videos.Count,
                Action = SessionVideoAction.Removed
            });
        }

        public void ClearSessionVideos()
        {
            SessionGeneratedVideos.Videos.Clear();
            _events.Publish(new SessionVideosChangedEventArgs
            {
                VideoCount = 0,
                Action = SessionVideoAction.Cleared
            });
        }

        #endregion
    }
}
