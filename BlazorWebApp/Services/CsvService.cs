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
        private List<TagSearchEntry>? _cachedSearchEntries;
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

        public async Task<IEnumerable<Tag>> SearchTags(string searchText, bool enableFuzzy = true, int maxResults = 0)
        {
            var searchEntries = LoadSearchEntriesCached();
            if (searchEntries.Count == 0)
                return Enumerable.Empty<Tag>();

            searchText = searchText.Replace(" ", "_").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(searchText))
                return Enumerable.Empty<Tag>();

            var recentTags = new HashSet<string>(_cacheService.GetRecentTags(10), StringComparer.OrdinalIgnoreCase);
            var localUsage = await _cacheService.GetLocalTagUsageSnapshot();
            var resultLimit = maxResults > 0 ? maxResults : int.MaxValue;
            var ranked = new List<TagSearchResult>(maxResults > 0 ? maxResults + 1 : 128);

            foreach (var entry in searchEntries)
            {
                int fuzzyScore;
                if (enableFuzzy)
                {
                    fuzzyScore = CalculateFuzzyScore(searchText, entry.NameSearch, entry.AliasSearchTerms, recentTags.Contains(entry.NameSearch));
                    if (fuzzyScore == 0) continue;
                }
                else
                {
                    if (!entry.NameSearch.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                        !entry.AliasSearch.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    fuzzyScore = entry.NameSearch.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ? 500 : 250;
                }

                var isRecentlyUsed = recentTags.Contains(entry.NameSearch);
                var localUses = localUsage.TryGetValue(entry.NameSearch, out var usageCount) ? usageCount : 0;
                AddRankedResult(ranked, new TagSearchResult(entry, fuzzyScore, localUses, isRecentlyUsed), resultLimit);
            }

            ranked.Sort(CompareSearchResults);
            return ranked.Select(MaterializeTag).ToList();
        }

        private void AddRankedResult(List<TagSearchResult> results, TagSearchResult candidate, int limit)
        {
            if (limit == int.MaxValue)
            {
                results.Add(candidate);
                return;
            }

            results.Add(candidate);
            results.Sort(CompareSearchResults);
            if (results.Count > limit)
                results.RemoveAt(results.Count - 1);
        }

        private static int CompareSearchResults(TagSearchResult left, TagSearchResult right)
        {
            var recentComparison = right.IsRecentlyUsed.CompareTo(left.IsRecentlyUsed);
            if (recentComparison != 0) return recentComparison;

            var localUsageComparison = right.LocalUses.CompareTo(left.LocalUses);
            if (localUsageComparison != 0) return localUsageComparison;

            var fuzzyComparison = right.FuzzyScore.CompareTo(left.FuzzyScore);
            if (fuzzyComparison != 0) return fuzzyComparison;

            return right.Entry.Uses.CompareTo(left.Entry.Uses);
        }

        private Tag MaterializeTag(TagSearchResult result) => new()
        {
            Name = result.Entry.DisplayName,
            Aliases = result.Entry.DisplayAliases,
            Color = result.Entry.Color,
            Uses = result.Entry.Uses,
            Source = _fileName,
            LocalUses = result.LocalUses,
            IsRecentlyUsed = result.IsRecentlyUsed,
            FuzzyScore = result.FuzzyScore
        };

        private int CalculateFuzzyScore(string searchText, string tagName, IReadOnlyList<string> aliases, bool isRecent)
        {
            int score = _cacheService.CalculateFuzzyScore(searchText, tagName);

            // Check aliases
            if (aliases.Count > 0 && score < 500)
            {
                foreach (var alias in aliases)
                {
                    int aliasScore = _cacheService.CalculateFuzzyScore(searchText, alias);
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

        private List<TagSearchEntry> LoadSearchEntriesCached()
        {
            if (_cachedSearchEntries != null) return _cachedSearchEntries;
            LoadRawTagsCached();
            return _cachedSearchEntries!;
        }

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
                    _cachedSearchEntries = new List<TagSearchEntry>();
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
                _cachedSearchEntries = _cachedRawTags.Select(CreateSearchEntry).ToList();
                return _cachedRawTags;
            }
        }

        private TagSearchEntry CreateSearchEntry(Tag tag)
        {
            var aliasSearch = tag.Aliases?.ToLowerInvariant().Replace("\"", string.Empty) ?? string.Empty;
            var aliasTerms = aliasSearch
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .ToArray();

            return new TagSearchEntry(
                tag.Name,
                tag.Name.ToLowerInvariant(),
                tag.Name.Replace("_", " "),
                tag.Aliases?.Replace("_", " ") ?? string.Empty,
                aliasSearch,
                aliasTerms,
                tag.Color,
                tag.Uses);
        }

        private HashSet<string> LoadRawNamesCached()
        {
            if (_cachedRawNames != null) return _cachedRawNames;
            LoadRawTagsCached();
            return _cachedRawNames!;
        }

        private sealed record TagSearchEntry(
            string RawName,
            string NameSearch,
            string DisplayName,
            string DisplayAliases,
            string AliasSearch,
            IReadOnlyList<string> AliasSearchTerms,
            int Color,
            int Uses);

        private sealed record TagSearchResult(TagSearchEntry Entry, int FuzzyScore, int LocalUses, bool IsRecentlyUsed);
    }
}
