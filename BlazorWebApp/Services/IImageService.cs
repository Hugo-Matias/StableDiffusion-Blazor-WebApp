using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for orchestrating image and video generation workflows.
    /// Events are published through IEventService (ImagesGeneratedEventArgs).
    /// </summary>
    public interface IImageService
    {
        #region Generation Results

        /// <summary>
        /// Raw generated images from the backend (base64 encoded).
        /// Contains Images list and Info (workflow JSON from ComfyUI).
        /// </summary>
        GeneratedImages Images { get; }

        /// <summary>
        /// Generated image entities saved to database.
        /// </summary>
        ImagesDto GeneratedImageEntities { get; set; }

        /// <summary>
        /// Last generated video result.
        /// </summary>
        GeneratedVideos GeneratedVideos { get; }

        /// <summary>
        /// Current inference progress. Can be set by websocket service.
        /// </summary>
        InferenceProgress Progress { get; set; }

        #endregion

        #region Generation Methods

        /// <summary>
        /// Generates images based on the specified mode (Txt2Img, Img2Img, or Extras/Upscale).
        /// Uses legacy parameter classes from StateService.
        /// </summary>
        /// <param name="mode">The generation mode to use.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
        Task<ImagesDto> GetImages(ModeType mode);

        /// <summary>
        /// Generates images using the new GenerationParameters model.
        /// Automatically determines mode from the workflow.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="workflow">The workflow to use for generation.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
        Task<ImagesDto> GenerateImagesAsync(GenerationParameters parameters, Workflow workflow);

        /// <summary>
        /// Generates a video from an image using Img2Vid parameters.
        /// Uses legacy parameter classes from StateService.
        /// </summary>
        /// <returns>Generated video result.</returns>
        Task<GeneratedVideos> GetVideo();

        /// <summary>
        /// Generates a video using the new GenerationParameters model.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="workflow">The workflow to use for generation.</param>
        /// <returns>Generated video result.</returns>
        Task<GeneratedVideos> GenerateVideoAsync(GenerationParameters parameters, Workflow workflow);

        /// <summary>
        /// Saves generated images to disk and database.
        /// </summary>
        /// <param name="outdirSamples">Output directory for sample images.</param>
        /// <param name="scriptName">Name of the script used (if any).</param>
        /// <returns>DTO containing saved image information.</returns>
        Task<ImagesDto> SaveImages(Outdir outdirSamples, string scriptName);

        /// <summary>
        /// Downloads an image from a URL and saves it as PNG.
        /// </summary>
        /// <param name="url">URL of the image to download.</param>
        /// <param name="path">Local path to save the image.</param>
        /// <param name="overwrite">Whether to overwrite existing file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DownloadImageAsPng(string url, string path, bool overwrite = true);

        #endregion
    }
}
