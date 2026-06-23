using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Models.CharacterCreator;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Services;

public class CharacterImportExportService : ICharacterImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(CharacterCreatorJsonOptions.Compact)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ICharacterRepository _characters;

    public CharacterImportExportService(ICharacterRepository characters)
    {
        _characters = characters;
    }

    public string ExportCharacter(CharacterEntity character)
    {
        var document = new CharacterExportDocument
        {
            Character = CharacterExportPayload.FromEntity(Clone(character))
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public string ExportReferenceSheet(CharacterEntity character, CharacterReferenceSheetBody sheet)
    {
        var document = new CharacterReferenceSheetExportDocument
        {
            CharacterName = character.Name,
            Sheet = Clone(sheet)
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public async Task<CharacterImportResult> ImportCharacterAsync(string json, CancellationToken cancellationToken = default)
    {
        var document = DeserializeCharacter(json);
        if (document.Character.Body is null)
        {
            throw new InvalidOperationException("Character import JSON does not contain a character body.");
        }

        var importName = await ResolveUniqueNameAsync(ResolveCharacterName(document.Character), cancellationToken);
        var created = await _characters.CreateAsync(importName, document.Character.Description, cancellationToken);
        created.ThumbnailImageId = document.Character.ThumbnailImageId;
        created.Body = Clone(document.Character.Body);
        created.Body.SchemaVersion = CharacterBody.CurrentSchemaVersion;
        created.Body.Identity.DisplayName = importName;
        created = await _characters.UpdateAsync(created, cancellationToken);

        return new CharacterImportResult
        {
            Success = true,
            CharacterId = created.Id,
            Message = $"Imported character '{created.Name}'."
        };
    }

    public async Task<CharacterImportResult> ImportReferenceSheetAsync(int characterId, string json, CancellationToken cancellationToken = default)
    {
        var document = DeserializeReferenceSheet(json);
        var importedSheet = Clone(document.Sheet);
        ValidateReferenceSheet(importedSheet);

        var character = await _characters.GetByIdAsync(characterId, cancellationToken)
            ?? throw new InvalidOperationException($"Character '{characterId}' was not found.");

        var existingBySource = character.Body.ReferenceSheets.FirstOrDefault(sheet => FingerprintsMatch(sheet.SourceImage.SourceFingerprint, importedSheet.SourceImage.SourceFingerprint));
        if (existingBySource is not null)
        {
            importedSheet.Id = existingBySource.Id;
            importedSheet.CreatedAt = existingBySource.CreatedAt;
            importedSheet.UpdatedAt = DateTime.UtcNow;
            await _characters.UpdateReferenceSheetAsync(characterId, importedSheet, cancellationToken);
            return new CharacterImportResult
            {
                Success = true,
                CharacterId = characterId,
                ReferenceSheetId = importedSheet.Id,
                Message = $"Updated reference sheet '{importedSheet.Label}'."
            };
        }

        if (string.IsNullOrWhiteSpace(importedSheet.Id)
            || character.Body.ReferenceSheets.Any(sheet => string.Equals(sheet.Id, importedSheet.Id, StringComparison.Ordinal)))
        {
            importedSheet.Id = CharacterIdFactory.CreateId("sheet");
        }

        importedSheet.CreatedAt = importedSheet.CreatedAt == default ? DateTime.UtcNow : importedSheet.CreatedAt;
        importedSheet.UpdatedAt = DateTime.UtcNow;
        character.Body.ReferenceSheets.Add(importedSheet);
        character.Body.ActiveReferenceSheetId = importedSheet.Id;
        await _characters.UpdateAsync(character, cancellationToken);

        return new CharacterImportResult
        {
            Success = true,
            CharacterId = characterId,
            ReferenceSheetId = importedSheet.Id,
            Message = $"Imported reference sheet '{importedSheet.Label}'."
        };
    }

    private static CharacterExportDocument DeserializeCharacter(string json)
    {
        var document = JsonSerializer.Deserialize<CharacterExportDocument>(json, JsonOptions);
        if (document?.Character is null || !string.Equals(document.DocumentType, "character", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Import file is not a Character export document.");
        }

        return document;
    }

    private static CharacterReferenceSheetExportDocument DeserializeReferenceSheet(string json)
    {
        var document = JsonSerializer.Deserialize<CharacterReferenceSheetExportDocument>(json, JsonOptions);
        if (document?.Sheet is null || !string.Equals(document.DocumentType, "character-reference-sheet", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Import file is not a Character reference sheet export document.");
        }

        return document;
    }

    private async Task<string> ResolveUniqueNameAsync(string desiredName, CancellationToken cancellationToken)
    {
        var summaries = await _characters.ListAsync(cancellationToken);
        var existingNames = summaries.Select(summary => summary.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(desiredName))
        {
            return desiredName;
        }

        var baseName = desiredName + " Imported";
        var candidate = baseName;
        var index = 2;
        while (existingNames.Contains(candidate))
        {
            candidate = $"{baseName} {index++}";
        }

        return candidate;
    }

    private static string ResolveCharacterName(CharacterExportPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.Name))
        {
            return payload.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(payload.Body?.Identity.DisplayName))
        {
            return payload.Body.Identity.DisplayName.Trim();
        }

        return "Imported Character";
    }

    private static void ValidateReferenceSheet(CharacterReferenceSheetBody sheet)
    {
        if (string.IsNullOrWhiteSpace(sheet.SourceImage.SourceFingerprint))
        {
            throw new InvalidOperationException("Reference sheet import JSON must include a source fingerprint.");
        }

        if (string.IsNullOrWhiteSpace(sheet.Label))
        {
            sheet.Label = "Imported Reference Sheet";
        }
    }

    private static bool FingerprintsMatch(string first, string second)
    {
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Could not clone {typeof(T).Name}.");
    }
}