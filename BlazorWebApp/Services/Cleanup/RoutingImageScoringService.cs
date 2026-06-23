using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class RoutingImageScoringService : IImageScoringService
    {
        private readonly IOptions<CleanupScoringOptions> _options;
        private readonly OnnxImageScoringService _onnx;
        private readonly ComfyImageScoringService _comfy;

        public RoutingImageScoringService(
            IOptions<CleanupScoringOptions> options,
            OnnxImageScoringService onnx,
            ComfyImageScoringService comfy)
        {
            _options = options;
            _onnx = onnx;
            _comfy = comfy;
        }

        public Task<double> ScoreImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            return _options.Value.RuntimeProvider == CleanupEmbeddingRuntimeProvider.ComfyUI
                ? _comfy.ScoreImageAsync(imagePath, cancellationToken)
                : _onnx.ScoreImageAsync(imagePath, cancellationToken);
        }
    }
}