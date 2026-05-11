using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupEmbeddingModelMetadataService : ICleanupEmbeddingModelMetadataService
    {
        private readonly IOptions<CleanupEmbeddingOptions> _options;

        public CleanupEmbeddingModelMetadataService(IOptions<CleanupEmbeddingOptions> options)
        {
            _options = options;
        }

        public CleanupEmbeddingOptions Options => _options.Value;

        public CleanupEmbeddingModelValidationResult Validate(CleanupEmbeddingOptions options)
        {
            var errors = new List<string>();
            var model = options.Model;

            if (!options.Enabled)
            {
                return new CleanupEmbeddingModelValidationResult();
            }

            if (string.IsNullOrWhiteSpace(model.ModelKey))
            {
                errors.Add("ModelKey is required when cleanup embeddings are enabled.");
            }

            if (string.IsNullOrWhiteSpace(model.ModelPath))
            {
                errors.Add("ModelPath is required when cleanup embeddings are enabled.");
            }
            else if (!File.Exists(model.ModelPath))
            {
                errors.Add($"ModelPath does not exist: {model.ModelPath}");
            }

            if (string.IsNullOrWhiteSpace(model.InputName))
            {
                errors.Add("InputName is required when cleanup embeddings are enabled.");
            }

            if (string.IsNullOrWhiteSpace(model.OutputName))
            {
                errors.Add("OutputName is required when cleanup embeddings are enabled.");
            }

            if (model.InputWidth <= 0 || model.InputHeight <= 0)
            {
                errors.Add("InputWidth and InputHeight must be greater than zero.");
            }

            if (model.Dimensions <= 0)
            {
                errors.Add("Dimensions must be greater than zero.");
            }

            if (!string.Equals(model.InputLayout, "NCHW", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(model.InputLayout, "NHWC", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("InputLayout must be NCHW or NHWC.");
            }

            if (model.Mean.Count != 3 || model.StandardDeviation.Count != 3)
            {
                errors.Add("Mean and StandardDeviation must each contain three channel values.");
            }

            if (model.StandardDeviation.Any(value => value == 0))
            {
                errors.Add("StandardDeviation values must be non-zero.");
            }

            return new CleanupEmbeddingModelValidationResult { Errors = errors };
        }

        public async Task<CleanupEmbeddingModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
        {
            var model = Options.Model;
            return new CleanupEmbeddingModelIdentity
            {
                ModelKey = model.ModelKey,
                ModelHash = await ComputeModelHashAsync(model.ModelPath, cancellationToken),
                Dimensions = model.Dimensions,
                RuntimeProvider = Options.RuntimeProvider.ToString()
            };
        }

        private static async Task<string?> ComputeModelHashAsync(string modelPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(modelPath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}