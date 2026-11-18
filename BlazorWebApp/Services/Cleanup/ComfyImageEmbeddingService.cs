using System.Text.Json;
using BlazorWebApp.Services;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class ComfyImageEmbeddingService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IOptions<CleanupEmbeddingOptions> _options;
        private readonly IComfyUIService _comfy;

        public ComfyImageEmbeddingService(IOptions<CleanupEmbeddingOptions> options, IComfyUIService comfy)
        {
            _options = options;
            _comfy = comfy;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Cleanup image was not found for ComfyUI embedding indexing.", imagePath);
            }

            var options = _options.Value;
            if (options.Model.Dimensions < 16)
            {
                throw new InvalidOperationException("ComfyUI embedding dimensions must be at least 16.");
            }

            var trackingId = Guid.NewGuid();
            await using var stream = File.OpenRead(imagePath);
            var uploadedFilename = await _comfy.UploadStreamAsync(
                stream,
                Path.GetFileName(imagePath),
                ResolveMediaType(imagePath),
                trackingId);

            var payload = ComfyCleanupWorkflowFactory.CreateEmbeddingPayload(uploadedFilename, options);
            var response = await _comfy.PostTextPromptAsync(payload, "payload_cleanup_embedding.json", trackingId);

            cancellationToken.ThrowIfCancellationRequested();
            var parsed = JsonSerializer.Deserialize<ComfyCleanupEmbeddingResponse>(response.Text, JsonOptions)
                ?? throw new InvalidOperationException("ComfyUI embedding response was empty.");

            if (parsed.Embedding.Count == 0)
            {
                throw new InvalidOperationException("ComfyUI embedding response did not contain a vector.");
            }

            if (parsed.Dimensions != parsed.Embedding.Count)
            {
                throw new InvalidOperationException($"ComfyUI embedding dimension mismatch. Response declared {parsed.Dimensions}, vector has {parsed.Embedding.Count} values.");
            }

            if (parsed.Embedding.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            {
                throw new InvalidOperationException("ComfyUI embedding response contained a non-finite value.");
            }

            return parsed.Embedding.ToArray();
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