namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Targets a single value inside a fragment's parameter dictionary.
    /// </summary>
    public sealed class FragmentTarget : ParameterTarget
    {
        /// <summary>
        /// Fragment identifier matching <see cref="BlazorWebApp.Models.FragmentKeys.Fragments"/> entries.
        /// Example: <c>"main_sampler"</c>.
        /// </summary>
        public string FragmentId { get; set; } = string.Empty;

        /// <summary>
        /// Parameter key matching <see cref="BlazorWebApp.Models.FragmentKeys.Params"/> entries.
        /// Example: <c>"steps"</c>.
        /// </summary>
        public string ParamKey { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string GetDisplayName() => $"{FragmentId}.{ParamKey}";
    }
}
