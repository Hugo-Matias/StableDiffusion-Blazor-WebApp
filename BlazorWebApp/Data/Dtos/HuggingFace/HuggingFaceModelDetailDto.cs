using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    /// <summary>
    /// Full model detail returned by /api/models/{author}/{repo}.
    /// Extends summary with siblings (file tree), safe tensors, and readme content.
    /// </summary>
    public class HuggingFaceModelDetailDto : HuggingFaceModelSummaryDto
    {
        [JsonPropertyName("safetensors")]
        public HuggingFaceSafetensorsDto? Safetensors { get; set; }

        [JsonPropertyName("spaces")]
        public List<string>? Spaces { get; set; }

        /// <summary>Raw markdown text of README.md; populated separately if requested.</summary>
        [JsonIgnore]
        public string? ReadmeMarkdown { get; set; }
    }

    public class HuggingFaceSafetensorsDto
    {
        [JsonPropertyName("parameters")]
        public Dictionary<string, long>? Parameters { get; set; }

        [JsonPropertyName("total")]
        public long? Total { get; set; }
    }
}
