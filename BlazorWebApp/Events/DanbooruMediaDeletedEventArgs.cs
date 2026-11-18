namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when a Danbooru library entry is deleted.
    /// Subscribe to this event to refresh library displays after a delete operation.
    /// </summary>
    public class DanbooruMediaDeletedEventArgs : EventArgs
    {
        /// <summary>
        /// Database primary key of the deleted entry.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The Danbooru post ID of the deleted entry.
        /// </summary>
        public int DanbooruPostId { get; set; }

        /// <summary>
        /// Whether the associated file was also deleted from disk.
        /// </summary>
        public bool FileDeleted { get; set; }

        public DanbooruMediaDeletedEventArgs(int id, int danbooruPostId, bool fileDeleted)
        {
            Id = id;
            DanbooruPostId = danbooruPostId;
            FileDeleted = fileDeleted;
        }
    }
}
