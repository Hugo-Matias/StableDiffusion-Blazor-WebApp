using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupGroupExplanationService
    {
        Task<CleanupGroupExplanation?> GetCachedExplanationAsync(int groupId, CancellationToken cancellationToken = default);

        Task<CleanupGroupExplanation> GenerateExplanationAsync(
            int groupId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
    }
}