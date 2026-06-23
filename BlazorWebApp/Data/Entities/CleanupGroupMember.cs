namespace BlazorWebApp.Data.Entities
{
    public class CleanupGroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public CleanupGroup? Group { get; set; }
        public int ImageId { get; set; }
        public Image? Image { get; set; }
        public CleanupGroupMemberRole Role { get; set; } = CleanupGroupMemberRole.Member;
        public CleanupSuggestedAction SuggestedAction { get; set; } = CleanupSuggestedAction.Review;
        public double? SimilarityScore { get; set; }
        public double? Distance { get; set; }
        public long? EstimatedBytes { get; set; }
        public int SortOrder { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}