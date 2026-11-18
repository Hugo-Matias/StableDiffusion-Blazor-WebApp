using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class ArtistTag
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("postCount")]
        public int PostCount { get; set; }

        [JsonPropertyName("shard")]
        public string Shard { get; set; } = string.Empty;
    }
}
