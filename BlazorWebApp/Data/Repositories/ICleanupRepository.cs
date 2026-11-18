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

    public record CleanupGroupReconciliationResult
    {
        public int RequestedGroups { get; init; }
        public int UpdatedGroups { get; init; }
        public int RemovedGroups { get; init; }
        public int UpdatedRuns { get; init; }
    }

    public record CleanupStorageSummary
    {
        public int IndexedImages { get; init; }
        public int MissingFiles { get; init; }
        public int ErrorImages { get; init; }
        public long IndexedBytes { get; init; }
        public int GroupRuns { get; init; }
        public int Groups { get; init; }
        public int GroupMembers { get; init; }
        public long GroupEstimatedBytes { get; init; }
    }

    public record CleanupMissingFileReportItem
    {
        public int ImageId { get; init; }
        public int ProjectId { get; init; }
        public string ImagePath { get; init; } = string.Empty;
        public long? FileSizeBytes { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    public record CleanupWorkflowStorageSummaryItem
    {
        public string WorkflowId { get; init; } = string.Empty;
        public int IndexedImages { get; init; }
        public int MissingFiles { get; init; }
        public long IndexedBytes { get; init; }
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
        Task<CleanupGroupReconciliationResult> ReconcileGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken cancellationToken = default);
        Task<CleanupStorageSummary> GetStorageSummaryAsync(int? projectId = null, int? runId = null, CancellationToken cancellationToken = default);
        Task<List<CleanupMissingFileReportItem>> GetMissingFileReportAsync(int? projectId, int skip, int take, CancellationToken cancellationToken = default);
        Task<List<CleanupWorkflowStorageSummaryItem>> GetWorkflowStorageSummaryAsync(int? projectId, int take, CancellationToken cancellationToken = default);
    }
}