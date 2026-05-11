namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupIndexingService
    {
        Task<CleanupIndexingResult> IndexImagesAsync(
            CleanupIndexingOptions options,
            IProgress<CleanupIndexingProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}