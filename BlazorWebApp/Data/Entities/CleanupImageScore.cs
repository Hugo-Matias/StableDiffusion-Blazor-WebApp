namespace BlazorWebApp.Data.Entities
{
    public class CleanupImageScore
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public Image? Image { get; set; }
        public string ModelKey { get; set; } = string.Empty;
        public string? ModelHash { get; set; }
        public string ScoreName { get; set; } = string.Empty;
        public string? RuntimeProvider { get; set; }
        public double Score { get; set; }
        public double? MinScore { get; set; }
        public double? MaxScore { get; set; }
        public CleanupScoreStatus Status { get; set; } = CleanupScoreStatus.Pending;
        public string? ErrorMessage { get; set; }
        public DateTime? IndexedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}