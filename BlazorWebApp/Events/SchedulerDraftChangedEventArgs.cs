namespace BlazorWebApp.Events
{
    /// <summary>
    /// Fired by <see cref="BlazorWebApp.Scheduler.ISchedulerEditorState"/> whenever the unsaved
    /// Editor draft transitions state (set, dirty flag change, cleared).
    /// </summary>
    public class SchedulerDraftChangedEventArgs : EventArgs
    {
        /// <summary>Identifier of the job being edited, or <c>null</c> for a brand new unsaved job.</summary>
        public Guid? EditingJobId { get; }

        /// <summary>Whether the draft currently differs from its baseline (last load / last save).</summary>
        public bool IsDirty { get; }

        /// <summary>Timestamp of the state transition.</summary>
        public DateTime At { get; }

        public SchedulerDraftChangedEventArgs(Guid? editingJobId, bool isDirty, DateTime at)
        {
            EditingJobId = editingJobId;
            IsDirty = isDirty;
            At = at;
        }
    }
}
