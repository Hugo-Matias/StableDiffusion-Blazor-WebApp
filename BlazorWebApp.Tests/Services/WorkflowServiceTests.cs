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
    private readonly Mock<ILogger<WorkflowTemplateParser>> _mockParserLogger;
    private readonly Mock<ILogger<FragmentSchemaService>> _mockSchemaLogger;
    private readonly Mock<ILogger<TemplateCacheService>> _mockCacheLogger;

    public WorkflowServiceTests()
    {
        _mockLogger = new Mock<ILogger<WorkflowService>>();
        _mockParserLogger = new Mock<ILogger<WorkflowTemplateParser>>();
        _mockSchemaLogger = new Mock<ILogger<FragmentSchemaService>>();
        _mockCacheLogger = new Mock<ILogger<TemplateCacheService>>();
    }

    private WorkflowService CreateWorkflowService(IIOService ioService)
    {
        var templateParser = new WorkflowTemplateParser(_mockParserLogger.Object);
        var fragmentSchemaService = new FragmentSchemaService(_mockSchemaLogger.Object);
        var templateCacheService = new TemplateCacheService(_mockCacheLogger.Object);
        return new WorkflowService(ioService, _mockLogger.Object, templateParser, fragmentSchemaService, templateCacheService,
            new FragmentConditionValidator(new Mock<ILogger<FragmentConditionValidator>>().Object));
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
        var service = CreateWorkflowService(ioService);
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

    #region Fragment Schema Parsing Tests

    [Fact]
    public void ParseFragmentSchema_WithValidUISchema_ShouldReturnSchema()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var ioService = new IOService(mockConfig.Object);
        var service = CreateWorkflowService(ioService);

        var fragmentText = @"
#meta
{
  ""outputs"": {
    ""latent_output"": {""node"": ""sampler"", ""index"": 0}
  },
  ""ui"": {
    ""component"": ""SamplerForm"",
    ""title"": ""Sampler"",
    ""icon"": ""fa-solid fa-dice"",
    ""order"": 50,
    ""collapsible"": true,
    ""chainable"": true,
    ""parameters"": {
      ""steps"": { ""min"": 1, ""max"": 150, ""step"": 1 },
      ""cfg"": { ""min"": 1, ""max"": 30, ""step"": 0.5 },
      ""sampler_name"": { ""source"": ""Backend.Samplers"" }
    }
  }
}
#end

{ ""test"": ""node"" }";

        // Act
        var schema = service.ParseFragmentSchema(fragmentText);

        // Assert
        Assert.NotNull(schema);
        Assert.Equal("SamplerForm", schema.Component);
        Assert.Equal("Sampler", schema.Title);
        Assert.Equal("fa-solid fa-dice", schema.Icon);
        Assert.Equal(50, schema.Order);
        Assert.True(schema.Collapsible);
        Assert.True(schema.Chainable);
        Assert.False(schema.DefaultCollapsed);
        
        // Check parameters
        Assert.True(schema.Parameters.ContainsKey("steps"));
        Assert.Equal(1, schema.Parameters["steps"].Min);
        Assert.Equal(150, schema.Parameters["steps"].Max);
        Assert.Equal(1, schema.Parameters["steps"].Step);
        
        Assert.True(schema.Parameters.ContainsKey("sampler_name"));
        Assert.Equal("Backend.Samplers", schema.Parameters["sampler_name"].Source);
    }

    [Fact]
    public void ParseFragmentSchema_WithNoUIBlock_ShouldReturnNull()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var ioService = new IOService(mockConfig.Object);
        var service = CreateWorkflowService(ioService);

        var fragmentText = @"
#meta
{
  ""outputs"": {
    ""model_output"": {""node"": ""loader"", ""index"": 0}
  }
}
#end

{ ""test"": ""node"" }";

        // Act
        var schema = service.ParseFragmentSchema(fragmentText);

        // Assert
        Assert.Null(schema);
    }

    [Fact]
    public void ParseFragmentSchema_WithNoMetaBlock_ShouldReturnNull()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var ioService = new IOService(mockConfig.Object);
        var service = CreateWorkflowService(ioService);

        var fragmentText = @"{ ""test"": ""node"" }";

        // Act
        var schema = service.ParseFragmentSchema(fragmentText);

        // Assert
        Assert.Null(schema);
    }

    [Fact]
    public void ParseFragmentSchema_WithDynamicFields_ShouldParseFields()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var ioService = new IOService(mockConfig.Object);
        var service = CreateWorkflowService(ioService);

        var fragmentText = @"
#meta
{
  ""outputs"": {},
  ""ui"": {
    ""component"": null,
    ""title"": ""Experimental Node"",
    ""collapsible"": true,
    ""fields"": [
      { ""parameter"": ""strength"", ""label"": ""Strength"", ""type"": ""slider"", ""min"": 0, ""max"": 1, ""step"": 0.01 },
      { ""parameter"": ""mode"", ""label"": ""Mode"", ""type"": ""select"", ""options"": [""fast"", ""quality""] }
    ]
  }
}
#end

{ ""test"": ""node"" }";

        // Act
        var schema = service.ParseFragmentSchema(fragmentText);

        // Assert
        Assert.NotNull(schema);
        Assert.Null(schema.Component);
        Assert.Equal("Experimental Node", schema.Title);
        Assert.True(schema.UsesDynamicFields);
        Assert.False(schema.HasDesignedComponent);
        
        // Check fields
        Assert.NotNull(schema.Fields);
        Assert.Equal(2, schema.Fields.Count);
        
        var strengthField = schema.Fields[0];
        Assert.Equal("strength", strengthField.Parameter);
        Assert.Equal("Strength", strengthField.Label);
        Assert.Equal("slider", strengthField.Type);
        Assert.Equal(0, strengthField.Min);
        Assert.Equal(1, strengthField.Max);
        Assert.Equal(0.01, strengthField.Step);
        
        var modeField = schema.Fields[1];
        Assert.Equal("mode", modeField.Parameter);
        Assert.Equal("select", modeField.Type);
        Assert.NotNull(modeField.Options);
        Assert.Equal(2, modeField.Options.Count);
        Assert.Contains("fast", modeField.Options);
        Assert.Contains("quality", modeField.Options);
    }

    [Fact]
    public void ParseFragmentSchema_WithDefaultCollapsed_ShouldParse()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var ioService = new IOService(mockConfig.Object);
        var service = CreateWorkflowService(ioService);

        var fragmentText = @"
#meta
{
  ""outputs"": {},
  ""ui"": {
    ""component"": ""DetailerForm"",
    ""title"": ""Detailer"",
    ""collapsible"": true,
    ""defaultCollapsed"": true,
    ""parameters"": {}
  }
}
#end

{ ""test"": ""node"" }";

        // Act
        var schema = service.ParseFragmentSchema(fragmentText);

        // Assert
        Assert.NotNull(schema);
        Assert.True(schema.Collapsible);
        Assert.True(schema.DefaultCollapsed);
    }

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
