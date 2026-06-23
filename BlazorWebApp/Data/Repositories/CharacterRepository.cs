using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models.CharacterCreator;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorWebApp.Data.Repositories;

public class CharacterRepository : ICharacterRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public CharacterRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<CharacterSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Characters
            .AsNoTracking()
            .OrderBy(character => character.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(character => new CharacterSummary(
                character.Id,
                character.Name,
                character.Description,
                character.ThumbnailImageId,
                character.Body.ActiveReferenceSheetId,
                character.Body.ReferenceSheets.Count,
                character.UpdatedAt))
            .ToList();
    }

    public async Task<CharacterEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(character => character.Id == id, cancellationToken);
    }

    public async Task<CharacterEntity> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var normalizedName = NormalizeName(name);
        var character = new CharacterEntity
        {
            Name = normalizedName,
            Description = description?.Trim() ?? string.Empty,
            Body = CharacterBody.CreateDefault(normalizedName),
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Characters.Add(character);
        await context.SaveChangesAsync(cancellationToken);
        return character;
    }

    public async Task<CharacterEntity> DuplicateAsync(int id, string? name = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(character => character.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Character '{id}' was not found.");

        var duplicateName = NormalizeName(string.IsNullOrWhiteSpace(name) ? $"{existing.Name} Copy" : name);
        var bodyJson = JsonSerializer.Serialize(existing.Body ?? CharacterBody.CreateDefault(existing.Name), CharacterCreatorJsonOptions.Compact);
        var body = JsonSerializer.Deserialize<CharacterBody>(bodyJson, CharacterCreatorJsonOptions.Compact) ?? CharacterBody.CreateDefault(duplicateName);
        body.Identity.DisplayName = duplicateName;

        var now = DateTime.UtcNow;
        var duplicate = new CharacterEntity
        {
            Name = duplicateName,
            Description = existing.Description,
            ThumbnailImageId = existing.ThumbnailImageId,
            Body = body,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Characters.Add(duplicate);
        await context.SaveChangesAsync(cancellationToken);
        return duplicate;
    }

    public async Task<CharacterEntity> UpdateAsync(CharacterEntity character, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Characters.FirstOrDefaultAsync(row => row.Id == character.Id, cancellationToken);
        if (existing is null)
        {
            character.Name = NormalizeName(character.Name);
            character.Description = character.Description?.Trim() ?? string.Empty;
            character.CreatedAt = character.CreatedAt == default ? DateTime.UtcNow : character.CreatedAt;
            character.UpdatedAt = DateTime.UtcNow;
            context.Characters.Add(character);
            await context.SaveChangesAsync(cancellationToken);
            return character;
        }

        existing.Name = NormalizeName(character.Name);
        existing.Description = character.Description?.Trim() ?? string.Empty;
        existing.ThumbnailImageId = character.ThumbnailImageId;
        existing.Body = character.Body ?? CharacterBody.CreateDefault(character.Name);
        existing.UpdatedAt = DateTime.UtcNow;
        context.Entry(existing).Property(row => row.Body).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Characters.FirstOrDefaultAsync(character => character.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        context.Characters.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CharacterReferenceSheetBody> AddOrGetReferenceSheetAsync(
        int characterId,
        CharacterReferenceSourceImage sourceImage,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceImage.SourceFingerprint))
        {
            throw new ArgumentException("Reference sheet source image must have a source fingerprint.", nameof(sourceImage));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var character = await GetTrackedCharacterAsync(context, characterId, cancellationToken);
        var existing = character.Body.ReferenceSheets.FirstOrDefault(sheet => FingerprintsMatch(sheet.SourceImage.SourceFingerprint, sourceImage.SourceFingerprint));
        if (existing is not null)
        {
            character.Body.ActiveReferenceSheetId = existing.Id;
            character.UpdatedAt = DateTime.UtcNow;
            context.Entry(character).Property(row => row.Body).IsModified = true;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var sheet = CharacterReferenceSheetBody.Create(sourceImage, label);
        character.Body.ReferenceSheets.Add(sheet);
        character.Body.ActiveReferenceSheetId = sheet.Id;
        character.UpdatedAt = DateTime.UtcNow;
        context.Entry(character).Property(row => row.Body).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
        return sheet;
    }

    public async Task<CharacterReferenceSheetBody?> GetReferenceSheetAsync(int characterId, string sheetId, CancellationToken cancellationToken = default)
    {
        var character = await GetByIdAsync(characterId, cancellationToken);
        return character?.Body.ReferenceSheets.FirstOrDefault(sheet => string.Equals(sheet.Id, sheetId, StringComparison.Ordinal));
    }

    public async Task<CharacterReferenceSheetBody> UpdateReferenceSheetAsync(
        int characterId,
        CharacterReferenceSheetBody sheet,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sheet.Id))
        {
            throw new ArgumentException("Reference sheet id is required.", nameof(sheet));
        }

        if (string.IsNullOrWhiteSpace(sheet.SourceImage.SourceFingerprint))
        {
            throw new ArgumentException("Reference sheet source fingerprint is required.", nameof(sheet));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var character = await GetTrackedCharacterAsync(context, characterId, cancellationToken);
        var existingIndex = character.Body.ReferenceSheets.FindIndex(existing => string.Equals(existing.Id, sheet.Id, StringComparison.Ordinal));
        if (existingIndex < 0)
        {
            throw new InvalidOperationException($"Reference sheet '{sheet.Id}' was not found for character '{characterId}'.");
        }

        var duplicate = character.Body.ReferenceSheets.FirstOrDefault(existing =>
            !string.Equals(existing.Id, sheet.Id, StringComparison.Ordinal)
            && FingerprintsMatch(existing.SourceImage.SourceFingerprint, sheet.SourceImage.SourceFingerprint));
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Reference sheet source '{sheet.SourceImage.SourceFingerprint}' already belongs to sheet '{duplicate.Id}'.");
        }

        sheet.UpdatedAt = DateTime.UtcNow;
        character.Body.ReferenceSheets[existingIndex] = sheet;
        character.Body.ActiveReferenceSheetId ??= sheet.Id;
        character.UpdatedAt = DateTime.UtcNow;
        context.Entry(character).Property(row => row.Body).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
        return sheet;
    }

    public async Task DeleteReferenceSheetAsync(int characterId, string sheetId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var character = await GetTrackedCharacterAsync(context, characterId, cancellationToken);
        var sheet = character.Body.ReferenceSheets.FirstOrDefault(existing => string.Equals(existing.Id, sheetId, StringComparison.Ordinal));
        if (sheet is null)
        {
            return;
        }

        character.Body.ReferenceSheets.Remove(sheet);
        if (string.Equals(character.Body.ActiveReferenceSheetId, sheetId, StringComparison.Ordinal))
        {
            character.Body.ActiveReferenceSheetId = character.Body.ReferenceSheets.FirstOrDefault()?.Id;
        }

        character.UpdatedAt = DateTime.UtcNow;
        context.Entry(character).Property(row => row.Body).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveReferenceSheetAsync(int characterId, string? sheetId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var character = await GetTrackedCharacterAsync(context, characterId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(sheetId)
            && character.Body.ReferenceSheets.All(sheet => !string.Equals(sheet.Id, sheetId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Reference sheet '{sheetId}' was not found for character '{characterId}'.");
        }

        character.Body.ActiveReferenceSheetId = sheetId;
        character.UpdatedAt = DateTime.UtcNow;
        context.Entry(character).Property(row => row.Body).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<CharacterEntity> GetTrackedCharacterAsync(AppDbContext context, int characterId, CancellationToken cancellationToken)
    {
        return await context.Characters.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            ?? throw new InvalidOperationException($"Character '{characterId}' was not found.");
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Untitled Character" : normalized;
    }

    private static bool FingerprintsMatch(string first, string second)
    {
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }
}