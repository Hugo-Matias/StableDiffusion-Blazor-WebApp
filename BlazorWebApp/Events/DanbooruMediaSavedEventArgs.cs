using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when a Danbooru post is saved to the local library.
    /// Subscribe to this event to refresh library displays after a save operation.
    /// </summary>
    public class DanbooruMediaSavedEventArgs : EventArgs
    {
        /// <summary>
        /// The saved media entity.
        /// </summary>
        public SavedDanbooruMedia Media { get; set; }

        public DanbooruMediaSavedEventArgs(SavedDanbooruMedia media)
        {
            Media = media;
        }
    }
}
