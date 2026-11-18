using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class RoutingImageEmbeddingService : IImageEmbeddingService
    {
        private readonly IOptions<CleanupEmbeddingOptions> _options;
        private readonly OnnxImageEmbeddingService _onnx;
        private readonly ComfyImageEmbeddingService _comfy;

        public RoutingImageEmbeddingService(
            IOptions<CleanupEmbeddingOptions> options,
            OnnxImageEmbeddingService onnx,
            ComfyImageEmbeddingService comfy)
        {
            _options = options;
            _onnx = onnx;
            _comfy = comfy;
        }

        public Task<float[]> GenerateEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            return _options.Value.RuntimeProvider == CleanupEmbeddingRuntimeProvider.ComfyUI
                ? _comfy.GenerateEmbeddingAsync(imagePath, cancellationToken)
                : _onnx.GenerateEmbeddingAsync(imagePath, cancellationToken);
        }
    }
}