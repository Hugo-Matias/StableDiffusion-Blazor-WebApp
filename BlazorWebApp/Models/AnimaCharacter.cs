using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Character record loaded from anima_characters.json (sourced from animadex.net).
    /// Note: tags is a space-delimited string from the API, parsed by CharacterBrowserService.
    /// </summary>
    public class AnimaCharacter
    {
        [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("copyright")] public string Copyright { get; set; } = string.Empty;
        [JsonPropertyName("copyright_name")] public string CopyrightName { get; set; } = string.Empty;
        [JsonPropertyName("trigger")] public string Trigger { get; set; } = string.Empty;
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
        [JsonPropertyName("thumb_url")] public string ThumbUrl { get; set; } = string.Empty;
        [JsonPropertyName("img_url")] public string ImgUrl { get; set; } = string.Empty;
        [JsonPropertyName("has_image")] public bool HasImage { get; set; }

        // Derived at load time by CharacterBrowserService
        [JsonIgnore] public string? HairColor { get; set; }
        [JsonIgnore] public string? HairLength { get; set; }
        [JsonIgnore] public string? EyeColor { get; set; }
        [JsonIgnore] public string? Gender { get; set; }
    }
}
