using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using Microsoft.Extensions.Logging;

namespace BlazorWebApp.Services;

/// <summary>
/// In-memory cache of Resource entities with lazy loading and event-based invalidation.
/// Subscribes to resource/download events to invalidate when resources are created, edited, or deleted.
/// </summary>
public class ResourceCacheService : IResourceCacheService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<ResourceCacheService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private List<Resource>? _cachedResources;
    private Dictionary<string, Resource>? _filenameIndex;

    public ResourceCacheService(
        IDatabaseService db,
        IEventService events,
        ILogger<ResourceCacheService> logger)
    {
        _db = db;
        _logger = logger;

        events.Subscribe<ResourcesChangedEventArgs>(OnResourcesChanged);
        events.Subscribe<DownloadCompletedEventArgs>(OnDownloadCompleted);
    }

    /// <inheritdoc />
    public async Task<List<Resource>> GetCachedResourcesAsync(int? typeId = null)
    {
        await EnsureLoadedAsync();

        if (typeId.HasValue)
            return _cachedResources!.Where(r => r.Type?.Id == typeId.Value).ToList();

        return _cachedResources!;
    }

    /// <inheritdoc />
    public async Task<Resource?> FindByFilenameAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        await EnsureLoadedAsync();

        var normalizedFilename = Path.GetFileName(filename).ToLowerInvariant();
        _filenameIndex!.TryGetValue(normalizedFilename, out var resource);
        return resource;
    }

    /// <inheritdoc />
    public async Task<List<Resource>> FindByBaseModelsAsync(IEnumerable<string> baseModels, int? typeId = null)
    {
        await EnsureLoadedAsync();

        var baseModelSet = new HashSet<string>(baseModels, StringComparer.OrdinalIgnoreCase);

        var query = _cachedResources!.Where(r =>
            !string.IsNullOrEmpty(r.BaseModel) && baseModelSet.Contains(r.BaseModel));

        if (typeId.HasValue)
            query = query.Where(r => r.Type?.Id == typeId.Value);

        return query.ToList();
    }

    /// <inheritdoc />
    public async Task<List<string>> GetDistinctBaseModelsAsync(IEnumerable<string> candidates, bool enabledOnly = true)
    {
        await EnsureLoadedAsync();

        var candidateSet = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        var existingBaseModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in _cachedResources!)
        {
            if (enabledOnly && !resource.IsEnabled)
                continue;

            if (!string.IsNullOrEmpty(resource.BaseModel) && candidateSet.Contains(resource.BaseModel))
                existingBaseModels.Add(resource.BaseModel);
        }

        // Preserve the order from the candidates list
        return candidates.Where(c => existingBaseModels.Contains(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        _cachedResources = null;
        _filenameIndex = null;
        _logger.LogDebug("Resource cache invalidated");
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cachedResources != null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_cachedResources != null)
                return;

            _cachedResources = await _db.GetResources();
            _filenameIndex = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);

            foreach (var resource in _cachedResources)
            {
                if (!string.IsNullOrWhiteSpace(resource.Filename))
                {
                    var key = Path.GetFileName(resource.Filename).ToLowerInvariant();
                    _filenameIndex[key] = resource;
                }
            }

            _logger.LogDebug("Resource cache loaded: {Count} resources, {IndexCount} filename entries",
                _cachedResources.Count, _filenameIndex.Count);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void OnResourcesChanged(ResourcesChangedEventArgs args)
    {
        InvalidateCache();
    }

    private void OnDownloadCompleted(DownloadCompletedEventArgs args)
    {
        InvalidateCache();
    }
}
