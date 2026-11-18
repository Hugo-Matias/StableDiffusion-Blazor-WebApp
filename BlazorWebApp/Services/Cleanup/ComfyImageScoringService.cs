using System.Text.Json;
using BlazorWebApp.Services;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class ComfyImageScoringService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IOptions<CleanupScoringOptions> _options;
        private readonly IComfyUIService _comfy;

        public ComfyImageScoringService(IOptions<CleanupScoringOptions> options, IComfyUIService comfy)
        {
            _options = options;
            _comfy = comfy;
        }

        public async Task<double> ScoreImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Cleanup image was not found for ComfyUI score indexing.", imagePath);
            }

            var options = _options.Value;
            var trackingId = Guid.NewGuid();
            await using var stream = File.OpenRead(imagePath);
            var uploadedFilename = await _comfy.UploadStreamAsync(
                stream,
                Path.GetFileName(imagePath),
                ResolveMediaType(imagePath),
                trackingId);

            var payload = ComfyCleanupWorkflowFactory.CreateScorePayload(uploadedFilename, options);
            var response = await _comfy.PostTextPromptAsync(payload, "payload_cleanup_score.json", trackingId);

            cancellationToken.ThrowIfCancellationRequested();
            var parsed = JsonSerializer.Deserialize<ComfyCleanupScoreResponse>(response.Text, JsonOptions)
                ?? throw new InvalidOperationException("ComfyUI score response was empty.");

            if (double.IsNaN(parsed.Score) || double.IsInfinity(parsed.Score))
            {
                throw new InvalidOperationException("ComfyUI score response contained a non-finite score.");
            }

            return Math.Clamp(parsed.Score, options.Model.MinScore, options.Model.MaxScore);
        }

        private static string ResolveMediaType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                _ => "image/png"
            };
        }
    }
}