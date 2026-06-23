namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Targets a workflow asset key in <see cref="BlazorWebApp.Models.GenerationParameters.Assets"/>.
    /// Examples: <c>"Model"</c>, <c>"Vae"</c>, <c>"Clip"</c>.
    /// </summary>
    public sealed class AssetTarget : ParameterTarget
    {
        /// <summary>
        /// The asset dictionary key as defined by the workflow metadata.
        /// </summary>
        public string AssetKey { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string GetDisplayName() => $"Asset:{AssetKey}";
    }
}
