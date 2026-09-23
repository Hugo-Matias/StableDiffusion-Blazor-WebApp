using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public enum TagGroupSortMode { Count, Name }

    public class TagGroupPreviewProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentTag { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    public interface ITagGroupService
    {
        // ── Catalogue ────────────────────────────────────────────────────────
        IReadOnlyList<string> Categories { get; }
        IReadOnlyList<TagGroupDescriptor> Groups { get; }
        IReadOnlyList<TagGroupDescriptor> GetGroupsForCategory(string category);

        List<DanbooruTag> GetTags(
            string? search = null,
            string? group = null,
            string? category = null,
            string? section = null,
            TagGroupSortMode sort = TagGroupSortMode.Count,
            bool descending = true);

        /// <summary>Returns the ordered list of distinct section labels for a group (tags with null section are excluded).</summary>
        IReadOnlyList<string> GetSections(string group);

        // ── Config (templates) ───────────────────────────────────────────────
        TagGroupConfig GetConfig(string group);
        void SetConfig(string group, string previewTemplate);
        Task SaveConfigAsync();

        // ── Preview images ───────────────────────────────────────────────────
        string GetPreviewImagePath(DanbooruTag tag);
        bool HasPreviewImage(DanbooruTag tag);
        TagGroupPreviewProgress PreviewProgress { get; }

        Task GenerateBatchPreviewsAsync(
            List<DanbooruTag> tags,
            int batchSize,
            bool skipExisting,
            bool useCoTags,
            CancellationToken ct,
            Func<Task>? onProgress = null);
    }
}
