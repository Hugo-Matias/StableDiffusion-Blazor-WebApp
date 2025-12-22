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
        /// Width of the source (if known).
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Height of the source (if known).
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Whether this source has valid data.
        /// </summary>
        public bool HasData => !string.IsNullOrWhiteSpace(Data) || !string.IsNullOrWhiteSpace(FilePath);

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
                Width = Width,
                Height = Height
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
            Width = null;
            Height = null;
        }
    }
}
