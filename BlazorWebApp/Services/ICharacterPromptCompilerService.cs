using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Services;

public interface ICharacterPromptCompilerService
{
    CharacterPromptCompilation Compile(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompileOptions? options = null);
    string ComposePrompt(string? existingPrompt, string compiledPrompt, CharacterPromptCompositionMode mode);
}