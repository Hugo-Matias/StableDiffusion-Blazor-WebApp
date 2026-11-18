using BlazorWebApp.Workflows.Builders;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Builders;

public class NodeBuilderTests
{
    [Fact]
    public void Build_WithTypeOnly_CreatesNodeWithClassType()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act
        var node = builder
            .Type("KSampler")
            .Build();

        // Assert
        Assert.NotNull(node);
        Assert.Equal("KSampler", node.ClassType);
        Assert.Empty(node.Inputs);
    }

    [Fact]
    public void Build_WithoutType_ThrowsException()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("missing class_type", exception.Message);
    }

    [Fact]
    public void Input_WithString_AddsStringInput()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act
        var node = builder
            .Type("CLIPTextEncode")
            .Input("text", "hello world")
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        Assert.Equal("hello world", node.Inputs["text"]);
    }

    [Fact]
    public void Input_WithInt_AddsIntegerInput()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act
        var node = builder
            .Type("KSampler")
            .Input("steps", 20)
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        Assert.Equal(20, node.Inputs["steps"]);
    }

    [Fact]
    public void Input_WithDouble_AddsDoubleInput()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act
        var node = builder
            .Type("KSampler")
            .Input("cfg", 7.5)
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        Assert.Equal(7.5, node.Inputs["cfg"]);
    }

    [Fact]
    public void Input_WithLong_AddsLongInput()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act
        var node = builder
            .Type("KSampler")
            .Input("seed", 12345678901234L)
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        Assert.Equal(12345678901234L, node.Inputs["seed"]);
    }

    [Fact]
    public void Input_WithBool_AddsBooleanInput()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");

        // Act
        var node = builder
            .Type("CheckpointLoaderSimple")
            .Input("force_reload", true)
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        Assert.Equal(true, node.Inputs["force_reload"]);
    }

    [Fact]
    public void InputRef_AddsNodeReference()
    {
        // Arrange
        var builder = new NodeBuilder("sampler");

        // Act
        var node = builder
            .Type("KSampler")
            .InputRef("model", ("unet_loader", 0))
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        var reference = Assert.IsType<object[]>(node.Inputs["model"]);
        Assert.Equal(2, reference.Length);
        Assert.Equal("unet_loader", reference[0]);
        Assert.Equal(0, reference[1]);
    }

    [Fact]
    public void Title_SetsCustomTitle()
    {
        // Arrange
        var builder = new NodeBuilder("sampler_1");

        // Act
        var node = builder
            .Type("KSampler")
            .Title("Main Sampler")
            .Build();

        // Assert
        Assert.Equal("Main Sampler", node.Meta.Title);
    }

    [Fact]
    public void Title_DefaultsToNodeId()
    {
        // Arrange
        var builder = new NodeBuilder("sampler_1");

        // Act
        var node = builder
            .Type("KSampler")
            .Build();

        // Assert
        Assert.Equal("sampler_1", node.Meta.Title);
    }

    [Fact]
    public void FluentChaining_AllowsMultipleInputs()
    {
        // Arrange
        var builder = new NodeBuilder("sampler");

        // Act
        var node = builder
            .Type("KSampler")
            .Input("sampler_name", "euler")
            .Input("steps", 20)
            .Input("cfg", 7.5)
            .Input("seed", -1L)
            .InputRef("model", ("loader", 0))
            .InputRef("positive", ("clip_positive", 0))
            .InputRef("negative", ("clip_negative", 0))
            .Title("Main Sampler")
            .Build();

        // Assert
        Assert.Equal("KSampler", node.ClassType);
        Assert.Equal(7, node.Inputs.Count);
        Assert.Equal("euler", node.Inputs["sampler_name"]);
        Assert.Equal(20, node.Inputs["steps"]);
        Assert.Equal(7.5, node.Inputs["cfg"]);
        Assert.Equal(-1L, node.Inputs["seed"]);
        Assert.Equal("Main Sampler", node.Meta.Title);
    }

    [Fact]
    public void Input_WithList_AddsListInput()
    {
        // Arrange
        var builder = new NodeBuilder("test_node");
        var list = new List<object> { 1, 2, 3 };

        // Act
        var node = builder
            .Type("TestNode")
            .Input("values", list)
            .Build();

        // Assert
        Assert.Single(node.Inputs);
        var result = Assert.IsType<List<object>>(node.Inputs["values"]);
        Assert.Equal(3, result.Count);
    }
}
