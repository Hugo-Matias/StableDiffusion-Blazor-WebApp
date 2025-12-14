namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when the generation converging state changes.
    /// IsConverging indicates whether the application is currently processing a generation request.
    /// </summary>
    public class ConvergingChangedEventArgs : EventArgs
    {
        /// <summary>
        /// True if a generation is currently in progress, false otherwise.
        /// </summary>
        public bool IsConverging { get; set; }

        public ConvergingChangedEventArgs(bool isConverging)
        {
            IsConverging = isConverging;
        }
    }
}
