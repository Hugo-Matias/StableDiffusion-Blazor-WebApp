namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupGroupingService
    {
        Task<CleanupGroupingResult> GenerateGroupsAsync(CleanupGroupingOptions options, CancellationToken cancellationToken = default);
    }
}