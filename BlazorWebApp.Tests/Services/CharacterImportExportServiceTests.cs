using BlazorWebApp.Data;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Models.CharacterCreator;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Tests.Services;

public class CharacterImportExportServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CharacterRepository _repository;
    private readonly CharacterImportExportService _service;

    public CharacterImportExportServiceTests()
    {
        _repository = new CharacterRepository(_factory);
        _service = new CharacterImportExportService(_repository);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ImportCharacterAsync_ShouldCreateUniqueImportedCharacter()
    {
        var character = await _repository.CreateAsync("Kira", "scout");
        character.Body.Identity.Summary = "silver-haired pathfinder";
        await _repository.UpdateAsync(character);

        var json = _service.ExportCharacter(character);
        var result = await _service.ImportCharacterAsync(json);

        result.Success.Should().BeTrue();
        result.CharacterId.Should().NotBe(character.Id);
        var imported = await _repository.GetByIdAsync(result.CharacterId!.Value);
        imported.Should().NotBeNull();
        imported!.Name.Should().Be("Kira Imported");
        imported.Description.Should().Be("scout");
        imported.Body.Identity.Summary.Should().Be("silver-haired pathfinder");
    }

    [Fact]
    public async Task ImportReferenceSheetAsync_ShouldAddSheetToSelectedCharacter()
    {
        var source = await _repository.CreateAsync("Source");
        var sheet = await _repository.AddOrGetReferenceSheetAsync(source.Id, CharacterReferenceSourceImage.FromImageId(7), "Turnaround");
        sheet.GlobalPositivePromptExtension = "consistent lighting";
        await _repository.UpdateReferenceSheetAsync(source.Id, sheet);
        source = await _repository.GetByIdAsync(source.Id) ?? source;

        var target = await _repository.CreateAsync("Target");
        var json = _service.ExportReferenceSheet(source, sheet);

        var result = await _service.ImportReferenceSheetAsync(target.Id, json);

        result.Success.Should().BeTrue();
        var imported = await _repository.GetReferenceSheetAsync(target.Id, result.ReferenceSheetId!);
        imported.Should().NotBeNull();
        imported!.Label.Should().Be("Turnaround");
        imported.GlobalPositivePromptExtension.Should().Be("consistent lighting");
    }

    [Fact]
    public async Task ImportReferenceSheetAsync_WhenSourceFingerprintMatches_ShouldUpdateExistingSheet()
    {
        var character = await _repository.CreateAsync("Kira");
        var existing = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(7), "Existing");
        var source = await _repository.CreateAsync("Source");
        var imported = await _repository.AddOrGetReferenceSheetAsync(source.Id, CharacterReferenceSourceImage.FromImageId(7), "Updated");
        imported.GlobalPositivePromptExtension = "new prompt";
        await _repository.UpdateReferenceSheetAsync(source.Id, imported);
        source = await _repository.GetByIdAsync(source.Id) ?? source;

        var json = _service.ExportReferenceSheet(source, imported);
        var result = await _service.ImportReferenceSheetAsync(character.Id, json);

        result.ReferenceSheetId.Should().Be(existing.Id);
        var updated = await _repository.GetByIdAsync(character.Id);
        updated!.Body.ReferenceSheets.Should().ContainSingle();
        updated.Body.ReferenceSheets[0].Label.Should().Be("Updated");
        updated.Body.ReferenceSheets[0].GlobalPositivePromptExtension.Should().Be("new prompt");
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<AppDbContext>(new AppDbContext(_options));

        public void Dispose()
        {
            using var context = CreateDbContext();
            context.Database.EnsureDeleted();
        }
    }
}