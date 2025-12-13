using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing backend (ComfyUI) availability and operations.
    /// Consolidates backend health checks, output path configuration, and resource loading.
    /// </summary>
    public class BackendService : IBackendService
    {
        private readonly IComfyUIService _comfyUI;
        private readonly IEventService _events;
        private readonly IConfiguration _configuration;
        private bool _isBackendAvailable;
        private Timer? _healthCheckTimer;

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

        /// <summary>
        /// Output path configuration loaded from appsettings.json
        /// </summary>
        public OutputPathsOptions OutputPaths { get; }

        public List<Models.Sampler> Samplers { get; private set; }
        public List<Scheduler> Schedulers { get; private set; }
        public List<Upscaler> Upscalers { get; private set; }

        public BackendService(IComfyUIService comfyUI, IEventService events, IConfiguration configuration)
        {
            _comfyUI = comfyUI;
            _events = events;
            _configuration = configuration;
            
            // Load output paths from configuration
            OutputPaths = new OutputPathsOptions();
            configuration.GetSection(OutputPathsOptions.SectionName).Bind(OutputPaths);
            
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
        /// Starts periodic monitoring of backend availability.
        /// Checks every 30 seconds if not already checking.
        /// </summary>
        public void StartMonitoring(int intervalSeconds = 30)
        {
            if (_healthCheckTimer != null)
                return; // Already monitoring

            _healthCheckTimer = new Timer(async _ =>
            {
                await CheckBackendAvailability();
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
        }

        /// <summary>
        /// Stops periodic monitoring of backend availability.
        /// </summary>
        public void StopMonitoring()
        {
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;
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

            Samplers = await _comfyUI.GetSamplers() ?? new List<Models.Sampler>();
            Schedulers = await _comfyUI.GetSchedulers() ?? new List<Scheduler>();
            Upscalers = await _comfyUI.GetUpscalers() ?? new List<Upscaler>();
        }

        /// <summary>
        /// Gets the full output path for a specific output type
        /// </summary>
        public string GetOutputPath(Outdir outdir)
        {
            var baseDir = _configuration["OutputDir"] ?? "";
            
            return outdir switch
            {
                Outdir.Txt2ImgSamples => Path.Combine(baseDir, OutputPaths.Txt2ImgSamples),
                Outdir.Txt2ImgGrid => Path.Combine(baseDir, OutputPaths.Txt2ImgSamples), // Grids go to same folder
                Outdir.Img2ImgSamples => Path.Combine(baseDir, OutputPaths.Img2ImgSamples),
                Outdir.Img2ImgGrid => Path.Combine(baseDir, OutputPaths.Img2ImgSamples),
                Outdir.Extras => Path.Combine(baseDir, OutputPaths.Extras),
                Outdir.Img2VidSamples => Path.Combine(baseDir, OutputPaths.Img2VidSamples),
                _ => baseDir
            };
        }
    }
}
