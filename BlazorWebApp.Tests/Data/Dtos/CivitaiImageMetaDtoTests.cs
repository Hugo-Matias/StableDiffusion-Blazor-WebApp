using System.Text.Json;
using BlazorWebApp.Components.Resources;
using BlazorWebApp.Data.Dtos;
using FluentAssertions;

namespace BlazorWebApp.Tests.Civitai;

public class CivitaiImageMetaDtoTests
{
    [Fact]
    public void Constructor_WithNestedWrapperMetadata_ReadsGenerationFields()
    {
        using var document = JsonDocument.Parse("""
        {
          "id": 129864601,
          "meta": {
            "seed": "1234567890123",
            "Model": "Synthetic Model",
            "steps": "12",
            "width": 832,
            "height": "1216",
            "prompt": "synthetic positive prompt",
            "negativePrompt": "synthetic negative prompt",
            "sampler": "Euler",
            "scheduler": "simple",
            "cfgScale": "1.5",
            "denoise": 0.65,
            "models": ["Synthetic Model", { "name": "Aux Model" }],
            "vaes": ["Synthetic VAE"],
            "resources": [
              { "name": "Synthetic LoRA", "type": "LORA", "weight": "0.7", "hash": "abc123" }
            ],
            "comfy": "{\"workflow\":true}"
          }
        }
        """);

        var meta = new CivitaiImageMetaDto(document.RootElement);

        meta.MetaId.Should().Be(129864601);
        meta.Seed.Should().Be(1234567890123);
        meta.Model.Should().Be("Synthetic Model");
        meta.Steps.Should().Be(12);
        meta.Width.Should().Be(832);
        meta.Height.Should().Be(1216);
        meta.Prompt.Should().Be("synthetic positive prompt");
        meta.NegativePrompt.Should().Be("synthetic negative prompt");
        meta.Sampler.Should().Be("Euler");
        meta.Scheduler.Should().Be("simple");
        meta.CfgScale.Should().Be(1.5f);
        meta.DenoisingStrength.Should().Be("0.65");
        meta.Models.Should().Equal("Synthetic Model", "Aux Model");
        meta.Vaes.Should().Equal("Synthetic VAE");
        meta.Resources.Should().ContainSingle().Which.Name.Should().Be("Synthetic LoRA");
        meta.Resources[0].Weight.Should().Be(0.7f);
        meta.Comfy.Should().Be("{\"workflow\":true}");
    }

    [Fact]
    public void Constructor_WithFlatMetadata_ReadsLegacyFields()
    {
        using var document = JsonDocument.Parse("""
        {
          "seed": 98765,
          "Model": "Legacy Model",
          "steps": 20,
          "prompt": "legacy positive prompt",
          "sampler": "DPM++ 2M",
          "cfgScale": 7,
          "Clip skip": "2",
          "Model hash": "legacyhash",
          "Denoising strength": "0.45",
          "Hires upscale": "1.5",
          "Hires upscaler": "Latent",
          "Hires steps": "8",
          "resources": [
            { "name": "Legacy LoRA", "type": "LORA", "weight": 0.4, "hash": "def456" }
          ]
        }
        """);

        var meta = new CivitaiImageMetaDto(document.RootElement);

        meta.Seed.Should().Be(98765);
        meta.Model.Should().Be("Legacy Model");
        meta.Steps.Should().Be(20);
        meta.Prompt.Should().Be("legacy positive prompt");
        meta.Sampler.Should().Be("DPM++ 2M");
        meta.CfgScale.Should().Be(7f);
        meta.ClipSkip.Should().Be("2");
        meta.ModelHash.Should().Be("legacyhash");
        meta.DenoisingStrength.Should().Be("0.45");
        meta.HiresUpscale.Should().Be("1.5");
        meta.HiresUpscaler.Should().Be("Latent");
        meta.HiresSteps.Should().Be("8");
        meta.Resources.Should().ContainSingle().Which.Hash.Should().Be("def456");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ \"id\": 1, \"meta\": null }")]
    [InlineData("{ \"id\": 1, \"meta\": { \"steps\": \"many\", \"resources\": \"nope\" } }")]
    public void Constructor_WithUnavailableOrMalformedMetadata_DoesNotThrow(string json)
    {
        using var document = JsonDocument.Parse(json);

        var act = () => new CivitaiImageMetaDto(document.RootElement);

        act.Should().NotThrow();
    }

    [Fact]
    public void Project_UsesRecoveredSchedulerAndMetadataDimensions()
    {
        using var document = JsonDocument.Parse("""
        {
          "meta": {
            "prompt": "projected prompt",
            "negativePrompt": "projected negative",
            "seed": "42",
            "steps": "6",
            "cfgScale": "1",
            "sampler": "Euler",
            "scheduler": "simple",
            "denoise": "0.25",
            "width": "640",
            "height": 960
          }
        }
        """);

        var dto = new CivitaiImageDto
        {
            Url = "https://example.test/image.png",
            Meta = new CivitaiImageMetaDto(document.RootElement)
        };

        var image = CivitaiAssetAdapter.Project(dto);

        image.Path.Should().Be("https://example.test/image.png");
        image.Prompt.Should().Be("projected prompt");
        image.NegativePrompt.Should().Be("projected negative");
        image.Seed.Should().Be(42);
        image.Steps.Should().Be(6);
        image.CfgScale.Should().Be(1f);
        image.Scheduler.Should().Be("simple");
        image.DenoisingStrength.Should().Be(0.25d);
        image.Width.Should().Be(640);
        image.Height.Should().Be(960);
    }
}