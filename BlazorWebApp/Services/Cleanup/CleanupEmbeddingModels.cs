namespace BlazorWebApp.Services.Cleanup
{
    public enum CleanupEmbeddingRuntimeProvider
    {
        CPU,
        CUDA,
        DirectML,
        ComfyUI
    }

    public record CleanupEmbeddingOptions
    {
        public const string SectionName = "Cleanup:Embeddings";

        public bool Enabled { get; init; }
        public bool RunForSavedImages { get; init; }
        public CleanupEmbeddingRuntimeProvider RuntimeProvider { get; init; } = CleanupEmbeddingRuntimeProvider.CPU;
        public int CudaDeviceId { get; init; }
        public CleanupEmbeddingModelOptions Model { get; init; } = new();
    }

    public record CleanupEmbeddingModelOptions
    {
        public string ModelKey { get; init; } = string.Empty;
        public string ModelPath { get; init; } = string.Empty;
        public string InputName { get; init; } = string.Empty;
        public string OutputName { get; init; } = string.Empty;
        public int InputWidth { get; init; } = 224;
        public int InputHeight { get; init; } = 224;
        public string InputLayout { get; init; } = "NCHW";
        public int Dimensions { get; init; }
        public List<float> Mean { get; init; } = new() { 0.48145466f, 0.4578275f, 0.40821073f };
        public List<float> StandardDeviation { get; init; } = new() { 0.26862954f, 0.26130258f, 0.27577711f };
    }

    public record CleanupEmbeddingModelIdentity
    {
        public string ModelKey { get; init; } = string.Empty;
        public string? ModelHash { get; init; }
        public int Dimensions { get; init; }
        public string RuntimeProvider { get; init; } = CleanupEmbeddingRuntimeProvider.CPU.ToString();
    }

    public record CleanupEmbeddingModelValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; init; } = new();
    }

    public record CleanupEmbeddingTensor
    {
        public float[] Values { get; init; } = Array.Empty<float>();
        public int[] Dimensions { get; init; } = Array.Empty<int>();
    }

    public record CleanupEmbeddingRuntimeProbeResult
    {
        public CleanupEmbeddingRuntimeProvider RequestedProvider { get; init; }
        public bool IsAvailable { get; init; }
        public string? ErrorMessage { get; init; }
    }
}