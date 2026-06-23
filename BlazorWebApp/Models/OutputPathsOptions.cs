namespace BlazorWebApp.Models
{
    /// <summary>
    /// Configuration for output paths and filename patterns.
    /// Maps to the "OutputPaths" section in appsettings.json.
    /// </summary>
    public class OutputPathsOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "OutputPaths";

        /// <summary>
        /// Subfolder for Txt2Img samples (relative to OutputDir)
        /// </summary>
        public string Txt2ImgSamples { get; set; } = "Text-2-Image\\_samples";

        /// <summary>
        /// Subfolder for Img2Img samples (relative to OutputDir)
        /// </summary>
        public string Img2ImgSamples { get; set; } = "Image-2-Image\\_samples";

        /// <summary>
        /// Subfolder for Img2Vid samples (relative to OutputDir)
        /// </summary>
        public string Img2VidSamples { get; set; } = "Image-2-Video\\_samples";

        /// <summary>
        /// Subfolder for Extras (upscale, etc.) samples (relative to OutputDir)
        /// </summary>
        public string Extras { get; set; } = "Extras";

        /// <summary>
        /// Pattern for directory names within output folders.
        /// Supports: [model_name], [sampler], [seed], [steps], [cfg]
        /// </summary>
        public string DirectoryPattern { get; set; } = "[model_name]/[sampler]";

        /// <summary>
        /// Pattern for sample filenames (without extension).
        /// Supports: [seed], [steps], [cfg], [sampler]
        /// </summary>
        public string FilenamePattern { get; set; } = "[seed]_[steps]_[cfg]";

        /// <summary>
        /// Image format for saved samples (png, jpg, webp)
        /// </summary>
        public string SamplesFormat { get; set; } = "png";

        /// <summary>
        /// Whether to save generated samples to disk
        /// </summary>
        public bool SaveSamples { get; set; } = true;
    }
}
