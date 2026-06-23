using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Services;

public interface ICharacterCreatorCatalogService
{
    Task<CharacterCreatorCatalog> LoadAsync(CancellationToken cancellationToken = default);
}