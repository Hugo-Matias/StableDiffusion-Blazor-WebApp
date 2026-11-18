namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when input images change (Img2Img, Img2Vid).
    /// Subscribe to this event to refresh UI when input image state changes.
    /// </summary>
    public class InputImageChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Type of input image that changed (Img2Img, Img2Vid, Upscale)
        /// </summary>
        public string ImageType { get; set; }

        /// <summary>
        /// Whether the editor state was reset
        /// </summary>
        public bool EditorStateReset { get; set; }

        public InputImageChangedEventArgs(string imageType = "Unknown", bool editorStateReset = false)
        {
            ImageType = imageType;
            EditorStateReset = editorStateReset;
        }
    }
}
