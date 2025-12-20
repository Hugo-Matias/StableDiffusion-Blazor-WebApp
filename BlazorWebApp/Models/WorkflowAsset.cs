namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents an asset (model/resource) required by a workflow.
    /// Assets are dynamically loaded from ComfyUI and displayed in the TopToolbar.
    /// </summary>
    public class WorkflowAsset
    {
        /// <summary>
        /// The parameter name that maps to the workflow template variable.
        /// Example: "HighModel", "LowModel", "Vae", "Clip"
        /// This name is used as the key in GenerationParameters.Assets dictionary.
        /// </summary>
        public string Parameter { get; set; } = string.Empty;

        /// <summary>
        /// The display label shown to the user in the UI.
        /// Example: "High Model", "Low Model", "VAE", "CLIP"
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// The type of asset, which determines which ComfyUI API endpoint to call.
        /// </summary>
        public AssetType Type { get; set; }

        /// <summary>
        /// The default value to use if no value is set in the application state.
        /// This should be a filename that exists in the user's ComfyUI models directory.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// The display order in the TopToolbar. Lower numbers appear first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Grid column size (1-12, for MudGrid). 0 = use component default.
        /// </summary>
        public int ColumnSize { get; set; }
    }
}
