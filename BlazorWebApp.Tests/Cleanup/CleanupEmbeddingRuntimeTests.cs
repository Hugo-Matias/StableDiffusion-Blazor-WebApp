using BlazorWebApp.Services.Cleanup;
using FluentAssertions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupEmbeddingRuntimeTests
{
    private readonly CleanupEmbeddingRuntime _runtime = new();

    [Fact]
    public void Probe_ReturnsAvailableForCpuProvider()
    {
        var result = _runtime.Probe(new CleanupEmbeddingOptions
        {
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.CPU
        });

        result.RequestedProvider.Should().Be(CleanupEmbeddingRuntimeProvider.CPU);
        result.IsAvailable.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Probe_ReturnsResultForCudaProviderWithoutThrowing()
    {
        var result = _runtime.Probe(new CleanupEmbeddingOptions
        {
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.CUDA,
            CudaDeviceId = 0
        });

        result.RequestedProvider.Should().Be(CleanupEmbeddingRuntimeProvider.CUDA);
    }

    [Fact]
    public void Probe_ReturnsUnavailableForDeferredDirectMlProvider()
    {
        var result = _runtime.Probe(new CleanupEmbeddingOptions
        {
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.DirectML
        });

        result.RequestedProvider.Should().Be(CleanupEmbeddingRuntimeProvider.DirectML);
        result.IsAvailable.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DirectML");
    }
}