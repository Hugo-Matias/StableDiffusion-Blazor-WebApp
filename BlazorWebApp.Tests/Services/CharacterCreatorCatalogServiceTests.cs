using BlazorWebApp.Models.CharacterCreator;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace BlazorWebApp.Tests.Services;

public class CharacterCreatorCatalogServiceTests : IDisposable
{
    private readonly string _catalogDirectory = Path.Combine(Path.GetTempPath(), "character-catalog-tests-" + Guid.NewGuid().ToString("N"));

    public CharacterCreatorCatalogServiceTests()
    {
        Directory.CreateDirectory(_catalogDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_catalogDirectory))
        {
            Directory.Delete(_catalogDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFilesAreMissing_ShouldReturnFallbackCatalog()
    {
        var catalog = await CreateService().LoadAsync();

        catalog.Regions.Should().Contain(region => region.Id == "head");
        catalog.TraitDefinitions.Should().Contain(trait => trait.Id == "hair.length");
        catalog.PromptRules.Should().Contain(rule => rule.ProfileId == "identity_plus_wardrobe");
        catalog.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_ShouldReadValidRegionFile()
    {
        WriteEnvelope("character_regions.json", "character-regions", new[]
        {
            new CharacterRegionDefinition { Id = "wings", Label = "Wings", Group = "fantasy" }
        });

        var catalog = await CreateService().LoadAsync();

        catalog.Regions.Should().ContainSingle().Which.Id.Should().Be("wings");
        catalog.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_ShouldSkipInvalidAndDuplicateItemsWithWarnings()
    {
        WriteEnvelope("character_regions.json", "character-regions", new[]
        {
            new CharacterRegionDefinition { Id = "head", Label = "Head" },
            new CharacterRegionDefinition { Id = "head", Label = "Duplicate Head" },
            new CharacterRegionDefinition { Id = "missing-label" }
        });

        var catalog = await CreateService().LoadAsync();

        catalog.Regions.Should().ContainSingle().Which.Label.Should().Be("Head");
        catalog.Warnings.Should().HaveCount(2);
        catalog.Warnings.Select(warning => warning.ItemId).Should().Contain(["head", "missing-label"]);
    }

    [Fact]
    public async Task LoadAsync_WhenCatalogIdDoesNotMatch_ShouldReturnFallbackAndWarning()
    {
        WriteEnvelope("character_regions.json", "wrong-id", new[]
        {
            new CharacterRegionDefinition { Id = "wings", Label = "Wings" }
        });

        var catalog = await CreateService().LoadAsync();

        catalog.Regions.Should().Contain(region => region.Id == "head");
        catalog.Warnings.Should().ContainSingle(warning => warning.FileName == "character_regions.json");
    }

    private CharacterCreatorCatalogService CreateService()
    {
        return new CharacterCreatorCatalogService(_catalogDirectory, NullLogger<CharacterCreatorCatalogService>.Instance);
    }

    private void WriteEnvelope<T>(string fileName, string catalogId, IEnumerable<T> items)
    {
        var envelope = new CharacterCatalogEnvelope<T>
        {
            SchemaVersion = 1,
            CatalogId = catalogId,
            DisplayName = catalogId,
            Items = items.ToList()
        };
        var json = JsonSerializer.Serialize(envelope);
        File.WriteAllText(Path.Combine(_catalogDirectory, fileName), json);
    }
}