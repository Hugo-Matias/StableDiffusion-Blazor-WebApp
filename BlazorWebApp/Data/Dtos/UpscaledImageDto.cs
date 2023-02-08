using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class UpscaledImageDto
    {
        [JsonPropertyName("image")]
        public string Image { get; set; }
        [JsonPropertyName("html_info")]
        public string Info { get; set; }
    }
}
