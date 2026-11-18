using BlazorWebApp.Data;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Models.CharacterCreator;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Tests.Repositories;

public class CharacterRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly CharacterRepository _repository;

    public CharacterRepositoryTests()
    {
        _factory = new TestDbContextFactory();
        _repository = new CharacterRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateAsync_ShouldPersistCharacterBody()
    {
        var character = await _repository.CreateAsync(" Kira ", "main character");

        var loaded = await _repository.GetByIdAsync(character.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Kira");
        loaded.Description.Should().Be("main character");
        loaded.Body.Identity.DisplayName.Should().Be("Kira");
        loaded.Body.PromptProfiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddOrGetReferenceSheetAsync_ShouldReuseSheetForSameSourceFingerprint()
    {
        var character = await _repository.CreateAsync("Kira");
        var source = CharacterReferenceSourceImage.FromImageId(12, "source.png", "Source A");

        var first = await _repository.AddOrGetReferenceSheetAsync(character.Id, source);
        var second = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(12, "other.png", "Source B"));

        second.Id.Should().Be(first.Id);
        var loaded = await _repository.GetByIdAsync(character.Id);
        loaded!.Body.ReferenceSheets.Should().ContainSingle();
        loaded.Body.ActiveReferenceSheetId.Should().Be(first.Id);
    }

    [Fact]
    public async Task DuplicateAsync_ShouldCopyCharacterBodyAndSheets()
    {
        var character = await _repository.CreateAsync("Kira", "main character");
        var sheet = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(12));

        var duplicate = await _repository.DuplicateAsync(character.Id, "Kira Variant");

        duplicate.Id.Should().NotBe(character.Id);
        duplicate.Name.Should().Be("Kira Variant");
        duplicate.Description.Should().Be("main character");
        duplicate.Body.Identity.DisplayName.Should().Be("Kira Variant");
        duplicate.Body.ReferenceSheets.Should().ContainSingle().Which.Id.Should().Be(sheet.Id);
    }

    [Fact]
    public async Task UpdateReferenceSheetAsync_ShouldPersistNestedJsonMutation()
    {
        var character = await _repository.CreateAsync("Kira");
        var sheet = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(1));
        sheet.GlobalPositivePromptExtension = "consistent painterly lighting";
        sheet.Slots[0].LastOutputImageId = 99;

        await _repository.UpdateReferenceSheetAsync(character.Id, sheet);

        var loaded = await _repository.GetReferenceSheetAsync(character.Id, sheet.Id);
        loaded.Should().NotBeNull();
        loaded!.GlobalPositivePromptExtension.Should().Be("consistent painterly lighting");
        loaded.Slots[0].LastOutputImageId.Should().Be(99);
    }

    [Fact]
    public async Task UpdateReferenceSheetAsync_ShouldRejectDuplicateSourceFingerprint()
    {
        var character = await _repository.CreateAsync("Kira");
        var first = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(1));
        var second = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(2));
        second.SourceImage = CharacterReferenceSourceImage.FromImageId(1);

        var act = async () => await _repository.UpdateReferenceSheetAsync(character.Id, second);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{first.Id}*");
    }

    [Fact]
    public async Task DeleteReferenceSheetAsync_ShouldClearActiveSheetWhenDeleted()
    {
        var character = await _repository.CreateAsync("Kira");
        var sheet = await _repository.AddOrGetReferenceSheetAsync(character.Id, CharacterReferenceSourceImage.FromImageId(1));

        await _repository.DeleteReferenceSheetAsync(character.Id, sheet.Id);

        var loaded = await _repository.GetByIdAsync(character.Id);
        loaded!.Body.ReferenceSheets.Should().BeEmpty();
        loaded.Body.ActiveReferenceSheetId.Should().BeNull();
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