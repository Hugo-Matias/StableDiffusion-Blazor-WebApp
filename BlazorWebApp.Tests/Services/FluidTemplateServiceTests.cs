using BlazorWebApp.Models;
using BlazorWebApp.Services.Templating;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class FluidTemplateServiceTests
{
    private readonly FluidTemplateService _service;
    private readonly Mock<ILogger<FluidTemplateService>> _mockLogger;

    public FluidTemplateServiceTests()
    {
        _mockLogger = new Mock<ILogger<FluidTemplateService>>();
        _service = new FluidTemplateService(_mockLogger.Object);
    }

    #region Basic Rendering Tests

    [Fact]
    public async Task RenderAsync_WithSimpleTemplate_ShouldRenderCorrectly()
    {
        // Arrange
        var template = "Hello, {{ name }}!";
        var parameters = new Dictionary<string, object?> { { "name", "World" } };

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("Hello, World!", rendered);
        Assert.Null(metadata);
    }

    [Fact]
    public async Task RenderAsync_WithMissingVariable_ShouldRenderEmpty()
    {
        // Arrange
        var template = "Hello, {{ name }}!";
        var parameters = new Dictionary<string, object?>();

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("Hello, !", rendered);
        Assert.Null(metadata);
    }

    #endregion

    #region Meta Block Tests

    [Fact]
    public async Task RenderAsync_WithMetaBlock_ShouldCaptureMetadataAndNotOutput()
    {
        // Arrange
        var template = @"{% meta %}
{
  ""outputs"": {
    ""model_output"": { ""node"": ""loader"", ""index"": 0 }
  }
}
{% endmeta %}
{""main"": ""content""}";
        var parameters = new Dictionary<string, object?>();

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.NotNull(metadata);
        Assert.Contains("outputs", metadata);
        Assert.Contains("model_output", metadata);
        Assert.DoesNotContain("outputs", rendered); // Meta should NOT be in output
        Assert.Contains("main", rendered); // Main content should be in output
    }

    [Fact]
    public async Task RenderAsync_WithMetaBlockContainingVariables_ShouldRenderVariablesInMeta()
    {
        // Arrange
        var template = @"{% meta %}
{
  ""outputs"": {
    ""{{ output_name }}"": { ""node"": ""{{ node_id }}"", ""index"": 0 }
  }
}
{% endmeta %}
{""test"": true}";
        var parameters = new Dictionary<string, object?>
        {
            { "output_name", "dynamic_output" },
            { "node_id", "node_123" }
        };

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.NotNull(metadata);
        Assert.Contains("dynamic_output", metadata);
        Assert.Contains("node_123", metadata);
    }

    [Fact]
    public async Task RenderAsync_WithNoMetaBlock_ShouldReturnNullMetadata()
    {
        // Arrange
        var template = @"{""simple"": ""template""}";
        var parameters = new Dictionary<string, object?>();

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Null(metadata);
        Assert.Contains("simple", rendered);
    }

    #endregion

    #region JSON Filter Tests

    [Fact]
    public async Task RenderAsync_WithJsonFilter_ShouldEncodeString()
    {
        // Arrange
        var template = @"{ ""value"": {{ name | json }} }";
        var parameters = new Dictionary<string, object?> { { "name", "test \"quoted\" value" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        // System.Text.Json uses Unicode escaping \u0022 for quotes
        Assert.Contains("test", rendered);
        Assert.Contains("quoted", rendered);
        Assert.Contains("value", rendered);
        // Verify it's properly quoted as a JSON string
        Assert.Contains("\"test", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithJsonFilter_ShouldEncodeNumber()
    {
        // Arrange
        var template = @"{ ""value"": {{ count | json }} }";
        var parameters = new Dictionary<string, object?> { { "count", 42 } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("42", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithJsonFilter_ShouldEncodeBoolean()
    {
        // Arrange
        var template = @"{ ""enabled"": {{ flag | json }} }";
        var parameters = new Dictionary<string, object?> { { "flag", true } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("true", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithJsonFilter_ShouldEncodeNull()
    {
        // Arrange
        var template = @"{ ""value"": {{ nothing | json }} }";
        var parameters = new Dictionary<string, object?> { { "nothing", null } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("null", rendered);
    }

    #endregion

    #region Get Ref Tag Tests

    [Fact]
    public async Task RenderAsync_WithGetRefTag_ShouldResolveNodeReference()
    {
        // Arrange
        var template = @"{ ""input"": {% get_ref ""model_output"" %} }";
        var parameters = new Dictionary<string, object?>();
        var nodeRegistry = new NodeRegistry();
        nodeRegistry.Register("model_output", "node_1", 0);

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters, nodeRegistry);

        // Assert
        Assert.Contains("[\"node_1\", 0]", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithGetRefTag_WithDifferentIndex_ShouldResolveCorrectly()
    {
        // Arrange
        var template = @"{ ""input"": {% get_ref ""latent_output"" %} }";
        var parameters = new Dictionary<string, object?>();
        var nodeRegistry = new NodeRegistry();
        nodeRegistry.Register("latent_output", "sampler_node", 2);

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters, nodeRegistry);

        // Assert
        Assert.Contains("[\"sampler_node\", 2]", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithGetRefTag_WithMissingRegistry_ShouldOutputUnresolved()
    {
        // Arrange
        var template = @"{ ""input"": {% get_ref ""missing_key"" %} }";
        var parameters = new Dictionary<string, object?>();
        // No NodeRegistry provided

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("UNRESOLVED", rendered);
    }

    #endregion

    #region String Contains Filter Tests

    [Fact]
    public async Task RenderAsync_WithStringContainsFilter_ShouldReturnTrue()
    {
        // Arrange - In Liquid, filters in conditionals require assign first
        var template = @"{% assign contains_sdxl = model_name | string_contains: ""SDXL"" %}{% if contains_sdxl %}is_sdxl{% endif %}";
        var parameters = new Dictionary<string, object?> { { "model_name", "my_SDXL_model.safetensors" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("is_sdxl", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithStringContainsFilter_ShouldReturnFalse()
    {
        // Arrange
        var template = @"{% assign contains_sdxl = model_name | string_contains: ""SDXL"" %}{% if contains_sdxl %}is_sdxl{% else %}not_sdxl{% endif %}";
        var parameters = new Dictionary<string, object?> { { "model_name", "sd15_model.safetensors" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("not_sdxl", rendered);
        Assert.DoesNotContain("is_sdxl", rendered);
    }

    #endregion

    #region Default Filter Tests

    [Fact]
    public async Task RenderAsync_WithDefaultFilter_ShouldUseValueWhenPresent()
    {
        // Arrange
        var template = @"{{ node_prefix | default: ""model"" }}_loader";
        var parameters = new Dictionary<string, object?> { { "node_prefix", "custom" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("custom_loader", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithDefaultFilter_ShouldUseDefaultWhenMissing()
    {
        // Arrange
        var template = @"{{ node_prefix | default: ""model"" }}_loader";
        var parameters = new Dictionary<string, object?>();

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("model_loader", rendered);
    }

    #endregion

    #region Append Filter Tests (String Concatenation)

    [Fact]
    public async Task RenderAsync_WithAppendFilter_ShouldConcatenateStrings()
    {
        // Arrange
        var template = @"{{ node_prefix | default: ""model"" | append: ""_unet_loader"" }}";
        var parameters = new Dictionary<string, object?> { { "node_prefix", "wan" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("wan_unet_loader", rendered);
    }

    #endregion

    #region Case Insensitive Parameter Access Tests

    [Fact]
    public async Task RenderAsync_WithSnakeCaseParameter_ShouldAccessWithPascalCase()
    {
        // Arrange - Parameter provided as snake_case, template uses PascalCase
        var template = @"{{ NodePrefix }}_loader";
        var parameters = new Dictionary<string, object?> { { "node_prefix", "test" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("test_loader", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithPascalCaseParameter_ShouldAccessWithSnakeCase()
    {
        // Arrange - Parameter provided as PascalCase, template uses snake_case
        var template = @"{{ node_prefix }}_loader";
        var parameters = new Dictionary<string, object?> { { "NodePrefix", "test" } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("test_loader", rendered);
    }

    #endregion

    #region Loop Tests

    [Fact]
    public async Task RenderAsync_WithForLoop_ShouldIterateCorrectly()
    {
        // Arrange
        var template = @"{% for item in items %}{{ item }}{% unless forloop.last %},{% endunless %}{% endfor %}";
        var parameters = new Dictionary<string, object?> 
        { 
            { "items", new List<string> { "a", "b", "c" } } 
        };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("a,b,c", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithForLoopIndex_ShouldProvideIndex()
    {
        // Arrange
        var template = @"{% for item in items %}{{ forloop.index0 }}:{{ item }} {% endfor %}";
        var parameters = new Dictionary<string, object?> 
        { 
            { "items", new List<string> { "x", "y", "z" } } 
        };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Contains("0:x", rendered);
        Assert.Contains("1:y", rendered);
        Assert.Contains("2:z", rendered);
    }

    #endregion

    #region Conditional Tests

    [Fact]
    public async Task RenderAsync_WithIfCondition_ShouldEvaluateTrue()
    {
        // Arrange
        var template = @"{% if enabled %}YES{% else %}NO{% endif %}";
        var parameters = new Dictionary<string, object?> { { "enabled", true } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("YES", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithIfCondition_ShouldEvaluateFalse()
    {
        // Arrange
        var template = @"{% if enabled %}YES{% else %}NO{% endif %}";
        var parameters = new Dictionary<string, object?> { { "enabled", false } };

        // Act
        var (rendered, _) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.Equal("NO", rendered);
    }

    #endregion

    #region Cache Tests

    [Fact]
    public async Task ClearCache_ShouldNotThrow()
    {
        // Arrange - Render something to populate cache
        await _service.RenderAsync("{{ x }}", new Dictionary<string, object?> { { "x", 1 } });

        // Act & Assert - Should not throw
        _service.ClearCache();
    }

    #endregion

    #region Context Tests

    [Fact]
    public void CreateContext_ShouldReturnContextWithParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "key", "value" } };
        var registry = new NodeRegistry();

        // Act
        var context = _service.CreateContext(parameters, registry);

        // Assert
        Assert.NotNull(context);
        Assert.Equal("value", context.Parameters["key"]);
        Assert.Same(registry, context.NodeRegistry);
    }

    [Fact]
    public async Task RenderAsync_WithFluidRenderContext_ShouldCaptureMetadata()
    {
        // Arrange
        var template = @"{% meta %}{""captured"": true}{% endmeta %}body";
        var context = _service.CreateContext(new Dictionary<string, object?>());

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, context);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(context.CapturedMetadata);
        Assert.Equal(metadata, context.CapturedMetadata);
        Assert.Contains("captured", metadata);
    }

    #endregion

    #region Complex Fragment Simulation Tests

    [Fact]
    public async Task RenderAsync_WithRealisticFragment_ShouldRenderCorrectly()
    {
        // Arrange - Simulate a real fragment with meta block and JSON body
        var template = @"{% meta %}
{
  ""outputs"": {
    ""{{ scope }}latent_output"": { ""node"": ""{{ scope }}sampler"", ""index"": 0 }
  },
  ""conditions"": {
    ""required"": [""sampler_enabled""]
  }
}
{% endmeta %}
""{{ scope }}sampler"": {
  ""class_type"": ""KSampler"",
  ""inputs"": {
    ""model"": {% get_ref ""model_output"" %},
    ""seed"": {{ seed | json }},
    ""steps"": {{ steps }},
    ""cfg"": {{ cfg }},
    ""sampler_name"": {{ sampler_name | json }},
    ""scheduler"": {{ scheduler | json }},
    ""denoise"": {{ denoise }}
  }
}";
        var parameters = new Dictionary<string, object?>
        {
            { "scope", "txt2img_" },
            { "seed", 12345 },
            { "steps", 20 },
            { "cfg", 7.5 },
            { "sampler_name", "euler_ancestral" },
            { "scheduler", "normal" },
            { "denoise", 1.0 }
        };
        var nodeRegistry = new NodeRegistry();
        nodeRegistry.Register("model_output", "load_checkpoint", 0);

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters, nodeRegistry);

        // Assert - Metadata captured correctly
        Assert.NotNull(metadata);
        Assert.Contains("txt2img_latent_output", metadata);
        Assert.Contains("txt2img_sampler", metadata);
        Assert.Contains("sampler_enabled", metadata);

        // Assert - Body rendered correctly
        Assert.Contains("\"txt2img_sampler\"", rendered);
        Assert.Contains("KSampler", rendered);
        Assert.Contains("[\"load_checkpoint\", 0]", rendered);
        Assert.Contains("12345", rendered);
        Assert.Contains("\"euler_ancestral\"", rendered);
        
        // Assert - Meta block NOT in output
        Assert.DoesNotContain("outputs", rendered);
        Assert.DoesNotContain("conditions", rendered);
    }

    #endregion

    #region Real Fragment Tests

    [Fact]
    public async Task RenderAsync_WithSaveFragmentSyntax_ShouldRenderCorrectly()
    {
        // Arrange - Simulates save.sbn structure
        var template = @"{% meta %}
{
  ""outputs"": {
    ""save_node"": { ""node"": ""save"", ""index"": 0 }
  }
}
{% endmeta %}

{
  ""save"": {
    ""inputs"": {
      ""filename_prefix"": {{ filename_prefix | default: ""tmp/img"" | json }},
      ""images"": {% get_ref ""image_output"" %}
    },
    ""class_type"": ""SaveImage""
  }
}";
        var parameters = new Dictionary<string, object?> { { "filename_prefix", "my_output" } };
        var nodeRegistry = new NodeRegistry();
        nodeRegistry.Register("image_output", "vae_decoder", 0);

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters, nodeRegistry);

        // Assert
        Assert.NotNull(metadata);
        Assert.Contains("save_node", metadata);
        Assert.Contains("\"my_output\"", rendered);
        Assert.Contains("[\"vae_decoder\", 0]", rendered);
        Assert.DoesNotContain("outputs", rendered); // meta should NOT be in output
    }

    [Fact]
    public async Task RenderAsync_WithEmptyLatentFragmentSyntax_ShouldRenderCorrectly()
    {
        // Arrange - Simulates empty-latent.sbn structure
        var template = @"{% meta %}
{
  ""outputs"": {
    ""{% if scope %}{{ scope }}{% endif %}latent_output"": { ""node"": ""{% if scope %}{{ scope }}{% endif %}empty_latent"", ""index"": 0 }
  }
}
{% endmeta %}

{
  ""{% if scope %}{{ scope }}{% endif %}empty_latent"": {
    ""inputs"": {
      ""width"": {{ width | json }},
      ""height"": {{ height | json }},
      ""batch_size"": {{ batch_size | json }}
    },
    ""class_type"": {{ latent_class | default: ""EmptySD3LatentImage"" | json }}
  }
}";
        var parameters = new Dictionary<string, object?>
        {
            { "scope", "txt2img_" },
            { "width", 1024 },
            { "height", 768 },
            { "batch_size", 1 }
        };

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters);

        // Assert
        Assert.NotNull(metadata);
        Assert.Contains("txt2img_latent_output", metadata);
        Assert.Contains("txt2img_empty_latent", metadata);
        Assert.Contains("\"txt2img_empty_latent\"", rendered);
        Assert.Contains("1024", rendered);
        Assert.Contains("768", rendered);
        Assert.Contains("\"EmptySD3LatentImage\"", rendered);
    }

    [Fact]
    public async Task RenderAsync_WithVaeDecodeFragmentSyntax_ShouldRenderCorrectly()
    {
        // Arrange - Simulates vae-decode.sbn structure
        var template = @"{% meta %}
{
  ""outputs"": {
    ""image_output"": { ""node"": ""vae_decoder"", ""index"": 0 }
  }
}
{% endmeta %}

{
  ""vae_decoder"": {
    ""inputs"": {
      ""samples"": {% get_ref ""latent_output"" %},
      ""vae"": {% assign vae_key = scope | default: """" | append: ""vae_output"" %}{% get_ref vae_key %}
    },
    ""class_type"": ""VAEDecode""
  }
}";
        var parameters = new Dictionary<string, object?> { { "scope", "txt2img_" } };
        var nodeRegistry = new NodeRegistry();
        nodeRegistry.Register("latent_output", "sampler_node", 0);
        nodeRegistry.Register("txt2img_vae_output", "vae_loader", 0);

        // Act
        var (rendered, metadata) = await _service.RenderAsync(template, parameters, nodeRegistry);

        // Assert
        Assert.NotNull(metadata);
        Assert.Contains("image_output", metadata);
        Assert.Contains("[\"sampler_node\", 0]", rendered);
        Assert.Contains("[\"vae_loader\", 0]", rendered);
    }

    #endregion
}
