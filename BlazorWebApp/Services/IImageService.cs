using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for orchestrating image and video generation workflows.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Event fired when image generation state changes.
        /// </summary>
        event Action OnChange;

        /// <summary>
        /// Last generated video result.
        /// </summary>
        GeneratedVideos GeneratedVideos { get; }

        /// <summary>
        /// Generates images based on the specified mode (Txt2Img, Img2Img, or Extras/Upscale).
        /// </summary>
        /// <param name="mode">The generation mode to use.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
        Task<ImagesDto> GetImages(ModeType mode);

        /// <summary>
        /// Generates a video from an image using Img2Vid parameters.
        /// </summary>
        /// <returns>Generated video result.</returns>
        Task<GeneratedVideos> GetVideo();

        /// <summary>
        /// Saves generated images to disk and database.
        /// </summary>
        /// <param name="outdirSamples">Output directory for sample images.</param>
        /// <param name="outdirGrid">Optional output directory for grid image.</param>
        /// <param name="scriptName">Name of the script used (if any).</param>
        /// <returns>DTO containing saved image information.</returns>
        Task<ImagesDto> SaveImages(Outdir outdirSamples, Outdir? outdirGrid, string scriptName);

        /// <summary>
        /// Saves an upscaled image.
        /// </summary>
        /// <returns>DTO containing saved image information.</returns>
        Task<ImagesDto?> SaveUpscaleImage();

        /// <summary>
        /// Downloads an image from a URL and saves it as PNG.
        /// </summary>
        /// <param name="url">URL of the image to download.</param>
        /// <param name="path">Local path to save the image.</param>
        /// <param name="overwrite">Whether to overwrite existing file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DownloadImageAsPng(string url, string path, bool overwrite = true);
    }
}
