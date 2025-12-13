using BlazorWebApp.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class CacheService
    {
        private readonly IDatabaseService _db;
        private readonly ILogger<CacheService> _logger;
        private readonly IWebHostEnvironment _env;

        private ConcurrentDictionary<string, int> _tagUsageCache = new();
        private Queue<string> _recentTagsQueue = new();
        private readonly int _maxRecentTags = 50;
        private Dictionary<DictionaryTheme, List<string>> _dictionaries = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(30);
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private readonly string _dictionariesPath;
        private bool _dictionariesLoaded = false;

        public CacheService(IDatabaseService db, ILogger<CacheService> logger, IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
            _dictionariesPath = Path.Combine(_env.ContentRootPath, "Data", "Dictionaries");

            _ = LoadDictionaries();
        }

        #region Tags
        /// <summary>
        /// Gets local usage count for a tag (cached for performance)
        /// </summary>
        public async Task<int> GetLocalTagUsageCount(string tagName)
        {
            await EnsureTagCacheUpdated();

            var normalizedTag = NormalizeTag(tagName);
            return _tagUsageCache.TryGetValue(normalizedTag, out var count) ? count : 0;
        }

        /// <summary>
        /// Gets top N most used tags locally
        /// </summary>
        public async Task<List<(string Tag, int Count)>> GetTopLocalTags(int count = 100)
        {
            await EnsureTagCacheUpdated();

            return _tagUsageCache
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }

        /// <summary>
        /// Gets the most recently used tags.
        /// Returns tags in order of most recent to least recent.
        /// </summary>
        /// <param name="count">Maximum number of recent tags to return</param>
        /// <returns>List of recently used tag names</returns>
        public List<string> GetRecentTags(int count = 10)
        {
            lock (_recentTagsQueue)
            {
                return _recentTagsQueue.Take(count).ToList();
            }
        }

        /// <summary>
        /// Increment usage for a tag when user selects it (real-time tracking)
        /// </summary>
        public void IncrementTagUsage(string tagName)
        {
            var normalized = NormalizeTag(tagName);
            _tagUsageCache.AddOrUpdate(normalized, 1, (_, count) => count + 1);

            lock (_recentTagsQueue)
            {
                // Remove tag if it already exists to avoid duplicates
                var tempQueue = new Queue<string>(_recentTagsQueue.Where(t => !t.Equals(normalized, StringComparison.InvariantCultureIgnoreCase)));

                // Add tag at the front (most recent)
                tempQueue = new Queue<string>(new[] { normalized }.Concat(tempQueue));

                // Limit queue size
                while (tempQueue.Count > _maxRecentTags)
                {
                    tempQueue.TryDequeue(out _);
                }

                _recentTagsQueue = tempQueue;
            }
        }

        /// <summary>
        /// Manually refresh the cache (call after generating images)
        /// </summary>
        public async Task RefreshTagCache()
        {
            await _cacheLock.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing tag usage cache from database...");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var newCache = new ConcurrentDictionary<string, int>();

                var images = await _db.GetRecentImagesWithPrompts(10000);

                foreach (var image in images)
                {
                    if (!string.IsNullOrWhiteSpace(image.Prompt))
                    {
                        var tags = ParseTagsFromPrompt(image.Prompt);
                        foreach (var tag in tags)
                        {
                            var normalized = NormalizeTag(tag);
                            newCache.AddOrUpdate(normalized, 1, (_, count) => count + 1);
                        }
                    }

                    //if (!string.IsNullOrWhiteSpace(image.NegativePrompt))
                    //{
                    //    var tags = ParseTagsFromPrompt(image.NegativePrompt);
                    //    foreach (var tag in tags)
                    //    {
                    //        var normalized = NormalizeTag(tag);
                    //        newCache.AddOrUpdate(normalized, 1, (_, count) => count + 1);
                    //    }
                    //}
                }

                _tagUsageCache = newCache;
                _lastCacheUpdate = DateTime.Now;

                stopwatch.Stop();
                _logger.LogInformation($"Tag usage cache refreshed. {_tagUsageCache.Count} unique tags found in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing tag usage cache");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task EnsureTagCacheUpdated()
        {
            if (DateTime.Now - _lastCacheUpdate > _cacheLifetime)
            {
                await RefreshTagCache();
            }
        }

        private List<string> ParseTagsFromPrompt(string prompt)
        {
            // Remove weight syntax like (tag:1.2) and <lora:...>
            var cleaned = Regex.Replace(prompt, @"\([^)]+:\d+\.?\d*\)", match =>
            {
                var inner = match.Value.Trim('(', ')');
                return inner.Split(':')[0];
            });

            cleaned = Regex.Replace(cleaned, @"<lora:[^>]+>", "");

            return cleaned
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
        }

        private string NormalizeTag(string tag)
        {
            return tag.ToLowerInvariant().Replace(" ", "_").Trim();
        }
        #endregion

        #region Dictionaries
        /// <summary>
        /// Loads all dictionary files from the Data/Dictionaries folder.
        /// Dictionary files should be named using the DictionaryTheme enum values (e.g., Scene.txt, Photography.txt).
        /// Each file should contain one word or phrase per line.
        /// </summary>
        public async Task LoadDictionaries()
        {
            try
            {
                _logger.LogDebug("Loading dictionaries from {Path}...", _dictionariesPath);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                if (!Directory.Exists(_dictionariesPath))
                {
                    _logger.LogWarning("Dictionaries directory not found at {Path}. Creating directory...", _dictionariesPath);
                    Directory.CreateDirectory(_dictionariesPath);
                    await CreateSampleDictionaries();
                }

                var newDictionaries = new Dictionary<DictionaryTheme, List<string>>();

                foreach (DictionaryTheme theme in Enum.GetValues(typeof(DictionaryTheme)))
                {
                    var filePath = Path.Combine(_dictionariesPath, $"{theme}.txt");

                    if (File.Exists(filePath))
                    {
                        var words = await File.ReadAllLinesAsync(filePath);
                        var cleanWords = words
                            .Where(w => !string.IsNullOrWhiteSpace(w) && !w.TrimStart().StartsWith("#")) // Skip empty lines and comments
                            .Select(w => w.Trim())
                            .Distinct(StringComparer.InvariantCultureIgnoreCase)
                            .ToList();

                        newDictionaries[theme] = cleanWords;
                        _logger.LogInformation("Loaded {Count} words from {Theme} dictionary", cleanWords.Count, theme);
                    }
                    else
                    {
                        _logger.LogDebug("Dictionary file not found for theme: {Theme}", theme);
                        newDictionaries[theme] = new List<string>();
                    }
                }

                _dictionaries = newDictionaries;
                _dictionariesLoaded = true;

                stopwatch.Stop();
                var totalWords = _dictionaries.Sum(d => d.Value.Count);
                _logger.LogInformation("Dictionaries loaded successfully. Total: {TotalWords} words across {ThemeCount} themes in {Elapsed}ms",
                    totalWords, _dictionaries.Count, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dictionaries");
                _dictionariesLoaded = false;
            }
        }

        /// <summary>
        /// Searches across all dictionaries for words matching the query using fuzzy matching.
        /// Returns results sorted by relevance score.
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <param name="excludeThemes">Optional themes to exclude from search</param>
        /// <returns>List of matching dictionary words with scores</returns>
        public List<DictionaryWord> SearchDictionaries(string query, int maxResults = 10, params DictionaryTheme[] excludeThemes)
        {
            if (string.IsNullOrWhiteSpace(query) || !_dictionariesLoaded)
                return new List<DictionaryWord>();

            var lowerQuery = query.ToLowerInvariant();
            var results = new List<DictionaryWord>();
            var excludeSet = new HashSet<DictionaryTheme>(excludeThemes);

            foreach (var (theme, words) in _dictionaries)
            {
                if (excludeSet.Contains(theme))
                    continue;

                foreach (var word in words)
                {
                    int score = CalculateFuzzyScore(lowerQuery, word.ToLowerInvariant());

                    if (score > 0)
                    {
                        results.Add(new DictionaryWord
                        {
                            Word = word,
                            Theme = theme,
                            Score = score,
                            Source = theme.ToString()
                        });
                    }
                }
            }

            return results
                .OrderByDescending(w => w.Score)
                .ThenBy(w => w.Word.Length)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Searches for words within specific dictionary themes.
        /// Useful for context-aware autocomplete (e.g., only show negative prompts).
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <param name="themes">Specific themes to search within</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of matching dictionary words with scores</returns>
        public List<DictionaryWord> SearchDictionaryThemes(string query, DictionaryTheme[] themes, int maxResults = 10)
        {
            if (string.IsNullOrWhiteSpace(query) || !_dictionariesLoaded || themes == null || themes.Length == 0)
                return new List<DictionaryWord>();

            var lowerQuery = query.ToLowerInvariant();
            var results = new List<DictionaryWord>();

            foreach (var theme in themes)
            {
                if (!_dictionaries.ContainsKey(theme))
                    continue;

                foreach (var word in _dictionaries[theme])
                {
                    int score = CalculateFuzzyScore(lowerQuery, word.ToLowerInvariant());

                    if (score > 0)
                    {
                        results.Add(new DictionaryWord
                        {
                            Word = word,
                            Theme = theme,
                            Score = score,
                            Source = theme.ToString()
                        });
                    }
                }
            }

            return results
                .OrderByDescending(w => w.Score)
                .ThenBy(w => w.Word.Length)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Gets all words from a specific dictionary theme.
        /// </summary>
        /// <param name="theme">Dictionary theme to retrieve</param>
        /// <returns>List of words in the specified theme</returns>
        public List<string> GetDictionaryWords(DictionaryTheme theme)
        {
            if (!_dictionariesLoaded || !_dictionaries.ContainsKey(theme))
                return new List<string>();

            return _dictionaries[theme].ToList();
        }

        /// <summary>
        /// Calculates a fuzzy matching score between search query and target word.
        /// Higher scores indicate better matches.
        /// Supports:
        /// - Exact matching
        /// - Prefix matching (starts with)
        /// - Substring matching (contains)
        /// - Subsequence matching (characters appear in order, e.g., "cascl" matches "casual clothing")
        /// - Levenshtein distance for typo tolerance
        /// </summary>
        /// <param name="search">Search query (normalized to lowercase)</param>
        /// <param name="target">Target word (normalized to lowercase)</param>
        /// <returns>Match score (0 = no match, 1000 = exact match)</returns>
        public int CalculateFuzzyScore(string search, string target)
        {
            if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(target))
                return 0;

            // Exact match (highest priority)
            if (target == search) return 1000;

            // Starts with search text (very high priority)
            if (target.StartsWith(search)) return 500;

            // Contains search text as a word boundary
            if (target.Contains($" {search}")) return 300;

            // Contains search text anywhere (substring match)
            if (target.Contains(search)) return 250;

            // Subsequence matching: all characters appear in order
            // e.g., "cascl" matches "casual clothing"
            int subsequenceScore = CalculateSubsequenceScore(search, target);
            if (subsequenceScore > 0) return subsequenceScore;

            // Levenshtein distance for fuzzy matching (typo tolerance)
            int distance = LevenshteinDistance(search, target);
            int maxLength = Math.Max(search.Length, target.Length);
            int similarity = (int)(100 * (1 - ((double)distance / maxLength)));

            return similarity > 70 ? similarity : 0;
        }

        /// <summary>
        /// Calculates a score for subsequence matching.
        /// A subsequence match means all characters from the search string appear in the target
        /// in the same order, but not necessarily consecutively.
        /// Examples:
        /// - "cascl" matches "casual clothing" (c-a-s-u-a-l c-l-o-t-h-i-n-g)
        /// </summary>
        /// <param name="search">Search query</param>
        /// <param name="target">Target word</param>
        /// <returns>Score based on match quality (0-200)</returns>
        private int CalculateSubsequenceScore(string search, string target)
        {
            int searchIdx = 0;
            int targetIdx = 0;
            int consecutiveMatches = 0;
            int maxConsecutive = 0;
            int totalGaps = 0;

            while (searchIdx < search.Length && targetIdx < target.Length)
            {
                if (search[searchIdx] == target[targetIdx])
                {
                    searchIdx++;
                    consecutiveMatches++;
                    maxConsecutive = Math.Max(maxConsecutive, consecutiveMatches);
                }
                else
                {
                    if (consecutiveMatches > 0)
                    {
                        totalGaps++;
                    }
                    consecutiveMatches = 0;
                }
                targetIdx++;
            }

            // If we didn't match all search characters, it's not a subsequence match
            if (searchIdx < search.Length)
                return 0;

            // Calculate score based on:
            // - How much of the search was matched consecutively
            // - How compact the match is (fewer gaps = better)
            int baseScore = 200; // Base score for subsequence match
            int consecutiveBonus = (maxConsecutive * 20) / search.Length; // Bonus for consecutive matches
            int gapPenalty = Math.Min(totalGaps * 10, 50); // Penalty for gaps (capped at 50)

            return Math.Max(0, baseScore + consecutiveBonus - gapPenalty);
        }

        /// <summary>
        /// Calculates the Levenshtein distance between two strings.
        /// This represents the minimum number of single-character edits required to change one word into another.
        /// </summary>
        private int LevenshteinDistance(string source, string target)
        {
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            int[,] matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; matrix[i, 0] = i++) { }
            for (int j = 0; j <= target.Length; matrix[0, j] = j++) { }

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        /// <summary>
        /// Creates sample dictionary files with example words for each theme.
        /// Called automatically if the dictionaries directory doesn't exist.
        /// </summary>
        private async Task CreateSampleDictionaries()
        {
            _logger.LogInformation("Creating sample dictionary files...");

            var sampleData = new Dictionary<DictionaryTheme, string[]>
            {
                [DictionaryTheme.Quality] = new[]
                {
                    "# Quality descriptors",
                    "masterpiece",
                    "best quality",
                    "high quality",
                    "ultra detailed",
                    "highly detailed",
                    "sharp focus",
                    "professional",
                    "8k uhd",
                    "absurdres"
                },
                [DictionaryTheme.Scene] = new[]
                {
                    "# Scene locations",
                    "beach",
                    "forest",
                    "mountain",
                    "urban",
                    "city street",
                    "park",
                    "desert",
                    "ocean",
                    "lake",
                    "river"
                },
                [DictionaryTheme.Negative] = new[]
                {
                    "# Negative prompts",
                    "low quality",
                    "worst quality",
                    "low resolution",
                    "blurry",
                    "bad anatomy",
                    "bad hands",
                    "poorly drawn",
                    "mutation",
                    "deformed"
                }
            };

            foreach (var (theme, words) in sampleData)
            {
                var filePath = Path.Combine(_dictionariesPath, $"{theme}.txt");
                await File.WriteAllLinesAsync(filePath, words);
                _logger.LogInformation("Created sample dictionary: {Theme}.txt", theme);
            }
        }

        /// <summary>
        /// Reloads all dictionaries from disk.
        /// Call this after manually updating dictionary files.
        /// </summary>
        public async Task ReloadDictionaries()
        {
            _logger.LogInformation("Reloading dictionaries...");
            await LoadDictionaries();
        }

        /// <summary>
        /// Checks if dictionaries have been loaded successfully.
        /// </summary>
        public bool AreDictionariesLoaded() => _dictionariesLoaded;

        /// <summary>
        /// Gets statistics about loaded dictionaries.
        /// </summary>
        /// <returns>Dictionary mapping theme names to word counts</returns>
        public Dictionary<string, int> GetDictionaryStats()
        {
            return _dictionaries.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value.Count
            );
        }
        #endregion
    }
}
