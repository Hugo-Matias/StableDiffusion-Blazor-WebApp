using BlazorWebApp.Events;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Persistence;
using BlazorWebApp.Services;

namespace BlazorWebApp.Scheduler
{
    /// <summary>
    /// Scoped holder for the Scheduler Editor's in-flight draft. Tracks dirty state,
    /// debounces persistence through <see cref="ISchedulerDraftStore"/>, and publishes
    /// <see cref="SchedulerDraftChangedEventArgs"/> via <see cref="IEventService"/>.
    /// </summary>
    public interface ISchedulerEditorState : IAsyncDisposable
    {
        /// <summary>The draft currently being edited, or <c>null</c> when none is loaded.</summary>
        Job? Draft { get; }

        /// <summary>Identifier of the persisted job being edited, or <c>null</c> for a new unsaved job.</summary>
        Guid? EditingJobId { get; }

        /// <summary>True when <see cref="Draft"/> differs from the last-committed baseline.</summary>
        bool IsDirty { get; }

        /// <summary>Initializes a new baseline. Clears dirty state. Does not persist.</summary>
        void SetBaseline(Job draft, Guid? editingJobId);

        /// <summary>Marks the draft as dirty and schedules a debounced persistence flush.</summary>
        void MarkDirty();

        /// <summary>Clears the in-memory draft and removes any persisted copy.</summary>
        Task ClearAsync();

        /// <summary>Immediately flushes any pending debounced save to the store.</summary>
        Task FlushAsync();

        /// <summary>Reads the persisted draft, if any, without affecting the current baseline.</summary>
        Task<SchedulerDraftRecord?> LoadPersistedAsync();
    }
}
