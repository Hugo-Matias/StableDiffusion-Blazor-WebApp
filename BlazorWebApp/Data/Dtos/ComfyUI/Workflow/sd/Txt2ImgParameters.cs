using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow.sd
{
    public class Txt2ImgParameters : SharedParameters
    {
        [JsonPropertyName("checkpoint")]
        public string? Checkpoint { get; set; }
        [JsonPropertyName("vae")]
        public string? VAE { get; set; }
        [JsonPropertyName("isUpscale")]
        public bool? IsUpscale { get; set; }
        [JsonPropertyName("upscale_model")]
        public string? UpscaleModel { get; set; }
        [JsonPropertyName("upscale_mult")]
        public double? UpscaleMult { get; set; }
        [JsonPropertyName("upscale_width")]
        public int? UpscaleWidth { get; set; }
        [JsonPropertyName("upscale_height")]
        public int? UpscaleHeight { get; set; }
        [JsonPropertyName("upscale_denoise")]
        public double? UpscaleDenoise { get; set; }
        [JsonPropertyName("upscale_steps")]
        public int? UpscaleSteps { get; set; }
        [JsonPropertyName("isDetailer")]
        public bool? IsDetailer { get; set; }
        [JsonPropertyName("detailer_model")]
        public string? DetailerModel { get; set; }
        [JsonPropertyName("detailer_sampler")]
        public string? DetailerSampler { get; set; }
        [JsonPropertyName("detailer_scheduler")]
        public string? DetailerScheduler { get; set; }
    }
}
