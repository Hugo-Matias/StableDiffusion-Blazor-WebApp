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

        // Forward slashes on purpose: these defaults are used on Linux, where a backslash is a literal
        // character in the directory name (created a folder literally named "Text-2-Image\_samples").
        // Forward slashes work on both platforms.

        /// <summary>
        /// Subfolder for Txt2Img samples (relative to OutputDir)
        /// </summary>
        public string Txt2ImgSamples { get; set; } = "Text-2-Image/_samples";

        /// <summary>
        /// Subfolder for Img2Img samples (relative to OutputDir)
        /// </summary>
        public string Img2ImgSamples { get; set; } = "Image-2-Image/_samples";

        /// <summary>
        /// Subfolder for Img2Vid samples (relative to OutputDir)
        /// </summary>
        public string Img2VidSamples { get; set; } = "Image-2-Video/_samples";

        /// <summary>
        /// Subfolder for Extras (upscale, etc.) samples (relative to OutputDir)
        /// </summary>
        public string Extras { get; set; } = "Extras";

        /// <summary>
        /// Subfolder for generated audio files (relative to OutputDir)
        /// </summary>
        public string Audio { get; set; } = "Audio";

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
