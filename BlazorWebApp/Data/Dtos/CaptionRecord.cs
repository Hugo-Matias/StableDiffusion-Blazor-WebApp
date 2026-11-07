namespace BlazorWebApp.Data.Dtos
{
    /// <summary>
    /// Represents a single caption record from captions.csv.
    /// </summary>
    public class CaptionRecord
    {
        /// <summary>
        /// Row number (1-based) used as a unique identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Full caption text — the core prompt value.
        /// </summary>
        public string Caption { get; set; } = string.Empty;

        /// <summary>
        /// Aspect ratio (e.g., "2:3", "3:2", "1:1", "16:9").
        /// </summary>
        public string AspectRatio { get; set; } = string.Empty;

        /// <summary>
        /// Nudity level ("none", "partial", "full").
        /// </summary>
        public string Nudity { get; set; } = string.Empty;

        /// <summary>
        /// Artistic quality score (0.0 – 1.0).
        /// </summary>
        public double ArtisticQuality { get; set; }

        /// <summary>
        /// Erotic content score (0.0 – 1.0).
        /// </summary>
        public double EroticScore { get; set; }

        /// <summary>
        /// Pornographic content score (0.0 – 1.0).
        /// </summary>
        public double PornographicScore { get; set; }

        /// <summary>
        /// Count of male subjects in the caption.
        /// </summary>
        public int Males { get; set; }

        /// <summary>
        /// Count of female subjects in the caption.
        /// </summary>
        public int Females { get; set; }

        /// <summary>
        /// List of clothing items present (parsed from JSON array string).
        /// </summary>
        public List<string> Clothing { get; set; } = new();
    }
}
