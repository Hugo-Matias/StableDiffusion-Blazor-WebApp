using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services.Cleanup
{
    public record CleanupGroupingOptions
    {
        public CleanupGroupingStrategy Strategy { get; init; }
        public string? Name { get; init; }
        public int? ProjectId { get; init; }
        public int MinimumGroupSize { get; init; } = 2;
        public int? MaxGroups { get; init; }
        public int PerceptualHashMaxDistance { get; init; } = 6;
        public double PromptFuzzyMinSimilarity { get; init; } = 0.8;
        public double VisualSimilarityMinSimilarity { get; init; } = 0.9;
    }

    public record CleanupGroupingResult
    {
        public int RunId { get; init; }
        public CleanupGroupRunStatus Status { get; init; }
        public int TotalGroups { get; init; }
        public int TotalMembers { get; init; }
        public string? ErrorMessage { get; init; }
    }
}