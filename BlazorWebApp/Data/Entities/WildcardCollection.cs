using System.ComponentModel.DataAnnotations;

namespace BlazorWebApp.Data.Entities
{
    public class WildcardCollection
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int UsageCount { get; set; }

        public ICollection<WildcardEntry> Entries { get; set; } = new List<WildcardEntry>();
    }
}
