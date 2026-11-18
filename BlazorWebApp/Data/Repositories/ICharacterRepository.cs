using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Data.Repositories;

public interface ICharacterRepository
{
    Task<IReadOnlyList<CharacterSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<CharacterEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CharacterEntity> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default);
    Task<CharacterEntity> DuplicateAsync(int id, string? name = null, CancellationToken cancellationToken = default);
    Task<CharacterEntity> UpdateAsync(CharacterEntity character, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<CharacterReferenceSheetBody> AddOrGetReferenceSheetAsync(int characterId, CharacterReferenceSourceImage sourceImage, string? label = null, CancellationToken cancellationToken = default);
    Task<CharacterReferenceSheetBody?> GetReferenceSheetAsync(int characterId, string sheetId, CancellationToken cancellationToken = default);
    Task<CharacterReferenceSheetBody> UpdateReferenceSheetAsync(int characterId, CharacterReferenceSheetBody sheet, CancellationToken cancellationToken = default);
    Task DeleteReferenceSheetAsync(int characterId, string sheetId, CancellationToken cancellationToken = default);
    Task SetActiveReferenceSheetAsync(int characterId, string? sheetId, CancellationToken cancellationToken = default);
}

public sealed record CharacterSummary(
    int Id,
    string Name,
    string Description,
    int? ThumbnailImageId,
    string? ActiveReferenceSheetId,
    int ReferenceSheetCount,
    DateTime UpdatedAt);