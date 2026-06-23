namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupIndexingService
    {
        Task<CleanupIndexingResult> IndexImageAsync(
            int imageId,
            bool force = false,
            CancellationToken cancellationToken = default);

        Task<CleanupIndexingResult> IndexImagesAsync(
            CleanupIndexingOptions options,
            IProgress<CleanupIndexingProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}