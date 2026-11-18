using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Services;

public interface ICharacterImageDraftService
{
    Task<CharacterImageDraft?> CreateDraftAsync(CharacterImageDraftRequest request, CancellationToken cancellationToken = default);
    bool TryParseDraft(string? rawJson, CharacterCreatorCatalog catalog, CharacterReferenceSourceImage? sourceImage, string? sourceLabel, out CharacterImageDraft draft);
    CharacterImageDraftApplyResult ApplyDraft(CharacterBody body, CharacterImageDraft draft);
}