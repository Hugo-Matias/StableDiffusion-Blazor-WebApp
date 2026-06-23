namespace BlazorWebApp.Data.Dtos
{
    public class WildcardCollectionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int UsageCount { get; set; }
        public int EntryCount { get; set; }
        public List<WildcardEntryDto>? Entries { get; set; }
    }
}
