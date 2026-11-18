using System.Text.Json;
using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupComfyBatchIndexingService : ICleanupComfyBatchIndexingService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupRepository _repository;
        private readonly ICleanupEmbeddingModelMetadataService _embeddingMetadata;
        private readonly ICleanupScoringModelMetadataService _scoringMetadata;
        private readonly IOptions<CleanupEmbeddingOptions> _embeddingOptions;
        private readonly IOptions<CleanupScoringOptions> _scoringOptions;
        private readonly ICleanupEmbeddingVectorCodec _vectorCodec;
        private readonly IComfyUIService _comfy;
        private readonly ILogger<CleanupComfyBatchIndexingService> _logger;

        public CleanupComfyBatchIndexingService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupRepository repository,
            ICleanupEmbeddingModelMetadataService embeddingMetadata,
            ICleanupScoringModelMetadataService scoringMetadata,
            IOptions<CleanupEmbeddingOptions> embeddingOptions,
            IOptions<CleanupScoringOptions> scoringOptions,
            ICleanupEmbeddingVectorCodec vectorCodec,
            IComfyUIService comfy,
            ILogger<CleanupComfyBatchIndexingService> logger)
        {
            _contextFactory = contextFactory;
            _repository = repository;
            _embeddingMetadata = embeddingMetadata;
            _scoringMetadata = scoringMetadata;
            _embeddingOptions = embeddingOptions;
            _scoringOptions = scoringOptions;
            _vectorCodec = vectorCodec;
            _comfy = comfy;
            _logger = logger;
        }

        public async Task<CleanupComfyBatchIndexingResult> IndexBatchAsync(
            IReadOnlyCollection<int> embeddingImageIds,
            IReadOnlyCollection<int> scoreImageIds,
            CancellationToken cancellationToken = default)
        {
            var embeddingIds = embeddingImageIds.ToHashSet();
            var scoreIds = scoreImageIds.ToHashSet();
            var requestedIds = embeddingIds.Concat(scoreIds).Distinct().OrderBy(id => id).ToList();
            if (requestedIds.Count == 0)
            {
                return new CleanupComfyBatchIndexingResult();
            }

            var embeddingIdentity = embeddingIds.Count > 0
                ? await _embeddingMetadata.GetIdentityAsync(cancellationToken)
                : null;
            var scoreIdentity = scoreIds.Count > 0
                ? await _scoringMetadata.GetIdentityAsync(cancellationToken)
                : null;
            var imagePaths = await LoadImagePathsAsync(requestedIds, cancellationToken);
            var result = new MutableResult();
            var uploadedFilenames = new Dictionary<int, string>();
            var trackingId = Guid.NewGuid();

            foreach (var imageId in requestedIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!imagePaths.TryGetValue(imageId, out var imagePath) || string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    await PersistImageErrorAsync(imageId, embeddingIds, scoreIds, embeddingIdentity, scoreIdentity, "Image file was not found.", result, cancellationToken);
                    continue;
                }

                try
                {
                    await using var stream = File.OpenRead(imagePath);
                    uploadedFilenames[imageId] = await _comfy.UploadStreamAsync(
                        stream,
                        Path.GetFileName(imagePath),
                        ResolveMediaType(imagePath),
                        trackingId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to upload image {ImageId} to ComfyUI cleanup batch", imageId);
                    await PersistImageErrorAsync(imageId, embeddingIds, scoreIds, embeddingIdentity, scoreIdentity, ex.Message, result, cancellationToken);
                }
            }

            if (uploadedFilenames.Count == 0)
            {
                return result.ToImmutable();
            }

            var workflow = ComfyCleanupWorkflowFactory.CreateBatchPayload(
                uploadedFilenames,
                embeddingIds,
                scoreIds,
                _embeddingOptions.Value,
                _scoringOptions.Value);

            try
            {
                _logger.LogInformation(
                    "Submitting Comfy cleanup batch with {ImageCount} images, {EmbeddingCount} embedding outputs, {ScoreCount} score outputs",
                    uploadedFilenames.Count,
                    workflow.Outputs.Count(output => output.Value.Signal == ComfyCleanupBatchSignal.Embedding),
                    workflow.Outputs.Count(output => output.Value.Signal == ComfyCleanupBatchSignal.Score));

                var response = await _comfy.PostTextPromptOutputsAsync(workflow.Payload, "payload_cleanup_batch.json", trackingId);
                foreach (var (nodeId, output) in workflow.Outputs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!response.TextByNodeId.TryGetValue(nodeId, out var text))
                    {
                        await PersistOutputErrorAsync(output, embeddingIdentity, scoreIdentity, "ComfyUI did not return text for cleanup output node.", result, cancellationToken);
                        continue;
                    }

                    await PersistOutputAsync(output, text, embeddingIdentity, scoreIdentity, result, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to execute ComfyUI cleanup batch");
                foreach (var imageId in uploadedFilenames.Keys)
                {
                    await PersistImageErrorAsync(imageId, embeddingIds, scoreIds, embeddingIdentity, scoreIdentity, ex.Message, result, cancellationToken);
                }
            }

            return result.ToImmutable();
        }

        private async Task<Dictionary<int, string>> LoadImagePathsAsync(IReadOnlyCollection<int> imageIds, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Images
                .AsNoTracking()
                .Where(image => imageIds.Contains(image.Id))
                .Select(image => new { image.Id, image.Path })
                .ToDictionaryAsync(image => image.Id, image => image.Path, cancellationToken);
        }

        private async Task PersistOutputAsync(
            ComfyCleanupBatchOutput output,
            string text,
            CleanupEmbeddingModelIdentity? embeddingIdentity,
            CleanupScoreModelIdentity? scoreIdentity,
            MutableResult result,
            CancellationToken cancellationToken)
        {
            if (output.Signal == ComfyCleanupBatchSignal.Embedding)
            {
                await PersistEmbeddingAsync(output.ImageId, text, embeddingIdentity!, result, cancellationToken);
                return;
            }

            await PersistScoreAsync(output.ImageId, text, scoreIdentity!, result, cancellationToken);
        }

        private async Task PersistEmbeddingAsync(
            int imageId,
            string text,
            CleanupEmbeddingModelIdentity identity,
            MutableResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ComfyCleanupEmbeddingResponse>(text, JsonOptions)
                    ?? throw new InvalidOperationException("ComfyUI embedding response was empty.");
                if (parsed.Embedding.Count != identity.Dimensions || parsed.Dimensions != identity.Dimensions)
                {
                    throw new InvalidOperationException($"ComfyUI embedding dimension mismatch. Expected {identity.Dimensions}, got {parsed.Embedding.Count}.");
                }

                var normalized = _vectorCodec.Normalize(parsed.Embedding.ToArray());
                await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
                {
                    ImageId = imageId,
                    ModelKey = identity.ModelKey,
                    ModelHash = identity.ModelHash,
                    RuntimeProvider = identity.RuntimeProvider,
                    Dimensions = identity.Dimensions,
                    Vector = _vectorCodec.Serialize(normalized),
                    Status = CleanupEmbeddingStatus.Indexed,
                    IndexedAtUtc = DateTime.UtcNow
                }, cancellationToken);
                result.EmbeddingsIndexed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await PersistEmbeddingErrorAsync(imageId, identity, ex.Message, cancellationToken);
                result.EmbeddingsFailed++;
            }
        }

        private async Task PersistScoreAsync(
            int imageId,
            string text,
            CleanupScoreModelIdentity identity,
            MutableResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ComfyCleanupScoreResponse>(text, JsonOptions)
                    ?? throw new InvalidOperationException("ComfyUI score response was empty.");
                if (double.IsNaN(parsed.Score) || double.IsInfinity(parsed.Score))
                {
                    throw new InvalidOperationException("ComfyUI score response contained a non-finite score.");
                }

                await _repository.UpsertImageScoreAsync(new CleanupImageScore
                {
                    ImageId = imageId,
                    ModelKey = identity.ModelKey,
                    ModelHash = identity.ModelHash,
                    ScoreName = identity.ScoreName,
                    RuntimeProvider = identity.RuntimeProvider,
                    Score = Math.Clamp(parsed.Score, identity.MinScore, identity.MaxScore),
                    MinScore = identity.MinScore,
                    MaxScore = identity.MaxScore,
                    Status = CleanupScoreStatus.Indexed,
                    IndexedAtUtc = DateTime.UtcNow
                }, cancellationToken);
                result.ScoresIndexed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await PersistScoreErrorAsync(imageId, identity, ex.Message, cancellationToken);
                result.ScoresFailed++;
            }
        }

        private async Task PersistOutputErrorAsync(
            ComfyCleanupBatchOutput output,
            CleanupEmbeddingModelIdentity? embeddingIdentity,
            CleanupScoreModelIdentity? scoreIdentity,
            string message,
            MutableResult result,
            CancellationToken cancellationToken)
        {
            if (output.Signal == ComfyCleanupBatchSignal.Embedding)
            {
                await PersistEmbeddingErrorAsync(output.ImageId, embeddingIdentity!, message, cancellationToken);
                result.EmbeddingsFailed++;
                return;
            }

            await PersistScoreErrorAsync(output.ImageId, scoreIdentity!, message, cancellationToken);
            result.ScoresFailed++;
        }

        private async Task PersistImageErrorAsync(
            int imageId,
            IReadOnlySet<int> embeddingIds,
            IReadOnlySet<int> scoreIds,
            CleanupEmbeddingModelIdentity? embeddingIdentity,
            CleanupScoreModelIdentity? scoreIdentity,
            string message,
            MutableResult result,
            CancellationToken cancellationToken)
        {
            if (embeddingIds.Contains(imageId))
            {
                await PersistEmbeddingErrorAsync(imageId, embeddingIdentity!, message, cancellationToken);
                result.EmbeddingsFailed++;
            }

            if (scoreIds.Contains(imageId))
            {
                await PersistScoreErrorAsync(imageId, scoreIdentity!, message, cancellationToken);
                result.ScoresFailed++;
            }
        }

        private Task PersistEmbeddingErrorAsync(int imageId, CleanupEmbeddingModelIdentity identity, string message, CancellationToken cancellationToken)
        {
            return _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
            {
                ImageId = imageId,
                ModelKey = identity.ModelKey,
                ModelHash = identity.ModelHash,
                RuntimeProvider = identity.RuntimeProvider,
                Dimensions = identity.Dimensions,
                Status = CleanupEmbeddingStatus.Error,
                ErrorMessage = message,
                IndexedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        private Task PersistScoreErrorAsync(int imageId, CleanupScoreModelIdentity identity, string message, CancellationToken cancellationToken)
        {
            return _repository.UpsertImageScoreAsync(new CleanupImageScore
            {
                ImageId = imageId,
                ModelKey = identity.ModelKey,
                ModelHash = identity.ModelHash,
                ScoreName = identity.ScoreName,
                RuntimeProvider = identity.RuntimeProvider,
                MinScore = identity.MinScore,
                MaxScore = identity.MaxScore,
                Status = CleanupScoreStatus.Error,
                ErrorMessage = message,
                IndexedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        private static string ResolveMediaType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                _ => "image/png"
            };
        }

        private sealed class MutableResult
        {
            public int EmbeddingsIndexed { get; set; }
            public int EmbeddingsFailed { get; set; }
            public int ScoresIndexed { get; set; }
            public int ScoresFailed { get; set; }

            public CleanupComfyBatchIndexingResult ToImmutable()
            {
                return new CleanupComfyBatchIndexingResult
                {
                    EmbeddingsIndexed = EmbeddingsIndexed,
                    EmbeddingsFailed = EmbeddingsFailed,
                    ScoresIndexed = ScoresIndexed,
                    ScoresFailed = ScoresFailed
                };
            }
        }
    }
}