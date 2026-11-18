namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupImageHashService
    {
        Task<string> ComputeExactHashAsync(string imagePath, CancellationToken cancellationToken = default);
        string ComputePerceptualHash(string imagePath);
    }
}