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
        /// Routes a text-to-image generation request to ComfyUI backend.
        /// </summary>
        /// <param name="parameters">The generation parameters.</param>
        /// <returns>The generated images result.</returns>
        Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters);

        /// <summary>
        /// Routes an image-to-image generation request to ComfyUI backend.
        /// </summary>
        /// <param name="parameters">The generation parameters including input image.</param>
        /// <returns>The generated images result.</returns>
        Task<GeneratedImages> PostImg2Img(Img2ImgParameters parameters);

        /// <summary>
        /// Routes an image-to-video generation request to ComfyUI backend.
        /// </summary>
        /// <param name="parameters">The video generation parameters including input image.</param>
        /// <returns>The generated video result.</returns>
        Task<GeneratedVideos> PostImg2Vid(Img2VidParameters parameters);
    }
}
