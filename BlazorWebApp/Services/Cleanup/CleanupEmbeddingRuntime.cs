using Microsoft.ML.OnnxRuntime;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupEmbeddingRuntime : ICleanupEmbeddingRuntime
    {
        public CleanupEmbeddingRuntimeProbeResult Probe(CleanupEmbeddingOptions options)
        {
            try
            {
                using var sessionOptions = CreateSessionOptions(options);
                return new CleanupEmbeddingRuntimeProbeResult
                {
                    RequestedProvider = options.RuntimeProvider,
                    IsAvailable = true
                };
            }
            catch (Exception ex)
            {
                return new CleanupEmbeddingRuntimeProbeResult
                {
                    RequestedProvider = options.RuntimeProvider,
                    IsAvailable = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        internal static Microsoft.ML.OnnxRuntime.SessionOptions CreateSessionOptions(CleanupEmbeddingOptions options)
        {
            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            switch (options.RuntimeProvider)
            {
                case CleanupEmbeddingRuntimeProvider.CPU:
                    break;
                case CleanupEmbeddingRuntimeProvider.CUDA:
                    sessionOptions.AppendExecutionProvider_CUDA(options.CudaDeviceId);
                    break;
                case CleanupEmbeddingRuntimeProvider.DirectML:
                    throw new NotSupportedException("DirectML cleanup embeddings are planned but not implemented yet.");
                case CleanupEmbeddingRuntimeProvider.ComfyUI:
                    throw new NotSupportedException("ComfyUI-backed cleanup embeddings are planned but not implemented yet.");
                default:
                    throw new NotSupportedException($"Unsupported cleanup embedding runtime provider: {options.RuntimeProvider}");
            }

            return sessionOptions;
        }
    }
}