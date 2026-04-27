using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using Sylvan.Data;
using Sylvan.Data.Csv;

namespace BlazorWebApp.Services
{
    public class CsvService
    {
        private readonly IBackendService _backend;
        private readonly IConfiguration _configuration;
        private readonly ICacheService _cacheService;
        private readonly Schema _schema;
        private readonly CsvDataReaderOptions _options;
        // Resolved lazily so the singleton survives a backend that wasn't ready at construction time.
        private string _path;
        private string _fileName;

        // Lazy in-memory cache of parsed CSV tags. Loaded on first access; reused for all subsequent searches.
        // Holds tags in their CSV form (underscored name + raw aliases) so consumers can decide on display formatting.
        private List<Tag>? _cachedRawTags;
        private HashSet<string>? _cachedRawNames; // exact-match lookup (underscored, lower-cased)
        private readonly object _cacheLock = new();

        public CsvService(IBackendService backend, IConfiguration configuration, ICacheService cacheService)
        {
            _backend = backend;
            _configuration = configuration;
            _cacheService = cacheService;
            _schema = Schema.Parse("Name,Color,Uses,Aliases");
            _options = new CsvDataReaderOptions() { Schema = new CsvSchema(_schema), HasHeaders = false };

            // ComfyUI only - WebUI removed. Path is resolved lazily; if the backend isn't
            // ready when the singleton is constructed, the cache will resolve it on first access.
            _path = _backend.IsBackendAvailable ? ResolveCsvPath() : string.Empty;
            _fileName = !string.IsNullOrWhiteSpace(_path) ? Path.GetFileNameWithoutExtension(_path) : string.Empty;
        }

        private string ResolveCsvPath()
        {
            var inputsRoot = _configuration["ComfyUI:InputsPath"];
            return string.IsNullOrWhiteSpace(inputsRoot)
                ? string.Empty
                : Path.Combine(inputsRoot, "danbooru.csv");
        }

        public async Task<IEnumerable<Tag>> SearchTags(string searchText, bool enableFuzzy = true)
        {
            var rawTags = LoadRawTagsCached();
            if (rawTags.Count == 0)
                return Enumerable.Empty<Tag>();

            searchText = searchText.Replace(" ", "_");

            var recentTags = _cacheService.GetRecentTags(10);
            List<Tag> result = new();

            foreach (var tag in rawTags)
            {
                var parsed = Parser.ParseCsvTag(tag);
                parsed.Source = _fileName;

                // Calculate fuzzy score
                if (enableFuzzy)
                {
                    parsed.FuzzyScore = CalculateFuzzyScore(searchText, tag.Name, tag.Aliases, recentTags.Contains(tag.Name));

                    if (parsed.FuzzyScore == 0) continue; // Skip if no match
                }
                else
                {
                    // Standard contains matching (fallback)
                    if (!tag.Name.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) &&
                        !(tag.Aliases?.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ?? false))
                        continue;

                    parsed.FuzzyScore = tag.Name.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase) ? 500 : 250;
                }

                // Get local usage
                var localUsage = await _cacheService.GetLocalTagUsageCount(tag.Name);
                parsed.LocalUses = localUsage;
                parsed.IsRecentlyUsed = recentTags.Contains(tag.Name);

                result.Add(parsed);
            }

            // Multi-tier sorting:
            // 1. Recently used tags first
            // 2. Then by local usage
            // 3. Then by fuzzy score
            // 4. Finally by global popularity
            return result
                .OrderByDescending(t => t.IsRecentlyUsed ? 1 : 0)
                .ThenByDescending(t => t.LocalUses)
                .ThenByDescending(t => t.FuzzyScore)
                .ThenByDescending(t => t.Uses);
        }

        private int CalculateFuzzyScore(string searchText, string tagName, string aliases, bool isRecent)
        {
            int score = _cacheService.CalculateFuzzyScore(searchText, tagName);

            // Check aliases
            if (!string.IsNullOrWhiteSpace(aliases) && score < 500)
            {
                var aliasesList = aliases.ToLowerInvariant().Replace("\"", "").Split(',');
                foreach (var alias in aliasesList)
                {
                    int aliasScore = _cacheService.CalculateFuzzyScore(searchText, alias.Trim());
                    score = Math.Max(score, aliasScore - 50); // Slight penalty for alias match
                }
            }

            // Boost recently used tags
            if (isRecent) score += 200;

            return score;
        }

        public Tag? GetTag(string name)
        {
            var rawTags = LoadRawTagsCached();
            if (rawTags.Count == 0) return null;
            name = name.Replace(" ", "_");
            return rawTags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public bool CheckTagExists(string name)
        {
            var names = LoadRawNamesCached();
            if (names.Count == 0) return false;
            return names.Contains(name.Replace(" ", "_").ToLowerInvariant());
        }

        /// <summary>
        /// Fast existence check using the cached name set. Identical to <see cref="CheckTagExists"/>
        /// but named to make cache use explicit at the call site.
        /// </summary>
        public bool CheckTagExistsCached(string name) => CheckTagExists(name);

        /// <summary>
        /// Returns the cached list of raw CSV tags (underscored names + raw aliases). Loads on first call.
        /// </summary>
        public IReadOnlyList<Tag> GetAllTagsCached() => LoadRawTagsCached();

        private List<Tag> LoadRawTagsCached()
        {
            if (_cachedRawTags != null) return _cachedRawTags;
            lock (_cacheLock)
            {
                if (_cachedRawTags != null) return _cachedRawTags;

                // Late path resolution: if the backend wasn't ready at construction we still
                // try to resolve here so the cache fills as soon as the configuration is usable.
                if (string.IsNullOrWhiteSpace(_path))
                {
                    _path = ResolveCsvPath();
                    if (!string.IsNullOrWhiteSpace(_path))
                        _fileName = Path.GetFileNameWithoutExtension(_path);
                }

                if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
                {
                    _cachedRawTags = new List<Tag>();
                    _cachedRawNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    return _cachedRawTags;
                }

                using var reader = CsvDataReader.Create(_path, _options);
                _cachedRawTags = reader.GetRecords<CsvTag>().Select(csvTag => new Tag
                {
                    Name = csvTag.Name,
                    Color = csvTag.Color,
                    Uses = csvTag.Uses,
                    Aliases = csvTag.Aliases,
                    Source = _fileName
                }).ToList();
                _cachedRawNames = new HashSet<string>(_cachedRawTags.Select(t => t.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                return _cachedRawTags;
            }
        }

        private HashSet<string> LoadRawNamesCached()
        {
            if (_cachedRawNames != null) return _cachedRawNames;
            LoadRawTagsCached();
            return _cachedRawNames!;
        }
    }
}
