using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow
{
    public class SharedParameters
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }
        [JsonPropertyName("negative_prompt")]
        public string? NegativePrompt { get; set; }
        [JsonPropertyName("width")]
        public int? Width { get; set; }
        [JsonPropertyName("height")]
        public int? Height { get; set; }
        [JsonPropertyName("seed")]
        public long? Seed { get; set; }
        [JsonPropertyName("steps")]
        public int? Steps { get; set; }
        [JsonPropertyName("cfg_scale")]
        public float? CfgScale { get; set; }
        [JsonPropertyName("sampler_name")]
        public string? SamplerName { get; set; }
        [JsonPropertyName("scheduler")]
        public string? Scheduler { get; set; }
        [JsonPropertyName("batch_size")]
        public int? BatchSize { get; set; }
    }
}
