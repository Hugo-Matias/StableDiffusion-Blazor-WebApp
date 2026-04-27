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
        /// Generates images using the new GenerationParameters model.
        /// Automatically determines mode from the workflow.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="workflow">The workflow to use for generation.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
        Task<ImagesDto> GenerateImagesAsync(GenerationParameters parameters, Workflow workflow);

        /// <summary>
        /// Generates a video using the new GenerationParameters model.
        /// </summary>
        /// <param name="parameters">The unified generation parameters.</param>
        /// <param name="workflow">The workflow to use for generation.</param>
        /// <returns>Generated video result.</returns>
        Task<GeneratedVideos> GenerateVideoAsync(GenerationParameters parameters, Workflow workflow);

        /// <summary>
        /// Generates a single image and saves it flagged as <see cref="Image.IsHidden"/> = true.
        /// Bypasses the Results tab accumulator and does not publish
        /// <see cref="Events.ImagesGeneratedEventArgs"/>. Used by Workshop preview generation
        /// and other internal-only flows that should not surface in the gallery.
        /// </summary>
        /// <param name="parameters">Pre-cloned generation parameters (caller is responsible for any per-call overrides).</param>
        /// <param name="workflow">The workflow to use for generation.</param>
        /// <returns>The persisted hidden <see cref="Image"/> entity, or null on failure.</returns>
        Task<Image?> GenerateHiddenImageAsync(GenerationParameters parameters, Workflow workflow);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Clears the session-wide accumulator of generated images. Publishes an
        /// <see cref="Events.ImagesGeneratedEventArgs"/> so the Results tab refreshes.
        /// Videos and progress state are not affected.
        /// </summary>
        void ClearGeneratedImages();

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
