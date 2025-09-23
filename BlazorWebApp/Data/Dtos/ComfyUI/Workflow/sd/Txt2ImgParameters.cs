using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow.sd
{
    public class Txt2ImgParameters : SharedParameters
    {
        [JsonPropertyName("checkpoint")]
        public string? Checkpoint { get; set; }
        [JsonPropertyName("vae")]
        public string? VAE { get; set; }
    }
}
