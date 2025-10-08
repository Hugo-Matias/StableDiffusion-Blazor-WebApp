using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI
{
    public class ComfyUIHistoryImageResponse
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }
        [JsonPropertyName("subfolder")]
        public string Subfolder { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
