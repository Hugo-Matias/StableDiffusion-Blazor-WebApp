namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Identifies which output routing field is being targeted.
    /// </summary>
    public enum OutputField
    {
        /// <summary>The <c>Project</c> field used to route generated images into a project folder.</summary>
        Project,

        /// <summary>The <c>Folder</c> field used to group projects into a folder.</summary>
        Folder
    }
}
