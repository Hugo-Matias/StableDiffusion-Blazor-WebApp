using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BlazorWebApp.Services.Cleanup
{
    public class OnnxImageScoringService : IImageScoringService, IDisposable
    {
        private readonly IOptions<CleanupScoringOptions> _options;
        private readonly ICleanupScoringModelMetadataService _modelMetadata;
        private readonly ICleanupEmbeddingImagePreprocessor _preprocessor;
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private InferenceSession? _session;
        private CleanupScoreModelIdentity? _sessionIdentity;

        public OnnxImageScoringService(
            IOptions<CleanupScoringOptions> options,
            ICleanupScoringModelMetadataService modelMetadata,
            ICleanupEmbeddingImagePreprocessor preprocessor)
        {
            _options = options;
            _modelMetadata = modelMetadata;
            _preprocessor = preprocessor;
        }

        public async Task<double> ScoreImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            var options = _options.Value;
            var validation = _modelMetadata.Validate(options);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Cleanup scoring configuration is invalid: " + string.Join(" ", validation.Errors));
            }

            var session = await GetSessionAsync(cancellationToken);
            var model = ToEmbeddingModelOptions(options.Model);
            var tensorData = _preprocessor.Preprocess(imagePath, model);
            var tensor = new DenseTensor<float>(tensorData.Values, tensorData.Dimensions);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(options.Model.InputName, tensor)
            };

            using var results = session.Run(inputs);
            var output = results.FirstOrDefault(result => result.Name == options.Model.OutputName) ?? results.FirstOrDefault();
            if (output == null)
            {
                throw new InvalidOperationException("ONNX scoring model returned no outputs.");
            }

            var values = output.AsTensor<float>().ToArray();
            if (values.Length == 0)
            {
                throw new InvalidOperationException("ONNX scoring model returned an empty output tensor.");
            }

            var score = values[0];
            if (float.IsNaN(score) || float.IsInfinity(score))
            {
                throw new InvalidOperationException("ONNX scoring model returned a non-finite score.");
            }

            return Math.Clamp(score, (float)options.Model.MinScore, (float)options.Model.MaxScore);
        }

        public void Dispose()
        {
            _session?.Dispose();
            _sessionLock.Dispose();
        }

        private async Task<InferenceSession> GetSessionAsync(CancellationToken cancellationToken)
        {
            var identity = await _modelMetadata.GetIdentityAsync(cancellationToken);
            if (_session != null && IsSameIdentity(identity, _sessionIdentity))
            {
                return _session;
            }

            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                if (_session != null && IsSameIdentity(identity, _sessionIdentity))
                {
                    return _session;
                }

                _session?.Dispose();
                using var sessionOptions = CleanupEmbeddingRuntime.CreateSessionOptions(ToEmbeddingOptions(_options.Value));
                _session = new InferenceSession(_options.Value.Model.ModelPath, sessionOptions);
                _sessionIdentity = identity;
                return _session;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private static bool IsSameIdentity(CleanupScoreModelIdentity left, CleanupScoreModelIdentity? right)
        {
            return right != null
                && left.ModelKey == right.ModelKey
                && left.ModelHash == right.ModelHash
                && left.ScoreName == right.ScoreName
                && left.RuntimeProvider == right.RuntimeProvider;
        }

        private static CleanupEmbeddingOptions ToEmbeddingOptions(CleanupScoringOptions options)
        {
            return new CleanupEmbeddingOptions
            {
                Enabled = options.Enabled,
                RuntimeProvider = options.RuntimeProvider,
                CudaDeviceId = options.CudaDeviceId,
                Model = ToEmbeddingModelOptions(options.Model)
            };
        }

        private static CleanupEmbeddingModelOptions ToEmbeddingModelOptions(CleanupScoreModelOptions model)
        {
            return new CleanupEmbeddingModelOptions
            {
                ModelKey = model.ModelKey,
                ModelPath = model.ModelPath,
                InputName = model.InputName,
                OutputName = model.OutputName,
                InputWidth = model.InputWidth,
                InputHeight = model.InputHeight,
                InputLayout = model.InputLayout,
                Dimensions = 1,
                Mean = model.Mean,
                StandardDeviation = model.StandardDeviation
            };
        }
    }
}