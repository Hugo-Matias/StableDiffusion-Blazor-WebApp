namespace BlazorWebApp.Services.Cleanup
{
    public interface IImageScoringService
    {
        Task<double> ScoreImageAsync(string imagePath, CancellationToken cancellationToken = default);
    }
}