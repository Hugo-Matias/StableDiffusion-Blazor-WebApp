using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BlazorWebApp.Services.WildcardForge.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorWebApp.Services.WildcardForge
{
    /// <summary>
    /// Phase 10 - Wildcard Forge knowledge layer.
    /// Loads <c>Documentation/Wildcards/THEME_CATALOG.json</c> and <c>LLM_PROMPTS.json</c> once at
    /// startup and exposes typed POCOs to <see cref="PromptComposer"/> and the view layer.
    /// No hot-reload; files are stable while the app runs.
    /// </summary>
    public class WildcardForgeKnowledge
    {
        private readonly ILogger<WildcardForgeKnowledge> _logger;
        private readonly List<CategoryCard> _categories = new();
        private readonly Dictionary<string, LlmTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CategoryCard> Categories => _categories;
        public IReadOnlyList<VerbosityLevelInfo> VerbosityLevels { get; }

        public WildcardForgeKnowledge(IHostEnvironment env, ILogger<WildcardForgeKnowledge> logger)
        {
            _logger = logger;

            // Static rubric - matches WILDCARD_GENERATION_GUIDE.md
            VerbosityLevels = new List<VerbosityLevelInfo>
            {
                new() { Key = "minimal",  Label = "Minimal",  WordRange = "1-3 words",   BestFor = "Quick variations, simple tags" },
                new() { Key = "balanced", Label = "Balanced", WordRange = "3-6 words",   BestFor = "Standard prompts, versatile use" },
                new() { Key = "detailed", Label = "Detailed", WordRange = "6-12 words",  BestFor = "Specific imagery, scene building" },
                new() { Key = "verbose",  Label = "Verbose",  WordRange = "12+ words",   BestFor = "Cinematic scenes, maximum detail" }
            };

            var docsRoot = ResolveDocsRoot(env);
            LoadCategories(Path.Combine(docsRoot, "THEME_CATALOG.json"));
            LoadTemplates(Path.Combine(docsRoot, "LLM_PROMPTS.json"));

            _logger.LogInformation("WildcardForgeKnowledge loaded {CategoryCount} categories and {TemplateCount} templates",
                _categories.Count, _templates.Count);
        }

        public CategoryCard? FindCategory(string? idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName)) return null;
            if (int.TryParse(idOrName, out var id))
            {
                var byId = _categories.FirstOrDefault(c => c.Id == id);
                if (byId != null) return byId;
            }
            return _categories.FirstOrDefault(c => string.Equals(c.Name, idOrName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Best-effort keyword classifier used by Simple mode. Returns the top matches by keyword hit count.
        /// </summary>
        public IReadOnlyList<CategoryCard> ClassifyByKeywords(string description, int top = 2)
        {
            if (string.IsNullOrWhiteSpace(description)) return Array.Empty<CategoryCard>();
            var tokens = description
                .ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n', '\r', '\t', '/', '\\', '-' },
                       StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();

            return _categories
                .Select(c => (Category: c, Score: c.Keywords.Count(k => tokens.Contains(k.ToLowerInvariant()))))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(top)
                .Select(x => x.Category)
                .ToList();
        }

        public LlmTemplate GetTemplate(ForgeOperation op)
        {
            var key = op switch
            {
                ForgeOperation.Generate => "basic_generation",
                ForgeOperation.Expand => "themed_expansion",
                ForgeOperation.Refine => "quality_enhancement",
                // Convert and Describe do not use LLM_PROMPTS.json templates;
                // they have custom code paths in WildcardForgeService / PromptComposer.
                _ => "basic_generation"
            };
            return _templates.TryGetValue(key, out var t) ? t : throw new InvalidOperationException($"Forge template '{key}' missing");
        }

        public LlmTemplate? GetTemplateByKey(string key)
            => _templates.TryGetValue(key, out var t) ? t : null;

        private static string ResolveDocsRoot(IHostEnvironment env)
        {
            // ContentRoot is BlazorWebApp/. Documentation/ sits one level up.
            var candidate = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "Documentation", "Wildcards"));
            if (Directory.Exists(candidate)) return candidate;

            // Fallback for tests / repacked layouts.
            var sibling = Path.GetFullPath(Path.Combine(env.ContentRootPath, "Documentation", "Wildcards"));
            if (Directory.Exists(sibling)) return sibling;

            throw new DirectoryNotFoundException(
                $"WildcardForgeKnowledge: cannot locate Documentation/Wildcards. Tried: {candidate} and {sibling}");
        }

        private void LoadCategories(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"WildcardForgeKnowledge: missing THEME_CATALOG.json at {path}", path);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("theme_categories", out var arr) || arr.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("THEME_CATALOG.json is missing 'theme_categories' array");

            foreach (var el in arr.EnumerateArray())
            {
                var card = new CategoryCard
                {
                    Id = el.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0,
                    Name = el.GetPropertyOrEmpty("name"),
                    Description = el.GetPropertyOrEmpty("description"),
                    TypicalVerbosity = el.GetPropertyOrDefault("typical_verbosity", "balanced"),
                    Icon = el.GetPropertyOrEmpty("icon"),
                    Subcategories = el.GetStringList("subcategories"),
                    Keywords = el.GetStringList("keywords"),
                    BestPractices = el.GetStringList("best_practices"),
                    Avoid = el.GetStringList("avoid"),
                };

                if (el.TryGetProperty("verbosity_examples", out var ve) && ve.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in ve.EnumerateObject())
                    {
                        card.VerbosityExamples[prop.Name] = prop.Value.ValueKind == JsonValueKind.Array
                            ? prop.Value.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
                            : new List<string>();
                    }
                }

                if (el.TryGetProperty("sample_collections", out var sc) && sc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sc.EnumerateArray())
                    {
                        card.SampleCollections.Add(new CategorySampleCollection
                        {
                            Name = s.GetPropertyOrEmpty("name"),
                            Verbosity = s.GetPropertyOrEmpty("verbosity"),
                            EntryCount = s.TryGetProperty("entry_count", out var ec) && ec.TryGetInt32(out var n) ? n : 0,
                            Examples = s.GetStringList("examples")
                        });
                    }
                }

                _categories.Add(card);
            }
        }

        private void LoadTemplates(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"WildcardForgeKnowledge: missing LLM_PROMPTS.json at {path}", path);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("prompt_templates", out var obj) || obj.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("LLM_PROMPTS.json is missing 'prompt_templates' object");

            foreach (var prop in obj.EnumerateObject())
            {
                var t = new LlmTemplate
                {
                    Key = prop.Name,
                    Name = prop.Value.GetPropertyOrEmpty("name"),
                    Description = prop.Value.GetPropertyOrEmpty("description"),
                    SystemPrompt = prop.Value.GetPropertyOrEmpty("system_prompt"),
                    UserPrompt = prop.Value.GetPropertyOrEmpty("user_prompt"),
                };

                if (prop.Value.TryGetProperty("recommended_settings", out var rs) && rs.ValueKind == JsonValueKind.Object)
                {
                    if (rs.TryGetProperty("temperature", out var temp) && temp.TryGetDouble(out var tv)) t.RecommendedTemperature = (float)tv;
                    if (rs.TryGetProperty("top_p", out var tp) && tp.TryGetDouble(out var tpv)) t.RecommendedTopP = (float)tpv;
                    if (rs.TryGetProperty("max_tokens", out var mt) && mt.TryGetInt32(out var mtv)) t.RecommendedMaxTokens = mtv;
                }

                _templates[prop.Name] = t;
            }
        }
    }

    internal static class JsonElementExtensions
    {
        public static string GetPropertyOrEmpty(this JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

        public static string GetPropertyOrDefault(this JsonElement el, string name, string fallback)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

        public static List<string> GetStringList(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return new List<string>();
            return arr.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? string.Empty : string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
    }
}
