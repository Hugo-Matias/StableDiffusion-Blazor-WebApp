using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models.CharacterCreator;

public class CharacterExportDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string DocumentType { get; set; } = "character";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public CharacterExportPayload Character { get; set; } = new();
}

public class CharacterExportPayload
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? ThumbnailImageId { get; set; }
    public CharacterBody Body { get; set; } = CharacterBody.CreateDefault();

    public static CharacterExportPayload FromEntity(CharacterEntity character)
    {
        return new CharacterExportPayload
        {
            Name = character.Name,
            Description = character.Description,
            ThumbnailImageId = character.ThumbnailImageId,
            Body = character.Body ?? CharacterBody.CreateDefault(character.Name)
        };
    }
}

public class CharacterReferenceSheetExportDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string DocumentType { get; set; } = "character-reference-sheet";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string CharacterName { get; set; } = string.Empty;
    public CharacterReferenceSheetBody Sheet { get; set; } = new();
}

public class CharacterImportResult
{
    public bool Success { get; set; }
    public int? CharacterId { get; set; }
    public string? ReferenceSheetId { get; set; }
    public string Message { get; set; } = string.Empty;
}