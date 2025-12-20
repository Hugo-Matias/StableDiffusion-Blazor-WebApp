using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Interface for ComfyUI backend operations.
    /// Handles model management, image generation, and video generation.
    /// </summary>
    public interface IComfyUIService
    {
        // Health Check
        Task<bool> CheckComfyUIState();

        // Model Retrieval
        Task<List<SDModel>> GetCheckpoints();
        Task<List<Upscaler>> GetUpscalers();
        Task<List<string>> GetVAEModels();
        Task<List<string>> GetTextEncoders();
        Task<List<SDModel>> GetDiffusionModels();
        Task<List<string>> GetClipModels();
        Task<List<string>> GetClipVisionModels();
        Task<List<string>> GetLoras();
        Task<List<string>> SearchLoras(string search);
        Task<List<string>> GetBBoxDetailers();
        Task<List<Models.Sampler>> GetSamplers();
        Task<List<Models.Scheduler>> GetSchedulers();
        Task<List<string>> GetDetailerSamplers();
        Task<List<string>> GetDetailerSchedulers();

        /// <summary>
        /// Gets input options for a specific node input from ComfyUI's object_info API.
        /// Used to dynamically fetch available options for node parameters (e.g., model lists).
        /// </summary>
        /// <param name="classType">The node class_type (e.g., "SeedVR2LoadDiTModel")</param>
        /// <param name="inputName">The input field name (e.g., "model")</param>
        /// <returns>List of available option strings for the input</returns>
        Task<List<string>> GetNodeInputOptionsAsync(string classType, string inputName);

        // History & File Operations
        Task<List<string>> GetFilenameFromHistory(Guid promptId);
        Task<List<string>> GetVideoFilenameFromHistory(Guid promptId);

        // Image Upload
        Task<string> UploadImageAsync(string base64Data, Guid? promptId = null);

        #region New GenerationParameters-based Methods

        /// <summary>
        /// Executes image generation using the unified GenerationParameters model.
        /// Builds the ComfyUI workflow payload directly from fragments.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="clientId">The WebSocket client ID for progress tracking.</param>
        /// <param name="workflow">The workflow template to execute.</param>
        /// <returns>The generated images result.</returns>
        Task<GeneratedImages> PostGenerationAsync(GenerationParameters parameters, string clientId, Workflow workflow);

        /// <summary>
        /// Executes video generation using the unified GenerationParameters model.
        /// Builds the ComfyUI workflow payload directly from fragments.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="clientId">The WebSocket client ID for progress tracking.</param>
        /// <param name="workflow">The workflow template to execute.</param>
        /// <returns>The generated video result.</returns>
        Task<GeneratedVideos> PostVideoGenerationAsync(GenerationParameters parameters, string clientId, Workflow workflow);

        #endregion

        // Queue Operations
        Task<string> PostInterrupt();
        Task<HttpResponseMessage> PostClearQueue();
    }
}
