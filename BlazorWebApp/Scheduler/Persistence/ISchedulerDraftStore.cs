using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler.Persistence
{
    /// <summary>
    /// Persists the Scheduler editor's unsaved draft to the database so it survives
    /// app restarts. Single-slot store; saving overwrites any existing draft.
    /// </summary>
    public interface ISchedulerDraftStore
    {
        /// <summary>Loads the persisted draft, or <c>null</c> when none is stored.</summary>
        Task<SchedulerDraftRecord?> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>Creates or overwrites the draft row with the supplied payload.</summary>
        Task SaveAsync(Job draft, Guid? editingJobId, CancellationToken cancellationToken = default);

        /// <summary>Removes the persisted draft, if any.</summary>
        Task ClearAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Snapshot returned from <see cref="ISchedulerDraftStore.LoadAsync"/>.</summary>
    public sealed record SchedulerDraftRecord(Job Draft, Guid? EditingJobId, DateTime UpdatedAt);
}
