using System.Collections.Generic;

namespace BlazorWebApp.Services.WildcardForge.Models
{
    /// <summary>
    /// Distilled view of a single category from <c>THEME_CATALOG.json</c>.
    /// Loaded once at startup; trimmed/sliced by <see cref="PromptComposer"/> per call.
    /// </summary>
    public class CategoryCard
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypicalVerbosity { get; set; } = "balanced";
        public string Icon { get; set; } = string.Empty;
        public List<string> Subcategories { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        /// <summary>Map of verbosity level (minimal|balanced|detailed|verbose) -> example entries.</summary>
        public Dictionary<string, List<string>> VerbosityExamples { get; set; } = new();
        public List<CategorySampleCollection> SampleCollections { get; set; } = new();
        public List<string> BestPractices { get; set; } = new();
        public List<string> Avoid { get; set; } = new();
    }

    public class CategorySampleCollection
    {
        public string Name { get; set; } = string.Empty;
        public string Verbosity { get; set; } = string.Empty;
        public int EntryCount { get; set; }
        public List<string> Examples { get; set; } = new();
    }
}
