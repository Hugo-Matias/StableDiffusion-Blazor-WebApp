namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Overrides the output routing (project / folder) for the action's iterations.
    /// Null fields mean "keep the existing value from the job default".
    /// </summary>
    public sealed class SetOutputDirective : Directive
    {
        /// <summary>
        /// Project name override. When null, the job's default project is used.
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Folder name override. When null, the job's default folder is used.
        /// </summary>
        public string? FolderName { get; set; }
    }
}
