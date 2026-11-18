using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupEmbeddingIndexingService : ICleanupEmbeddingIndexingService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupRepository _repository;
        private readonly ICleanupEmbeddingModelMetadataService _modelMetadata;
        private readonly IImageEmbeddingService _imageEmbeddingService;
        private readonly ICleanupEmbeddingVectorCodec _vectorCodec;
        private readonly ILogger<CleanupEmbeddingIndexingService> _logger;

        public CleanupEmbeddingIndexingService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupRepository repository,
            ICleanupEmbeddingModelMetadataService modelMetadata,
            IImageEmbeddingService imageEmbeddingService,
            ICleanupEmbeddingVectorCodec vectorCodec,
            ILogger<CleanupEmbeddingIndexingService> logger)
        {
            _contextFactory = contextFactory;
            _repository = repository;
            _modelMetadata = modelMetadata;
            _imageEmbeddingService = imageEmbeddingService;
            _vectorCodec = vectorCodec;
            _logger = logger;
        }

        public async Task<CleanupImageEmbedding> IndexImageEmbeddingAsync(
            int imageId,
            CancellationToken cancellationToken = default)
        {
            if (imageId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(imageId), "Image id must be greater than zero.");
            }

            var imagePath = await LoadImagePathAsync(imageId, cancellationToken);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new InvalidOperationException($"Image {imageId} could not be found for cleanup embedding indexing.");
            }

            var identity = await _modelMetadata.GetIdentityAsync(cancellationToken);
            try
            {
                var vector = await _imageEmbeddingService.GenerateEmbeddingAsync(imagePath, cancellationToken);
                if (vector.Length != identity.Dimensions)
                {
                    throw new InvalidOperationException($"Embedding dimension mismatch. Expected {identity.Dimensions}, got {vector.Length}.");
                }

                var normalizedVector = _vectorCodec.Normalize(vector);

                return await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
                {
                    ImageId = imageId,
                    ModelKey = identity.ModelKey,
                    ModelHash = identity.ModelHash,
                    RuntimeProvider = identity.RuntimeProvider,
                    Dimensions = identity.Dimensions,
                    Vector = _vectorCodec.Serialize(normalizedVector),
                    Status = CleanupEmbeddingStatus.Indexed,
                    IndexedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to index cleanup embedding for image {ImageId}", imageId);
                return await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
                {
                    ImageId = imageId,
                    ModelKey = identity.ModelKey,
                    ModelHash = identity.ModelHash,
                    RuntimeProvider = identity.RuntimeProvider,
                    Dimensions = identity.Dimensions,
                    Status = CleanupEmbeddingStatus.Error,
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