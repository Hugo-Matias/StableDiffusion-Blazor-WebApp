using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing and tracking progress of long-running operations.
    /// </summary>
    public interface IProgressService
    {
        /// <summary>
        /// Collection of active progress trackers.
        /// </summary>
        List<BaseProgress> Progresses { get; set; }

        /// <summary>
        /// Current generation progress percentage (0-100).
        /// </summary>
        int CurrentProgress { get; set; }
        
        /// <summary>
        /// Indicates whether generation is currently in progress (converging).
        /// </summary>
        bool IsConverging { get; set; }

        /// <summary>
        /// Event fired when progress is updated, added, or removed.
        /// </summary>
        event Action OnUpdate;

        /// <summary>
        /// Adds a new progress tracker to the collection.
        /// </summary>
        /// <param name="progress">The progress tracker to add.</param>
        void Add(BaseProgress progress);

        /// <summary>
        /// Updates the value of an existing progress tracker.
        /// </summary>
        /// <param name="id">The unique identifier of the progress tracker.</param>
        /// <param name="value">The new progress value.</param>
        void Update(Guid id, float value);

        /// <summary>
        /// Removes a progress tracker from the collection.
        /// </summary>
        /// <param name="id">The unique identifier of the progress tracker to remove.</param>
        void Remove(Guid id);
        
        /// <summary>
        /// Notifies subscribers that progress has changed.
        /// </summary>
        void NotifyProgressChanged();
    }
}
