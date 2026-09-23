using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// A single tag entry, populated from tag_groups_flat.jsonl by TagGroupService.
    /// </summary>
    public class DanbooruTag
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
        /// <summary>Post count; 0 until enriched from an external source.</summary>
        public int Count { get; set; }
        /// <summary>
        /// Wiki section heading this tag belongs to (deepest heading level).
        /// Taken from section_path[-1] in the flat JSONL source.
        /// </summary>
        [JsonPropertyName("section")] public string? Section { get; set; }
        /// <summary>
        /// Tags that commonly co-occur with this tag (from Danbooru related_tags API).
        /// Populated from the co_tags field in tag_groups_flat.jsonl.
        /// Used by preview generation when UseCoTags is enabled.
        /// </summary>
        [JsonIgnore] public List<string>? CoTags { get; set; }
        // Populated at load time by TagGroupService
        [JsonIgnore] public string Group { get; set; } = string.Empty;
        [JsonIgnore] public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// User-editable per-group configuration (prompt templates, etc.).
    /// Persisted to Data/tag-group-config.json.
    /// </summary>
    public class TagGroupConfig
    {
        [JsonPropertyName("group")] public string Group { get; set; } = string.Empty;
        [JsonPropertyName("preview_template")] public string PreviewTemplate { get; set; } = string.Empty;
    }

    /// <summary>
    /// Runtime descriptor for one loaded group: combined data + user config.
    /// </summary>
    public class TagGroupDescriptor
    {
        public string Group { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int TagCount { get; set; }

        /// Live reference to the mutable config entry.
        public TagGroupConfig Config { get; set; } = new();
    }

    /// <summary>
    /// One row from tag_groups_flat.jsonl — the PBandDev scraper format.
    /// Only the fields consumed by TagGroupService are declared here.
    /// </summary>
    internal sealed class TagGroupJsonlRow
    {
        [JsonPropertyName("page_slug")] public string? PageSlug { get; init; }
        [JsonPropertyName("section_title")] public string? SectionTitle { get; init; }
        [JsonPropertyName("section_path")] public List<string>? SectionPath { get; init; }
        [JsonPropertyName("tag_label")] public string? TagLabel { get; init; }
        [JsonPropertyName("canonical_name")] public string? CanonicalName { get; init; }
        [JsonPropertyName("source")] public string? Source { get; init; }
        [JsonPropertyName("co_tags")] public List<string>? CoTags { get; init; }
    }
}
