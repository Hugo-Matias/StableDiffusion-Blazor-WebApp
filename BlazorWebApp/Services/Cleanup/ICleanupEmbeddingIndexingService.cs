using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupEmbeddingIndexingService
    {
        Task<CleanupImageEmbedding> IndexImageEmbeddingAsync(
            int imageId,
            CancellationToken cancellationToken = default);
    }
}