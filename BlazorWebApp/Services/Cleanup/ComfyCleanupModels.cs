using System.Text.Json.Serialization;

namespace BlazorWebApp.Services.Cleanup
{
    public record ComfyCleanupEmbeddingResponse
    {
        [JsonPropertyName("modelKey")]
        public string ModelKey { get; init; } = string.Empty;

        [JsonPropertyName("dimensions")]
        public int Dimensions { get; init; }

        [JsonPropertyName("embedding")]
        public List<float> Embedding { get; init; } = new();
    }

    public record ComfyCleanupScoreResponse
    {
        [JsonPropertyName("modelKey")]
        public string ModelKey { get; init; } = string.Empty;

        [JsonPropertyName("scoreName")]
        public string ScoreName { get; init; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; init; }

        [JsonPropertyName("minScore")]
        public double MinScore { get; init; }

        [JsonPropertyName("maxScore")]
        public double MaxScore { get; init; }
    }
}