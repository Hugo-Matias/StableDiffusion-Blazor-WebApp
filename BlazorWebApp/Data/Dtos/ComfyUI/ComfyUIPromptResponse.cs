using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.ComfyUI
{
    public class ComfyUIPromptResponse<TInput>
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("input")]
        public TInput Input { get; set; }

        [JsonPropertyName("images")]
        public List<string> Images { get; set; }

        [JsonPropertyName("filenames")]
        public List<string> Filenames { get; set; }

        [JsonPropertyName("prompt")]
        public JsonElement Prompt { get; set; }
    }
}
