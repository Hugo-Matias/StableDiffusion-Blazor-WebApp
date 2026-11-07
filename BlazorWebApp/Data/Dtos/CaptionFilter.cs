namespace BlazorWebApp.Data.Dtos
{
    /// <summary>
    /// Sort options for the caption library grid.
    /// </summary>
    public enum CaptionSortBy
    {
        Default,
        Id,
        CaptionLength,
        ArtisticQuality,
        EroticScore,
        PornographicScore,
        Males,
        Females,
        Random
    }

    /// <summary>
    /// Filter criteria for querying the caption library.
    /// </summary>
    public class CaptionFilter
    {
        /// <summary>
        /// Full-text search term to match against caption text.
        /// </summary>
        public string? SearchText { get; set; }

        /// <summary>
        /// Negative search term — captions containing any of these terms are excluded.
        /// Works with SearchText in an AND logic (must match positive AND not match negative).
        /// </summary>
        public string? NegativeSearchText { get; set; }

        /// <summary>
        /// Filter by aspect ratio (e.g., "2:3", "3:2"). Null means any.
        /// </summary>
        public string? AspectRatio { get; set; }

        /// <summary>
        /// Filter by nudity level ("none", "partial", "full"). Null means any.
        /// </summary>
        public string? Nudity { get; set; }

        /// <summary>
        /// Minimum artistic quality threshold (0.0 – 1.0). Null means no minimum.
        /// </summary>
        public double? MinArtisticQuality { get; set; }

        /// <summary>
        /// Minimum erotic score threshold (0.0 – 1.0). Null means no minimum.
        /// </summary>
        public double? MinEroticScore { get; set; }

        /// <summary>
        /// Maximum pornographic score threshold (0.0 – 1.0). Null means no maximum.
        /// </summary>
        public double? MaxPornographicScore { get; set; }

        /// <summary>
        /// Exact male subject count. Null means any.
        /// </summary>
        public int? Males { get; set; }

        /// <summary>
        /// Exact female subject count. Null means any.
        /// </summary>
        public int? Females { get; set; }

        /// <summary>
        /// Filter by clothing items (matches entries containing any of the selected items). Null/empty means any.
        /// </summary>
        public List<string>? ClothingTags { get; set; }

        /// <summary>
        /// Sort option for the caption grid.
        /// </summary>
        public CaptionSortBy SortBy { get; set; } = CaptionSortBy.Default;

        /// <summary>
        /// Returns true if all filter properties are null/empty (no filters active).
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(SearchText)
                    && string.IsNullOrWhiteSpace(NegativeSearchText)
                    && string.IsNullOrWhiteSpace(AspectRatio)
                    && string.IsNullOrWhiteSpace(Nudity)
                    && MinArtisticQuality == null
                    && MinEroticScore == null
                    && MaxPornographicScore == null
                    && Males == null
                    && Females == null
                    && (ClothingTags == null || ClothingTags.Count == 0)
                    && SortBy == CaptionSortBy.Default;
            }
        }

        /// <summary>
        /// Creates a shallow copy of this filter.
        /// </summary>
        public CaptionFilter Clone()
        {
            return new CaptionFilter
            {
                SearchText = SearchText,
                NegativeSearchText = NegativeSearchText,
                AspectRatio = AspectRatio,
                Nudity = Nudity,
                MinArtisticQuality = MinArtisticQuality,
                MinEroticScore = MinEroticScore,
                MaxPornographicScore = MaxPornographicScore,
                Males = Males,
                Females = Females,
                ClothingTags = ClothingTags?.ToList(),
                SortBy = SortBy
            };
        }
    }
}
