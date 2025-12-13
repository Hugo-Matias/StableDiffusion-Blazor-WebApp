using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing backend (ComfyUI) availability and operations.
    /// Handles health checks, backend-dependent resources, and output path configuration.
    /// </summary>
    public interface IBackendService
    {
        bool IsBackendAvailable { get; }
        
        /// <summary>
        /// Output path configuration loaded from appsettings.json
        /// </summary>
        OutputPathsOptions OutputPaths { get; }
        
        // Backend resources
        List<Models.Sampler> Samplers { get; }
        List<Scheduler> Schedulers { get; }
        List<Upscaler> Upscalers { get; }
        
        Task<bool> CheckBackendAvailability();
        void StartMonitoring(int intervalSeconds = 30);
        void StopMonitoring();
        Task LoadBackendDependentResources();
        
        /// <summary>
        /// Gets the full output path for a specific output type
        /// </summary>
        string GetOutputPath(Outdir outdir);
    }
}
