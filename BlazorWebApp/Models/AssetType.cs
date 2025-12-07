namespace BlazorWebApp.Models
{
    /// <summary>
    /// Defines the types of assets that can be loaded from ComfyUI for workflow execution.
    /// Each type maps to a specific ComfyUI API endpoint.
    /// </summary>
    public enum AssetType
    {
        /// <summary>
        /// Standard checkpoint models (e.g., SD 1.5, SDXL)
        /// Maps to: ComfyUIService.GetCheckpoints()
        /// </summary>
        CheckpointModel,

        /// <summary>
        /// Diffusion models for the Unet loader (e.g., Flux, Qwen, Wan models)
        /// Maps to: ComfyUIService.GetDiffusionModels()
        /// </summary>
        DiffusionModel,

        /// <summary>
        /// VAE (Variational Autoencoder) models
        /// Maps to: ComfyUIService.GetVAEs()
        /// </summary>
        Vae,

        /// <summary>
        /// CLIP text encoder models
        /// Maps to: ComfyUIService.GetClipModels()
        /// </summary>
        Clip,

        /// <summary>
        /// CLIP Vision models for image understanding
        /// Maps to: ComfyUIService.GetClipVisionModels()
        /// </summary>
        ClipVision
    }
}
