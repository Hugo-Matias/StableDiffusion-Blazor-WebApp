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
        
        Task<bool> CheckBackendAvailability();
        Task LoadBackendDependentResources();
        Task GetOptions();
        Task<string> PostOptions(Options options);
    }
}
