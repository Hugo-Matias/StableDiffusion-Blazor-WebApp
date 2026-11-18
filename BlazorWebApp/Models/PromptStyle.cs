using BlazorWebApp.Data.Entities;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class PromptStyle
    {
        public string Name { get; set; }
        public string Prompt { get; set; }
        [JsonPropertyName("negative_prompt")]
        public string NegativePrompt { get; set; }
        public List<Lora> Loras { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public bool IsFavorite { get; set; }

        public PromptStyle() { }
        public PromptStyle(Prompt prompt)
        {
            Name = prompt.Title;
            Prompt = prompt.Positive;
            NegativePrompt = prompt.Negative;
            Loras = prompt.Loras;
            Category = prompt.Category;
            Tags = prompt.Tags;
            IsFavorite = prompt.IsFavorite;
        }
    }
}
