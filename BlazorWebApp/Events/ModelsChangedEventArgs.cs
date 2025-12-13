namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when model collections change (CheckpointModels, LoraModels, VAEModels, etc.).
    /// Subscribe to this event to refresh UI when model state changes.
    /// </summary>
    public class ModelsChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Type of models that changed (Checkpoint, Lora, VAE, Embedding, etc.)
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// Optional message describing what changed
        /// </summary>
        public string? Message { get; set; }

        public ModelsChangedEventArgs(string modelType = "Unknown", string? message = null)
        {
            ModelType = modelType;
            Message = message;
        }
    }
}
