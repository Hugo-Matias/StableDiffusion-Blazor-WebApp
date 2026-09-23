using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    public class Prompt
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Positive { get; set; }
        public string? Negative { get; set; }
        public string? ImagePath { get; set; }
        public bool IsFavorite { get; set; }
        public List<Lora> Loras { get; set; } = [];

        // NEW: Organization properties
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }

        // NEW: Pinning & ordering
        public bool IsPinned { get; set; }
        public int SortOrder { get; set; }

        // NEW: Usage tracking
        public DateTime? LastUsedAt { get; set; }
        public int UsageCount { get; set; }

        public Prompt()
        {
            Title = string.Empty;
            Loras = [];
        }

        public Prompt(PromptResource prompt)
        {
            Id = prompt.Id;
            Title = prompt.Title;
            Positive = prompt.Positive;
            Negative = prompt.Negative;
            ImagePath = prompt.ImageSrc;
            IsFavorite = prompt.IsFavorite;
            Loras = prompt.Loras ?? [];
            Category = prompt.Category;
            Tags = prompt.Tags;
            IsPinned = prompt.IsPinned;
            SortOrder = prompt.SortOrder;
            LastUsedAt = prompt.LastUsedAt;
            UsageCount = prompt.UsageCount;
        }
    }
}
