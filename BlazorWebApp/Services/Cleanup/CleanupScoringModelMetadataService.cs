using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupScoringModelMetadataService : ICleanupScoringModelMetadataService
    {
        private readonly IOptions<CleanupScoringOptions> _options;

        public CleanupScoringModelMetadataService(IOptions<CleanupScoringOptions> options)
        {
            _options = options;
        }

        public CleanupScoringOptions Options => _options.Value;

        public CleanupScoringValidationResult Validate(CleanupScoringOptions options)
        {
            var errors = new List<string>();
            var model = options.Model;

            if (!options.Enabled)
            {
                return new CleanupScoringValidationResult();
            }

            if (string.IsNullOrWhiteSpace(model.ModelKey))
            {
                errors.Add("ModelKey is required when cleanup scoring is enabled.");
            }

            if (string.IsNullOrWhiteSpace(model.ScoreName))
            {
                errors.Add("ScoreName is required when cleanup scoring is enabled.");
            }

            if (string.IsNullOrWhiteSpace(model.ModelPath))
            {
                errors.Add("ModelPath is required when cleanup scoring is enabled.");
            }
            else if (!File.Exists(model.ModelPath))
            {
                errors.Add($"ModelPath does not exist: {model.ModelPath}");
            }

            if (string.IsNullOrWhiteSpace(model.InputName))
            {
                errors.Add("InputName is required when cleanup scoring is enabled.");
            }

            if (string.IsNullOrWhiteSpace(model.OutputName))
            {
                errors.Add("OutputName is required when cleanup scoring is enabled.");
            }

            if (model.InputWidth <= 0 || model.InputHeight <= 0)
            {
                errors.Add("InputWidth and InputHeight must be greater than zero.");
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

            if (model.MaxScore <= model.MinScore)
            {
                errors.Add("MaxScore must be greater than MinScore.");
            }

            if (options.CudaDeviceId < 0)
            {
                errors.Add("CudaDeviceId cannot be negative.");
            }

            return new CleanupScoringValidationResult { Errors = errors };
        }

        public async Task<CleanupScoreModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
        {
            var model = Options.Model;
            return new CleanupScoreModelIdentity
            {
                ModelKey = model.ModelKey,
                ModelHash = await ComputeModelHashAsync(model.ModelPath, cancellationToken),
                ScoreName = model.ScoreName,
                MinScore = model.MinScore,
                MaxScore = model.MaxScore,
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