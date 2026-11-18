using BlazorWebApp.Models;

namespace BlazorWebApp.Services;

/// <summary>
/// Filters ComfyUI model and LoRA lists using the resource cache and workflow compatibility data.
/// </summary>
public interface IResourceFilterService
{
    /// <summary>
    /// Filters a list of ComfyUI asset filenames to only those compatible with the given workflow.
    /// Resources with a BaseModel not in the workflow's CompatibleResourceBaseModels are excluded.
    /// Untracked resources (no Resource record or no BaseModel) pass through when includeUntracked is true.
    /// If the workflow has no CompatibleResourceBaseModels defined, all assets are returned unfiltered.
    /// </summary>
    Task<List<string>> FilterAssetsByWorkflowAsync(List<string> assetNames, Workflow workflow, bool includeUntracked = true);

    /// <summary>
    /// Filters a list of ComfyUI LoRA filenames to only those compatible with the given workflow.
    /// Same filtering logic as FilterAssetsByWorkflowAsync.
    /// </summary>
    Task<List<string>> FilterLorasByWorkflowAsync(List<string> loraNames, Workflow workflow, bool includeUntracked = true);

    /// <summary>
    /// Filters a list of filenames using an explicit set of allowed base models.
    /// Used when the caller has already resolved which base models are enabled (e.g., from ResourceFilterStateService).
    /// </summary>
    Task<List<string>> FilterByBaseModelsAsync(List<string> names, IEnumerable<string> allowedBaseModels, bool includeUntracked = true);
}
