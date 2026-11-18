namespace BlazorWebApp.Data.Entities
{
    public class CleanupImageIndex
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public Image? Image { get; set; }
        public int ProjectId { get; set; }
        public int ModeId { get; set; }
        public int? ResourceId { get; set; }
        public string? WorkflowId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public bool FileExists { get; set; }
        public long? FileSizeBytes { get; set; }
        public DateTime? FileLastWriteUtc { get; set; }
        public string? ExactHash { get; set; }
        public string? PerceptualHash { get; set; }
        public string? PromptNormalized { get; set; }
        public string? PromptFingerprint { get; set; }
        public string? PromptTokenSignature { get; set; }
        public CleanupIndexStatus Status { get; set; } = CleanupIndexStatus.Pending;
        public string? ErrorMessage { get; set; }
        public int IndexVersion { get; set; } = 1;
        public DateTime? MetadataIndexedAtUtc { get; set; }
        public DateTime? HashIndexedAtUtc { get; set; }
        public DateTime? IndexedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}