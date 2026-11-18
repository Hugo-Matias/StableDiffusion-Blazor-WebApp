using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI
{
    public class ComfyUIPromptSubmitResponse
    {
        [JsonPropertyName("prompt_id")]
        public string PromptId { get; set; }
        [JsonPropertyName("number")]
        public int Number { get; set; }
    }
}
