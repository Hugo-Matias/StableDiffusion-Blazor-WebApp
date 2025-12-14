using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service interface for caching tag usage data and dictionary words.
    /// Provides fuzzy matching and search capabilities for autocomplete features.
    /// </summary>
    public interface ICacheService
    {
        #region Tag Operations

        /// <summary>
        /// Gets local usage count for a tag (cached for performance).
        /// </summary>
        /// <param name="tagName">The tag name to look up</param>
        /// <returns>Usage count for the tag</returns>
        Task<int> GetLocalTagUsageCount(string tagName);

        /// <summary>
        /// Gets top N most used tags locally.
        /// </summary>
        /// <param name="count">Number of top tags to return</param>
        /// <returns>List of tag names with their counts</returns>
        Task<List<(string Tag, int Count)>> GetTopLocalTags(int count = 100);

        /// <summary>
        /// Gets the most recently used tags.
        /// Returns tags in order of most recent to least recent.
        /// </summary>
        /// <param name="count">Maximum number of recent tags to return</param>
        /// <returns>List of recently used tag names</returns>
        List<string> GetRecentTags(int count = 10);

        /// <summary>
        /// Increment usage for a tag when user selects it (real-time tracking).
        /// </summary>
        /// <param name="tagName">The tag name to increment</param>
        void IncrementTagUsage(string tagName);

        /// <summary>
        /// Manually refresh the tag cache from the database.
        /// Call after generating images or when cache may be stale.
        /// </summary>
        Task RefreshTagCache();

        #endregion

        #region Dictionary Operations

        /// <summary>
        /// Loads all dictionary files from the Data/Dictionaries folder.
        /// </summary>
        Task LoadDictionaries();

        /// <summary>
        /// Reloads all dictionaries from disk.
        /// Call this after manually updating dictionary files.
        /// </summary>
        Task ReloadDictionaries();

        /// <summary>
        /// Searches across all dictionaries for words matching the query using fuzzy matching.
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <param name="excludeThemes">Optional themes to exclude from search</param>
        /// <returns>List of matching dictionary words with scores</returns>
        List<DictionaryWord> SearchDictionaries(string query, int maxResults = 10, params DictionaryTheme[] excludeThemes);

        /// <summary>
        /// Searches for words within specific dictionary themes.
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <param name="themes">Specific themes to search within</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of matching dictionary words with scores</returns>
        List<DictionaryWord> SearchDictionaryThemes(string query, DictionaryTheme[] themes, int maxResults = 10);

        /// <summary>
        /// Gets all words from a specific dictionary theme.
        /// </summary>
        /// <param name="theme">Dictionary theme to retrieve</param>
        /// <returns>List of words in the specified theme</returns>
        List<string> GetDictionaryWords(DictionaryTheme theme);

        /// <summary>
        /// Checks if dictionaries have been loaded successfully.
        /// </summary>
        bool AreDictionariesLoaded();

        /// <summary>
        /// Gets statistics about loaded dictionaries.
        /// </summary>
        /// <returns>Dictionary mapping theme names to word counts</returns>
        Dictionary<string, int> GetDictionaryStats();

        #endregion

        #region Fuzzy Matching

        /// <summary>
        /// Calculates a fuzzy matching score between search query and target word.
        /// Higher scores indicate better matches.
        /// </summary>
        /// <param name="search">Search query (normalized to lowercase)</param>
        /// <param name="target">Target word (normalized to lowercase)</param>
        /// <returns>Match score (0 = no match, 1000 = exact match)</returns>
        int CalculateFuzzyScore(string search, string target);

        #endregion
    }
}
