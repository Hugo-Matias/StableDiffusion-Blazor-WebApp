using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    /// <summary>
    /// Response from the quicksearch endpoint
    /// GET /api/quicksearch?q={query}
    /// Contains separate buckets for models, datasets, and spaces.
    /// </summary>
    public class HuggingFaceQuickSearchDto
    {
        [JsonPropertyName("models")]
        public List<HuggingFaceQuickSearchResultDto>? Models { get; set; }

        [JsonPropertyName("datasets")]
        public List<HuggingFaceQuickSearchResultDto>? Datasets { get; set; }

        [JsonPropertyName("spaces")]
        public List<HuggingFaceQuickSearchResultDto>? Spaces { get; set; }
    }

    public class HuggingFaceQuickSearchResultDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("pipeline_tag")]
        public string? PipelineTag { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("gated")]
        public JsonElement? Gated { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("likes")]
        public int Likes { get; set; }

        [JsonPropertyName("trending_score")]
        public double? TrendingScore { get; set; }

        /// <summary>Inferred display name: last segment of <see cref="Id"/>.</summary>
        [JsonIgnore]
        public string DisplayName => Id.Contains('/') ? Id[(Id.IndexOf('/') + 1)..] : Id;

        /// <summary>Inferred author: first segment of <see cref="Id"/>.</summary>
        [JsonIgnore]
        public string ResolvedAuthor => Author ?? (Id.Contains('/') ? Id[..Id.IndexOf('/')] : string.Empty);
    }
}
