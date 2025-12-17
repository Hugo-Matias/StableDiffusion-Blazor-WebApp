using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Interface for wildcard business logic operations.
    /// Handles wildcard parsing, random selection, and collection management.
    /// </summary>
    public interface IWildcardService
    {
        /// <summary>
        /// Gets a random entry from the specified collection (equal probability).
        /// </summary>
        /// <param name="collectionName">Name of the collection (e.g., "clothing/tops")</param>
        /// <returns>Random entry value or null if collection not found</returns>
        Task<string?> GetRandomEntry(string collectionName);

        /// <summary>
        /// Gets a random entry from the specified collection using weighted probability.
        /// </summary>
        /// <param name="collectionName">Name of the collection</param>
        /// <returns>Weighted random entry value or null if collection not found</returns>
        Task<string?> GetRandomEntryWeighted(string collectionName);

        /// <summary>
        /// Gets all entry values from a collection.
        /// </summary>
        /// <param name="collectionName">Name of the collection</param>
        /// <returns>List of all entry values</returns>
        Task<List<string>> GetAllEntryValues(string collectionName);

        /// <summary>
        /// Checks if a collection exists in the database.
        /// </summary>
        /// <param name="collectionName">Name of the collection to check</param>
        /// <returns>True if collection exists, false otherwise</returns>
        Task<bool> CollectionExists(string collectionName);

        /// <summary>
        /// Parses text and replaces wildcard syntax (__name__) with random values.
        /// </summary>
        /// <param name="input">Text containing wildcard syntax</param>
        /// <returns>Text with wildcards expanded to random values</returns>
        Task<string> ParseWildcards(string input);

        /// <summary>
        /// Detects wildcard syntax in text.
        /// </summary>
        /// <param name="input">Text to analyze</param>
        /// <returns>List of detected wildcard names</returns>
        List<string> DetectWildcards(string input);

        /// <summary>
        /// Seeds sample wildcard collections if none exist in the database.
        /// Called during application startup to provide default wildcard options.
        /// </summary>
        Task SeedCollections();

        /// <summary>
        /// Gets all distinct wildcard categories for autocomplete.
        /// </summary>
        /// <returns>List of category names (e.g., "Clothing", "Locations")</returns>
        Task<List<string>> GetCategories();

        /// <summary>
        /// Gets all collections within a specific category.
        /// </summary>
        /// <param name="category">Category name to filter by</param>
        /// <returns>List of collections in the category</returns>
        Task<List<WildcardCollection>> GetCollectionsByCategory(string category);

        /// <summary>
        /// Searches collections by name pattern.
        /// </summary>
        /// <param name="searchQuery">Search query to filter collection names</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of matching collections</returns>
        Task<List<WildcardCollection>> SearchCollections(string searchQuery, int maxResults = 10);
    }
}
