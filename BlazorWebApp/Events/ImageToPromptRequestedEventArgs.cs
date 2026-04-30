namespace BlazorWebApp.Events
{
    /// <summary>
    /// Published when a user picks "Generate Prompt" on a local image (Gallery / AssetInfoPanel)
    /// to ask the LLM Tools tab to load that image into the Image-to-Prompt view.
    /// </summary>
    public class ImageToPromptRequestedEventArgs : EventArgs
    {
        /// <summary>Absolute on-disk path to the source image.</summary>
        public string ImagePath { get; init; } = string.Empty;

        /// <summary>Optional friendly source label shown next to the loaded image (e.g. "Gallery", "Asset Info").</summary>
        public string? SourceLabel { get; init; }
    }
}
