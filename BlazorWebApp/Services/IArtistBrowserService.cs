using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public enum ArtistSortMode { PostCount, Name, Random }

    public class ArtistPreviewProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentSlug { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    public interface IArtistBrowserService
    {
        int TotalCount { get; }

        ArtistPreviewProgress PreviewProgress { get; }

        List<ArtistTag> GetArtists(string? search = null, ArtistSortMode sort = ArtistSortMode.PostCount, bool descending = true, bool favoritesOnly = false, int? randomSeed = null, IEnumerable<string>? userTags = null);

        bool IsFavorite(string tag);

        void ToggleFavorite(string tag);

        List<string> GetFavorites();

        string GetPreviewImagePath(ArtistTag artist);

        bool HasPreviewImage(ArtistTag artist);

        string GetBaselineImagePath();

        bool HasBaselineImage();

        // User tagging
        List<string> GetUserTags(string slug);
        void SetUserTags(string slug, List<string> tags);
        void AddUserTag(string slug, string tag);
        void RemoveUserTag(string slug, string tag);
        List<string> GetAllUserTags();
        Task SaveUserTagsAsync();

        Task GenerateBatchPreviewsAsync(List<ArtistTag> artists, int batchSize, bool skipExisting, CancellationToken ct, Func<Task>? onProgress = null);
    }
}
