using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow.sd
{
    public class Txt2ImgParameters : SharedParameters
    {
        [JsonPropertyName("checkpoint")]
        public string? Checkpoint { get; set; }
        [JsonPropertyName("vae")]
        public string? VAE { get; set; }
        [JsonPropertyName("upscale")]
        public UpscaleParameters? Upscale { get; set; }
        [JsonPropertyName("detailer")]
        public DetailerParameters? Detailer { get; set; }
    }

    public class UpscaleParameters
    {
        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        [JsonPropertyName("mult")]
        public double? Mult { get; set; }
        [JsonPropertyName("width")]
        public int? Width { get; set; }
        [JsonPropertyName("height")]
        public int? Height { get; set; }
        [JsonPropertyName("denoise")]
        public double? Denoise { get; set; }
        [JsonPropertyName("steps")]
        public int? Steps { get; set; }
    }

    public class DetailerParameters
    {
        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        [JsonPropertyName("sampler")]
        public string? Sampler { get; set; }
        [JsonPropertyName("scheduler")]
        public string? Scheduler { get; set; }
        [JsonPropertyName("checkpoint")]
        public string? Checkpoint { get; set; }
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }
        [JsonPropertyName("negative_prompt")]
        public string? NegativePrompt { get; set; }
        [JsonPropertyName("steps")]
        public int? Steps { get; set; }
        [JsonPropertyName("cfg_scale")]
        public double? CFGScale { get; set; }
        [JsonPropertyName("feather")]
        public int? Feather { get; set; }
        [JsonPropertyName("denoise")]
        public double? Denoise { get; set; }
        [JsonPropertyName("bbox_threshold")]
        public double? BBoxThreshold { get; set; }
        [JsonPropertyName("drop_size")]
        public int? DropSize { get; set; }

    }
}
