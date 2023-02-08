using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class UpscalerModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("model_name")]
        public string ModelName { get; set; }
        [JsonPropertyName("model_path")]
        public string ModelPath { get; set; }
        [JsonPropertyName("model_url")]
        public string ModelUrl { get; set; }
        //[JsonPropertyName("scale")]
        //public int Scale { get; set; }
    }
}
