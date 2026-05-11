using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Data.Repositories
{
    public record CleanupIndexFilter
    {
        public int? ProjectId { get; init; }
        public CleanupIndexStatus? Status { get; init; }
        public string? PromptFingerprint { get; init; }
        public int Skip { get; init; }
        public int Take { get; init; } = 100;
    }

    public record CleanupEmbeddingFilter
    {
        public string? ModelKey { get; init; }
        public string? ModelHash { get; init; }
        public CleanupEmbeddingStatus? Status { get; init; }
        public int Skip { get; init; }
        public int Take { get; init; } = 100;
    }

    public record CleanupScoreFilter
    {
        public string? ModelKey { get; init; }
        public string? ModelHash { get; init; }
        public string? ScoreName { get; init; }
        public CleanupScoreStatus? Status { get; init; }
        public double? MaxScore { get; init; }
        public int Skip { get; init; }
        public int Take { get; init; } = 100;
    }

    public interface ICleanupRepository
    {
        Task<CleanupImageIndex> UpsertImageIndexAsync(CleanupImageIndex index, CancellationToken cancellationToken = default);
        Task<CleanupImageIndex?> GetImageIndexAsync(int imageId, CancellationToken cancellationToken = default);
        Task<List<CleanupImageIndex>> GetImageIndexesAsync(CleanupIndexFilter filter, CancellationToken cancellationToken = default);
        Task<List<CleanupImageIndex>> GetStaleImageIndexesAsync(DateTime staleBeforeUtc, int take, CancellationToken cancellationToken = default);
        Task<List<CleanupImageIndex>> GetMissingFileIndexesAsync(int skip, int take, CancellationToken cancellationToken = default);
        Task<CleanupImageEmbedding> UpsertImageEmbeddingAsync(CleanupImageEmbedding embedding, CancellationToken cancellationToken = default);
        Task<CleanupImageEmbedding?> GetImageEmbeddingAsync(int imageId, string modelKey, string? modelHash, CancellationToken cancellationToken = default);
        Task<List<CleanupImageEmbedding>> GetImageEmbeddingsAsync(CleanupEmbeddingFilter filter, CancellationToken cancellationToken = default);
        Task<List<CleanupImageEmbedding>> GetStaleImageEmbeddingsAsync(string modelKey, string? modelHash, int dimensions, int take, CancellationToken cancellationToken = default);
        Task<CleanupImageScore> UpsertImageScoreAsync(CleanupImageScore score, CancellationToken cancellationToken = default);
        Task<CleanupImageScore?> GetImageScoreAsync(int imageId, string modelKey, string? modelHash, string scoreName, CancellationToken cancellationToken = default);
        Task<List<CleanupImageScore>> GetImageScoresAsync(CleanupScoreFilter filter, CancellationToken cancellationToken = default);
        Task<List<CleanupImageScore>> GetStaleImageScoresAsync(string modelKey, string? modelHash, string scoreName, int take, CancellationToken cancellationToken = default);
        Task<CleanupGroupRun> CreateGroupRunAsync(CleanupGroupRun run, CancellationToken cancellationToken = default);
        Task<CleanupGroupRun?> GetGroupRunAsync(int runId, CancellationToken cancellationToken = default);
        Task<List<CleanupGroupRun>> GetGroupRunsAsync(int skip, int take, CancellationToken cancellationToken = default);
        Task AddGroupsAsync(int runId, IReadOnlyList<CleanupGroup> groups, CancellationToken cancellationToken = default);
        Task<List<CleanupGroup>> GetGroupsAsync(int runId, int skip, int take, CancellationToken cancellationToken = default);
        Task<List<CleanupGroupMember>> GetGroupMembersAsync(int groupId, int? take = null, CancellationToken cancellationToken = default);
        Task<CleanupGroupExplanation?> GetGroupExplanationAsync(int groupId, CancellationToken cancellationToken = default);
        Task<CleanupGroupExplanation> UpsertGroupExplanationAsync(CleanupGroupExplanation explanation, CancellationToken cancellationToken = default);
    }
}