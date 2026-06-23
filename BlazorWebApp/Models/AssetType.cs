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
        ClipVision,

        /// <summary>
        /// LoRA models for fine-tuning
        /// Maps to: ComfyUIService.GetLoras()
        /// </summary>
        Lora,

        /// <summary>
        /// ESRGAN-style spatial upscalers loaded by <c>UpscaleModelLoader</c> from
        /// the ComfyUI <c>upscale_models</c> folder.
        /// Maps to: ComfyUIService.GetUpscaleModels()
        /// </summary>
        UpscaleModel,

        /// <summary>
        /// Diffusion-space (latent) upscalers loaded by <c>LatentUpscaleModelLoader</c>
        /// from the ComfyUI <c>latent_upscale_models</c> folder. Distinct from
        /// <see cref="UpscaleModel"/> which is the pixel-space variant.
        /// Maps to: ComfyUIService.GetLatentUpscaleModels()
        /// </summary>
        LatentUpscaleModel,

        /// <summary>
        /// ControlNet models loaded by <c>ControlNetLoader</c> from the ComfyUI
        /// <c>controlnet</c> folder.
        /// Maps to: ComfyUIService.GetControlNetModels()
        /// </summary>
        ControlNet,

        /// <summary>
        /// Model patches loaded by <c>ModelPatchLoader</c> from ComfyUI's
        /// <c>model_patches</c> folder. Used by newer patch-style ControlNet nodes.
        /// Maps to: ComfyUIService.GetModelPatches()
        /// </summary>
        ModelPatch
    }
}
