using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for routing generation requests to ComfyUI backend.
    /// </summary>
    public class RouterService
    {
        private readonly ComfyUIService _capi;
        private readonly ManagerService _m;
        private readonly ILogger<RouterService> _logger;

        public RouterService(ComfyUIService capi, ManagerService m, ILogger<RouterService> logger)
        {
            _capi = capi;
            _m = m;
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

        /// <summary>
        /// Routes a text-to-image generation request to ComfyUI backend.
        /// </summary>
        /// <param name="parameters">The generation parameters.</param>
        /// <returns>The generated images result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when ComfyUI is not available.</exception>
        public async Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters)
        {
            if (!_m.IsComfyUIUp)
            {
                _logger.LogError("ComfyUI backend not available for Txt2Img generation");
                throw new InvalidOperationException("ComfyUI backend not available");
            }

            _logger.LogInformation("Routing Txt2Img request to ComfyUI backend");
            var model = _m.GetCurrentModel(ModeType.Txt2Img);
            var vae = _m.GetCurrentVae(ModeType.Txt2Img);
            var workflow = parameters.Comfy.Workflow ?? _m.ParametersTxt2Img.Comfy.Workflow;
            _logger.LogDebug("Using model: {Model}, VAE: {Vae}, Workflow: {WorkflowId}", model, vae, workflow?.Id);
            return await _capi.PostTxt2Img(parameters.ToTxt2ImgComfyUI(model, vae), _m.ComfyWSClientId, workflow);
        }

        /// <summary>
        /// Routes an image-to-image generation request to ComfyUI backend.
        /// </summary>
        /// <param name="parameters">The generation parameters including input image.</param>
        /// <returns>The generated images result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when ComfyUI is not available or no workflow is configured.</exception>
        public async Task<GeneratedImages> PostImg2Img(Img2ImgParameters parameters)
        {
            if (!_m.IsComfyUIUp)
            {
                _logger.LogError("ComfyUI backend not available for Img2Img generation");
                throw new InvalidOperationException("ComfyUI backend not available");
            }

            _logger.LogInformation("Routing Img2Img request to ComfyUI backend");
            var workflow = parameters.Comfy.Workflow ?? _m.ParametersImg2Img.Comfy.Workflow;
            if (workflow == null)
            {
                _logger.LogError("No workflow configured for Img2Img generation on ComfyUI");
                throw new InvalidOperationException("No workflow configured for Img2Img generation");
            }

            _logger.LogDebug("Using workflow: {WorkflowId}", workflow.Id);
            return await _capi.PostImg2Img(parameters.ToComfyUI(), _m.ComfyWSClientId, workflow);
        }

        /// <summary>
        /// Routes an image-to-video generation request to ComfyUI backend.
        /// </summary>
        /// <param name="parameters">The video generation parameters including input image.</param>
        /// <returns>The generated video result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when ComfyUI is not available or no workflow is configured.</exception>
        public async Task<GeneratedVideos> PostImg2Vid(Img2VidParameters parameters)
        {
            if (!_m.IsComfyUIUp)
            {
                _logger.LogError("ComfyUI backend not available for Img2Vid generation");
                throw new InvalidOperationException("ComfyUI backend not available");
            }

            _logger.LogInformation("Routing Img2Vid request to ComfyUI backend");
            var workflow = parameters.Comfy.Workflow ?? _m.ParametersImg2Vid.Comfy.Workflow;
            if (workflow == null)
            {
                _logger.LogError("No workflow configured for Img2Vid generation on ComfyUI");
                throw new InvalidOperationException("No workflow configured for Img2Vid generation");
            }

            _logger.LogDebug("Using workflow: {WorkflowId}", workflow.Id);
            return await _capi.PostImg2Vid(parameters.ToComfyUI(), _m.ComfyWSClientId, workflow);
        }
    }
}
