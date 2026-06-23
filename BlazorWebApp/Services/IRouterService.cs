using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for routing generation requests to ComfyUI backend.
    /// </summary>
    public interface IRouterService
    {
        /// <summary>
        /// Searches for LoRA models on ComfyUI backend.
        /// </summary>
        /// <param name="backend">The backend to search on (must be ComfyUI).</param>
        /// <param name="search">Optional search query to filter results.</param>
        /// <returns>Collection of matching LoRA model names.</returns>
        Task<IEnumerable<string>> SearchLoras(Backend backend, string search = "");

        /// <summary>
        /// Executes a generation workflow using the unified GenerationParameters model.
        /// Works for Txt2Img and Img2Img modes.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="workflow">The workflow to execute.</param>
        /// <returns>The generated images result.</returns>
        Task<GeneratedImages> PostGenerationAsync(GenerationParameters parameters, Workflow workflow);

        /// <summary>
        /// Executes a video generation workflow using the unified GenerationParameters model.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="workflow">The workflow to execute.</param>
        /// <returns>The generated video result.</returns>
        Task<GeneratedVideos> PostVideoGenerationAsync(GenerationParameters parameters, Workflow workflow);
    }
}
