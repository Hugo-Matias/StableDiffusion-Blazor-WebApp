using BlazorWebApp.Events;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing backend (ComfyUI) availability and operations.
    /// Consolidates backend health checks, options management, and resource loading.
    /// </summary>
    public class BackendService : IBackendService
    {
        private readonly ComfyUIService _comfyUI;
        private readonly IEventService _events;
        private bool _isBackendAvailable;

        public bool IsBackendAvailable
        {
            get => _isBackendAvailable;
            private set
            {
                if (_isBackendAvailable != value)
                {
                    _isBackendAvailable = value;
                    _events.Publish(new BackendAvailabilityChangedEventArgs
                    {
                        IsAvailable = value
                    });
                }
            }
        }

        public Options Options { get; private set; }

        // These will move to ModelService in Phase 5
        public List<Models.Sampler> Samplers { get; private set; }
        public List<Scheduler> Schedulers { get; private set; }
        public List<Upscaler> Upscalers { get; private set; }

        public BackendService(ComfyUIService comfyUI, IEventService events)
        {
            _comfyUI = comfyUI;
            _events = events;
            Options = new Options();
            Samplers = new List<Models.Sampler>();
            Schedulers = new List<Scheduler>();
            Upscalers = new List<Upscaler>();
        }

        /// <summary>
        /// Checks if ComfyUI backend is available by attempting a health check.
        /// Updates IsBackendAvailable property and fires events if state changes.
        /// </summary>
        public async Task<bool> CheckBackendAvailability()
        {
            try
            {
                // ComfyUIService has a health check method we can use
                var isAvailable = await _comfyUI.CheckComfyUIState();
                IsBackendAvailable = isAvailable;
                return isAvailable;
            }
            catch (Exception)
            {
                IsBackendAvailable = false;
                return false;
            }
        }

        /// <summary>
        /// Loads resources that depend on the backend being available.
        /// This includes samplers, schedulers, and upscalers.
        /// Should be called when backend comes online.
        /// </summary>
        public async Task LoadBackendDependentResources()
        {
            if (!IsBackendAvailable)
                return;

            // Load samplers, schedulers, and upscalers directly from ComfyUI
            // These will move to ModelService in Phase 5
            Samplers = await _comfyUI.GetSamplers() ?? new List<Models.Sampler>();
            Schedulers = await _comfyUI.GetSchedulers() ?? new List<Scheduler>();
            Upscalers = await _comfyUI.GetUpscalers() ?? new List<Upscaler>();
        }

        /// <summary>
        /// Loads backend options/configuration from ComfyUI.
        /// </summary>
        public async Task GetOptions()
        {
            if (!IsBackendAvailable)
            {
                Options = new Options();
                return;
            }

            Options = await _comfyUI.GenerateOptions();
        }

        /// <summary>
        /// Posts updated options to the backend and reloads current options.
        /// </summary>
        public async Task<string> PostOptions(Options options)
        {
            if (!IsBackendAvailable)
                return "Backend not available";

            // ComfyUI doesn't support posting options like WebUI did
            // This is a no-op for ComfyUI but kept for interface compatibility
            await GetOptions();
            return "Options updated (ComfyUI only)";
        }
    }
}
