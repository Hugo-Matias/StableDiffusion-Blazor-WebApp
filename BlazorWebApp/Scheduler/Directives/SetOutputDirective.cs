namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Overrides the output routing (project) for the action's iterations.
    /// Folder is a UI-level filter only (Phase 16c) and is not persisted: saved images are
    /// associated with the chosen <see cref="ProjectName"/>, whose parent folder is derived
    /// from the database at save time.
    /// </summary>
    public sealed class SetOutputDirective : Directive
    {
        /// <summary>
        /// Project name override. When null, the job's default project is used.
        /// </summary>
        public string? ProjectName { get; set; }
    }
}
