namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when samplers, schedulers, or upscalers are loaded from the backend.
    /// Published when LoadBackendDependentResources() completes successfully.
    /// </summary>
    public class SamplersSchedulersChangedEventArgs : EventArgs
    {
        // Sampler/Scheduler data is accessed directly from BackendService
        // No additional properties needed for this event
    }
}
