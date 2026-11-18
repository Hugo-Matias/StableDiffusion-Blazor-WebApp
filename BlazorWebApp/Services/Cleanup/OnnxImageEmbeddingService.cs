using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BlazorWebApp.Services.Cleanup
{
    public class OnnxImageEmbeddingService : IImageEmbeddingService, IDisposable
    {
        private readonly IOptions<CleanupEmbeddingOptions> _options;
        private readonly ICleanupEmbeddingModelMetadataService _modelMetadata;
        private readonly ICleanupEmbeddingImagePreprocessor _preprocessor;
        private readonly ICleanupEmbeddingVectorCodec _vectorCodec;
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private InferenceSession? _session;
        private CleanupEmbeddingModelIdentity? _sessionIdentity;

        public OnnxImageEmbeddingService(
            IOptions<CleanupEmbeddingOptions> options,
            ICleanupEmbeddingModelMetadataService modelMetadata,
            ICleanupEmbeddingImagePreprocessor preprocessor,
            ICleanupEmbeddingVectorCodec vectorCodec)
        {
            _options = options;
            _modelMetadata = modelMetadata;
            _preprocessor = preprocessor;
            _vectorCodec = vectorCodec;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            var options = _options.Value;
            var validation = _modelMetadata.Validate(options);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Cleanup embedding configuration is invalid: " + string.Join(" ", validation.Errors));
            }

            var session = await GetSessionAsync(cancellationToken);
            var tensorData = _preprocessor.Preprocess(imagePath, options.Model);
            var tensor = new DenseTensor<float>(tensorData.Values, tensorData.Dimensions);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(options.Model.InputName, tensor)
            };

            using var results = session.Run(inputs);
            var output = results.FirstOrDefault(result => result.Name == options.Model.OutputName) ?? results.FirstOrDefault();
            if (output == null)
            {
                throw new InvalidOperationException("ONNX embedding model returned no outputs.");
            }

            var vector = output.AsTensor<float>().ToArray();
            if (vector.Length != options.Model.Dimensions)
            {
                throw new InvalidOperationException($"ONNX embedding output dimension mismatch. Expected {options.Model.Dimensions}, got {vector.Length}.");
            }

            return _vectorCodec.Normalize(vector);
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
                using var sessionOptions = CleanupEmbeddingRuntime.CreateSessionOptions(_options.Value);
                _session = new InferenceSession(_options.Value.Model.ModelPath, sessionOptions);
                _sessionIdentity = identity;
                return _session;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private static bool IsSameIdentity(CleanupEmbeddingModelIdentity left, CleanupEmbeddingModelIdentity? right)
        {
            return right != null
                && left.ModelKey == right.ModelKey
                && left.ModelHash == right.ModelHash
                && left.Dimensions == right.Dimensions
                && left.RuntimeProvider == right.RuntimeProvider;
        }
    }
}