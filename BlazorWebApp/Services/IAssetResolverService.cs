using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for resolving workflow asset values from ComfyUI.
    /// Handles loading available options for each asset type and resolving
    /// the correct value based on state, defaults, and availability.
    /// </summary>
    public interface IAssetResolverService
    {
        /// <summary>
        /// Gets all available options for an asset type from ComfyUI.
        /// Maps AssetType to the appropriate ComfyUI API call.
        /// </summary>
        /// <param name="assetType">The type of asset to get options for</param>
        /// <returns>List of available asset filenames/names</returns>
        Task<List<string>> GetAssetOptions(AssetType assetType);

        /// <summary>
        /// Resolves the correct value for an asset based on priority:
        /// 1. Current state value (if valid/exists in available options)
        /// 2. Workflow default value (if valid)
        /// 3. First available option (fallback)
        /// </summary>
        /// <param name="asset">The workflow asset definition</param>
        /// <param name="currentStateValue">The current value from state (may be null)</param>
        /// <returns>Tuple of (resolvedValue, isValid). isValid=false means resolution failed.</returns>
        Task<(string value, bool isValid)> ResolveAssetValue(WorkflowAsset asset, string? currentStateValue);

        /// <summary>
        /// Validates all assets for a workflow and returns any validation errors.
        /// </summary>
        /// <param name="workflow">The workflow to validate</param>
        /// <param name="workflowAssets">Current asset values from state</param>
        /// <returns>List of validation error messages (empty if all valid)</returns>
        Task<List<string>> ValidateWorkflowAssets(Workflow workflow, Dictionary<string, string>? workflowAssets);

        /// <summary>
        /// Initializes workflow assets with resolved values.
        /// Populates the WorkflowAssets dictionary with valid values for each asset.
        /// </summary>
        /// <param name="workflow">The workflow containing asset definitions</param>
        /// <param name="workflowAssets">The dictionary to populate with resolved values</param>
        /// <returns>True if all assets were resolved successfully</returns>
        Task<bool> InitializeWorkflowAssets(Workflow workflow, Dictionary<string, string> workflowAssets);

        /// <summary>
        /// Preloads asset options for all asset types in a workflow.
        /// Call this to cache options before rendering UI.
        /// </summary>
        /// <param name="workflow">The workflow containing asset definitions</param>
        Task PreloadAssetOptions(Workflow workflow);

        /// <summary>
        /// Gets cached asset options if available, otherwise loads them.
        /// </summary>
        /// <param name="assetType">The asset type to get options for</param>
        /// <returns>Cached or freshly loaded options</returns>
        Task<List<string>> GetCachedAssetOptions(AssetType assetType);

        /// <summary>
        /// Gets cached asset options filtered by the workflow's CompatibleResourceBaseModels.
        /// Falls back to unfiltered list if filtering yields no results.
        /// Any values in <paramref name="alwaysInclude"/> that exist in the full options
        /// list are guaranteed to be in the result, even if filtering would exclude them.
        /// This preserves the user's current selection when filters change.
        /// </summary>
        /// <param name="assetType">The asset type to get options for</param>
        /// <param name="workflow">The workflow to filter by</param>
        /// <param name="alwaysInclude">Values (typically currently-selected) to preserve in the output</param>
        /// <returns>Filtered (or unfiltered fallback) options</returns>
        Task<List<string>> GetFilteredAssetOptions(AssetType assetType, Workflow workflow, IEnumerable<string>? alwaysInclude = null);

        /// <summary>
        /// Clears the asset options cache, forcing a reload on next access.
        /// </summary>
        void ClearCache();
    }
}
