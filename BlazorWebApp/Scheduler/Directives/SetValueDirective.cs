using BlazorWebApp.Scheduler.Targets;

namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Sets a parameter at the specified target to a fixed value.
    /// </summary>
    public sealed class SetValueDirective : Directive
    {
        /// <summary>
        /// Where the value should be written.
        /// </summary>
        public ParameterTarget Target { get; set; } = default!;

        /// <summary>
        /// The value to assign. Type is inferred by the applier from the target kind.
        /// </summary>
        public object? Value { get; set; }
    }
}
