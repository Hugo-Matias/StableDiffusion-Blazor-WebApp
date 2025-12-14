using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models
{
    public class PromptResource : BaseResource
    {
        public int Id { get; set; }
        public string? Positive { get; set; }
        public string? Negative { get; set; }
        public bool IsFavorite { get; set; }
        public List<Lora> Loras { get; set; }
        
        // NEW: Organization properties
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        
        // NEW: Pinning & ordering
        public bool IsPinned { get; set; }
        public int SortOrder { get; set; }
        
        // NEW: Usage tracking
        public DateTime? LastUsedAt { get; set; }
        public int UsageCount { get; set; }

        public PromptResource() { }
        
        public PromptResource(Prompt entity)
        {
            Id = entity.Id;
            Title = entity.Title;
            ImageSrc = entity.ImagePath;
            Positive = entity.Positive;
            Negative = entity.Negative;
            IsFavorite = entity.IsFavorite;
            Loras = entity.Loras ?? [];
            Category = entity.Category;
            Tags = entity.Tags;
            IsPinned = entity.IsPinned;
            SortOrder = entity.SortOrder;
            LastUsedAt = entity.LastUsedAt;
            UsageCount = entity.UsageCount;
        }
    }
}
