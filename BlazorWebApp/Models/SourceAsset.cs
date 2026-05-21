namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents an input source (image or video) for a workflow.
    /// Used for Img2Img, Img2Vid, ControlNet inputs, etc.
    /// </summary>
    public class SourceAsset
    {
        /// <summary>
        /// Display label for the input (e.g., "Source Image", "Reference Pose").
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Type of source: "image" or "video".
        /// </summary>
        public string Type { get; set; } = "image";

        /// <summary>
        /// Base64 encoded data (with or without data URI prefix).
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// Original filename if uploaded.
        /// </summary>
        public string? Filename { get; set; }

        /// <summary>
        /// File path if loaded from disk.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Optional black/white PNG mask data for inpainting. White pixels are edited.
        /// </summary>
        public string? MaskData { get; set; }

        /// <summary>
        /// Uploaded mask filename if available in ComfyUI input storage.
        /// </summary>
        public string? MaskFilename { get; set; }

        /// <summary>
        /// Optional local mask file path.
        /// </summary>
        public string? MaskFilePath { get; set; }

        /// <summary>
        /// Width of the source (if known).
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Height of the source (if known).
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Optional loader settings for video sources.
        /// </summary>
        public VideoSourceOptions? VideoOptions { get; set; }

        /// <summary>
        /// Optional loader settings for audio sources (trim start, trim duration).
        /// </summary>
        public AudioSourceOptions? AudioOptions { get; set; }

        /// <summary>
        /// Whether this source has valid data.
        /// </summary>
        public bool HasData => !string.IsNullOrWhiteSpace(Data) || !string.IsNullOrWhiteSpace(FilePath);

        /// <summary>
        /// Whether this source has mask data available for inpainting.
        /// </summary>
        public bool HasMask => !string.IsNullOrWhiteSpace(MaskData) ||
                       !string.IsNullOrWhiteSpace(MaskFilename) ||
                       !string.IsNullOrWhiteSpace(MaskFilePath);

        /// <summary>
        /// Gets the data without the base64 header prefix.
        /// </summary>
        public string? GetRawData()
        {
            if (string.IsNullOrWhiteSpace(Data))
                return null;

            // Remove data URI prefix if present
            var commaIndex = Data.IndexOf(',');
            return commaIndex >= 0 ? Data[(commaIndex + 1)..] : Data;
        }

        /// <summary>
        /// Creates a deep copy of this source asset.
        /// </summary>
        public SourceAsset Clone()
        {
            return new SourceAsset
            {
                Label = Label,
                Type = Type,
                Data = Data,
                Filename = Filename,
                FilePath = FilePath,
                MaskData = MaskData,
                MaskFilename = MaskFilename,
                MaskFilePath = MaskFilePath,
                Width = Width,
                Height = Height,
                VideoOptions = VideoOptions?.Clone(),
                AudioOptions = AudioOptions?.Clone()
            };
        }

        /// <summary>
        /// Clears the data from this source.
        /// </summary>
        public void Clear()
        {
            Data = null;
            Filename = null;
            FilePath = null;
            MaskData = null;
            MaskFilename = null;
            MaskFilePath = null;
            Width = null;
            Height = null;
        }
    }
}
