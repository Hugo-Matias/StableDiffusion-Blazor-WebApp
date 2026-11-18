using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Services;

public interface ICharacterLlmAuthoringService
{
    Task<CharacterLlmAuthoringSuggestion?> ExpandSummaryAsync(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompilation? compilation, CancellationToken cancellationToken = default);
    Task<CharacterLlmAuthoringSuggestion?> SummarizeAsync(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompilation? compilation, CancellationToken cancellationToken = default);
    Task<CharacterLlmAuthoringSuggestion?> RewriteRegionAsync(CharacterBody body, CharacterCreatorCatalog catalog, string regionId, CancellationToken cancellationToken = default);
    Task<CharacterLlmAuthoringSuggestion?> FillMissingAsync(CharacterBody body, CharacterCreatorCatalog catalog, CancellationToken cancellationToken = default);
    CharacterLlmAuthoringApplyResult ApplySuggestion(CharacterBody body, CharacterLlmAuthoringSuggestion suggestion);
    bool TryParseSuggestion(string? rawJson, CharacterLlmAuthoringOperation operation, out CharacterLlmAuthoringSuggestion suggestion);
}