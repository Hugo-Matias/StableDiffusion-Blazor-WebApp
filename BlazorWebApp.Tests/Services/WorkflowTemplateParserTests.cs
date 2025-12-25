using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Services.Templating;
using Microsoft.Extensions.Logging;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class WorkflowTemplateParserTests
{
    private readonly WorkflowTemplateParser _sutSync;
    private readonly WorkflowTemplateParser _sutAsync;
    private readonly Mock<ILogger<WorkflowTemplateParser>> _loggerMock;
    private readonly FluidTemplateService _fluidService;

    public WorkflowTemplateParserTests()
    {
        _loggerMock = new Mock<ILogger<WorkflowTemplateParser>>();
        
        // Sync parser (no Fluid service)
        _sutSync = new WorkflowTemplateParser(_loggerMock.Object);
        
        // Async parser (with Fluid service)
        var fluidLoggerMock = new Mock<ILogger<FluidTemplateService>>();
        _fluidService = new FluidTemplateService(fluidLoggerMock.Object);
        _sutAsync = new WorkflowTemplateParser(_loggerMock.Object, _fluidService);
    }

    #region Sync Parsing Tests (Legacy)

    [Fact]
    public void ParseWorkflowTemplate_ParsesBasicMetadata()
    {
        var template = """
        {
          "Title": "Test Workflow",
          "Base": "Flux",
          "Mode": "txt2img",
          "Assets": [],
          "Pipeline": []
        }
        """;

        var result = _sutSync.ParseWorkflowTemplate(template);

        Assert.Equal("Test Workflow", result.Title);
        Assert.Equal(ModelBase.Flux, result.Base);
        Assert.Equal(ModeType.Txt2Img, result.Mode);
    }

    [Fact]
    public void ParseWorkflowTemplate_ParsesAssets()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "Flux",
          "Mode": "txt2img",
          "Assets": [
            { "parameter": "Model", "label": "Main Model", "type": "CheckpointModel", "default": "model.safetensors", "order": 1, "columnSize": 6 }
          ],
          "Pipeline": []
        }
        """;

        var result = _sutSync.ParseWorkflowTemplate(template);

        Assert.NotNull(result.Assets);
        Assert.Single(result.Assets);
        Assert.Equal("Model", result.Assets[0].Parameter);
        Assert.Equal("Main Model", result.Assets[0].Label);
        Assert.Equal(AssetType.CheckpointModel, result.Assets[0].Type);
        Assert.Equal("model.safetensors", result.Assets[0].DefaultValue);
        Assert.Equal(1, result.Assets[0].Order);
        Assert.Equal(6, result.Assets[0].ColumnSize);
    }

    [Fact]
    public void ParseWorkflowTemplate_ParsesSources()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "Wan",
          "Mode": "img2vid",
          "Sources": [
            { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
          ],
          "Pipeline": []
        }
        """;

        var result = _sutSync.ParseWorkflowTemplate(template);

        Assert.NotNull(result.Sources);
        Assert.Single(result.Sources);
        Assert.Equal("source_image", result.Sources[0].Id);
        Assert.Equal("Source Image", result.Sources[0].Label);
        Assert.Equal("image", result.Sources[0].Type);
        Assert.True(result.Sources[0].Required);
    }

    [Fact]
    public void GenerateDeterministicGuid_ProducesSameGuidForSameInput()
    {
        var input = "Test_Flux_txt2img";
        
        var guid1 = WorkflowTemplateParser.GenerateDeterministicGuid(input);
        var guid2 = WorkflowTemplateParser.GenerateDeterministicGuid(input);
        
        Assert.Equal(guid1, guid2);
    }

    [Fact]
    public void GenerateDeterministicGuid_ProducesDifferentGuidsForDifferentInputs()
    {
        var guid1 = WorkflowTemplateParser.GenerateDeterministicGuid("Test1_Flux_txt2img");
        var guid2 = WorkflowTemplateParser.GenerateDeterministicGuid("Test2_Flux_txt2img");
        
        Assert.NotEqual(guid1, guid2);
    }

    #endregion

    #region Async Parsing Tests (JSON-based)

    [Fact]
    public async Task ParseWorkflowTemplateAsync_ParsesBasicMetadata()
    {
        // This template uses Fluid syntax that renders to valid JSON
        var template = """
        {
          "Title": "Test Workflow",
          "Base": "Flux",
          "Mode": "txt2img",
          "Assets": [],
          "Pipeline": []
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        Assert.Equal("Test Workflow", result.Title);
        Assert.Equal(ModelBase.Flux, result.Base);
        Assert.Equal(ModeType.Txt2Img, result.Mode);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_ParsesAssetsFromJson()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "StableDiffusion",
          "Mode": "txt2img",
          "Assets": [
            { "parameter": "Model", "label": "Main Model", "type": "CheckpointModel", "default": "model.safetensors", "order": 1, "columnSize": 6 }
          ],
          "Pipeline": []
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        Assert.NotNull(result.Assets);
        Assert.Single(result.Assets);
        Assert.Equal("Model", result.Assets[0].Parameter);
        Assert.Equal("Main Model", result.Assets[0].Label);
        Assert.Equal(AssetType.CheckpointModel, result.Assets[0].Type);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_ParsesSourcesFromJson()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "Wan",
          "Mode": "img2vid",
          "Assets": [],
          "Sources": [
            { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
          ],
          "Pipeline": []
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        Assert.NotNull(result.Sources);
        Assert.Single(result.Sources);
        Assert.Equal("source_image", result.Sources[0].Id);
        Assert.Equal("Source Image", result.Sources[0].Label);
        Assert.True(result.Sources[0].Required);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_ParsesPipelineSteps()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "Flux",
          "Mode": "txt2img",
          "Assets": [],
          "Pipeline": [
            { "id": "loader", "fragment": "load-checkpoint.liquid", "parameters": {} },
            { "id": "sampler", "fragment": "sampler.liquid", "parameters": {} }
          ]
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        Assert.NotNull(result.Pipeline);
        Assert.Equal(2, result.Pipeline.Count);
        Assert.Equal("loader", result.Pipeline[0].Id);
        Assert.Equal("load-checkpoint.liquid", result.Pipeline[0].Fragment);
        Assert.Equal("sampler", result.Pipeline[1].Id);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_SkipsDynamicMarkers()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "Wan",
          "Mode": "img2vid",
          "Assets": [],
          "Pipeline": [
            { "id": "loader", "fragment": "loader.liquid", "parameters": {} },
            { "$foreach": "Loras", "$as": "lora", "$template": { "id": "lora", "fragment": "lora.liquid", "parameters": {} } },
            { "$if": "upscale.IsActive", "id": "upscale", "fragment": "upscale.liquid", "parameters": {} },
            { "id": "save", "fragment": "save.liquid", "parameters": {} }
          ]
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        // Should only include static steps (loader and save)
        Assert.NotNull(result.Pipeline);
        Assert.Equal(2, result.Pipeline.Count);
        Assert.Equal("loader", result.Pipeline[0].Id);
        Assert.Equal("save", result.Pipeline[1].Id);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_WithFluidSyntax_RendersCorrectly()
    {
        // Template with Fluid syntax that uses safe defaults
        var template = """
        {
          "Title": "Fluid Test",
          "Base": "Flux",
          "Mode": "txt2img",
          "Assets": [
            { "parameter": "Model", "label": "Model", "type": "DiffusionModel", "default": "flux.safetensors" }
          ],
          "Pipeline": [
            {
              "id": "sampler",
              "fragment": "sampler.liquid",
              "parameters": {
                "steps": {{ steps | default: 20 | json }},
                "seed": {{ seed | default: 42 | json }}
              }
            }
          ]
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        Assert.Equal("Fluid Test", result.Title);
        Assert.NotNull(result.Pipeline);
        Assert.Single(result.Pipeline);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_PreservesRawJson()
    {
        var template = """
        {
          "Title": "Test",
          "Base": "Flux",
          "Mode": "txt2img",
          "Pipeline": []
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        // RawJson should contain the original template text (not rendered)
        Assert.Equal(template, result.RawJson);
    }

    [Fact]
    public async Task ParseWorkflowTemplateAsync_FallsBackToRegexOnJsonError()
    {
        // Template with Scriban syntax that won't produce valid JSON with safe defaults
        // This should fallback to regex parsing
        var template = """
        {
          "Title": "Scriban Test",
          "Base": "Wan",
          "Mode": "img2vid",
          "Assets": [],
          "Pipeline": [
            {{~ for lora in Loras ~}}
            { "id": "lora_{{ for.index }}", "fragment": "lora.liquid" },
            {{~ end ~}}
            { "id": "save", "fragment": "save.liquid" }
          ]
        }
        """;

        var result = await _sutAsync.ParseWorkflowTemplateAsync(template);

        // Should still parse the title via fallback
        Assert.Equal("Scriban Test", result.Title);
    }

    #endregion
}
