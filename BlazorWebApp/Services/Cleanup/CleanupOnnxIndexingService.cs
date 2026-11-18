using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupOnnxIndexingService : ICleanupOnnxIndexingService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupEmbeddingModelMetadataService _embeddingMetadata;
        private readonly ICleanupScoringModelMetadataService _scoringMetadata;
        private readonly ICleanupEmbeddingIndexingService _embeddingIndexing;
        private readonly ICleanupScoreIndexingService _scoreIndexing;
        private readonly ICleanupComfyBatchIndexingService _comfyBatchIndexing;
        private readonly ILogger<CleanupOnnxIndexingService> _logger;

        public CleanupOnnxIndexingService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupEmbeddingModelMetadataService embeddingMetadata,
            ICleanupScoringModelMetadataService scoringMetadata,
            ICleanupEmbeddingIndexingService embeddingIndexing,
            ICleanupScoreIndexingService scoreIndexing,
            ICleanupComfyBatchIndexingService comfyBatchIndexing,
            ILogger<CleanupOnnxIndexingService> logger)
        {
            _contextFactory = contextFactory;
            _embeddingMetadata = embeddingMetadata;
            _scoringMetadata = scoringMetadata;
            _embeddingIndexing = embeddingIndexing;
            _scoreIndexing = scoreIndexing;
            _comfyBatchIndexing = comfyBatchIndexing;
            _logger = logger;
        }

        public async Task<CleanupOnnxIndexingResult> IndexAsync(
            CleanupOnnxIndexingOptions options,
            IProgress<CleanupOnnxIndexingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var runEmbeddings = options.IncludeEmbeddings && _embeddingMetadata.Options.Enabled;
            var runScores = options.IncludeScores && _scoringMetadata.Options.Enabled;
            if (!runEmbeddings && !runScores)
            {
                throw new InvalidOperationException("Enable Cleanup:Embeddings or Cleanup:Scoring before running ONNX cleanup indexing.");
            }

            CleanupEmbeddingModelIdentity? embeddingIdentity = null;
            CleanupScoreModelIdentity? scoreIdentity = null;
            if (runEmbeddings)
            {
                ValidateEmbeddings();
                embeddingIdentity = await _embeddingMetadata.GetIdentityAsync(cancellationToken);
            }

            if (runScores)
            {
                ValidateScoring();
                scoreIdentity = await _scoringMetadata.GetIdentityAsync(cancellationToken);
            }

            var batchSize = Math.Clamp(options.BatchSize, 1, 250);
            var lastImageId = 0;
            var result = new CleanupOnnxIndexingResult
            {
                EmbeddingsEnabled = runEmbeddings,
                ScoresEnabled = runScores,
                TotalCandidates = await CountCandidatesAsync(options.ProjectId, cancellationToken)
            };

            progress?.Report(result);
            while (result.Processed < result.TotalCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imageIds = await LoadCandidateImageIdsAsync(options.ProjectId, lastImageId, batchSize, cancellationToken);
                if (imageIds.Count == 0)
                {
                    break;
                }

                lastImageId = imageIds[^1];
                var indexedEmbeddings = runEmbeddings && !options.Force
                    ? await LoadIndexedEmbeddingIdsAsync(imageIds, embeddingIdentity!, cancellationToken)
                    : new HashSet<int>();
                var indexedScores = runScores && !options.Force
                    ? await LoadIndexedScoreIdsAsync(imageIds, scoreIdentity!, cancellationToken)
                    : new HashSet<int>();

                var useComfyEmbeddings = runEmbeddings && _embeddingMetadata.Options.RuntimeProvider == CleanupEmbeddingRuntimeProvider.ComfyUI;
                var useComfyScores = runScores && _scoringMetadata.Options.RuntimeProvider == CleanupEmbeddingRuntimeProvider.ComfyUI;
                var comfyEmbeddingIds = useComfyEmbeddings
                    ? imageIds.Where(imageId => !indexedEmbeddings.Contains(imageId)).ToList()
                    : new List<int>();
                var comfyScoreIds = useComfyScores
                    ? imageIds.Where(imageId => !indexedScores.Contains(imageId)).ToList()
                    : new List<int>();

                CleanupComfyBatchIndexingResult? comfyBatchResult = null;
                if (comfyEmbeddingIds.Count > 0 || comfyScoreIds.Count > 0)
                {
                    comfyBatchResult = await _comfyBatchIndexing.IndexBatchAsync(comfyEmbeddingIds, comfyScoreIds, cancellationToken);
                }

                foreach (var imageId in imageIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result = result with
                    {
                        LastImageId = imageId,
                        Processed = result.Processed + 1
                    };

                    if (runEmbeddings && !useComfyEmbeddings)
                    {
                        result = await IndexEmbeddingAsync(imageId, indexedEmbeddings, result, cancellationToken);
                    }
                    else if (runEmbeddings && indexedEmbeddings.Contains(imageId))
                    {
                        result = result with { EmbeddingsSkipped = result.EmbeddingsSkipped + 1 };
                    }

                    if (runScores && !useComfyScores)
                    {
                        result = await IndexScoreAsync(imageId, indexedScores, result, cancellationToken);
                    }
                    else if (runScores && indexedScores.Contains(imageId))
                    {
                        result = result with { ScoresSkipped = result.ScoresSkipped + 1 };
                    }
                }

                if (comfyBatchResult != null)
                {
                    result = result with
                    {
                        EmbeddingsIndexed = result.EmbeddingsIndexed + comfyBatchResult.EmbeddingsIndexed,
                        EmbeddingsFailed = result.EmbeddingsFailed + comfyBatchResult.EmbeddingsFailed,
                        ScoresIndexed = result.ScoresIndexed + comfyBatchResult.ScoresIndexed,
                        ScoresFailed = result.ScoresFailed + comfyBatchResult.ScoresFailed
                    };
                }

                progress?.Report(result);
            }

            return result;
        }

        private void ValidateEmbeddings()
        {
            var validation = _embeddingMetadata.Validate(_embeddingMetadata.Options);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Cleanup embedding configuration is invalid: " + string.Join(" ", validation.Errors));
            }
        }

        private void ValidateScoring()
        {
            var validation = _scoringMetadata.Validate(_scoringMetadata.Options);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Cleanup scoring configuration is invalid: " + string.Join(" ", validation.Errors));
            }
        }

        private async Task<CleanupOnnxIndexingResult> IndexEmbeddingAsync(
            int imageId,
            IReadOnlySet<int> indexedEmbeddings,
            CleanupOnnxIndexingResult result,
            CancellationToken cancellationToken)
        {
            if (indexedEmbeddings.Contains(imageId))
            {
                return result with { EmbeddingsSkipped = result.EmbeddingsSkipped + 1 };
            }

            try
            {
                var embedding = await _embeddingIndexing.IndexImageEmbeddingAsync(imageId, cancellationToken);
                return embedding.Status == CleanupEmbeddingStatus.Indexed
                    ? result with { EmbeddingsIndexed = result.EmbeddingsIndexed + 1 }
                    : result with { EmbeddingsFailed = result.EmbeddingsFailed + 1 };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to bulk-index cleanup embedding for image {ImageId}", imageId);
                return result with { EmbeddingsFailed = result.EmbeddingsFailed + 1 };
            }
        }

        private async Task<CleanupOnnxIndexingResult> IndexScoreAsync(
            int imageId,
            IReadOnlySet<int> indexedScores,
            CleanupOnnxIndexingResult result,
            CancellationToken cancellationToken)
        {
            if (indexedScores.Contains(imageId))
            {
                return result with { ScoresSkipped = result.ScoresSkipped + 1 };
            }

            try
            {
                var score = await _scoreIndexing.IndexImageScoreAsync(imageId, cancellationToken);
                return score.Status == CleanupScoreStatus.Indexed
                    ? result with { ScoresIndexed = result.ScoresIndexed + 1 }
                    : result with { ScoresFailed = result.ScoresFailed + 1 };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to bulk-index cleanup score for image {ImageId}", imageId);
                return result with { ScoresFailed = result.ScoresFailed + 1 };
            }
        }

        private async Task<int> CountCandidatesAsync(int? projectId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await BuildCandidateQuery(context, projectId, 0).CountAsync(cancellationToken);
        }

        private async Task<List<int>> LoadCandidateImageIdsAsync(
            int? projectId,
            int lastImageId,
            int take,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await BuildCandidateQuery(context, projectId, lastImageId)
                .OrderBy(index => index.ImageId)
                .Take(take)
                .Select(index => index.ImageId)
                .ToListAsync(cancellationToken);
        }

        private async Task<HashSet<int>> LoadIndexedEmbeddingIdsAsync(
            IReadOnlyCollection<int> imageIds,
            CleanupEmbeddingModelIdentity identity,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var indexedImageIds = await context.CleanupImageEmbeddings
                .AsNoTracking()
                .Where(embedding => imageIds.Contains(embedding.ImageId)
                    && embedding.Status == CleanupEmbeddingStatus.Indexed
                    && embedding.ModelKey == identity.ModelKey
                    && embedding.ModelHash == identity.ModelHash
                    && embedding.Dimensions == identity.Dimensions)
                .Select(embedding => embedding.ImageId)
                .ToListAsync(cancellationToken);

            return indexedImageIds.ToHashSet();
        }

        private async Task<HashSet<int>> LoadIndexedScoreIdsAsync(
            IReadOnlyCollection<int> imageIds,
            CleanupScoreModelIdentity identity,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var indexedImageIds = await context.CleanupImageScores
                .AsNoTracking()
                .Where(score => imageIds.Contains(score.ImageId)
                    && score.Status == CleanupScoreStatus.Indexed
                    && score.ModelKey == identity.ModelKey
                    && score.ModelHash == identity.ModelHash
                    && score.ScoreName == identity.ScoreName)
                .Select(score => score.ImageId)
                .ToListAsync(cancellationToken);

            return indexedImageIds.ToHashSet();
        }

        private static IQueryable<CleanupImageIndex> BuildCandidateQuery(
            AppDbContext context,
            int? projectId,
            int lastImageId)
        {
            var query = context.CleanupImageIndexes
                .AsNoTracking()
                .Where(index => index.ImageId > lastImageId
                    && index.Status == CleanupIndexStatus.Indexed
                    && index.FileExists);

            if (projectId.HasValue)
            {
                query = query.Where(index => index.ProjectId == projectId.Value);
            }

            return query;
        }
    }
}