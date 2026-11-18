namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Targets an output routing field on a <see cref="BlazorWebApp.Scheduler.Models.JobOutputConfig"/>.
    /// Used to redirect generation results to specific projects or folders during a Scheduler run.
    /// </summary>
    public sealed class OutputTarget : ParameterTarget
    {
        /// <summary>
        /// Which output field this target addresses.
        /// </summary>
        public OutputField Field { get; set; }

        /// <inheritdoc />
        public override string GetDisplayName() => $"Output:{Field}";
    }
}
