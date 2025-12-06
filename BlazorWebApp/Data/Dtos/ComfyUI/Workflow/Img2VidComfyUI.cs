using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow
{
    /// <summary>
    /// Parameters for Image-to-Video generation workflows (WAN 2.2, etc.)
    /// </summary>
    public class Img2VidComfyUI : SharedComfyUI
    {
        /// <summary>
        /// Input image path for I2V generation
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// High noise model for dual-model workflows (e.g., WAN 2.2)
        /// </summary>
        public string? HighModel { get; set; }

        /// <summary>
        /// Low noise model for dual-model workflows (e.g., WAN 2.2)
        /// </summary>
        public string? LowModel { get; set; }

        /// <summary>
        /// CLIP model name
        /// </summary>
        public string? Clip { get; set; }

        /// <summary>
        /// CLIP Vision model name
        /// </summary>
        public string? ClipVision { get; set; }

        /// <summary>
        /// VAE model name
        /// </summary>
        public string? Vae { get; set; }

        /// <summary>
        /// List of LoRAs to apply (supports dual-model LoRAs with HighPath/LowPath)
        /// </summary>
        public List<Lora>? Loras { get; set; }

        /// <summary>
        /// Video length in frames (default: 81 for ~5 seconds at 16fps)
        /// </summary>
        public int? Length { get; set; }

        /// <summary>
        /// Motion amplitude for PainterI2V node (default: 1.1)
        /// </summary>
        public float? MotionAmplitude { get; set; }

        /// <summary>
        /// ModelSamplingSD3 shift value (default: 5)
        /// </summary>
        public int? Shift { get; set; }

        /// <summary>
        /// Base frame rate before interpolation (default: 16)
        /// </summary>
        public int? FrameRate { get; set; }

        /// <summary>
        /// Frame interpolation settings for upscaling and smoothing video output
        /// </summary>
        public FrameInterpolationParameters? FrameInterpolation { get; set; }
    }

    /// <summary>
    /// Parameters for frame interpolation (RIFE) and upscaling
    /// </summary>
    public class FrameInterpolationParameters
    {
        /// <summary>
        /// Whether frame interpolation is enabled
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Scale factor for upscaling frames before interpolation (default: 2.0)
        /// </summary>
        public double? ScaleBy { get; set; }

        /// <summary>
        /// Frame rate multiplier (default: 2, doubles the frame rate)
        /// </summary>
        public int? Multiplier { get; set; }

        /// <summary>
        /// RIFE model to use for interpolation (default: "rife49.pth")
        /// </summary>
        public string? RifeModel { get; set; }
    }
}
