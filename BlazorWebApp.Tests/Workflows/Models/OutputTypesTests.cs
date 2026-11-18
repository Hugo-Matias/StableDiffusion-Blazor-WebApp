using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Models;

public class OutputTypesTests
{
    [Fact]
    public void ModelOutput_CreatesWithNodeIdAndIndex()
    {
        // Act
        var output = new ModelOutput("unet_loader", 0);

        // Assert
        Assert.Equal("unet_loader", output.NodeId);
        Assert.Equal(0, output.OutputIndex);
    }

    [Fact]
    public void ClipOutput_CreatesWithNodeIdAndIndex()
    {
        // Act
        var output = new ClipOutput("clip_loader", 1);

        // Assert
        Assert.Equal("clip_loader", output.NodeId);
        Assert.Equal(1, output.OutputIndex);
    }

    [Fact]
    public void VaeOutput_CreatesWithNodeIdAndIndex()
    {
        // Act
        var output = new VaeOutput("vae_loader", 0);

        // Assert
        Assert.Equal("vae_loader", output.NodeId);
        Assert.Equal(0, output.OutputIndex);
    }

    [Fact]
    public void LatentOutput_CreatesWithNodeIdAndIndex()
    {
        // Act
        var output = new LatentOutput("empty_latent", 0);

        // Assert
        Assert.Equal("empty_latent", output.NodeId);
        Assert.Equal(0, output.OutputIndex);
    }

    [Fact]
    public void ImageOutput_CreatesWithNodeIdAndIndex()
    {
        // Act
        var output = new ImageOutput("vae_decode", 0);

        // Assert
        Assert.Equal("vae_decode", output.NodeId);
        Assert.Equal(0, output.OutputIndex);
    }

    [Fact]
    public void ConditioningOutput_CreatesWithNodeIdAndIndex()
    {
        // Act
        var output = new ConditioningOutput("clip_text_encode", 0);

        // Assert
        Assert.Equal("clip_text_encode", output.NodeId);
        Assert.Equal(0, output.OutputIndex);
    }

    [Fact]
    public void NodeOutput_SupportsRecordEquality()
    {
        // Arrange
        var output1 = new ModelOutput("unet_loader", 0);
        var output2 = new ModelOutput("unet_loader", 0);
        var output3 = new ModelOutput("different_loader", 0);

        // Assert
        Assert.Equal(output1, output2);
        Assert.NotEqual(output1, output3);
    }

    [Fact]
    public void NodeOutput_DifferentTypesAreNotEqual()
    {
        // Arrange
        var modelOutput = new ModelOutput("loader", 0);
        var clipOutput = new ClipOutput("loader", 0);

        // Assert - different types should not be equal even with same node/index
        Assert.NotEqual((NodeOutput)modelOutput, (NodeOutput)clipOutput);
    }

    [Fact]
    public void NodeOutput_WithDifferentIndexes_AreNotEqual()
    {
        // Arrange
        var output1 = new ModelOutput("multi_output_node", 0);
        var output2 = new ModelOutput("multi_output_node", 1);

        // Assert
        Assert.NotEqual(output1, output2);
    }
}
