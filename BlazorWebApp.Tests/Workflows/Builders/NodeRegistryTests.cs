using BlazorWebApp.Workflows.Builders;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Builders;

public class NodeRegistryTests
{
    [Fact]
    public void Register_AddsOutputToRegistry()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register("model_output", "unet_loader", 0);

        // Assert
        Assert.True(registry.HasOutput("model_output"));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void GetRef_ReturnsRegisteredOutput()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register("model_output", "unet_loader", 0);

        // Act
        var reference = registry.GetRef("model_output");

        // Assert
        Assert.Equal("unet_loader", reference.nodeId);
        Assert.Equal(0, reference.outputIndex);
    }

    [Fact]
    public void GetRef_WithUnregisteredOutput_ThrowsException()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => registry.GetRef("missing_output"));
        Assert.Contains("has not been registered", exception.Message);
        Assert.Contains("missing_output", exception.Message);
    }

    [Fact]
    public void Register_WithMultipleOutputs_TracksAll()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register("model_output", "unet_loader", 0);
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);

        // Assert
        Assert.Equal(3, registry.Count);
        Assert.True(registry.HasOutput("model_output"));
        Assert.True(registry.HasOutput("clip_output"));
        Assert.True(registry.HasOutput("vae_output"));
    }

    [Fact]
    public void Register_OverwritesExistingOutput()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register("latent_output", "empty_latent", 0);

        // Act
        registry.Register("latent_output", "sampler", 0);
        var reference = registry.GetRef("latent_output");

        // Assert
        Assert.Equal("sampler", reference.nodeId);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void GetAllOutputNames_ReturnsAllKeys()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register("model_output", "unet_loader", 0);
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);

        // Act
        var names = registry.GetAllOutputNames().ToList();

        // Assert
        Assert.Equal(3, names.Count);
        Assert.Contains("model_output", names);
        Assert.Contains("clip_output", names);
        Assert.Contains("vae_output", names);
    }

    [Fact]
    public void Clear_RemovesAllOutputs()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register("model_output", "unet_loader", 0);
        registry.Register("clip_output", "clip_loader", 0);

        // Act
        registry.Clear();

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.False(registry.HasOutput("model_output"));
        Assert.False(registry.HasOutput("clip_output"));
    }

    [Fact]
    public void HasOutput_ReturnsFalseForUnregistered()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert
        Assert.False(registry.HasOutput("nonexistent"));
    }

    [Fact]
    public void Register_WithDifferentOutputIndexes_StoresCorrectly()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register("output_0", "multi_output_node", 0);
        registry.Register("output_1", "multi_output_node", 1);
        registry.Register("output_2", "multi_output_node", 2);

        // Assert
        Assert.Equal(0, registry.GetRef("output_0").outputIndex);
        Assert.Equal(1, registry.GetRef("output_1").outputIndex);
        Assert.Equal(2, registry.GetRef("output_2").outputIndex);
    }
}
