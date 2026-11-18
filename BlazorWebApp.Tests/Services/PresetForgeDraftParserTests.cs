using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services.PresetForge;
using BlazorWebApp.Services.PresetForge.Models;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class PresetForgeDraftParserTests
{
    [Fact]
    public void Parse_NormalizesStructuredPresetAndAddsSemanticTags()
    {
        var request = new PresetForgeRequest
        {
            Kind = PresetForgeKind.BuildingBlock,
            Category = "Expressions",
            AdditionalTags = ["Portrait Detail"]
        };

        var entries = PresetForgeDraftParser.Parse(
            "```json\n[{\"name\":\"Guarded Suspicion\",\"prompt\":\"a guarded, suspicious expression\",\"negative_prompt\":null,\"category\":\"\",\"tags\":[\"Facial Expression\"]}]\n```",
            request,
            []);

        entries.Should().ContainSingle();
        entries[0].Category.Should().Be("Expressions");
        entries[0].Tags.Should().BeEquivalentTo(
            ["facial-expression", "portrait-detail", "preset-forge", "t2i", "building-block"]);
        entries[0].IsValid.Should().BeTrue();
        entries[0].Accepted.Should().BeTrue();
    }

    [Fact]
    public void Parse_KeepsInvalidAndDuplicateEntriesVisible()
    {
        var request = new PresetForgeRequest { Category = "Expressions" };
        var existing = new[]
        {
            new Prompt { Title = "Known Smile", Positive = "a familiar smile" }
        };

        var entries = PresetForgeDraftParser.Parse(
            "[{\"name\":\"Known Smile\",\"prompt\":\"new text\"},{\"name\":\"Missing Prompt\"}]",
            request,
            existing);

        entries.Should().HaveCount(2);
        entries[0].IsDuplicate.Should().BeTrue();
        entries[0].Accepted.Should().BeFalse();
        entries[1].ValidationErrors.Should().Contain("Prompt is required.");
        entries[1].Accepted.Should().BeFalse();
    }
}