using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Services;

public interface ICharacterImportExportService
{
    string ExportCharacter(CharacterEntity character);
    string ExportReferenceSheet(CharacterEntity character, CharacterReferenceSheetBody sheet);
    Task<CharacterImportResult> ImportCharacterAsync(string json, CancellationToken cancellationToken = default);
    Task<CharacterImportResult> ImportReferenceSheetAsync(int characterId, string json, CancellationToken cancellationToken = default);
}