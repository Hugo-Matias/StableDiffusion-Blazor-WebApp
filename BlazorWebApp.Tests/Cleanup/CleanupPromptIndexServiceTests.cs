using BlazorWebApp.Services.Cleanup;
using FluentAssertions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupPromptIndexServiceTests
{
    private readonly CleanupPromptIndexService _service = new();

    [Fact]
    public void BuildPromptIndex_NormalizesWeightsWhitespaceAndTokenOrderSignature()
    {
        var result = _service.BuildPromptIndex(" ((Masterpiece:1.2)),  blue_hair, <lora:My Style:0.8>, blue_hair ");

        result.NormalizedPrompt.Should().Be("masterpiece, blue hair, lora my style, blue hair");
        result.Fingerprint.Should().HaveLength(64);
        result.TokenSignature.Should().Be("blue hair|lora my style|masterpiece");
    }

    [Fact]
    public void BuildPromptIndex_ReturnsEmptyIndex_ForBlankPrompt()
    {
        var result = _service.BuildPromptIndex("  ");

        result.NormalizedPrompt.Should().BeNull();
        result.Fingerprint.Should().BeNull();
        result.TokenSignature.Should().BeNull();
    }
}