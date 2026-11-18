using BlazorWebApp.Workflows.Builders;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Builders;

public class ComfyWorkflowBuilderTests
{
    [Fact]
    public void AddNode_AddsNodeToWorkflow()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();

        // Act
        builder.AddNode("sampler", node => node
            .Type("KSampler")
            .Input("steps", 20));

        // Assert
        Assert.Equal(1, builder.NodeCount);
        Assert.True(builder.HasNode("sampler"));
    }

    [Fact]
    public void AddNode_WithMultipleNodes_AddsAll()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();

        // Act
        builder
            .AddNode("loader", node => node.Type("CheckpointLoaderSimple"))
            .AddNode("sampler", node => node.Type("KSampler"))
            .AddNode("decoder", node => node.Type("VAEDecode"));

        // Assert
        Assert.Equal(3, builder.NodeCount);
        Assert.True(builder.HasNode("loader"));
        Assert.True(builder.HasNode("sampler"));
        Assert.True(builder.HasNode("decoder"));
    }

    [Fact]
    public void GetNodeIds_ReturnsAllNodeIds()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        builder
            .AddNode("loader", node => node.Type("CheckpointLoaderSimple"))
            .AddNode("sampler", node => node.Type("KSampler"))
            .AddNode("decoder", node => node.Type("VAEDecode"));

        // Act
        var nodeIds = builder.GetNodeIds().ToList();

        // Assert
        Assert.Equal(3, nodeIds.Count);
        Assert.Contains("loader", nodeIds);
        Assert.Contains("sampler", nodeIds);
        Assert.Contains("decoder", nodeIds);
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        builder.AddNode("sampler", node => node
            .Type("KSampler")
            .Input("steps", 20)
            .Input("cfg", 7.5));

        // Act
        var json = builder.ToJson();

        // Assert
        Assert.NotEmpty(json);
        
        // Verify it's valid JSON
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void ToJson_ContainsNodeData()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        builder.AddNode("sampler", node => node
            .Type("KSampler")
            .Input("sampler_name", "euler")
            .Input("steps", 20));

        // Act
        var json = builder.ToJson();
        var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("sampler", out var samplerNode));
        Assert.Equal("KSampler", samplerNode.GetProperty("class_type").GetString());
        
        var inputs = samplerNode.GetProperty("inputs");
        Assert.Equal("euler", inputs.GetProperty("sampler_name").GetString());
        Assert.Equal(20, inputs.GetProperty("steps").GetInt32());
    }

    [Fact]
    public void ToJson_WithNodeReferences_FormatsCorrectly()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        builder
            .AddNode("loader", node => node
                .Type("CheckpointLoaderSimple")
                .Input("ckpt_name", "model.safetensors"))
            .AddNode("sampler", node => node
                .Type("KSampler")
                .InputRef("model", ("loader", 0))
                .InputRef("positive", ("clip_pos", 0))
                .InputRef("negative", ("clip_neg", 0)));

        // Act
        var json = builder.ToJson();
        var doc = JsonDocument.Parse(json);

        // Assert
        var samplerInputs = doc.RootElement.GetProperty("sampler").GetProperty("inputs");
        
        var modelRef = samplerInputs.GetProperty("model");
        Assert.Equal(JsonValueKind.Array, modelRef.ValueKind);
        Assert.Equal("loader", modelRef[0].GetString());
        Assert.Equal(0, modelRef[1].GetInt32());
    }

    [Fact]
    public void ToComfyWorkflow_ReturnsWorkflowWithJson()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        
        builder.AddNode("sampler", node => node
            .Type("KSampler")
            .Input("steps", 20));

        // Act
        var workflow = builder.ToComfyWorkflow(registry);

        // Assert
        Assert.NotNull(workflow);
        Assert.NotNull(workflow.Json);
        Assert.NotEmpty(workflow.Json);
        Assert.Equal(registry, workflow.Registry);
    }

    [Fact]
    public void ToComfyWorkflow_WithoutRegistry_CreatesWorkflow()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        builder.AddNode("sampler", node => node.Type("KSampler"));

        // Act
        var workflow = builder.ToComfyWorkflow();

        // Assert
        Assert.NotNull(workflow);
        Assert.NotNull(workflow.Json);
        Assert.Null(workflow.Registry);
    }

    [Fact]
    public void FluentChaining_BuildsCompleteWorkflow()
    {
        // Arrange & Act
        var builder = new ComfyWorkflowBuilder();
        builder
            .AddNode("loader", node => node
                .Type("CheckpointLoaderSimple")
                .Input("ckpt_name", "model.safetensors"))
            .AddNode("positive_prompt", node => node
                .Type("CLIPTextEncode")
                .Input("text", "beautiful landscape")
                .InputRef("clip", ("loader", 1)))
            .AddNode("negative_prompt", node => node
                .Type("CLIPTextEncode")
                .Input("text", "ugly, blurry")
                .InputRef("clip", ("loader", 1)))
            .AddNode("latent", node => node
                .Type("EmptyLatentImage")
                .Input("width", 512)
                .Input("height", 512)
                .Input("batch_size", 1))
            .AddNode("sampler", node => node
                .Type("KSampler")
                .Input("seed", -1L)
                .Input("steps", 20)
                .Input("cfg", 7.5)
                .Input("sampler_name", "euler")
                .Input("scheduler", "normal")
                .InputRef("model", ("loader", 0))
                .InputRef("positive", ("positive_prompt", 0))
                .InputRef("negative", ("negative_prompt", 0))
                .InputRef("latent_image", ("latent", 0)))
            .AddNode("decoder", node => node
                .Type("VAEDecode")
                .InputRef("samples", ("sampler", 0))
                .InputRef("vae", ("loader", 2)))
            .AddNode("save", node => node
                .Type("SaveImage")
                .Input("filename_prefix", "output")
                .InputRef("images", ("decoder", 0)));

        // Assert
        Assert.Equal(7, builder.NodeCount);
        
        var json = builder.ToJson();
        var doc = JsonDocument.Parse(json);
        
        // Verify all nodes exist in JSON
        Assert.True(doc.RootElement.TryGetProperty("loader", out _));
        Assert.True(doc.RootElement.TryGetProperty("positive_prompt", out _));
        Assert.True(doc.RootElement.TryGetProperty("negative_prompt", out _));
        Assert.True(doc.RootElement.TryGetProperty("latent", out _));
        Assert.True(doc.RootElement.TryGetProperty("sampler", out _));
        Assert.True(doc.RootElement.TryGetProperty("decoder", out _));
        Assert.True(doc.RootElement.TryGetProperty("save", out _));
    }

    [Fact]
    public void AddNode_WithPrebuiltNode_AddsToWorkflow()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();
        var nodeBuilder = new NodeBuilder("test");
        var node = nodeBuilder.Type("KSampler").Build();

        // Act
        builder.AddNode("test", node);

        // Assert
        Assert.Equal(1, builder.NodeCount);
        Assert.True(builder.HasNode("test"));
    }

    [Fact]
    public void HasNode_WithNonexistentNode_ReturnsFalse()
    {
        // Arrange
        var builder = new ComfyWorkflowBuilder();

        // Act & Assert
        Assert.False(builder.HasNode("nonexistent"));
    }
}
