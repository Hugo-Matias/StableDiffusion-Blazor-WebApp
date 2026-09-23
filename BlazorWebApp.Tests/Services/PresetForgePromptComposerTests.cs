using BlazorWebApp.Services.PresetForge;
using BlazorWebApp.Services.PresetForge.Models;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class PresetForgePromptComposerTests
{
    [Fact]
    public void Compose_BuildingBlockRestrictsOutputToSelectedFacet()
    {
        var request = new PresetForgeRequest
        {
            Kind = PresetForgeKind.BuildingBlock,
            Theme = "subtle emotions",
            Facet = "expression"
        };

        var messages = new PresetForgePromptComposer().Compose(request);

        messages[0].Content.Should().Contain("composable T2I building block");
        messages[0].Content.Should().Contain("only the selected facet");
        messages[1].Content.Should().Contain("Facet: expression");
    }

    [Fact]
    public void Compose_EditInstructionUsesSelectedPreservationContract()
    {
        var request = new PresetForgeRequest
        {
            Kind = PresetForgeKind.EditInstruction,
            PreservationPolicy = PresetPreservationPolicy.PartialBody,
            Theme = "hair changes"
        };

        var messages = new PresetForgePromptComposer().Compose(request);

        messages[0].Content.Should().Contain("modifying only the specified target region");
        messages[0].Content.Should().Contain("I2I targeted-edit instruction");
    }
}