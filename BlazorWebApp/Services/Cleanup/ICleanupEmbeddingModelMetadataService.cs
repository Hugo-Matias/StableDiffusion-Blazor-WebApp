namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupEmbeddingModelMetadataService
    {
        CleanupEmbeddingOptions Options { get; }

        CleanupEmbeddingModelValidationResult Validate(CleanupEmbeddingOptions options);

        Task<CleanupEmbeddingModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);
    }
}