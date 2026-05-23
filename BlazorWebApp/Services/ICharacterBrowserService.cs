using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public enum CharacterSortMode { PostCount, Name, Copyright, Random }

    public class CharacterPreviewProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentSlug { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    public interface ICharacterBrowserService
    {
        int TotalCount { get; }
        CharacterPreviewProgress PreviewProgress { get; }

        List<AnimaCharacter> GetCharacters(
            string? search = null,
            CharacterSortMode sort = CharacterSortMode.PostCount,
            bool descending = true,
            bool favoritesOnly = false,
            int? randomSeed = null,
            string? copyright = null,
            string? hairColor = null,
            string? hairLength = null,
            string? eyeColor = null,
            string? gender = null);

        IReadOnlyList<string> GetAllCopyrights();
        IReadOnlyList<string> HairColorFacets { get; }
        IReadOnlyList<string> HairLengthFacets { get; }
        IReadOnlyList<string> EyeColorFacets { get; }
        IReadOnlyList<string> GenderFacets { get; }

        bool IsFavorite(string slug);
        void ToggleFavorite(string slug);
        List<string> GetFavorites();

        string GetPreviewImagePath(AnimaCharacter character);
        bool HasPreviewImage(AnimaCharacter character);

        Task GenerateBatchPreviewsAsync(
            List<AnimaCharacter> characters,
            int batchSize,
            bool skipExisting,
            bool useTags,
            CancellationToken ct,
            Func<Task>? onProgress = null);
    }
}
