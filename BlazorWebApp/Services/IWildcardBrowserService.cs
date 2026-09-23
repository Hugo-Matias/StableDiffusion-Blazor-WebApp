using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    public enum WildcardEntrySortMode { Value, Group, Category, Weight, Usage, Updated, Random }

    public class WildcardEntryPreviewProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentEntry { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    public class WildcardBrowserEntry
    {
        public required WildcardCollection Collection { get; init; }
        public required WildcardEntry Entry { get; init; }

        public int CollectionId => Collection.Id;
        public string CollectionName => Collection.Name;
        public string Category => Collection.Category;
        public string Description => Collection.Description ?? string.Empty;
        public int UsageCount => Collection.UsageCount;
        public DateTime UpdatedAt => Collection.UpdatedAt;
        public int EntryId => Entry.Id;
        public string Value => Entry.Value;
        public float Weight => Entry.Weight;
        public int SortOrder => Entry.SortOrder;
    }

    public interface IWildcardBrowserService
    {
        WildcardEntryPreviewProgress PreviewProgress { get; }
        string GetPreviewImagePath(WildcardBrowserEntry entry);
        string GetPreviewImageSource(WildcardBrowserEntry entry, long cacheBust = 0);
        bool HasPreviewImage(WildcardBrowserEntry entry);

        Task GenerateBatchPreviewsAsync(
            List<WildcardBrowserEntry> entries,
            int batchSize,
            bool skipExisting,
            CancellationToken ct,
            Func<Task>? onProgress = null);
    }
}