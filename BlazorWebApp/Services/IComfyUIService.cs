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

        // Generation Operations
        Task<GeneratedImages> PostTxt2Img(Txt2ImgComfyUI param, string clientId, Workflow workflow);
        Task<GeneratedImages> PostImg2Img(Img2ImgComfyUI param, string clientId, Workflow workflow);
        Task<GeneratedVideos> PostImg2Vid(Img2VidComfyUI param, string clientId, Workflow workflow);

        // Queue Operations
        Task<string> PostInterrupt();
        Task<HttpResponseMessage> PostClearQueue();
    }
}
