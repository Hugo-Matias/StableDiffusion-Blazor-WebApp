using BlazorWebApp.Models;
using MudBlazor;
using System.Collections.Concurrent;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for resolving workflow asset values from ComfyUI.
    /// Handles loading available options for each asset type and resolving
    /// the correct value based on state, defaults, and availability.
    /// </summary>
    public class AssetResolverService : IAssetResolverService
    {
        private readonly ComfyUIService _comfy;
        private readonly IResourceFilterService _resourceFilter;
        private readonly IResourceFilterStateService _filterState;
        private readonly ISnackbar _snackbar;
        private readonly ILogger<AssetResolverService> _logger;

        // Cache of asset options by type to avoid repeated API calls
        private readonly ConcurrentDictionary<AssetType, List<string>> _assetOptionsCache = new();
        private readonly ConcurrentDictionary<AssetType, DateTime> _cacheTimestamps = new();

        // Cache expiration time (5 minutes)
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public AssetResolverService(
            ComfyUIService comfy,
            IResourceFilterService resourceFilter,
            IResourceFilterStateService filterState,
            ISnackbar snackbar,
            ILogger<AssetResolverService> logger)
        {
            _comfy = comfy;
            _resourceFilter = resourceFilter;
            _filterState = filterState;
            _snackbar = snackbar;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetAssetOptions(AssetType assetType)
        {
            try
            {
                return assetType switch
                {
                    AssetType.CheckpointModel => (await _comfy.GetCheckpoints())?.Select(m => m.Model_name).ToList() ?? new List<string>(),
                    AssetType.DiffusionModel => (await _comfy.GetDiffusionModels())?.Select(m => m.Model_name).ToList() ?? new List<string>(),
                    AssetType.Vae => await _comfy.GetVAEModels() ?? new List<string>(),
                    AssetType.Clip => await _comfy.GetClipModels() ?? new List<string>(),
                    AssetType.ClipVision => await _comfy.GetClipVisionModels() ?? new List<string>(),
                    _ => new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get asset options for type {AssetType}", assetType);
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetCachedAssetOptions(AssetType assetType)
        {
            // Check if cache is valid
            if (_assetOptionsCache.TryGetValue(assetType, out var cached) &&
                _cacheTimestamps.TryGetValue(assetType, out var timestamp) &&
                DateTime.UtcNow - timestamp < _cacheExpiration)
            {
                return cached;
            }

            // Load fresh options
            var options = await GetAssetOptions(assetType);

            // Update cache
            _assetOptionsCache[assetType] = options;
            _cacheTimestamps[assetType] = DateTime.UtcNow;

            return options;
        }

        /// <inheritdoc/>
        public void ClearCache()
        {
            _assetOptionsCache.Clear();
            _cacheTimestamps.Clear();
        }

        /// <inheritdoc/>
        public async Task<(string value, bool isValid)> ResolveAssetValue(WorkflowAsset asset, string? currentStateValue)
        {
            var options = await GetCachedAssetOptions(asset.Type);

            // Empty options = API call failed or model type not available
            if (options == null || options.Count == 0)
            {
                _logger.LogWarning("No {AssetType} models found for asset '{Label}'", asset.Type, asset.Label);
                return (asset.DefaultValue ?? "", false);
            }

            // Priority 1: Use existing state value if valid
            if (!string.IsNullOrWhiteSpace(currentStateValue))
            {
                // Check for exact match
                if (options.Contains(currentStateValue))
                {
                    return (currentStateValue, true);
                }

                // Check for partial match (filename without path)
                var filenameMatch = options.FirstOrDefault(o =>
                    Path.GetFileName(o).Equals(currentStateValue, StringComparison.OrdinalIgnoreCase) ||
                    o.EndsWith(currentStateValue, StringComparison.OrdinalIgnoreCase));

                if (filenameMatch != null)
                {
                    return (filenameMatch, true);
                }
            }

            // Priority 2: Use workflow default if valid
            if (!string.IsNullOrWhiteSpace(asset.DefaultValue))
            {
                // Check for exact match
                if (options.Contains(asset.DefaultValue))
                {
                    return (asset.DefaultValue, true);
                }

                // Check for partial match
                var defaultMatch = options.FirstOrDefault(o =>
                    Path.GetFileName(o).Equals(asset.DefaultValue, StringComparison.OrdinalIgnoreCase) ||
                    o.EndsWith(asset.DefaultValue, StringComparison.OrdinalIgnoreCase));

                if (defaultMatch != null)
                {
                    _logger.LogDebug("{Label}: Using matched default '{Match}' for '{Default}'",
                        asset.Label, defaultMatch, asset.DefaultValue);
                    return (defaultMatch, true);
                }
            }

            // Priority 3: Fallback to first available option
            var fallback = options.First();
            _logger.LogWarning("{Label}: Default '{Default}' not found, falling back to '{Fallback}'",
                asset.Label, asset.DefaultValue, fallback);

            _snackbar.Add($"{asset.Label}: Default '{asset.DefaultValue}' not found, using '{Path.GetFileName(fallback)}'",
                Severity.Warning);

            return (fallback, true);
        }

        /// <inheritdoc/>
        public async Task<List<string>> ValidateWorkflowAssets(Workflow workflow, Dictionary<string, string>? workflowAssets)
        {
            var errors = new List<string>();

            if (workflow?.Assets == null || workflow.Assets.Count == 0)
            {
                return errors; // No assets to validate
            }

            foreach (var asset in workflow.Assets)
            {
                var currentValue = workflowAssets?.GetValueOrDefault(asset.Parameter);
                var options = await GetCachedAssetOptions(asset.Type);

                if (options == null || options.Count == 0)
                {
                    errors.Add($"{asset.Label}: No {asset.Type} models available in ComfyUI");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(currentValue))
                {
                    if (!string.IsNullOrWhiteSpace(asset.DefaultValue))
                    {
                        // Will use default - not an error
                        continue;
                    }
                    errors.Add($"{asset.Label}: No value set and no default available");
                    continue;
                }

                // Check if value exists in options
                var exists = options.Any(o =>
                    o.Equals(currentValue, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(o).Equals(currentValue, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    errors.Add($"{asset.Label}: Value '{currentValue}' not found in available options");
                }
            }

            return errors;
        }

        /// <inheritdoc/>
        public async Task<bool> InitializeWorkflowAssets(Workflow workflow, Dictionary<string, string> workflowAssets)
        {
            if (workflow?.Assets == null || workflow.Assets.Count == 0)
            {
                return true; // Nothing to initialize
            }

            var allSuccessful = true;

            foreach (var asset in workflow.Assets.OrderBy(a => a.Order))
            {
                var currentValue = workflowAssets.GetValueOrDefault(asset.Parameter);
                var (resolvedValue, isValid) = await ResolveAssetValue(asset, currentValue);

                if (!isValid)
                {
                    _logger.LogWarning("Failed to resolve valid value for asset '{Label}' ({Parameter})",
                        asset.Label, asset.Parameter);
                    allSuccessful = false;
                }

                // Set the resolved value (even if invalid, we set what we have)
                workflowAssets[asset.Parameter] = resolvedValue;

                _logger.LogDebug("Initialized {Parameter} = {Value} (valid: {IsValid})",
                    asset.Parameter, resolvedValue, isValid);
            }

            return allSuccessful;
        }

        /// <inheritdoc/>
        public async Task PreloadAssetOptions(Workflow workflow)
        {
            if (workflow?.Assets == null || workflow.Assets.Count == 0)
            {
                return;
            }

            // Get unique asset types
            var assetTypes = workflow.Assets.Select(a => a.Type).Distinct();

            // Load all asset options in parallel
            var loadTasks = assetTypes.Select(async assetType =>
            {
                try
                {
                    var options = await GetAssetOptions(assetType);
                    _assetOptionsCache[assetType] = options;
                    _cacheTimestamps[assetType] = DateTime.UtcNow;
                    _logger.LogDebug("Preloaded {Count} options for {AssetType}", options.Count, assetType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to preload options for {AssetType}", assetType);
                }
            });

            await Task.WhenAll(loadTasks);
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetFilteredAssetOptions(AssetType assetType, Workflow workflow)
        {
            var options = await GetCachedAssetOptions(assetType);

            if (options.Count == 0)
                return options;

            await _filterState.EnsureInitializedForWorkflowAsync(workflow);
            var effectiveBaseModels = _filterState.GetEffectiveBaseModels();

            List<string> filtered;
            if (effectiveBaseModels == null)
            {
                // AllowAll or no filter state - use full workflow compatibility
                filtered = await _resourceFilter.FilterAssetsByWorkflowAsync(options, workflow, _filterState.IncludeUntracked);
            }
            else if (effectiveBaseModels.Count == 0)
            {
                // All chips disabled - check untracked toggle
                if (_filterState.IncludeUntracked)
                {
                    // Show only untracked resources (no base model match, no tracked model)
                    filtered = await _resourceFilter.FilterByBaseModelsAsync(options, effectiveBaseModels, includeUntracked: true);
                }
                else
                {
                    // No chips, no untracked - fall back to full workflow compatibility without untracked
                    filtered = await _resourceFilter.FilterAssetsByWorkflowAsync(options, workflow, includeUntracked: false);
                }
            }
            else
            {
                filtered = await _resourceFilter.FilterByBaseModelsAsync(options, effectiveBaseModels, _filterState.IncludeUntracked);
            }

            // Graceful degradation: if chip filtering removes everything, fall back to full workflow compatibility
            if (filtered.Count == 0 && options.Count > 0 && effectiveBaseModels != null && effectiveBaseModels.Count > 0)
            {
                _logger.LogDebug("Chip filtering {AssetType} for workflow '{Title}' returned no results, falling back to workflow compatibility ({Count} options)",
                    assetType, workflow.Title, options.Count);

                // Fall back to workflow-level filtering (all compatible base models), not completely unfiltered
                filtered = await _resourceFilter.FilterAssetsByWorkflowAsync(options, workflow, _filterState.IncludeUntracked);

                if (filtered.Count == 0)
                {
                    _logger.LogDebug("Workflow compatibility filtering also returned no results for {AssetType}, returning all {Count} options",
                        assetType, options.Count);
                    return options;
                }
            }

            return filtered;
        }
    }
}
