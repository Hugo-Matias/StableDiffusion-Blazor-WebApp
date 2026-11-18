using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for model changes.
    /// Fired when the current model is changed for a specific generation mode.
    /// </summary>
    public class ModelChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The model that was previously selected
        /// </summary>
        public string PreviousModel { get; init; } = string.Empty;

        /// <summary>
        /// The newly selected model
        /// </summary>
        public string NewModel { get; init; } = string.Empty;

        /// <summary>
        /// The generation mode this model change applies to
        /// </summary>
        public ModeType Mode { get; init; }
    }
}
