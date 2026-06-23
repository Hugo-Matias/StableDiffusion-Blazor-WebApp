namespace BlazorWebApp.Data.Entities
{
    public class CleanupImageEmbedding
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public Image? Image { get; set; }
        public string ModelKey { get; set; } = string.Empty;
        public string? ModelHash { get; set; }
        public string? RuntimeProvider { get; set; }
        public int Dimensions { get; set; }
        public byte[] Vector { get; set; } = Array.Empty<byte>();
        public CleanupEmbeddingStatus Status { get; set; } = CleanupEmbeddingStatus.Pending;
        public string? ErrorMessage { get; set; }
        public DateTime? IndexedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}