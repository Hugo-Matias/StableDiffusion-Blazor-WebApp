namespace BlazorWebApp.Data.Entities
{
    public class CleanupGroupRun
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CleanupGroupingStrategy Strategy { get; set; }
        public CleanupGroupRunStatus Status { get; set; } = CleanupGroupRunStatus.Pending;
        public int? ProjectId { get; set; }
        public string? ScopeKey { get; set; }
        public string? ConfigurationJson { get; set; }
        public string? SummaryJson { get; set; }
        public int TotalGroups { get; set; }
        public int TotalMembers { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public List<CleanupGroup> Groups { get; set; } = new();
    }
}