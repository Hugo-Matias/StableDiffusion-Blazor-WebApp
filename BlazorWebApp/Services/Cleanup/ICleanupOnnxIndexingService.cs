namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupOnnxIndexingService
    {
        Task<CleanupOnnxIndexingResult> IndexAsync(
            CleanupOnnxIndexingOptions options,
            IProgress<CleanupOnnxIndexingProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }

    public record CleanupOnnxIndexingOptions
    {
        public int? ProjectId { get; init; }
        public int BatchSize { get; init; } = 50;
        public bool IncludeEmbeddings { get; init; } = true;
        public bool IncludeScores { get; init; } = true;
        public bool Force { get; init; }
    }

    public record CleanupOnnxIndexingProgress
    {
        public int LastImageId { get; init; }
        public int TotalCandidates { get; init; }
        public int Processed { get; init; }
        public int EmbeddingsIndexed { get; init; }
        public int EmbeddingsSkipped { get; init; }
        public int EmbeddingsFailed { get; init; }
        public int ScoresIndexed { get; init; }
        public int ScoresSkipped { get; init; }
        public int ScoresFailed { get; init; }
    }

    public record CleanupOnnxIndexingResult : CleanupOnnxIndexingProgress
    {
        public bool EmbeddingsEnabled { get; init; }
        public bool ScoresEnabled { get; init; }
    }
}