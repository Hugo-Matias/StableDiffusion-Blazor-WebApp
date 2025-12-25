using BlazorWebApp.Models;
using BlazorWebApp.Services.Templating.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BlazorWebApp.Tests.Services.Templating.Pipeline;

public class ComputeRegistryTests
{
    private readonly ComputeRegistry _sut;
    private readonly Mock<ILogger<ComputeRegistry>> _loggerMock;

    public ComputeRegistryTests()
    {
        _loggerMock = new Mock<ILogger<ComputeRegistry>>();
        _sut = new ComputeRegistry(_loggerMock.Object);
    }

    [Fact]
    public void IsComputeMarker_WithValidMarker_ReturnsTrue()
    {
        Assert.True(ComputeRegistry.IsComputeMarker("$compute:half_steps"));
        Assert.True(ComputeRegistry.IsComputeMarker("$compute:interpolated_framerate"));
        Assert.True(ComputeRegistry.IsComputeMarker("$COMPUTE:HALF_STEPS")); // Case insensitive
    }

    [Fact]
    public void IsComputeMarker_WithInvalidMarker_ReturnsFalse()
    {
        Assert.False(ComputeRegistry.IsComputeMarker(null));
        Assert.False(ComputeRegistry.IsComputeMarker(""));
        Assert.False(ComputeRegistry.IsComputeMarker("half_steps"));
        Assert.False(ComputeRegistry.IsComputeMarker("compute:half_steps")); // Missing $
    }

    [Fact]
    public void GetComputeFunctionName_ExtractsFunctionName()
    {
        Assert.Equal("half_steps", ComputeRegistry.GetComputeFunctionName("$compute:half_steps"));
        Assert.Equal("interpolated_framerate", ComputeRegistry.GetComputeFunctionName("$compute:interpolated_framerate"));
        Assert.Null(ComputeRegistry.GetComputeFunctionName("not_a_marker"));
    }

    [Fact]
    public void Compute_HalfSteps_ReturnsHalfOfSteps()
    {
        var parameters = new GenerationParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);

        var result = _sut.Compute("half_steps", parameters);

        Assert.Equal(10, result);
    }

    [Fact]
    public void Compute_HalfSteps_WithOddSteps_RoundsCorrectly()
    {
        var parameters = new GenerationParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 9);

        var result = _sut.Compute("half_steps", parameters);

        // 9/2 = 4.5, Math.Round uses banker's rounding which rounds to nearest even (4)
        Assert.Equal(4, result);
    }

    [Fact]
    public void Compute_HalfSteps_WithNoSteps_UsesDefault()
    {
        var parameters = new GenerationParameters();
        // No fragment set - should use default of 8

        var result = _sut.Compute("half_steps", parameters);

        Assert.Equal(4, result); // Default 8 / 2 = 4
    }

    [Fact]
    public void Compute_UnknownFunction_ReturnsNull()
    {
        var parameters = new GenerationParameters();

        var result = _sut.Compute("unknown_function", parameters);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveValue_WithComputeMarker_ComputesValue()
    {
        var parameters = new GenerationParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);

        var result = _sut.ResolveValue("$compute:half_steps", parameters);

        Assert.Equal(10, result);
    }

    [Fact]
    public void ResolveValue_WithNonMarker_ReturnsOriginalValue()
    {
        var parameters = new GenerationParameters();

        var result = _sut.ResolveValue(42, parameters);
        Assert.Equal(42, result);

        var stringResult = _sut.ResolveValue("regular string", parameters);
        Assert.Equal("regular string", stringResult);
    }

    [Fact]
    public void GetRegisteredFunctions_ReturnsAllFunctions()
    {
        var functions = _sut.GetRegisteredFunctions().ToList();

        Assert.Contains("half_steps", functions);
        Assert.Contains("interpolated_framerate", functions);
        Assert.Contains("double_steps", functions);
        Assert.Contains("upscale_width", functions);
        Assert.Contains("upscale_height", functions);
    }
}

public class ForeachProcessorTests
{
    private readonly ForeachProcessor _sut;
    private readonly ComputeRegistry _computeRegistry;
    private readonly Mock<ILogger<ForeachProcessor>> _loggerMock;
    private readonly Mock<ILogger<ComputeRegistry>> _computeLoggerMock;

    public ForeachProcessorTests()
    {
        _loggerMock = new Mock<ILogger<ForeachProcessor>>();
        _computeLoggerMock = new Mock<ILogger<ComputeRegistry>>();
        _computeRegistry = new ComputeRegistry(_computeLoggerMock.Object);
        _sut = new ForeachProcessor(_loggerMock.Object);
    }

    [Fact]
    public void CanProcess_WithForeachMarker_ReturnsTrue()
    {
        var json = """{"$foreach": "Loras", "$as": "lora", "$template": {}}""";
        using var doc = JsonDocument.Parse(json);

        Assert.True(_sut.CanProcess(doc.RootElement));
    }

    [Fact]
    public void CanProcess_WithoutForeachMarker_ReturnsFalse()
    {
        var json = """{"id": "test", "fragment": "test.liquid"}""";
        using var doc = JsonDocument.Parse(json);

        Assert.False(_sut.CanProcess(doc.RootElement));
    }

    [Fact]
    public void Process_WithEmptyCollection_ReturnsNoSteps()
    {
        var json = """
        {
            "$foreach": "Loras",
            "$as": "lora",
            "$template": {
                "id": "lora_{{ $index }}",
                "fragment": "lora-loader.liquid",
                "parameters": {}
            }
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var parameters = new GenerationParameters { Loras = new List<Lora>() };

        var result = _sut.Process(doc.RootElement, parameters, _computeRegistry).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Process_WithLoras_ExpandsTemplate()
    {
        var json = """
        {
            "$foreach": "Loras",
            "$as": "lora",
            "$template": {
                "id": "lora_{{ $index }}",
                "fragment": "lora-loader.liquid",
                "parameters": {
                    "lora_name": "{{ lora.Name }}",
                    "lora_strength": 0.8
                }
            }
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var parameters = new GenerationParameters
        {
            Loras = new List<Lora>
            {
                new Lora { Name = "test_lora_1", Strength = 0.8f, IsEnabled = true },
                new Lora { Name = "test_lora_2", Strength = 0.5f, IsEnabled = true }
            }
        };

        var result = _sut.Process(doc.RootElement, parameters, _computeRegistry).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("lora_0", result[0].Id);
        Assert.Equal("lora_1", result[1].Id);
        Assert.Equal("lora-loader.liquid", result[0].Fragment);
        Assert.Equal("test_lora_1", result[0].Parameters["lora_name"]);
        Assert.Equal("test_lora_2", result[1].Parameters["lora_name"]);
    }
}

public class ConditionalProcessorTests
{
    private readonly ConditionalProcessor _sut;
    private readonly ComputeRegistry _computeRegistry;
    private readonly Mock<ILogger<ConditionalProcessor>> _loggerMock;
    private readonly Mock<ILogger<ComputeRegistry>> _computeLoggerMock;

    public ConditionalProcessorTests()
    {
        _loggerMock = new Mock<ILogger<ConditionalProcessor>>();
        _computeLoggerMock = new Mock<ILogger<ComputeRegistry>>();
        _computeRegistry = new ComputeRegistry(_computeLoggerMock.Object);
        _sut = new ConditionalProcessor(_loggerMock.Object);
    }

    [Fact]
    public void CanProcess_WithIfMarker_ReturnsTrue()
    {
        var json = """{"$if": "detailer.IsActive", "id": "test", "fragment": "test.liquid"}""";
        using var doc = JsonDocument.Parse(json);

        Assert.True(_sut.CanProcess(doc.RootElement));
    }

    [Fact]
    public void CanProcess_WithoutIfMarker_ReturnsFalse()
    {
        var json = """{"id": "test", "fragment": "test.liquid"}""";
        using var doc = JsonDocument.Parse(json);

        Assert.False(_sut.CanProcess(doc.RootElement));
    }

    [Fact]
    public void Process_WithTrueCondition_ReturnsStep()
    {
        var json = """
        {
            "$if": "detailer.IsActive",
            "id": "detailer",
            "fragment": "detailer.liquid",
            "parameters": {
                "strength": 0.5
            }
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var parameters = new GenerationParameters();
        var detailerFragment = parameters.GetOrCreateFragment("detailer");
        detailerFragment.IsActive = true;

        var result = _sut.Process(doc.RootElement, parameters, _computeRegistry).ToList();

        Assert.Single(result);
        Assert.Equal("detailer", result[0].Id);
        Assert.Equal("detailer.liquid", result[0].Fragment);
    }

    [Fact]
    public void Process_WithFalseCondition_ReturnsNoSteps()
    {
        var json = """
        {
            "$if": "detailer.IsActive",
            "id": "detailer",
            "fragment": "detailer.liquid",
            "parameters": {}
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var parameters = new GenerationParameters();
        var detailerFragment = parameters.GetOrCreateFragment("detailer");
        detailerFragment.IsActive = false;

        var result = _sut.Process(doc.RootElement, parameters, _computeRegistry).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Process_WithMissingFragment_ReturnsNoSteps()
    {
        var json = """
        {
            "$if": "nonexistent_fragment.IsActive",
            "id": "test",
            "fragment": "test.liquid",
            "parameters": {}
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var parameters = new GenerationParameters();

        var result = _sut.Process(doc.RootElement, parameters, _computeRegistry).ToList();

        Assert.Empty(result);
    }
}

public class PipelineExpanderTests
{
    private readonly PipelineExpander _sut;
    private readonly ComputeRegistry _computeRegistry;

    public PipelineExpanderTests()
    {
        var expanderLoggerMock = new Mock<ILogger<PipelineExpander>>();
        var foreachLoggerMock = new Mock<ILogger<ForeachProcessor>>();
        var conditionalLoggerMock = new Mock<ILogger<ConditionalProcessor>>();
        var computeLoggerMock = new Mock<ILogger<ComputeRegistry>>();

        _computeRegistry = new ComputeRegistry(computeLoggerMock.Object);

        var processors = new List<IPipelineProcessor>
        {
            new ForeachProcessor(foreachLoggerMock.Object),
            new ConditionalProcessor(conditionalLoggerMock.Object)
        };

        _sut = new PipelineExpander(expanderLoggerMock.Object, processors, _computeRegistry);
    }

    [Fact]
    public void ExpandPipeline_WithRegularSteps_ReturnsSteps()
    {
        var json = """
        [
            { "id": "step1", "fragment": "fragment1.liquid", "parameters": { "value": 10 } },
            { "id": "step2", "fragment": "fragment2.liquid", "parameters": { "value": 20 } }
        ]
        """;



        var parameters = new GenerationParameters();

        var result = _sut.ExpandPipeline(json, parameters);

        Assert.Equal(2, result.Count);
        Assert.Equal("step1", result[0].Id);
        Assert.Equal("step2", result[1].Id);
        // JSON numbers come through as int
        Assert.Equal(10, Convert.ToInt32(result[0].Parameters["value"]));
        Assert.Equal(20, Convert.ToInt32(result[1].Parameters["value"]));
    }

    [Fact]
    public void ExpandPipeline_WithComputeMarker_ResolvesValue()
    {
        var json = """
        [
            { 
                "id": "sampler", 
                "fragment": "sampler.liquid", 
                "parameters": { 
                    "end_at_step": "$compute:half_steps" 
                } 
            }
        ]
        """;

        var parameters = new GenerationParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);

        var result = _sut.ExpandPipeline(json, parameters);

        Assert.Single(result);
        Assert.Equal(10, result[0].Parameters["end_at_step"]);
    }

    [Fact]
    public void ExpandPipeline_WithMixedSteps_HandlesAll()
    {
        var json = """
        [
            { "id": "loader", "fragment": "loader.liquid", "parameters": {} },
            { 
                "$if": "upscale.IsActive",
                "id": "upscale", 
                "fragment": "upscale.liquid", 
                "parameters": {} 
            },
            { "id": "save", "fragment": "save.liquid", "parameters": {} }
        ]
        """;

        var parameters = new GenerationParameters();
        var upscaleFragment = parameters.GetOrCreateFragment("upscale");
        upscaleFragment.IsActive = true;

        var result = _sut.ExpandPipeline(json, parameters);

        Assert.Equal(3, result.Count);
        Assert.Equal("loader", result[0].Id);
        Assert.Equal("upscale", result[1].Id);
        Assert.Equal("save", result[2].Id);
    }

    [Fact]
    public void ExpandPipeline_OrderIsPreserved()
    {
        var json = """
        [
            { "id": "step1", "fragment": "f1.liquid", "parameters": {} },
            { "id": "step2", "fragment": "f2.liquid", "parameters": {} },
            { "id": "step3", "fragment": "f3.liquid", "parameters": {} }
        ]
        """;

        var parameters = new GenerationParameters();

        var result = _sut.ExpandPipeline(json, parameters);

        Assert.Equal(0, result[0].Order);
        Assert.Equal(1, result[1].Order);
        Assert.Equal(2, result[2].Order);
    }
}
