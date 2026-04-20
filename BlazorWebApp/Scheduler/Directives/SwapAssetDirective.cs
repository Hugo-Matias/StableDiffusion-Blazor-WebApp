namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Replaces a workflow asset value (model, VAE, CLIP, etc.) in
    /// <see cref="BlazorWebApp.Models.GenerationParameters.Assets"/>.
    /// </summary>
    public sealed class SwapAssetDirective : Directive
    {
        /// <summary>
        /// The asset dictionary key (e.g., <c>"Model"</c>, <c>"Vae"</c>).
        /// </summary>
        public string AssetKey { get; set; } = string.Empty;

        /// <summary>
        /// The new asset identifier to assign.
        /// </summary>
        public string AssetValue { get; set; } = string.Empty;
    }
}
