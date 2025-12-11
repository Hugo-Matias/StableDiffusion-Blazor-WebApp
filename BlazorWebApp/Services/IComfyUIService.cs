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
        // Configuration & Health
        Task<Options> GenerateOptions();
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
        
        // Note: LLM methods (GeneratePromptWithLLM) excluded temporarily until LLMRequest/LLMResponse types are defined
    }
}
