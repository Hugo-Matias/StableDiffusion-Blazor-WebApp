namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when backend options change.
    /// Published when GetOptions() or PostOptions() completes successfully.
    /// </summary>
    public class OptionsChangedEventArgs : EventArgs
    {
        // Options data is accessed directly from BackendService.Options
        // No additional properties needed for this event
    }
}
