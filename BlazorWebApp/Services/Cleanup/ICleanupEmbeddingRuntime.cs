namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupEmbeddingRuntime
    {
        CleanupEmbeddingRuntimeProbeResult Probe(CleanupEmbeddingOptions options);
    }
}