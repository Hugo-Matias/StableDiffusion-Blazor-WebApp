namespace BlazorWebApp.Data.Entities
{
    public class CleanupGroupExplanation
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public CleanupGroup? Group { get; set; }
        public int? RepresentativeImageId { get; set; }
        public Image? RepresentativeImage { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}