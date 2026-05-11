using System.Security.Cryptography;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupEmbeddingModelMetadataServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "cleanup-embedding-model-tests-" + Guid.NewGuid());

    public CleanupEmbeddingModelMetadataServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Validate_AllowsEmptyConfigurationWhenEmbeddingsAreDisabled()
    {
        var service = CreateService(new CleanupEmbeddingOptions { Enabled = false });

        var result = service.Validate(service.Options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RequiresModelMetadataWhenEmbeddingsAreEnabled()
    {
        var service = CreateService(new CleanupEmbeddingOptions
        {
            Enabled = true,
            Model = new CleanupEmbeddingModelOptions()
        });

        var result = service.Validate(service.Options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("ModelKey"));
        result.Errors.Should().Contain(error => error.Contains("ModelPath"));
        result.Errors.Should().Contain(error => error.Contains("InputName"));
        result.Errors.Should().Contain(error => error.Contains("OutputName"));
        result.Errors.Should().Contain(error => error.Contains("Dimensions"));
    }

    [Fact]
    public void Validate_AllowsComfyConfigurationWithoutLocalOnnxMetadata()
    {
        var service = CreateService(new CleanupEmbeddingOptions
        {
            Enabled = true,
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupEmbeddingModelOptions
            {
                ModelKey = "blazor_stats_v1_512",
                Dimensions = 512
            }
        });

        var result = service.Validate(service.Options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsComfyEmbeddingDimensionsBelowNodeMinimum()
    {
        var service = CreateService(new CleanupEmbeddingOptions
        {
            Enabled = true,
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupEmbeddingModelOptions
            {
                ModelKey = "blazor_stats_v1_512",
                Dimensions = 1
            }
        });

        var result = service.Validate(service.Options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("at least 16"));
    }

    [Fact]
    public async Task GetIdentityAsync_ComputesModelHash()
    {
        var modelPath = Path.Combine(_tempDirectory, "model.onnx");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(modelPath, bytes);
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var service = CreateService(new CleanupEmbeddingOptions
        {
            Enabled = true,
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.CUDA,
            Model = new CleanupEmbeddingModelOptions
            {
                ModelKey = "clip-vit-b32",
                ModelPath = modelPath,
                InputName = "pixel_values",
                OutputName = "image_embeds",
                Dimensions = 512
            }
        });

        var identity = await service.GetIdentityAsync();

        identity.ModelKey.Should().Be("clip-vit-b32");
        identity.ModelHash.Should().Be(expectedHash);
        identity.Dimensions.Should().Be(512);
        identity.RuntimeProvider.Should().Be("CUDA");
    }

    [Fact]
    public async Task GetIdentityAsync_UsesStableRemoteHashForComfy()
    {
        var service = CreateService(new CleanupEmbeddingOptions
        {
            Enabled = true,
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupEmbeddingModelOptions
            {
                ModelKey = "blazor_stats_v1_512",
                Dimensions = 512
            }
        });

        var identity = await service.GetIdentityAsync();

        identity.ModelKey.Should().Be("blazor_stats_v1_512");
        identity.ModelHash.Should().NotBeNullOrWhiteSpace();
        identity.Dimensions.Should().Be(512);
        identity.RuntimeProvider.Should().Be("ComfyUI");
    }

    private static CleanupEmbeddingModelMetadataService CreateService(CleanupEmbeddingOptions options)
        => new(Options.Create(options));
}