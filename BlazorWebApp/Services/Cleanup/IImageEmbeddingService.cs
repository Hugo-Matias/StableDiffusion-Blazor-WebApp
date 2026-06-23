namespace BlazorWebApp.Services.Cleanup
{
    public interface IImageEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default);
    }
}