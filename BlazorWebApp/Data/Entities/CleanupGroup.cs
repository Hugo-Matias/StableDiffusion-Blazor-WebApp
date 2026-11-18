namespace BlazorWebApp.Data.Entities
{
    public class CleanupGroup
    {
        public int Id { get; set; }
        public int RunId { get; set; }
        public CleanupGroupRun? Run { get; set; }
        public string GroupKey { get; set; } = string.Empty;
        public CleanupGroupingStrategy Strategy { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int? RepresentativeImageId { get; set; }
        public Image? RepresentativeImage { get; set; }
        public int MemberCount { get; set; }
        public long? EstimatedBytes { get; set; }
        public double? MinSimilarity { get; set; }
        public double? MaxSimilarity { get; set; }
        public double? Confidence { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public List<CleanupGroupMember> Members { get; set; } = new();
    }
}