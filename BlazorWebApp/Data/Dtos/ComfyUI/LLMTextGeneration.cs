using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI
{
    public class LLMRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public long Seed { get; set; } = -1;
        public string Instructions { get; set; } = "Generate a detailed prompt from \"{prompt}\"";
    }

    public class LLMResponse
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("prompt_id")]
        public string PromptId { get; set; } = string.Empty;
    }

    public class ComfyTextPromptResponse
    {
        [JsonPropertyName("prompt_id")]
        public string PromptId { get; set; } = string.Empty;

        [JsonPropertyName("text_by_node_id")]
        public Dictionary<string, string> TextByNodeId { get; set; } = new();
    }
}