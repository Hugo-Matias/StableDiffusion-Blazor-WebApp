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
        Task<List<string>> GetVAEModels();
        Task<List<string>> GetTextEncoders();
        Task<List<SDModel>> GetDiffusionModels();
        Task<List<string>> GetClipModels();
        Task<List<string>> GetClipVisionModels();
        Task<List<string>> GetLoras();
        Task<List<string>> SearchLoras(string search);
        Task<List<string>> GetBBoxDetailers();

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

        /// <summary>
        /// Uploads an audio file to ComfyUI's input folder. The extension is preserved
        /// (defaults to <c>.wav</c>) so that nodes such as <c>LoadAudio</c> can read it.
        /// </summary>
        Task<string> UploadAudioAsync(string base64Data, string? extensionOrFilename = null, Guid? promptId = null);

        /// <summary>
        /// Streams an upload directly to ComfyUI's input folder. Used by the browser-side
        /// video / large-file upload flow that posts via fetch to a minimal API endpoint
        /// and bypasses the SignalR circuit entirely. Returns the uploaded filename.
        /// </summary>
        Task<string> UploadStreamAsync(Stream stream, string originalFilename, string mediaType, Guid? promptId = null);

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
