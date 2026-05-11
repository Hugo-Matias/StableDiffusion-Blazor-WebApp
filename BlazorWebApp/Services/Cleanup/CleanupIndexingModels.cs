namespace BlazorWebApp.Services.Cleanup
{
    public record CleanupIndexingOptions
    {
        public int? ProjectId { get; init; }
        public int BatchSize { get; init; } = 100;
        public int? MaxImages { get; init; }
        public int StartAfterImageId { get; init; }
        public bool IncludeHidden { get; init; }
        public bool Force { get; init; }
    }

    public record CleanupIndexingProgress
    {
        public int LastImageId { get; init; }
        public int TotalCandidates { get; init; }
        public int Indexed { get; init; }
        public int Skipped { get; init; }
        public int MissingFiles { get; init; }
        public int Failed { get; init; }
    }

    public record CleanupIndexingResult
    {
        public int? LastImageId { get; init; }
        public int TotalCandidates { get; init; }
        public int Indexed { get; init; }
        public int Skipped { get; init; }
        public int MissingFiles { get; init; }
        public int Failed { get; init; }
    }

    public record CleanupPromptIndex
    {
        public string? NormalizedPrompt { get; init; }
        public string? Fingerprint { get; init; }
        public string? TokenSignature { get; init; }
    }
}