using BlazorWebApp.Events;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Persistence;
using BlazorWebApp.Services;

namespace BlazorWebApp.Scheduler
{
    /// <inheritdoc />
    public class SchedulerEditorState : ISchedulerEditorState
    {
        /// <summary>Debounce window between the last change and the persistence flush.</summary>
        public static TimeSpan DebounceInterval { get; set; } = TimeSpan.FromSeconds(2);

        private readonly ISchedulerDraftStore _store;
        private readonly IEventService _events;
        private readonly ILogger<SchedulerEditorState> _logger;
        private readonly SemaphoreSlim _flushLock = new(1, 1);

        private Job? _draft;
        private Guid? _editingJobId;
        private bool _isDirty;
        private DateTime _lastMarkedAt;
        private CancellationTokenSource? _debounceCts;

        public SchedulerEditorState(ISchedulerDraftStore store, IEventService events, ILogger<SchedulerEditorState> logger)
        {
            _store = store;
            _events = events;
            _logger = logger;
        }

        public Job? Draft => _draft;
        public Guid? EditingJobId => _editingJobId;
        public bool IsDirty => _isDirty;

        public void SetBaseline(Job draft, Guid? editingJobId)
        {
            _draft = draft;
            _editingJobId = editingJobId;
            var wasDirty = _isDirty;
            _isDirty = false;
            _debounceCts?.Cancel();
            if (wasDirty) Publish();
        }

        public void MarkDirty()
        {
            if (_draft is null) return;
            _lastMarkedAt = DateTime.UtcNow;
            var wasDirty = _isDirty;
            _isDirty = true;
            ScheduleDebouncedFlush();
            if (!wasDirty) Publish();
        }

        public async Task ClearAsync()
        {
            _debounceCts?.Cancel();
            _draft = null;
            _editingJobId = null;
            var wasDirty = _isDirty;
            _isDirty = false;
            await _store.ClearAsync();
            if (wasDirty) Publish();
        }

        public async Task FlushAsync()
        {
            _debounceCts?.Cancel();
            await FlushCoreAsync();
        }

        public Task<SchedulerDraftRecord?> LoadPersistedAsync() => _store.LoadAsync();

        public async ValueTask DisposeAsync()
        {
            try
            {
                _debounceCts?.Cancel();
                if (_isDirty) await FlushCoreAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SchedulerEditorState dispose flush failed.");
            }
            _debounceCts?.Dispose();
            _flushLock.Dispose();
        }

        // --------------------------------------------------------------------

        private void ScheduleDebouncedFlush()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceInterval, token);
                    if (token.IsCancellationRequested) return;
                    await FlushCoreAsync();
                }
                catch (TaskCanceledException) { /* next change rescheduled */ }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Debounced Scheduler draft flush failed.");
                }
            }, token);
        }

        private async Task FlushCoreAsync()
        {
            if (_draft is null) return;
            await _flushLock.WaitAsync();
            try
            {
                if (_draft is null) return;
                await _store.SaveAsync(_draft, _editingJobId);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private void Publish() =>
            _events.Publish(new SchedulerDraftChangedEventArgs(_editingJobId, _isDirty, DateTime.UtcNow));
    }
}
