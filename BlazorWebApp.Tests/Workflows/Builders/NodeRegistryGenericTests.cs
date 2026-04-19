using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Builders;

public class NodeRegistryGenericTests
{
    [Fact]
    public void RegisterGeneric_AddsTypedOutputToRegistry()
    {
        // Arrange
        var registry = new NodeRegistry();
        var output = new ModelOutput("unet_loader", 0);

        // Act
        registry.Register(output);

        // Assert
        Assert.True(registry.HasOutput<ModelOutput>());
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void GetRefGeneric_ReturnsRegisteredOutput()
    {
        // Arrange
        var registry = new NodeRegistry();
        var output = new ModelOutput("unet_loader", 0);
        registry.Register(output);

        // Act
        var reference = registry.GetRef<ModelOutput>();

        // Assert
        Assert.Equal("unet_loader", reference.nodeId);
        Assert.Equal(0, reference.index);
    }

    [Fact]
    public void GetRefGeneric_WithUnregisteredType_ThrowsException()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => registry.GetRef<ModelOutput>());
        Assert.Contains("No output of type ModelOutput", exception.Message);
    }

    [Fact]
    public void GetRefGeneric_WithMultipleOutputs_ThrowsException()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register(new LatentOutput("empty_latent", 0));
        registry.Register(new LatentOutput("sampler", 0));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => registry.GetRef<LatentOutput>());
        Assert.Contains("Multiple outputs of type LatentOutput", exception.Message);
        Assert.Contains("Use GetRef<TOutput>(string scopePrefix)", exception.Message);
    }

    [Fact]
    public void GetRefGeneric_WithScopePrefix_ReturnsCorrectOutput()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register(new ImageOutput("main_vae_decode", 0));
        registry.Register(new ImageOutput("upscale_vae_decode", 0));
        registry.Register(new ImageOutput("detailer_vae_decode", 0));

        // Act
        var upscaleRef = registry.GetRef<ImageOutput>("upscale_");
        var detailerRef = registry.GetRef<ImageOutput>("detailer_");

        // Assert
        Assert.Equal("upscale_vae_decode", upscaleRef.nodeId);
        Assert.Equal("detailer_vae_decode", detailerRef.nodeId);
    }

    [Fact]
    public void GetRefGeneric_WithInvalidScope_ThrowsException()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register(new ImageOutput("main_vae_decode", 0));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.GetRef<ImageOutput>("upscale_"));
        Assert.Contains("No output of type ImageOutput with scope prefix 'upscale_'", exception.Message);
    }

    [Fact]
    public void GetAll_ReturnsAllOutputsOfType()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register(new ClipOutput("clip_loader_1", 0));
        registry.Register(new ClipOutput("clip_loader_2", 0));
        registry.Register(new ModelOutput("unet_loader", 0));

        // Act
        var clipOutputs = registry.GetAll<ClipOutput>();

        // Assert
        Assert.Equal(2, clipOutputs.Count);
        Assert.All(clipOutputs, output => Assert.IsType<ClipOutput>(output));
    }

    [Fact]
    public void GetAll_WithNoOutputs_ReturnsEmptyList()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        var outputs = registry.GetAll<ModelOutput>();

        // Assert
        Assert.Empty(outputs);
    }

    [Fact]
    public void HasOutputGeneric_ReturnsTrueWhenExists()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register(new VaeOutput("vae_loader", 0));

        // Act & Assert
        Assert.True(registry.HasOutput<VaeOutput>());
    }

    [Fact]
    public void HasOutputGeneric_ReturnsFalseWhenNotExists()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert
        Assert.False(registry.HasOutput<VaeOutput>());
    }

    [Fact]
    public void RegisterGeneric_WithMultipleTypes_TracksIndependently()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register(new ModelOutput("unet_loader", 0));
        registry.Register(new ClipOutput("clip_loader", 0));
        registry.Register(new VaeOutput("vae_loader", 0));
        registry.Register(new LatentOutput("empty_latent", 0));
        registry.Register(new ImageOutput("vae_decode", 0));
        registry.Register(new ConditioningOutput("clip_text_encode", 0));

        // Assert
        Assert.Equal(6, registry.Count);
        Assert.True(registry.HasOutput<ModelOutput>());
        Assert.True(registry.HasOutput<ClipOutput>());
        Assert.True(registry.HasOutput<VaeOutput>());
        Assert.True(registry.HasOutput<LatentOutput>());
        Assert.True(registry.HasOutput<ImageOutput>());
        Assert.True(registry.HasOutput<ConditioningOutput>());
    }

    [Fact]
    public void Clear_RemovesAllTypedOutputs()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register(new ModelOutput("unet_loader", 0));
        registry.Register(new ClipOutput("clip_loader", 0));

        // Act
        registry.Clear();

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.False(registry.HasOutput<ModelOutput>());
        Assert.False(registry.HasOutput<ClipOutput>());
    }

    [Fact]
    public void Count_IncludesBothStringAndTypedOutputs()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register("string_output", "node1", 0);
        registry.Register(new ModelOutput("node2", 0));

        // Assert
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void Clear_RemovesBothStringAndTypedOutputs()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register("string_output", "node1", 0);
        registry.Register(new ModelOutput("node2", 0));

        // Act
        registry.Clear();

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.False(registry.HasOutput("string_output"));
        Assert.False(registry.HasOutput<ModelOutput>());
    }

    [Fact]
    public void RegisterGeneric_WithMultipleSameType_AllowsMultiple()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register(new LatentOutput("empty_latent", 0));
        registry.Register(new LatentOutput("sampler_1", 0));
        registry.Register(new LatentOutput("sampler_2", 0));

        // Assert
        Assert.Equal(3, registry.Count);
        var allLatents = registry.GetAll<LatentOutput>();
        Assert.Equal(3, allLatents.Count);
    }

    [Fact]
    public void BackwardCompatibility_StringAndGenericMethodsWorkTogether()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act - mix string-based and generic registration
        registry.Register("model_output", "unet_loader", 0);
        registry.Register(new ClipOutput("clip_loader", 0));

        // Assert - both methods work independently
        Assert.Equal("unet_loader", registry.GetRef("model_output").nodeId);
        Assert.Equal("clip_loader", registry.GetRef<ClipOutput>().nodeId);
        Assert.Equal(2, registry.Count);
    }
}
