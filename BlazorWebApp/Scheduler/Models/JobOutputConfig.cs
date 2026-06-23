namespace BlazorWebApp.Scheduler.Models
{
    /// <summary>
    /// Default output routing for a <see cref="Job"/>. Individual actions may override via
    /// <see cref="JobAction.OutputOverride"/> or a <see cref="BlazorWebApp.Scheduler.Directives.SetOutputDirective"/>.
    /// </summary>
    public sealed class JobOutputConfig
    {
        /// <summary>Default project name new images are routed to.</summary>
        public string? ProjectName { get; set; }

        /// <summary>Default folder name new images are routed to.</summary>
        public string? FolderName { get; set; }
    }
}
