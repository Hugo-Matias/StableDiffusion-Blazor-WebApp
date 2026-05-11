namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupScoringModelMetadataService
    {
        CleanupScoringOptions Options { get; }

        CleanupScoringValidationResult Validate(CleanupScoringOptions options);

        Task<CleanupScoreModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);
    }
}