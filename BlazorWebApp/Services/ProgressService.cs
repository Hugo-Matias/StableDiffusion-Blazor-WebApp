using BlazorWebApp.Events;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing and tracking progress of long-running operations such as downloads and generations.
    /// </summary>
    public class ProgressService : IProgressService
    {
        private readonly ILogger<ProgressService> _logger;
        private readonly IEventService _events;
        private int _currentProgress;
        private bool _isConverging;
        private int _queueRemaining;

        /// <summary>
        /// Collection of active progress trackers.
        /// </summary>
        public List<BaseProgress> Progresses { get; set; } = new();

        /// <summary>
        /// Current generation progress percentage (0-100).
        /// </summary>
        public int CurrentProgress
        {
            get => _currentProgress;
            set
            {
                _currentProgress = value;
                _events.Publish(new ProgressChangedEventArgs(value));
            }
        }

        /// <summary>
        /// Indicates whether generation is currently in progress (converging).
        /// </summary>
        public bool IsConverging
        {
            get => _isConverging;
            set
            {
                _isConverging = value;
                _events.Publish(new ConvergingChangedEventArgs(_isConverging));
            }
        }

        /// <summary>
        /// Number of prompts remaining on the backend queue (includes the currently running one).
        /// </summary>
        public int QueueRemaining
        {
            get => _queueRemaining;
            set
            {
                if (_queueRemaining == value) return;
                _queueRemaining = value;
                _events.Publish(new QueueChangedEventArgs(_queueRemaining));
            }
        }

        /// <summary>
        /// Event fired when progress is updated, added, or removed.
        /// </summary>
        public event Action OnUpdate;

        public ProgressService(ILogger<ProgressService> logger, IEventService events)
        {
            _logger = logger;
            _events = events;
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
        /// Notifies subscribers that progress has changed.
        /// </summary>
        public void NotifyProgressChanged() => _events.Publish(new ProgressChangedEventArgs(_currentProgress));

        /// <summary>
        /// Notifies subscribers that progress has been updated.
        /// </summary>
        private void Refresh() => OnUpdate?.Invoke();
    }
}
