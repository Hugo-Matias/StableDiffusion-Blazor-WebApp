namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Applies one or more named prompt styles to the parameters.
    /// Resolution of style names against the current style catalog is performed by the applier (Phase 4).
    /// </summary>
    public sealed class AddPromptStyleDirective : Directive
    {
        /// <summary>
        /// Ordered list of style names to apply.
        /// </summary>
        public List<string> StyleNames { get; set; } = new();
    }
}
