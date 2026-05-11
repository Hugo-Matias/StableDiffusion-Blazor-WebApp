using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupScoreIndexingService
    {
        Task<CleanupImageScore> IndexImageScoreAsync(
            int imageId,
            CancellationToken cancellationToken = default);
    }
}