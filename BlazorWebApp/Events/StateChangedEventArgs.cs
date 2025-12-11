namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for state changes in the application.
    /// Provides information about what type of state changed and the old/new values.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The type of state that changed
        /// </summary>
        public StateChangeType ChangeType { get; init; }

        /// <summary>
        /// The previous value before the change (optional)
        /// </summary>
        public object? OldValue { get; init; }

        /// <summary>
        /// The new value after the change (optional)
        /// </summary>
        public object? NewValue { get; init; }
    }

    /// <summary>
    /// Defines the different types of state changes that can occur
    /// </summary>
    public enum StateChangeType
    {
        /// <summary>
        /// Application-wide state changed
        /// </summary>
        AppState,

        /// <summary>
        /// Text-to-Image parameters changed
        /// </summary>
        Txt2ImgParameters,

        /// <summary>
        /// Image-to-Image parameters changed
        /// </summary>
        Img2ImgParameters,

        /// <summary>
        /// Upscale parameters changed
        /// </summary>
        UpscaleParameters,

        /// <summary>
        /// Image-to-Video parameters changed
        /// </summary>
        Img2VidParameters
    }
}
