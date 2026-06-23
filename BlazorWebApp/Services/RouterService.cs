using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for routing generation requests to ComfyUI backend.
    /// </summary>
    public class RouterService : IRouterService
    {
        private readonly IComfyUIService _capi;
        private readonly IBackendService _backend;
        private readonly ILogger<RouterService> _logger;

        public RouterService(IComfyUIService capi, IBackendService backend, ILogger<RouterService> logger)
        {
            _capi = capi;
            _backend = backend;
            _logger = logger;
        }

        /// <summary>
        /// Searches for LoRA models on ComfyUI backend.
        /// </summary>
        /// <param name="backend">The backend to search on (must be ComfyUI).</param>
        /// <param name="search">Optional search query to filter results.</param>
        /// <returns>Collection of matching LoRA model names.</returns>
        public async Task<IEnumerable<string>> SearchLoras(Backend backend, string search = "")
        {
            _logger.LogDebug("Searching LoRAs on {Backend} with query: {SearchQuery}", backend, search);
            return string.IsNullOrEmpty(search) ? await _capi.GetLoras() : await _capi.SearchLoras(search);
        }

        /// <inheritdoc />
        public async Task<GeneratedImages> PostGenerationAsync(GenerationParameters parameters, Workflow workflow)
        {
            if (!_backend.IsBackendAvailable)
            {
                _logger.LogError("ComfyUI backend not available for generation");
                throw new InvalidOperationException("ComfyUI backend not available");
            }

            if (workflow == null)
            {
                _logger.LogError("No workflow provided for generation");
                throw new ArgumentNullException(nameof(workflow), "Workflow is required for generation");
            }

            _logger.LogInformation("Routing generation request to ComfyUI for workflow: {WorkflowTitle} (Mode: {Mode})", 
                workflow.Title, workflow.Mode);

            return await _capi.PostGenerationAsync(parameters, _backend.ComfyWSClientId, workflow);
        }

        /// <inheritdoc />
        public async Task<GeneratedVideos> PostVideoGenerationAsync(GenerationParameters parameters, Workflow workflow)
        {
            if (!_backend.IsBackendAvailable)
            {
                _logger.LogError("ComfyUI backend not available for video generation");
                throw new InvalidOperationException("ComfyUI backend not available");
            }

            if (workflow == null)
            {
                _logger.LogError("No workflow provided for video generation");
                throw new ArgumentNullException(nameof(workflow), "Workflow is required for video generation");
            }

            _logger.LogInformation("Routing video generation request to ComfyUI for workflow: {WorkflowTitle}", 
                workflow.Title);

            return await _capi.PostVideoGenerationAsync(parameters, _backend.ComfyWSClientId, workflow);
        }
    }
}
