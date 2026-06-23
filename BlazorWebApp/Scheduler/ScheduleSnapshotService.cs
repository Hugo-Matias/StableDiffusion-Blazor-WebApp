using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler
{
    /// <summary>
    /// Scoped buffer used to hand a freshly-snapshotted <see cref="Job"/> from the Generate page to
    /// the Scheduler Editor tab without piping the full payload through a URL query string.
    /// The snapshot is consumed at most once - calling <see cref="Consume"/> returns and clears it.
    /// </summary>
    public interface IScheduleSnapshotService
    {
        /// <summary>Stores a pending snapshot.</summary>
        void Set(Job job);

        /// <summary>Returns and clears the pending snapshot, or <c>null</c> when none is set.</summary>
        Job? Consume();

        /// <summary>True when a pending snapshot exists.</summary>
        bool HasPending { get; }
    }

    /// <inheritdoc />
    public sealed class ScheduleSnapshotService : IScheduleSnapshotService
    {
        private Job? _pending;

        public bool HasPending => _pending is not null;

        public void Set(Job job) => _pending = job;

        public Job? Consume()
        {
            var snap = _pending;
            _pending = null;
            return snap;
        }
    }
}
