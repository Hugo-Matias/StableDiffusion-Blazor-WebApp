using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using ImageMagick;
using System.Text.Json;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Services
{
    public class TagGroupService : ITagGroupService
    {
        // ── Category → group slug mapping ────────────────────────────────────
        // Order defines the display order in the UI.
        private static readonly IReadOnlyDictionary<string, string[]> CategoryMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Composition"] = new[]
                {
                    "image_composition", "backgrounds", "focus_tags", "lighting",
                    "visual_aesthetic", "artistic_license", "character_count"
                },
                ["Body"] = new[]
                {
                    "face_tags", "eyes_tags", "ears_tags", "hair",
                    "hair_styles", "hair_color", "posture", "gestures",
                    "hands", "body_parts"
                },
                ["Attire"] = new[]
                {
                    "attire", "accessories", "headwear", "legwear",
                    "dress", "handwear", "sleeves", "neck_and_neckwear",
                    "eyewear", "fashion_style", "embellishment", "makeup"
                },
                ["Objects"] = new[]
                {
                    "holding_tags", "audio_tags", "piercings", "flowers"
                },
                ["Real World"] = new[]
                {
                    "locations", "real_world_locations", "jobs",
                    "holidays_and_celebrations", "technology"
                },
                ["Themes"] = new[]
                {
                    "theme", "subjective", "verbs_and_gerunds",
                    "food_tags", "water", "fire", "dances"
                },
            };

        // ── JSONL page_slug → our group slug ────────────────────────────────
        // Mirrors the SLUG_MAP in Utils/Convert-DanbooruDataset.py.
        // page_slug is the key in tag_groups_flat.jsonl; value is the short slug
        // used throughout the app (CategoryMap, TagGroupConfig, preview images).
        private static readonly IReadOnlyDictionary<string, string> PageSlugMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Composition
                ["tag_group:image_composition"] = "image_composition",
                ["tag_group:backgrounds"] = "backgrounds",
                ["tag_group:focus_tags"] = "focus_tags",
                ["tag_group:lighting"] = "lighting",
                ["tag_group:visual_aesthetic"] = "visual_aesthetic",
                ["tag_group:artistic_license"] = "artistic_license",
                ["tag_group:character_count"] = "character_count",
                // Body
                ["tag_group:face_tags"] = "face_tags",
                ["tag_group:eyes_tags"] = "eyes_tags",
                ["tag_group:ears_tags"] = "ears_tags",
                ["tag_group:hair"] = "hair",
                ["tag_group:hair_styles"] = "hair_styles",
                ["tag_group:hair_color"] = "hair_color",
                ["tag_group:posture"] = "posture",
                ["tag_group:gestures"] = "gestures",
                ["tag_group:hands"] = "hands",
                ["tag_group:body_parts"] = "body_parts",
                // Attire
                ["tag_group:attire"] = "attire",
                ["tag_group:accessories"] = "accessories",
                ["tag_group:headwear"] = "headwear",
                ["tag_group:legwear"] = "legwear",
                ["tag_group:dress"] = "dress",
                ["tag_group:handwear"] = "handwear",
                ["tag_group:sleeves"] = "sleeves",
                ["tag_group:neck_and_neckwear"] = "neck_and_neckwear",
                ["tag_group:eyewear"] = "eyewear",
                ["tag_group:fashion_style"] = "fashion_style",
                ["tag_group:embellishment"] = "embellishment",
                ["tag_group:makeup"] = "makeup",
                // Objects
                ["tag_group:holding_tags"] = "holding_tags",
                ["tag_group:audio_tags"] = "audio_tags",
                ["tag_group:piercings"] = "piercings",
                ["tag_group:flowers"] = "flowers",
                // Real World
                ["tag_group:locations"] = "locations",
                ["tag_group:real_world_locations"] = "real_world_locations",
                ["tag_group:jobs"] = "jobs",
                ["tag_group:holidays_and_celebrations"] = "holidays_and_celebrations",
                ["tag_group:technology"] = "technology",
                // Themes
                ["tag_group:theme"] = "theme",
                ["tag_group:subjective"] = "subjective",
                ["tag_group:verbs_and_gerunds"] = "verbs_and_gerunds",
                ["tag_group:food_tags"] = "food_tags",
                ["tag_group:water"] = "water",
                ["tag_group:fire"] = "fire",
                ["tag_group:dances"] = "dances",
            };

        // Default preview templates per category.
        private static readonly IReadOnlyDictionary<string, string> DefaultTemplates =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Composition"] = "1girl, school_uniform, simple background, masterpiece, best quality, {tag}",
                ["Body"] = "1girl, upper body, close-up, simple background, masterpiece, best quality, {tag}",
                ["Attire"] = "1girl, standing, simple background, full body, masterpiece, best quality, {tag}",
                ["Objects"] = "1girl, school_uniform, simple background, masterpiece, best quality, {tag}",
                ["Real World"] = "1girl, standing, school_uniform, masterpiece, best quality, {tag}",
                ["Themes"] = "1girl, school_uniform, simple background, masterpiece, best quality, {tag}",
            };

        private const string DefaultPositive = "1girl, school_uniform, simple background, masterpiece, best quality, {tag}";

        private readonly IStateService _state;
        private readonly IRouterService _router;
        private readonly IWorkflowService _workflowService;
        private readonly ILogger<TagGroupService> _logger;
        private readonly string _wwwrootPath;
        private readonly string _dataPath;
        private readonly string _configPath;
        private readonly string _promptsPath;

        private readonly List<DanbooruTag> _allTags = new();
        private readonly List<TagGroupDescriptor> _groups = new();
        private readonly Dictionary<string, TagGroupConfig> _config = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _groupPrompts = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _loadLock = new();
        private bool _loaded;

        // ── Reverse lookup: group → category ─────────────────────────────────
        private static readonly IReadOnlyDictionary<string, string> GroupToCategory =
            CategoryMap
                .SelectMany(kv => kv.Value.Select(g => (Group: g, Category: kv.Key)))
                .ToDictionary(t => t.Group, t => t.Category, StringComparer.OrdinalIgnoreCase);

        public TagGroupPreviewProgress PreviewProgress { get; } = new();

        public TagGroupService(
            IStateService state,
            IRouterService router,
            IWorkflowService workflowService,
            IWebHostEnvironment env,
            ILogger<TagGroupService> logger)
        {
            _state = state;
            _router = router;
            _workflowService = workflowService;
            _logger = logger;
            _wwwrootPath = env.WebRootPath;
            _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            _configPath = Path.Combine(_dataPath, "tag-group-config.json");
            _promptsPath = Path.Combine(_dataPath, "tag_group_prompts.json");
        }

        // ── Catalogue ────────────────────────────────────────────────────────

        public IReadOnlyList<string> Categories => CategoryMap.Keys.ToList();

        public IReadOnlyList<TagGroupDescriptor> Groups
        {
            get { EnsureLoaded(); return _groups; }
        }

        public IReadOnlyList<TagGroupDescriptor> GetGroupsForCategory(string category)
        {
            EnsureLoaded();
            return _groups.Where(g => g.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public IReadOnlyList<string> GetSections(string group)
        {
            EnsureLoaded();
            // Return sections in order of first appearance within the group.
            return _allTags
                .Where(t => t.Group.Equals(group, StringComparison.OrdinalIgnoreCase) && t.Section != null)
                .Select(t => t.Section!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<DanbooruTag> GetTags(
            string? search = null,
            string? group = null,
            string? category = null,
            string? section = null,
            TagGroupSortMode sort = TagGroupSortMode.Count,
            bool descending = true)
        {
            EnsureLoaded();

            var query = _allTags.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(group))
                query = query.Where(t => t.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
            else if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(section))
                query = query.Where(t => t.Section != null &&
                    t.Section.Equals(section, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t =>
                    t.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    t.Label.Contains(search, StringComparison.OrdinalIgnoreCase));

            query = sort switch
            {
                TagGroupSortMode.Name => descending
                    ? query.OrderByDescending(t => t.Label)
                    : query.OrderBy(t => t.Label),
                _ => descending
                    ? query.OrderByDescending(t => t.Count)
                    : query.OrderBy(t => t.Count),
            };

            return query.ToList();
        }

        // ── Config ───────────────────────────────────────────────────────────

        public TagGroupConfig GetConfig(string group)
        {
            EnsureLoaded();
            return _config.TryGetValue(group, out var cfg) ? cfg : BuildDefaultConfig(group);
        }

        public void SetConfig(string group, string previewTemplate)
        {
            EnsureLoaded();
            if (!_config.TryGetValue(group, out var cfg))
            {
                cfg = new TagGroupConfig { Group = group };
                _config[group] = cfg;
            }
            cfg.PreviewTemplate = previewTemplate;

            // Propagate to the matching descriptor so the panel sees it live.
            var desc = _groups.FirstOrDefault(g => g.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
            if (desc != null) desc.Config = cfg;
        }

        public async Task SaveConfigAsync()
        {
            var list = _config.Values.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }

        // ── Preview images ───────────────────────────────────────────────────

        public string GetPreviewImagePath(DanbooruTag tag) =>
            Path.Combine(_wwwrootPath, "images", "tags", tag.Group, $"{tag.Name}.webp");

        public bool HasPreviewImage(DanbooruTag tag) =>
            File.Exists(GetPreviewImagePath(tag));

        // ── Preview generation ───────────────────────────────────────────────

        public async Task GenerateBatchPreviewsAsync(
            List<DanbooruTag> tags,
            int batchSize,
            bool skipExisting,
            CancellationToken ct,
            Func<Task>? onProgress = null)
        {
            PreviewProgress.IsRunning = true;
            PreviewProgress.Current = 0;
            PreviewProgress.Skipped = 0;
            PreviewProgress.Failed = 0;
            var effectiveBatch = batchSize <= 0 ? tags.Count : batchSize;
            PreviewProgress.Total = Math.Min(effectiveBatch, tags.Count);
            PreviewProgress.CurrentTag = string.Empty;

            var workflowId = _state.State.Generation.CurrentWorkflowId;
            if (workflowId == null)
            {
                _logger.LogError("No workflow selected for tag preview generation");
                PreviewProgress.IsRunning = false;
                return;
            }

            var workflow = _workflowService.GetWorkflowById(workflowId.Value);
            if (workflow == null)
            {
                _logger.LogError("Workflow {Id} not found", workflowId);
                PreviewProgress.IsRunning = false;
                return;
            }

            var generated = 0;
            try
            {
                foreach (var tag in tags)
                {
                    if (ct.IsCancellationRequested || generated >= effectiveBatch)
                        break;

                    PreviewProgress.CurrentTag = tag.Name;

                    if (skipExisting && HasPreviewImage(tag))
                    {
                        PreviewProgress.Skipped++;
                        if (onProgress != null) await onProgress.Invoke();
                        continue;
                    }

                    try
                    {
                        var cfg = GetConfig(tag.Group);
                        var positive = cfg.PreviewTemplate.Replace("{tag}", tag.Name.EscapeParenthesis());

                        var prepared = _state.GenerationParameters.Clone();
                        var promptsFrag = prepared.GetFragment(Fragments.Prompts);
                        if (promptsFrag != null)
                        {
                            promptsFrag.SetValue(Params.Positive, positive);
                            // Negative is left as-is from the current generation parameters.
                        }

                        // Randomize seed when unset — all other params come from the workflow as-is.
                        var samplerFrag = prepared.GetFragment(Fragments.MainSampler)
                                       ?? prepared.GetFragment(Fragments.SamplerAdvanced);
                        if (samplerFrag != null)
                        {
                            var seed = samplerFrag.GetValueOrDefault(Params.Seed, -1L);
                            if (seed <= 0)
                                samplerFrag.SetValue(Params.Seed, (long)Random.Shared.Next(0, int.MaxValue));
                        }

                        var result = await _router.PostGenerationAsync(prepared, workflow);
                        if (result?.Images != null && result.Images.Count > 0)
                        {
                            var imageBytes = Convert.FromBase64String(result.Images[0]);
                            var outputPath = GetPreviewImagePath(tag);
                            var dir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                            using var image = new MagickImage(imageBytes);
                            await image.WriteAsync(outputPath, MagickFormat.WebP);
                        }
                        else
                        {
                            PreviewProgress.Failed++;
                            _logger.LogWarning("No image returned for tag {Name}", tag.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        PreviewProgress.Failed++;
                        _logger.LogError(ex, "Failed to generate preview for tag {Name}", tag.Name);
                    }

                    generated++;
                    PreviewProgress.Current = generated;
                    if (onProgress != null) await onProgress.Invoke();
                }
            }
            finally
            {
                PreviewProgress.IsRunning = false;
                if (onProgress != null) await onProgress.Invoke();
            }
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_loadLock)
            {
                if (_loaded) return;
                Load();
                _loaded = true;
            }
        }

        private void Load()
        {
            LoadConfig();
            LoadTagFiles();
        }

        private void LoadConfig()
        {
            // Load per-group prompt templates (static defaults shipped with the app).
            if (File.Exists(_promptsPath))
            {
                try
                {
                    var json = File.ReadAllText(_promptsPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                               ?? new Dictionary<string, string>();
                    foreach (var kv in dict)
                        if (!kv.Key.StartsWith("_"))   // skip _comment etc.
                            _groupPrompts[kv.Key] = kv.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tag_group_prompts.json");
                }
            }

            // Load user-saved overrides (written by SaveConfigAsync).
            if (!File.Exists(_configPath)) return;
            try
            {
                var json = File.ReadAllText(_configPath);
                var list = JsonSerializer.Deserialize<List<TagGroupConfig>>(json) ?? new();
                foreach (var c in list)
                    _config[c.Group] = c;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tag-group-config.json");
            }
        }

        private void LoadTagFiles()
        {
            var jsonlPath = Path.Combine(_dataPath, "tag_groups_flat.jsonl");
            if (!File.Exists(jsonlPath))
            {
                _logger.LogWarning("tag_groups_flat.jsonl not found in {Path} — tag browser will be empty", _dataPath);
                return;
            }

            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };

            // Pass 1: parse all rows, deduplicate per (groupSlug, canonicalName).
            // Wiki-sourced entries win over implication-derived duplicates.
            // Insertion order is preserved so sections appear in wiki order.
            var groupTagMaps = new Dictionary<string, Dictionary<string, (TagGroupJsonlRow Row, bool IsWiki)>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var line in File.ReadLines(jsonlPath, System.Text.Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                TagGroupJsonlRow? row;
                try { row = JsonSerializer.Deserialize<TagGroupJsonlRow>(line, jsonOpts); }
                catch { continue; }
                if (row is null) continue;

                if (!PageSlugMap.TryGetValue(row.PageSlug ?? string.Empty, out var groupSlug)) continue;

                var name = row.CanonicalName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;

                var isWiki = string.Equals(row.Source, "wiki", StringComparison.OrdinalIgnoreCase);

                if (!groupTagMaps.TryGetValue(groupSlug, out var tagMap))
                {
                    tagMap = new Dictionary<string, (TagGroupJsonlRow, bool)>(StringComparer.OrdinalIgnoreCase);
                    groupTagMaps[groupSlug] = tagMap;
                }

                // Keep wiki entry; skip implication entry when wiki already present.
                if (tagMap.TryGetValue(name, out var existing) && existing.IsWiki && !isWiki)
                    continue;

                tagMap[name] = (row, isWiki);
            }

            // Pass 2: build DanbooruTag list and group descriptors in category order.
            var categoryOrder = CategoryMap.Keys
                .Select((cat, idx) => (cat, idx))
                .ToDictionary(t => t.cat, t => t.idx, StringComparer.OrdinalIgnoreCase);

            var orderedGroups = groupTagMaps
                .OrderBy(kv =>
                {
                    var cat = GroupToCategory.TryGetValue(kv.Key, out var c) ? c : string.Empty;
                    return categoryOrder.GetValueOrDefault(cat, 99);
                })
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var (groupSlug, tagMap) in orderedGroups)
            {
                var category = GroupToCategory.TryGetValue(groupSlug, out var cat) ? cat : "Other";
                var cfg = _config.TryGetValue(groupSlug, out var existingCfg)
                    ? existingCfg
                    : BuildDefaultConfig(groupSlug);

                foreach (var (name, (row, _)) in tagMap)
                {
                    var sectionPath = row.SectionPath;
                    var rawSection = sectionPath?.Count > 0 ? sectionPath[^1] : row.SectionTitle;
                    var section = string.IsNullOrWhiteSpace(rawSection) ? null : rawSection.Trim();

                    _allTags.Add(new DanbooruTag
                    {
                        Name = name,
                        Label = !string.IsNullOrWhiteSpace(row.TagLabel)
                                       ? row.TagLabel.Trim()
                                       : name.Replace("_", " "),
                        Section = section,
                        Group = groupSlug,
                        Category = category,
                    });
                }

                _config[groupSlug] = cfg;
                _groups.Add(new TagGroupDescriptor
                {
                    Group = groupSlug,
                    Category = category,
                    TagCount = tagMap.Count,
                    Config = cfg,
                });
            }

            _logger.LogInformation(
                "Loaded {Tags} tags across {Groups} groups from tag_groups_flat.jsonl",
                _allTags.Count, _groups.Count);
        }

        private TagGroupConfig BuildDefaultConfig(string group)
        {
            // Per-group prompts file takes priority over category-level defaults.
            if (!_groupPrompts.TryGetValue(group, out var pos))
            {
                var category = GroupToCategory.TryGetValue(group, out var cat) ? cat : string.Empty;
                pos = DefaultTemplates.TryGetValue(category, out var t) ? t : DefaultPositive;
            }
            var cfg = new TagGroupConfig
            {
                Group = group,
                PreviewTemplate = pos,
            };
            _config[group] = cfg;
            return cfg;
        }
    }
}
