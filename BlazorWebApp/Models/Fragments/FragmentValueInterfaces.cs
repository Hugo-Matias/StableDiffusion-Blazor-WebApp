namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Interface for sampler fragment parameter values.
    /// Provides strongly-typed access to common sampler parameters.
    /// </summary>
    public interface ISamplerValues
    {
        /// <summary>Number of sampling steps.</summary>
        int Steps { get; set; }

        /// <summary>Classifier-free guidance scale.</summary>
        double Cfg { get; set; }

        /// <summary>Random seed for generation (-1 for random).</summary>
        long Seed { get; set; }

        /// <summary>Sampler algorithm name (e.g., "euler", "dpmpp_2m").</summary>
        string SamplerName { get; set; }

        /// <summary>Scheduler type (e.g., "normal", "karras", "simple").</summary>
        string Scheduler { get; set; }

        /// <summary>Denoising strength (0.0 to 1.0, used in img2img).</summary>
        double Denoise { get; set; }
    }

    /// <summary>
    /// Interface for latent/resolution fragment parameter values.
    /// </summary>
    public interface ILatentValues
    {
        /// <summary>Image width in pixels.</summary>
        int Width { get; set; }

        /// <summary>Image height in pixels.</summary>
        int Height { get; set; }

        /// <summary>Number of images to generate in a batch.</summary>
        int BatchSize { get; set; }
    }

    /// <summary>
    /// Interface for prompts fragment parameter values.
    /// </summary>
    public interface IPromptsValues
    {
        /// <summary>Positive prompt text.</summary>
        string Positive { get; set; }

        /// <summary>Negative prompt text.</summary>
        string Negative { get; set; }
    }

    /// <summary>
    /// Interface for detailer fragment parameter values.
    /// </summary>
    public interface IDetailerValues
    {
        /// <summary>Whether the detailer is enabled.</summary>
        bool IsActive { get; set; }

        /// <summary>Detailer CFG scale.</summary>
        double DetailerCfg { get; set; }

        /// <summary>Detailer denoising strength.</summary>
        double DetailerDenoise { get; set; }

        /// <summary>Detailer sampling steps.</summary>
        int DetailerSteps { get; set; }

        /// <summary>Detection model name.</summary>
        string DetectionModel { get; set; }
    }

    /// <summary>
    /// Interface for upscale/enhancement fragment parameter values.
    /// </summary>
    public interface IUpscaleValues
    {
        /// <summary>Whether upscaling is enabled.</summary>
        bool IsActive { get; set; }

        /// <summary>Upscale factor (e.g., 2.0 for 2x).</summary>
        double UpscaleFactor { get; set; }

        /// <summary>Upscale model name.</summary>
        string UpscaleModel { get; set; }
    }
}
