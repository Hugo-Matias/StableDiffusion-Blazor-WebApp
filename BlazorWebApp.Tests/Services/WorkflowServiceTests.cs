using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Data.Entities;
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

    private WorkflowService CreateWorkflowService()
    {
        return new WorkflowService(_mockLogger.Object);
    }

    #region Workflow Model Tests

    [Fact]
    public void Workflow_ShouldHaveCorrectDefaultValues()
    {
        // Arrange & Act
        var workflow = new Workflow();

        // Assert
        Assert.Equal(default(Guid), workflow.Id);
        Assert.Empty(workflow.Title);
        Assert.Equal(default(ModelBase), workflow.Base);
        Assert.Equal(default(ModeType), workflow.Mode);
        Assert.Null(workflow.Assets);
        Assert.Null(workflow.Sources);
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

    #endregion

    #region WorkflowService Tests

    [Fact]
    public void WorkflowService_Constructor_ShouldNotThrow()
    {
        // Act & Assert - Constructor should not throw
        var service = CreateWorkflowService();
        Assert.NotNull(service);
    }

    [Fact]
    public void WorkflowService_GetWorkflows_ShouldReturnDiscoveredBuilders()
    {
        // Arrange
        var service = CreateWorkflowService();

        // Act
        var workflows = service.GetWorkflows();

        // Assert
        Assert.NotNull(workflows);
        // Should discover at least ZImageTxt2ImgWorkflow
        Assert.True(workflows.Count >= 1, "Should discover at least one C# workflow builder");
    }

    [Fact]
    public void WorkflowService_GetWorkflowBuilders_ShouldReturnBuilders()
    {
        // Arrange
        var service = CreateWorkflowService();

        // Act
        var builders = service.GetWorkflowBuilders();

        // Assert
        Assert.NotNull(builders);
        Assert.True(builders.Count >= 1, "Should discover at least one C# workflow builder");
    }

    [Fact]
    public void WorkflowService_HasWorkflowBuilder_ShouldReturnTrueForExistingBuilder()
    {
        // Arrange
        var service = CreateWorkflowService();
        var builders = service.GetWorkflowBuilders();

        if (builders.Count == 0)
        {
            return; // Skip if no builders discovered
        }

        var firstBuilderId = builders.Keys.First();

        // Act
        var hasBuilder = service.HasWorkflowBuilder(firstBuilderId);

        // Assert
        Assert.True(hasBuilder);
    }

    [Fact]
    public void WorkflowService_HasWorkflowBuilder_ShouldReturnFalseForNonExistentBuilder()
    {
        // Arrange
        var service = CreateWorkflowService();

        // Act
        var hasBuilder = service.HasWorkflowBuilder(Guid.NewGuid());

        // Assert
        Assert.False(hasBuilder);
    }

    [Fact]
    public void WorkflowService_GetWorkflowById_ShouldReturnNullForNonExistent()
    {
        // Arrange
        var service = CreateWorkflowService();

        // Act
        var workflow = service.GetWorkflowById(Guid.NewGuid());

        // Assert
        Assert.Null(workflow);
    }

    [Fact]
    public void GetWorkflows_AllWorkflows_ShouldHaveNonEmptyCompatibleResourceBaseModels()
    {
        // Arrange
        var service = CreateWorkflowService();

        // Act
        var workflows = service.GetWorkflows();

        // Assert
        foreach (var workflow in workflows)
        {
            Assert.NotNull(workflow.CompatibleResourceBaseModels);
            Assert.True(workflow.CompatibleResourceBaseModels.Count > 0,
                $"Workflow '{workflow.Title}' (Base={workflow.Base}) should declare CompatibleResourceBaseModels");
        }
    }

    [Fact]
    public void GetWorkflows_CompatibleResourceBaseModels_ShouldPropagateThroughConversion()
    {
        // Arrange
        var service = CreateWorkflowService();

        // Act
        var workflows = service.GetWorkflows();
        var builders = service.GetWorkflowBuilders();

        // Assert - verify each workflow's CompatibleResourceBaseModels matches its builder's metadata
        foreach (var workflow in workflows)
        {
            var builder = builders[workflow.Id];
            var expected = builder.Metadata.CompatibleResourceBaseModels;

            Assert.Equal(expected, workflow.CompatibleResourceBaseModels);
        }
    }

    [Fact]
    public void GetWorkflows_DeterministicId_ShouldBeUnaffectedByCompatibleResourceBaseModels()
    {
        // Arrange
        var service = CreateWorkflowService();
        var workflows = service.GetWorkflows();

        // Act & Assert - IDs should be deterministic from Base + Mode + Title only
        foreach (var workflow in workflows)
        {
            var expectedId = BlazorWebApp.Workflows.Models.WorkflowMetadata.GenerateDeterministicId(workflow.Base, workflow.Mode, workflow.Title);
            Assert.Equal(expectedId, workflow.Id);
        }
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
    public void NodeRegistry_GetReference_WithNonExistentKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => registry.GetReference("nonexistent"));
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

    #region ParameterConstraints Tests

    [Fact]
    public void ParameterConstraints_GetMin_ShouldReturnTypedValue()
    {
        // Arrange
        var constraints = new ParameterConstraints { Min = 1, Max = 150, Step = 1 };

        // Act & Assert
        Assert.Equal(1, constraints.GetMin<int>(0));
        Assert.Equal(1.0, constraints.GetMin<double>(0));
        Assert.Equal(150, constraints.GetMax<int>(0));
        Assert.Equal(1, constraints.GetStep<int>(0));
    }

    [Fact]
    public void ParameterConstraints_GetMin_WithNullValue_ShouldReturnDefault()
    {
        // Arrange
        var constraints = new ParameterConstraints();

        // Act & Assert
        Assert.Equal(42, constraints.GetMin<int>(42));
        Assert.Equal(99.9, constraints.GetMax<double>(99.9));
        Assert.Equal(5, constraints.GetStep<int>(5));
    }

    [Fact]
    public void FieldSchema_Validate_WithValidSlider_ShouldReturnNoErrors()
    {
        // Arrange
        var field = new FieldSchema
        {
            Parameter = "steps",
            Label = "Steps",
            Type = "slider",
            Min = 1,
            Max = 150
        };

        // Act
        var errors = field.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void FieldSchema_Validate_WithMissingSliderMinMax_ShouldReturnErrors()
    {
        // Arrange
        var field = new FieldSchema
        {
            Parameter = "steps",
            Label = "Steps",
            Type = "slider"
            // Missing min and max
        };

        // Act
        var errors = field.Validate();

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("min"));
        Assert.Contains(errors, e => e.Contains("max"));
    }

    [Fact]
    public void FieldSchema_Validate_WithSelectMissingOptions_ShouldReturnError()
    {
        // Arrange
        var field = new FieldSchema
        {
            Parameter = "mode",
            Label = "Mode",
            Type = "select"
            // Missing source and options
        };

        // Act
        var errors = field.Validate();

        // Assert
        Assert.Single(errors);
        Assert.Contains("source", errors[0]);
    }

    #endregion
}
