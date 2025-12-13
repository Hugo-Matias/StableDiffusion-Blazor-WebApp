namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when generation parameters change (Txt2Img, Img2Img, Img2Vid, Upscale).
    /// Subscribe to this event to refresh UI when parameter state changes.
    /// </summary>
    public class ParametersChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Type of parameters that changed (Txt2Img, Img2Img, Img2Vid, Upscale)
        /// </summary>
        public string ParametersType { get; set; }

        /// <summary>
        /// Optional message describing what changed
        /// </summary>
        public string? Message { get; set; }

        public ParametersChangedEventArgs(string parametersType = "Unknown", string? message = null)
        {
            ParametersType = parametersType;
            Message = message;
        }
    }
}
