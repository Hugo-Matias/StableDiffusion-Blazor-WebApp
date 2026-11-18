using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services;

/// <summary>
/// Provides an in-memory cache of Resource entities with event-based invalidation.
/// Avoids per-query database operations for resource lookups.
/// </summary>
public interface IResourceCacheService
{
    /// <summary>
    /// Returns all cached resources, optionally filtered by resource type ID.
    /// Loads from database on first call.
    /// </summary>
    Task<List<Resource>> GetCachedResourcesAsync(int? typeId = null);

    /// <summary>
    /// Finds a resource by its filename (case-insensitive, matches partial paths).
    /// </summary>
    Task<Resource?> FindByFilenameAsync(string filename);

    /// <summary>
    /// Returns resources whose BaseModel matches any of the given base model strings.
    /// Optionally filtered by resource type ID.
    /// </summary>
    Task<List<Resource>> FindByBaseModelsAsync(IEnumerable<string> baseModels, int? typeId = null);

    /// <summary>
    /// Returns which of the candidate base model strings actually have at least one resource in the cache.
    /// When enabledOnly is true (default), only enabled resources are considered.
    /// </summary>
    Task<List<string>> GetDistinctBaseModelsAsync(IEnumerable<string> candidates, bool enabledOnly = true);

    /// <summary>
    /// Forces the cache to reload on next access.
    /// </summary>
    void InvalidateCache();
}
