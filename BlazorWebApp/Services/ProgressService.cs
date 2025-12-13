using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing and tracking progress of long-running operations such as downloads and generations.
    /// </summary>
    public class ProgressService : IProgressService
    {
        private readonly ILogger<ProgressService> _logger;

        /// <summary>
        /// Collection of active progress trackers.
        /// </summary>
        public List<BaseProgress> Progresses { get; set; } = new();

        /// <summary>
        /// Event fired when progress is updated, added, or removed.
        /// </summary>
        public event Action OnUpdate;

        public ProgressService(ILogger<ProgressService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Adds a new progress tracker to the collection.
        /// </summary>
        /// <param name="progress">The progress tracker to add.</param>
        public void Add(BaseProgress progress)
        {
            _logger.LogDebug("Adding progress tracker: {ProgressId} - {ProgressLabel}", progress.Id, progress.Label);
            Progresses.Add(progress);
            Refresh();
        }

        /// <summary>
        /// Updates the value of an existing progress tracker.
        /// </summary>
        /// <param name="id">The unique identifier of the progress tracker.</param>
        /// <param name="value">The new progress value.</param>
        public void Update(Guid id, float value)
        {
            var progress = Progresses.FirstOrDefault(p => p.Id == id);
            if (progress != null)
            {
                if (value < 0 || value > progress.MaxValue)
                {
                    _logger.LogDebug("Progress {ProgressId} completed or invalid, removing", id);
                    Remove(id);
                    return;
                }
                else
                {
                    progress.Value = value;
                    _logger.LogTrace("Progress {ProgressId} updated to {Value}/{MaxValue}", id, value, progress.MaxValue);
                }
            }
            Refresh();
        }

        /// <summary>
        /// Removes a progress tracker from the collection.
        /// </summary>
        /// <param name="id">The unique identifier of the progress tracker to remove.</param>
        public void Remove(Guid id)
        {
            var progress = Progresses?.FirstOrDefault(p => p.Id == id);
            if (progress != null)
            {
                _logger.LogDebug("Removing progress tracker: {ProgressId}", id);
                Progresses.Remove(progress);
            }
            Refresh();
        }

        /// <summary>
        /// Notifies subscribers that progress has been updated.
        /// </summary>
        private void Refresh() => OnUpdate?.Invoke();
    }
}
