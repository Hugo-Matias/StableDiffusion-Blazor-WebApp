using System.ComponentModel.DataAnnotations;

namespace BlazorWebApp.Data.Entities
{
    public class WildcardEntry
    {
        public int Id { get; set; }

        public int CollectionId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        public float Weight { get; set; } = 1.0f;
        
        public int SortOrder { get; set; }

        public WildcardCollection Collection { get; set; } = null!;
    }
}
