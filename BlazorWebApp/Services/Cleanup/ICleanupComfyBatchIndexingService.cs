namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupComfyBatchIndexingService
    {
        Task<CleanupComfyBatchIndexingResult> IndexBatchAsync(
            IReadOnlyCollection<int> embeddingImageIds,
            IReadOnlyCollection<int> scoreImageIds,
            CancellationToken cancellationToken = default);
    }

    public record CleanupComfyBatchIndexingResult
    {
        public int EmbeddingsIndexed { get; init; }
        public int EmbeddingsFailed { get; init; }
        public int ScoresIndexed { get; init; }
        public int ScoresFailed { get; init; }
    }
}