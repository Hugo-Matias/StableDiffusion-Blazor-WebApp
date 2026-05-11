using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupScoreIndexingService : ICleanupScoreIndexingService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupRepository _repository;
        private readonly ICleanupScoringModelMetadataService _modelMetadata;
        private readonly IImageScoringService _imageScoringService;
        private readonly ILogger<CleanupScoreIndexingService> _logger;

        public CleanupScoreIndexingService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupRepository repository,
            ICleanupScoringModelMetadataService modelMetadata,
            IImageScoringService imageScoringService,
            ILogger<CleanupScoreIndexingService> logger)
        {
            _contextFactory = contextFactory;
            _repository = repository;
            _modelMetadata = modelMetadata;
            _imageScoringService = imageScoringService;
            _logger = logger;
        }

        public async Task<CleanupImageScore> IndexImageScoreAsync(int imageId, CancellationToken cancellationToken = default)
        {
            if (imageId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(imageId), "Image id must be greater than zero.");
            }

            var imagePath = await LoadImagePathAsync(imageId, cancellationToken);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new InvalidOperationException($"Image {imageId} could not be found for cleanup score indexing.");
            }

            var identity = await _modelMetadata.GetIdentityAsync(cancellationToken);
            try
            {
                var score = await _imageScoringService.ScoreImageAsync(imagePath, cancellationToken);
                return await _repository.UpsertImageScoreAsync(new CleanupImageScore
                {
                    ImageId = imageId,
                    ModelKey = identity.ModelKey,
                    ModelHash = identity.ModelHash,
                    ScoreName = identity.ScoreName,
                    RuntimeProvider = identity.RuntimeProvider,
                    Score = score,
                    MinScore = identity.MinScore,
                    MaxScore = identity.MaxScore,
                    Status = CleanupScoreStatus.Indexed,
                    IndexedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to index cleanup score for image {ImageId}", imageId);
                return await _repository.UpsertImageScoreAsync(new CleanupImageScore
                {
                    ImageId = imageId,
                    ModelKey = identity.ModelKey,
                    ModelHash = identity.ModelHash,
                    ScoreName = identity.ScoreName,
                    RuntimeProvider = identity.RuntimeProvider,
                    MinScore = identity.MinScore,
                    MaxScore = identity.MaxScore,
                    Status = CleanupScoreStatus.Error,
                    ErrorMessage = ex.Message,
                    IndexedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        private async Task<string?> LoadImagePathAsync(int imageId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Images
                .AsNoTracking()
                .Where(image => image.Id == imageId)
                .Select(image => image.Path)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}