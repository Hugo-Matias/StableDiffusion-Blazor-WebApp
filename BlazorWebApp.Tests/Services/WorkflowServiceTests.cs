using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class WorkflowServiceTests
{
    private readonly Mock<ILogger<WorkflowService>> _mockLogger;

    public WorkflowServiceTests()
    {
        _mockLogger = new Mock<ILogger<WorkflowService>>();
    }

    #region Workflow Model Tests

    [Fact]
    public void Workflow_ShouldHaveCorrectDefaultValues()
    {
        // Arrange & Act
        var workflow = new Workflow();

        // Assert
        Assert.Equal(default(Guid), workflow.Id);
        Assert.Null(workflow.Title);
        Assert.Equal(default(ModelBase), workflow.Base);
        Assert.Equal(default(ModeType), workflow.Mode);
        Assert.Null(workflow.Assets);
        Assert.Null(workflow.Pipeline);
        Assert.Null(workflow.RawJson);
    }

    [Fact]
    public void WorkflowAsset_ShouldHaveCorrectDefaultValues()
    {
        // Arrange & Act
        var asset = new WorkflowAsset();

        // Assert
        Assert.Equal(string.Empty, asset.Parameter);
        Assert.Equal(string.Empty, asset.Label);
        Assert.Equal(default(AssetType), asset.Type);
        Assert.Null(asset.DefaultValue);
        Assert.Equal(0, asset.Order);
        Assert.Equal(0, asset.ColumnSize);
    }

    [Fact]
    public void WorkflowAsset_ShouldSupportAllAssetTypes()
    {
        // Arrange & Act & Assert - Test all defined AssetType values
        var checkpointAsset = new WorkflowAsset { Type = AssetType.CheckpointModel };
        var diffusionAsset = new WorkflowAsset { Type = AssetType.DiffusionModel };
        var vaeAsset = new WorkflowAsset { Type = AssetType.Vae };
        var clipAsset = new WorkflowAsset { Type = AssetType.Clip };
        var clipVisionAsset = new WorkflowAsset { Type = AssetType.ClipVision };

        Assert.Equal(AssetType.CheckpointModel, checkpointAsset.Type);
        Assert.Equal(AssetType.DiffusionModel, diffusionAsset.Type);
        Assert.Equal(AssetType.Vae, vaeAsset.Type);
        Assert.Equal(AssetType.Clip, clipAsset.Type);
        Assert.Equal(AssetType.ClipVision, clipVisionAsset.Type);
    }

    [Fact]
    public void WorkflowStep_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var step = new WorkflowStep();

        // Assert
        Assert.Null(step.Fragment);
        Assert.Null(step.RawParameters);
        Assert.Null(step.Parameters);
        Assert.Null(step.Outputs);
    }

    [Fact]
    public void OutputMapping_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var mapping = new OutputMapping();

        // Assert
        Assert.Null(mapping.Node);
        Assert.Equal(0, mapping.Index);
    }

    #endregion

    #region Integration-style Tests

    [Fact]
    public void WorkflowService_Constructor_ShouldNotThrow()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var ioService = new IOService(mockConfig.Object);

        // Act & Assert - Constructor should not throw
        var service = new WorkflowService(ioService, _mockLogger.Object);
        Assert.NotNull(service);
    }

    #endregion

    #region NodeRegistry Tests

    [Fact]
    public void NodeRegistry_ShouldRegisterAndRetrieveOutputs()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register("output1", "node_1", 0);
        var reference = registry.GetReference("output1");

        // Assert
        Assert.Equal("[\"node_1\", 0]", reference);
    }

    [Fact]
    public void NodeRegistry_GetReference_WithNonExistentKey_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert
        var exception = Assert.Throws<KeyNotFoundException>(() => registry.GetReference("nonexistent"));
        Assert.Contains("nonexistent", exception.Message);
    }

    [Fact]
    public void NodeRegistry_Merge_ShouldCombineRegistries()
    {
        // Arrange
        var registry1 = new NodeRegistry();
        registry1.Register("output1", "node_1", 0);

        var registry2 = new NodeRegistry();
        registry2.Register("output2", "node_2", 1);

        // Act
        registry1.Merge(registry2);

        // Assert
        Assert.Equal("[\"node_1\", 0]", registry1.GetReference("output1"));
        Assert.Equal("[\"node_2\", 1]", registry1.GetReference("output2"));
    }

    [Fact]
    public void NodeRegistry_Register_WithDifferentOutputIndex_ShouldWork()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        registry.Register("output", "node_1", 2);

        // Assert
        Assert.Equal("[\"node_1\", 2]", registry.GetReference("output"));
    }

    [Fact]
    public void NodeRegistry_Register_OverwriteExistingKey_ShouldUpdateValue()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register("output", "node_1", 0);

        // Act
        registry.Register("output", "node_2", 1);

        // Assert
        Assert.Equal("[\"node_2\", 1]", registry.GetReference("output"));
    }

    #endregion

    #region SubgraphContext Tests

    [Fact]
    public void SubgraphContext_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var context = new SubgraphContext();

        // Assert - SubgraphContext has default initializers in the class
        Assert.NotNull(context.Parameters);
        Assert.NotNull(context.Outputs);
    }

    [Fact]
    public void SubgraphContext_ShouldAllowSettingProperties()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { { "key", "value" } };
        var outputs = new NodeRegistry();

        // Act
        var context = new SubgraphContext
        {
            Parameters = parameters,
            Outputs = outputs
        };

        // Assert
        Assert.Same(parameters, context.Parameters);
        Assert.Same(outputs, context.Outputs);
    }

    #endregion
}
