using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    /// <summary>
    /// Summary entry returned by the /api/models list endpoint.
    /// </summary>
    public class HuggingFaceModelSummaryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("modelId")]
        public string? ModelId { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTime? LastModified { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("gated")]
        public JsonElement? Gated { get; set; }

        [JsonPropertyName("disabled")]
        public bool Disabled { get; set; }

        [JsonPropertyName("pipeline_tag")]
        public string? PipelineTag { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("likes")]
        public int Likes { get; set; }

        [JsonPropertyName("trending_score")]
        public double? TrendingScore { get; set; }

        [JsonPropertyName("cardData")]
        public HuggingFaceCardDataDto? CardData { get; set; }

        [JsonPropertyName("siblings")]
        public List<HuggingFaceSiblingDto>? Siblings { get; set; }

        /// <summary>
        /// Inferred display name: last segment of <see cref="Id"/> when author prefix is present.
        /// </summary>
        [JsonIgnore]
        public string DisplayName => Id.Contains('/') ? Id[(Id.IndexOf('/') + 1)..] : Id;

        /// <summary>
        /// Inferred author: first segment of <see cref="Id"/>.
        /// </summary>
        [JsonIgnore]
        public string ResolvedAuthor => Author ?? (Id.Contains('/') ? Id[..Id.IndexOf('/')] : string.Empty);

        /// <summary>
        /// True when the repo is gated (requires approval). Handles bool or string from API.
        /// </summary>
        [JsonIgnore]
        public bool IsGated => Gated?.ValueKind is System.Text.Json.JsonValueKind.True
            || (Gated?.ValueKind is System.Text.Json.JsonValueKind.String
                && Gated.Value.GetString() is string g
                && !string.Equals(g, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(g, "manual", StringComparison.OrdinalIgnoreCase));
    }

    public class HuggingFaceCardDataDto
    {
        [JsonPropertyName("license")]
        public string? License { get; set; }

        [JsonPropertyName("base_model")]
        public JsonElement? BaseModel { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("pipeline_tag")]
        public string? PipelineTag { get; set; }

        /// <summary>
        /// Resolves base_model from card data as a string regardless of whether it
        /// is a single string or the first entry of a list.
        /// </summary>
        [JsonIgnore]
        public string? BaseModelString
        {
            get
            {
                if (BaseModel is null) return null;
                if (BaseModel.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    return BaseModel.Value.GetString();
                if (BaseModel.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var elem in BaseModel.Value.EnumerateArray())
                        return elem.GetString();
                }
                return null;
            }
        }
    }

    public class HuggingFaceSiblingDto
    {
        [JsonPropertyName("rfilename")]
        public string Filename { get; set; } = string.Empty;
    }
}
