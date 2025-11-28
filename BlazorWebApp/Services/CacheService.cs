using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class CacheService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<CacheService> _logger;
        private ConcurrentDictionary<string, int> _tagUsageCache = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(30);
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        public CacheService(DatabaseService db, ILogger<CacheService> logger)
        {
            _db = db;
            _logger = logger;
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

        /// <summary>
        /// Increment usage for a tag when user selects it (real-time tracking)
        /// </summary>
        public void IncrementTagUsage(string tagName)
        {
            var normalized = NormalizeTag(tagName);
            _tagUsageCache.AddOrUpdate(normalized, 1, (_, count) => count + 1);
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
    }
}
