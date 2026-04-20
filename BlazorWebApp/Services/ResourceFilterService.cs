using BlazorWebApp.Models;
using Microsoft.Extensions.Logging;

namespace BlazorWebApp.Services;

/// <summary>
/// Filters ComfyUI model and LoRA lists by cross-referencing with cached Resource records
/// and the workflow's CompatibleResourceBaseModels.
/// </summary>
public class ResourceFilterService : IResourceFilterService
{
    private readonly IResourceCacheService _cache;
    private readonly ILogger<ResourceFilterService> _logger;

    public ResourceFilterService(IResourceCacheService cache, ILogger<ResourceFilterService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<string>> FilterAssetsByWorkflowAsync(List<string> assetNames, Workflow workflow, bool includeUntracked = true)
    {
        return FilterByWorkflowAsync(assetNames, workflow, includeUntracked);
    }

    /// <inheritdoc />
    public Task<List<string>> FilterLorasByWorkflowAsync(List<string> loraNames, Workflow workflow, bool includeUntracked = true)
    {
        return FilterByWorkflowAsync(loraNames, workflow, includeUntracked);
    }

    /// <inheritdoc />
    public Task<List<string>> FilterByBaseModelsAsync(List<string> names, IEnumerable<string> allowedBaseModels, bool includeUntracked = true)
    {
        if (names.Count == 0)
            return Task.FromResult(names);

        var compatibleSet = new HashSet<string>(allowedBaseModels, StringComparer.OrdinalIgnoreCase);
        if (compatibleSet.Count == 0)
            return Task.FromResult(names);

        return FilterCoreAsync(names, compatibleSet, includeUntracked);
    }

    private async Task<List<string>> FilterByWorkflowAsync(List<string> names, Workflow workflow, bool includeUntracked)
    {
        if (workflow.CompatibleResourceBaseModels == null || workflow.CompatibleResourceBaseModels.Count == 0)
            return names;

        if (names.Count == 0)
            return names;

        var compatibleSet = new HashSet<string>(workflow.CompatibleResourceBaseModels, StringComparer.OrdinalIgnoreCase);
        return await FilterCoreAsync(names, compatibleSet, includeUntracked);
    }

    private async Task<List<string>> FilterCoreAsync(List<string> names, HashSet<string> compatibleSet, bool includeUntracked)
    {
        var result = new List<string>(names.Count);

        foreach (var name in names)
        {
            var resource = await _cache.FindByFilenameAsync(name);

            if (resource == null || string.IsNullOrEmpty(resource.BaseModel))
            {
                if (includeUntracked)
                    result.Add(name);
                continue;
            }

            if (compatibleSet.Contains(resource.BaseModel))
                result.Add(name);
        }

        _logger.LogDebug("Filtered {InputCount} assets to {OutputCount} (includeUntracked={IncludeUntracked})",
            names.Count, result.Count, includeUntracked);

        return result;
    }
}
