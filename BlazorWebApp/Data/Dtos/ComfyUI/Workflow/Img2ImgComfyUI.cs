using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow
{
    /// <summary>
    /// Parameters for Image-to-Image generation workflows (Qwen Edit, etc.)
    /// </summary>
    public class Img2ImgComfyUI : SharedComfyUI
    {
        /// <summary>
        /// Input image for I2I generation
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// Diffusion model name
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// CLIP model name
        /// </summary>
        public string? Clip { get; set; }

        /// <summary>
        /// VAE model name
        /// </summary>
        public string? Vae { get; set; }

        /// <summary>
        /// Lightning LoRA model for fast inference
        /// </summary>
        public string? LightningLora { get; set; }

        /// <summary>
        /// LoRA strength multiplier (default: 1)
        /// </summary>
        public float? LoraStrength { get; set; }

        /// <summary>
        /// Model shift value for Qwen models (default: 3)
        /// </summary>
        public int? ModelShift { get; set; }

        /// <summary>
        /// CFG normalization strength (default: 1)
        /// </summary>
        public float? CfgNormStrength { get; set; }

        /// <summary>
        /// Target megapixels for image scaling (default: 1)
        /// </summary>
        public float? Megapixels { get; set; }

        /// <summary>
        /// Denoising strength (default: 1)
        /// </summary>
        public float? Denoise { get; set; }

        /// <summary>
        /// List of additional LoRAs to apply
        /// </summary>
        public List<Lora>? Loras { get; set; }
    }
}
