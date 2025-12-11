using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing backend (ComfyUI) availability and operations.
    /// Handles health checks, backend-dependent resources, and options management.
    /// </summary>
    public interface IBackendService
    {
        bool IsBackendAvailable { get; }
        Options Options { get; }
        
        // Temporary - will move to ModelService in Phase 5
        List<Models.Sampler> Samplers { get; }
        List<Scheduler> Schedulers { get; }
        List<Upscaler> Upscalers { get; }
        
        Task<bool> CheckBackendAvailability();
        Task LoadBackendDependentResources();
        Task GetOptions();
        Task<string> PostOptions(Options options);
    }
}
