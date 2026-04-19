using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using System.Collections.Concurrent;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Operates on StateService.GenerationParameters for state persistence.
    /// Integrates with WorkflowStateService for per-workflow parameter persistence.
    /// Uses C# IWorkflowBuilder workflows exclusively.
    /// </summary>
    public class GenerationParameterService : IGenerationParameterService
    {
        private readonly ILogger<GenerationParameterService> _logger;
        private readonly IWorkflowService _workflowService;
        private readonly IWorkflowStateService _workflowStateService;
        private readonly IEventService _eventService;
        private readonly IStateService _stateService;
        private readonly IComfyUIService _comfyUIService;
        private readonly IBackendService _backendService;

        /// <summary>
        /// Cache for resolved source options. Key format: "{classType}:{inputName}"
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _sourceOptionsCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cache for resolved options per fragment parameter. Key format: "{fragmentId}.{parameterName}"
        /// Used by UI components to get pre-resolved options without async calls.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _resolvedFragmentOptions = new(StringComparer.OrdinalIgnoreCase);

        // Fragment discovery cache - updated when workflow changes
        private FragmentReference? _primaryLatentFragment;
        private FragmentReference? _primarySamplerFragment;
        private FragmentReference? _promptsFragment;
        private List<FragmentReference> _optionalFragments = new();

        /// <summary>
        /// Pending parameter overrides queued via QueuePendingOverride.
        /// Applied and cleared after the next InitializeFromWorkflowAsync.
        /// </summary>
        private readonly List<(string FragmentId, string Key, object? Value)> _pendingOverrides = new();

        /// <inheritdoc />
        public GenerationParameters Current => _stateService.GenerationParameters;

        #region Fragment Discovery Properties

        /// <inheritdoc />
        public FragmentReference? PrimaryLatentFragment => _primaryLatentFragment;

        /// <inheritdoc />
        public FragmentReference? PrimarySamplerFragment => _primarySamplerFragment;

        /// <inheritdoc />
        public FragmentReference? PromptsFragment => _promptsFragment;

        /// <inheritdoc />
        public IReadOnlyList<FragmentReference> OptionalFragments => _optionalFragments.AsReadOnly();

        #endregion

        #region Fragment Property Helpers

        /// <inheritdoc />
        public T GetFragmentProperty<T>(FragmentReference? fragment, string key, T defaultValue)
        {
            if (fragment == null)
                return defaultValue;

            var fragmentParams = Current.GetFragment(fragment.Id);
            if (fragmentParams == null)
                return defaultValue;

            return fragmentParams.GetValueOrDefault(key, defaultValue);
        }

        /// <inheritdoc />
        public void SetFragmentProperty<T>(FragmentReference? fragment, string key, T value, bool notify = true)
        {
            if (fragment == null)
            {
                _logger.LogWarning("Cannot set property '{Key}' on null fragment reference", key);
                return;
            }

            SetFragmentValue(fragment.Id, key, value);

            if (notify)
            {
                PublishChange(GenerationParametersChangedEventArgs.FragmentValueChanged(fragment.Id, key));
            }
        }

        #endregion

        public GenerationParameterService(
            ILogger<GenerationParameterService> logger,
            IWorkflowService workflowService,
            IWorkflowStateService workflowStateService,
            IEventService eventService,
            IStateService stateService,
            IComfyUIService comfyUIService,
            IBackendService backendService)
        {
            _logger = logger;
            _workflowService = workflowService;
            _workflowStateService = workflowStateService;
            _eventService = eventService;
            _stateService = stateService;
            _comfyUIService = comfyUIService;
            _backendService = backendService;
        }

        /// <inheritdoc />
        public void InitializeFromWorkflow(Workflow workflow)
        {
            InitializeFromWorkflowInternal(workflow, savedState: null);
        }

        /// <inheritdoc />
        public async Task<GenerationParameters> InitializeFromWorkflowAsync(Workflow workflow)
        {
            if (workflow == null)
            {
                _logger.LogWarning("Cannot initialize from null workflow");
                return Current;
            }

            // Save current workflow state before switching (if we have a different workflow loaded)
            var currentWorkflowId = Current.WorkflowId;
            if (currentWorkflowId.HasValue && currentWorkflowId.Value != workflow.Id)
            {
                await SaveCurrentWorkflowStateAsync();
            }

            // Try to load saved state for the target workflow
            var savedState = await _workflowStateService.LoadWorkflowStateAsync(workflow.Id);

            // Initialize from workflow, applying saved state if available
            InitializeFromWorkflowInternal(workflow, savedState);

            // Apply any pending parameter overrides (from "Send Parameters To" feature)
            ApplyPendingOverrides();

            // Pre-resolve dynamic source options for all fragments
            await PreResolveDynamicSourcesAsync(workflow);

            return Current;
        }

        /// <inheritdoc />
        public void QueuePendingOverride(string fragmentId, string key, object? value)
        {
            _pendingOverrides.Add((fragmentId, key, value));
            _logger.LogDebug("Queued pending override: {FragmentId}.{Key}", fragmentId, key);
        }

        /// <summary>
        /// Applies and clears all pending parameter overrides.
        /// </summary>
        private void ApplyPendingOverrides()
        {
            if (_pendingOverrides.Count == 0) return;

            _logger.LogDebug("Applying {Count} pending parameter override(s)", _pendingOverrides.Count);
            foreach (var (fragmentId, key, value) in _pendingOverrides)
            {
                var fragment = Current.GetOrCreateFragment(fragmentId);
                fragment.SetValue(key, value);
            }
            _pendingOverrides.Clear();

            PublishChange(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.ParametersLoaded));
        }

        /// <summary>
        /// Saves the current workflow's parameters to the database.
        /// </summary>
        public async Task SaveCurrentWorkflowStateAsync()
        {
            var workflowId = Current.WorkflowId;
            if (!workflowId.HasValue)
            {
                _logger.LogDebug("No workflow selected, skipping state save");
                return;
            }

            await _workflowStateService.SaveWorkflowStateAsync(workflowId.Value, Current);
            _logger.LogDebug("Saved current workflow state for {WorkflowId}", workflowId.Value);
        }

        /// <summary>
        /// Internal initialization that handles both fresh initialization and restoring saved state.
        /// </summary>
        private void InitializeFromWorkflowInternal(Workflow workflow, GenerationParameters? savedState)
        {
            if (workflow == null)
            {
                _logger.LogWarning("Cannot initialize from null workflow");
                return;
            }

            // Verify this is a C# workflow
            if (!_workflowService.HasWorkflowBuilder(workflow.Id))
            {
                _logger.LogError("Workflow '{Title}' (ID: {Id}) is not a C# workflow - cannot initialize",
                    workflow.Title, workflow.Id);
                return;
            }

            _logger.LogDebug("Initializing parameters from workflow: {WorkflowTitle} (hasSavedState: {HasSaved})",
                workflow.Title, savedState != null);

            // Clear source options cache on workflow change
            ClearSourceCache();

            var current = Current;
            current.WorkflowId = workflow.Id;

            if (savedState != null && savedState.Fragments.Count > 0)
            {
                // Restore from saved state
                RestoreFromSavedState(workflow, current, savedState);
            }
            else
            {
                // Initialize fresh from workflow template
                InitializeFreshFromWorkflow(workflow, current);
            }

            // Discover key fragments after initialization
            DiscoverFragments();

            PublishChange(GenerationParametersChangedEventArgs.WorkflowChanged(workflow.Id));
        }

        /// <summary>
        /// Restores parameters from saved state, merging with workflow for any new fragments.
        /// </summary>
        private void RestoreFromSavedState(Workflow workflow, GenerationParameters current, GenerationParameters savedState)
        {
            _logger.LogDebug("Restoring saved state for workflow '{WorkflowTitle}'", workflow.Title);

            // Start with saved state
            current.Fragments.Clear();
            foreach (var kvp in savedState.Fragments)
            {
                current.Fragments[kvp.Key] = kvp.Value.Clone();
            }

            current.Assets.Clear();
            foreach (var kvp in savedState.Assets)
            {
                current.Assets[kvp.Key] = kvp.Value;
            }

            // Sources are workflow-specific and should come from saved state
            // But we need to ensure all workflow-defined sources exist
            current.Sources.Clear();
            if (workflow.Sources != null)
            {
                foreach (var source in workflow.Sources)
                {
                    if (savedState.Sources.TryGetValue(source.Id, out var savedSource))
                    {
                        current.Sources[source.Id] = savedSource.Clone();
                    }
                    else
                    {
                        current.Sources[source.Id] = new SourceAsset
                        {
                            Label = source.Label,
                            Type = source.Type
                        };
                    }
                }
            }

            // Loras from saved state (if any)
            current.Loras.Clear();
            current.Loras.AddRange(savedState.Loras.Select(l => new Lora(l)));

            // Check for any new fragments in the C# workflow that weren't in saved state
            var builder = _workflowService.GetWorkflowBuilder(workflow.Id);
            if (builder != null)
            {
                var order = current.Fragments.Values.Any() ? current.Fragments.Values.Max(f => f.Order) + 1 : 0;
                foreach (var fragmentBuilder in builder.GetFragments())
                {
                    var metadata = fragmentBuilder.Metadata;
                    if (metadata.IsHidden) continue;

                    if (!current.Fragments.ContainsKey(metadata.Id))
                    {
                        var isOptional = metadata.Collapsible || metadata.Type == FragmentType.Enhancement;
                        var fragment = new FragmentParameters
                        {
                            FragmentFile = $"fluent:{metadata.Id}",
                            IsActive = !isOptional,
                            Order = order++
                        };

                        if (metadata.Parameters != null)
                        {
                            foreach (var param in metadata.Parameters)
                            {
                                if (param.DefaultValue != null)
                                {
                                    fragment.Values[param.Name] = param.DefaultValue;
                                }
                            }
                        }

                        current.Fragments[metadata.Id] = fragment;
                        _logger.LogDebug("Added new fragment '{FragmentId}' from updated C# workflow", metadata.Id);
                    }
                }
            }

            // Check for any new assets in the workflow that weren't in saved state
            if (workflow.Assets != null)
            {
                foreach (var asset in workflow.Assets)
                {
                    if (!current.Assets.ContainsKey(asset.Parameter) && !string.IsNullOrWhiteSpace(asset.DefaultValue))
                    {
                        current.Assets[asset.Parameter] = asset.DefaultValue;
                        _logger.LogDebug("Added new asset '{AssetName}' from updated workflow", asset.Parameter);
                    }
                }
            }

            _logger.LogDebug("Restored {FragmentCount} fragments, {AssetCount} assets from saved state",
                current.Fragments.Count, current.Assets.Count);
        }

        /// <summary>
        /// Initializes fresh from C# workflow builder (no saved state).
        /// </summary>
        private void InitializeFreshFromWorkflow(Workflow workflow, GenerationParameters current)
        {
            _logger.LogDebug("Initializing fresh from workflow '{WorkflowTitle}'", workflow.Title);

            current.Fragments.Clear();
            current.Assets.Clear();
            current.Sources.Clear();

            // Initialize assets from workflow defaults
            if (workflow.Assets != null)
            {
                foreach (var asset in workflow.Assets)
                {
                    if (!string.IsNullOrWhiteSpace(asset.DefaultValue))
                    {
                        current.Assets[asset.Parameter] = asset.DefaultValue;
                    }
                }
            }

            // Initialize sources from workflow definition
            if (workflow.Sources != null)
            {
                foreach (var source in workflow.Sources)
                {
                    current.Sources[source.Id] = new SourceAsset
                    {
                        Label = source.Label,
                        Type = source.Type
                    };
                    _logger.LogDebug("Initialized source '{SourceId}' ({Type})", source.Id, source.Type);
                }
            }

            // Initialize from C# workflow builder
            InitializeFragmentsFromBuilder(workflow, current);
        }

        /// <summary>
        /// Initializes fragment parameters from C# IWorkflowBuilder.GetFragments().
        /// </summary>
        private void InitializeFragmentsFromBuilder(Workflow workflow, GenerationParameters parameters)
        {
            var builder = _workflowService.GetWorkflowBuilder(workflow.Id);
            if (builder == null)
            {
                _logger.LogWarning("No builder found for workflow {WorkflowId}", workflow.Id);
                return;
            }

            var fragments = builder.GetFragments().ToList();
            var order = 0;

            foreach (var fragmentBuilder in fragments)
            {
                var metadata = fragmentBuilder.Metadata;
                var fragmentId = metadata.Id;

                // Check if fragment should be hidden (utility fragments with no UI)
                if (metadata.IsHidden)
                {
                    _logger.LogTrace("Skipping hidden fragment '{FragmentId}'", fragmentId);
                    continue;
                }

                // Determine if this is an optional fragment (defaultCollapsed or enhancement type)
                var isOptional = metadata.Collapsible || metadata.Type == FragmentType.Enhancement;

                // Create fragment parameters
                var fragment = new FragmentParameters
                {
                    FragmentFile = $"fluent:{fragmentId}", // Mark as fluent API fragment
                    IsActive = !isOptional, // Optional fragments start inactive
                    Order = order++
                };

                // Apply default values from metadata parameters
                if (metadata.Parameters != null)
                {
                    foreach (var param in metadata.Parameters)
                    {
                        if (param.DefaultValue != null)
                        {
                            fragment.Values[param.Name] = param.DefaultValue;
                            _logger.LogTrace("Set default for '{FragmentId}.{Param}' = {Value}",
                                fragmentId, param.Name, param.DefaultValue);
                        }
                    }
                }

                parameters.Fragments[fragmentId] = fragment;
                _logger.LogDebug("Initialized fragment '{FragmentId}' (IsActive: {IsActive}, Type: {Type})",
                    fragmentId, fragment.IsActive, metadata.Type);
            }

            _logger.LogDebug("Initialized {Count} fragments from C# workflow builder for '{WorkflowTitle}'",
                parameters.Fragments.Count, workflow.Title);
        }

        /// <summary>
        /// Discovers key fragments from the current parameters.
        /// </summary>
        private void DiscoverFragments()
        {
            _primaryLatentFragment = null;
            _primarySamplerFragment = null;
            _promptsFragment = null;
            _optionalFragments.Clear();

            var current = Current;
            if (current.Fragments.Count == 0)
            {
                _logger.LogDebug("No fragments to discover");
                return;
            }

            _logger.LogDebug("Discovering fragments from {Count} total fragments", current.Fragments.Count);

            // Get C# workflow builder metadata
            IWorkflowBuilder? builder = null;
            Dictionary<string, FragmentMetadata>? builderMetadata = null;
            if (current.WorkflowId.HasValue && _workflowService.HasWorkflowBuilder(current.WorkflowId.Value))
            {
                builder = _workflowService.GetWorkflowBuilder(current.WorkflowId.Value);
                if (builder != null)
                {
                    builderMetadata = builder.GetFragments()
                        .ToDictionary(f => f.Metadata.Id, f => f.Metadata, StringComparer.OrdinalIgnoreCase);
                }
            }

            foreach (var kvp in current.Fragments)
            {
                var fragmentId = kvp.Key;
                var fragment = kvp.Value;

                FragmentSchema? schema = null;
                FragmentMetadata? metadata = null;

                if (builderMetadata?.TryGetValue(fragmentId, out metadata) == true)
                {
                    schema = BuildSchemaFromMetadata(metadata);
                }

                if (schema == null && metadata == null)
                {
                    _logger.LogTrace("Fragment '{FragmentId}' has no schema or metadata, skipping discovery", fragmentId);
                    continue;
                }

                var fragmentType = metadata?.Type ?? schema?.Type ?? FragmentType.Unknown;

                var reference = new FragmentReference
                {
                    Id = fragmentId,
                    Parameters = fragment,
                    Schema = schema
                };

                switch (fragmentType)
                {
                    case FragmentType.Prompts:
                        _promptsFragment ??= reference;
                        _logger.LogTrace("Discovered prompts fragment: '{FragmentId}'", fragmentId);
                        break;

                    case FragmentType.Sampler:
                        _primarySamplerFragment ??= reference;
                        _logger.LogTrace("Discovered primary sampler fragment: '{FragmentId}'", fragmentId);
                        break;

                    case FragmentType.Latent:
                        _primaryLatentFragment ??= reference;
                        _logger.LogTrace("Discovered latent fragment: '{FragmentId}'", fragmentId);
                        break;

                    case FragmentType.Settings:
                        _optionalFragments.Add(reference);
                        _logger.LogTrace("Discovered settings fragment: '{FragmentId}'", fragmentId);
                        break;

                    case FragmentType.Loader:
                    case FragmentType.Input:
                        if (_primaryLatentFragment == null &&
                            (fragment.Values.ContainsKey("width") || fragment.Values.ContainsKey("height")))
                        {
                            _primaryLatentFragment = reference;
                            _logger.LogTrace("Using loader/input fragment as latent: '{FragmentId}'", fragmentId);
                        }
                        break;

                    case FragmentType.Enhancement:
                        _optionalFragments.Add(reference);
                        _logger.LogTrace("Discovered enhancement fragment: '{FragmentId}'", fragmentId);
                        break;

                    case FragmentType.Unknown:
                    case FragmentType.Conditioning:
                    case FragmentType.Output:
                        var isCollapsible = metadata?.Collapsible ?? schema?.DefaultCollapsed ?? false;
                        var hasComponent = schema?.HasDesignedComponent ?? (metadata != null);
                        if (isCollapsible || hasComponent)
                        {
                            _optionalFragments.Add(reference);
                            _logger.LogTrace("Discovered optional fragment: '{FragmentId}'", fragmentId);
                        }
                        break;
                }
            }

            // Fallback: If no latent fragment found by type, check for any fragment with resolution params
            if (_primaryLatentFragment == null)
            {
                foreach (var kvp in current.Fragments)
                {
                    if (kvp.Value.Values.ContainsKey("width") && kvp.Value.Values.ContainsKey("height"))
                    {
                        _primaryLatentFragment = new FragmentReference
                        {
                            Id = kvp.Key,
                            Parameters = kvp.Value,
                            Schema = null
                        };
                        _logger.LogDebug("Fallback: Using fragment '{FragmentId}' as latent (has width/height)", kvp.Key);
                        break;
                    }
                }
            }

            _optionalFragments = _optionalFragments.OrderBy(f => f.Schema?.Order ?? 999).ToList();

            _logger.LogInformation(
                "Fragment discovery complete - Prompts: {HasPrompts}, Latent: {HasLatent}, Sampler: {HasSampler}, Optional: {OptionalCount}",
                _promptsFragment != null,
                _primaryLatentFragment != null,
                _primarySamplerFragment != null,
                _optionalFragments.Count);
        }

        /// <summary>
        /// Builds a FragmentSchema from C# FragmentMetadata.
        /// </summary>
        private static FragmentSchema BuildSchemaFromMetadata(FragmentMetadata metadata)
        {
            var schema = new FragmentSchema
            {
                Type = metadata.Type,
                Title = metadata.Title,
                Icon = metadata.Icon,
                Order = metadata.Order,
                DefaultCollapsed = metadata.Collapsible && metadata.DefaultCollapsed,
                Collapsible = metadata.Collapsible,
                Component = metadata.Component
            };

            if (metadata.Parameters != null)
            {
                schema.Parameters = new Dictionary<string, ParameterConstraints>(StringComparer.OrdinalIgnoreCase);
                foreach (var param in metadata.Parameters)
                {
                    schema.Parameters[param.Name] = new ParameterConstraints
                    {
                        Default = param.DefaultValue,
                        Min = param.Min,
                        Max = param.Max,
                        Step = param.Step,
                        Options = param.Options?.ToList(),
                        Source = param.Source?.NodeType,
                        InputName = param.Source?.InputName
                    };
                }
            }

            return schema;
        }

        #region Fragment CRUD Operations

        /// <inheritdoc />
        public void SetFragmentValue(string fragmentId, string parameter, object? value)
        {
            var fragment = Current.GetOrCreateFragment(fragmentId);
            fragment.SetValue(parameter, value);
            _logger.LogTrace("Set {FragmentId}.{Parameter} = {Value}", fragmentId, parameter, value);
        }

        /// <inheritdoc />
        public void SetFragmentValueAndNotify(string fragmentId, string parameter, object? value)
        {
            SetFragmentValue(fragmentId, parameter, value);
            PublishChange(GenerationParametersChangedEventArgs.FragmentValueChanged(fragmentId, parameter));
        }

        /// <inheritdoc />
        public T? GetFragmentValue<T>(string fragmentId, string parameter)
        {
            var fragment = Current.GetFragment(fragmentId);
            if (fragment == null)
                return default;
            return fragment.GetValue<T>(parameter);
        }

        /// <inheritdoc />
        public void SetFragmentActive(string fragmentId, bool isActive)
        {
            var fragment = Current.GetFragment(fragmentId);

            // If fragment doesn't exist and we're activating it, create it with defaults
            if (fragment == null && isActive)
            {
                fragment = CreateFragmentWithDefaults(fragmentId);
                if (fragment == null)
                {
                    _logger.LogWarning("Cannot activate fragment '{FragmentId}' - unable to determine defaults", fragmentId);
                    return;
                }
                Current.Fragments[fragmentId] = fragment;
                _logger.LogDebug("Created fragment '{FragmentId}' with defaults on activation", fragmentId);
            }

            if (fragment != null)
            {
                fragment.IsActive = isActive;
                _logger.LogDebug("Set fragment '{FragmentId}' active = {IsActive}", fragmentId, isActive);
                PublishChange(GenerationParametersChangedEventArgs.FragmentActiveChanged(fragmentId, isActive));
            }
        }

        /// <inheritdoc />
        public bool IsFragmentActive(string fragmentId)
        {
            var fragment = Current.GetFragment(fragmentId);
            return fragment?.IsActive ?? false;
        }

        /// <inheritdoc />
        public (string fragmentId, FragmentParameters parameters) AddFragmentInstance(string fragmentFile, string? baseId = null)
        {
            var baseName = baseId ?? Path.GetFileNameWithoutExtension(fragmentFile).Replace("-", "_");
            var index = 1;
            var fragmentId = baseName;

            while (Current.Fragments.ContainsKey(fragmentId))
            {
                fragmentId = $"{baseName}_{index++}";
            }

            var maxOrder = Current.Fragments.Values.Any()
                ? Current.Fragments.Values.Max(f => f.Order)
                : 0;

            var parameters = new FragmentParameters
            {
                FragmentFile = fragmentFile,
                IsActive = true,
                Order = maxOrder + 1
            };

            Current.Fragments[fragmentId] = parameters;
            _logger.LogDebug("Added fragment instance '{FragmentId}' for {FragmentFile}", fragmentId, fragmentFile);

            PublishChange(GenerationParametersChangedEventArgs.FragmentAdded(fragmentId));

            return (fragmentId, parameters);
        }

        /// <inheritdoc />
        public bool RemoveFragmentInstance(string fragmentId)
        {
            var removed = Current.Fragments.Remove(fragmentId);
            if (removed)
            {
                _logger.LogDebug("Removed fragment instance '{FragmentId}'", fragmentId);
                PublishChange(GenerationParametersChangedEventArgs.FragmentRemoved(fragmentId));
            }
            return removed;
        }

        /// <inheritdoc />
        public void ReorderFragments(IEnumerable<string> fragmentIds)
        {
            var order = 0;
            foreach (var id in fragmentIds)
            {
                if (Current.Fragments.TryGetValue(id, out var fragment))
                {
                    fragment.Order = order++;
                }
            }
            PublishChange(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.FragmentReordered));
        }

        /// <inheritdoc />
        public void SetAsset(string assetName, string value)
        {
            Current.Assets[assetName] = value;
            _logger.LogTrace("Set asset {AssetName} = {Value}", assetName, value);
        }

        /// <inheritdoc />
        public void SetAssetAndNotify(string assetName, string value)
        {
            SetAsset(assetName, value);
            PublishChange(GenerationParametersChangedEventArgs.AssetChanged(assetName));
        }

        /// <inheritdoc />
        public string? GetAsset(string assetName)
        {
            return Current.Assets.GetValueOrDefault(assetName);
        }

        /// <inheritdoc />
        public void SetSource(string sourceId, SourceAsset source)
        {
            Current.Sources[sourceId] = source;
            _logger.LogTrace("Set source {SourceId}", sourceId);
        }

        /// <inheritdoc />
        public void SetSourceAndNotify(string sourceId, SourceAsset source)
        {
            SetSource(sourceId, source);
            PublishChange(GenerationParametersChangedEventArgs.SourceChanged(sourceId));
        }

        /// <inheritdoc />
        public SourceAsset? GetSource(string sourceId)
        {
            return Current.Sources.GetValueOrDefault(sourceId);
        }

        /// <inheritdoc />
        public void ClearSource(string sourceId)
        {
            if (Current.Sources.TryGetValue(sourceId, out var source))
            {
                source.Clear();
                PublishChange(GenerationParametersChangedEventArgs.SourceChanged(sourceId));
            }
        }

        /// <inheritdoc />
        public void LoadParameters(GenerationParameters parameters)
        {
            var current = Current;
            current.WorkflowId = parameters.WorkflowId;
            current.Fragments.Clear();
            foreach (var kvp in parameters.Fragments)
            {
                current.Fragments[kvp.Key] = kvp.Value;
            }
            current.Assets.Clear();
            foreach (var kvp in parameters.Assets)
            {
                current.Assets[kvp.Key] = kvp.Value;
            }
            current.Sources.Clear();
            foreach (var kvp in parameters.Sources)
            {
                current.Sources[kvp.Key] = kvp.Value;
            }
            current.Loras.Clear();
            current.Loras.AddRange(parameters.Loras);

            _logger.LogDebug("Loaded generation parameters (WorkflowId: {WorkflowId})", current.WorkflowId);
            PublishChange(GenerationParametersChangedEventArgs.ParametersLoaded());
        }

        /// <inheritdoc />
        public GenerationParameters CreateSnapshot()
        {
            return Current.Clone();
        }

        /// <inheritdoc />
        public void NotifyChanged()
        {
            PublishChange(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.Unknown));
        }

        /// <summary>
        /// Publishes a change event through the event service.
        /// </summary>
        private void PublishChange(GenerationParametersChangedEventArgs args)
        {
            _eventService.Publish(args);
        }

        #endregion

        #region Source Options Resolution

        /// <inheritdoc />
        public void ClearSourceCache()
        {
            _sourceOptionsCache.Clear();
            _resolvedFragmentOptions.Clear();
            _logger.LogDebug("Cleared source options cache");
        }

        /// <summary>
        /// Pre-resolves all dynamic source options for the workflow's fragments.
        /// This populates the cache and sets default values for parameters that have dynamic sources
        /// but no value set yet.
        /// </summary>
        private async Task PreResolveDynamicSourcesAsync(Workflow workflow)
        {
            if (workflow == null) return;

            var schemas = _workflowService.GetWorkflowFragmentSchemas(workflow);
            var resolvedCount = 0;
            var defaultsSetCount = 0;

            foreach (var (fragmentId, schema) in schemas)
            {
                if (schema?.Parameters == null) continue;

                var fragment = Current.GetFragment(fragmentId);
                foreach (var (paramName, constraints) in schema.Parameters)
                {
                    if (!constraints.HasDynamicSource) continue;

                    try
                    {
                        // Resolve options and cache them
                        var options = await ResolveSourceOptionsAsync(constraints);
                        resolvedCount++;

                        // If fragment has no value set for this parameter and options are available,
                        // set the first option as default
                        if (options.Count > 0 && !fragment.HasValue(paramName))
                        {
                            fragment.SetValue(paramName, options[0]);
                            defaultsSetCount++;
                            _logger.LogDebug("Set default for {FragmentId}.{Parameter} = {Value} (from dynamic source)",
                                fragmentId, paramName, options[0]);
                        }

                        // Also store resolved options in fragment for UI access
                        StoreResolvedOptions(fragmentId, paramName, options);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to pre-resolve source for {FragmentId}.{Parameter}", fragmentId, paramName);
                    }
                }

                // Also check fields array for dynamic sources
                if (schema.Fields != null)
                {
                    await PreResolveFieldSourcesAsync(fragmentId, fragment, schema.Fields);
                }
            }

            if (resolvedCount > 0)
            {
                _logger.LogInformation("Pre-resolved {ResolvedCount} dynamic sources, set {DefaultsCount} default values for workflow '{WorkflowTitle}'",
                    resolvedCount, defaultsSetCount, workflow.Title);
            }

            // Notify UI components that dynamic sources are now available
            // This allows components to refresh their dropdown options
            PublishChange(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.DynamicSourcesResolved));
        }

        /// <summary>
        /// Pre-resolves dynamic sources from field schemas (used when component is null and fields are defined).
        /// </summary>
        private async Task PreResolveFieldSourcesAsync(string fragmentId, FragmentParameters fragment, List<FieldSchema> fields)
        {
            foreach (var field in fields)
            {
                if (field.HasDynamicSource)
                {
                    try
                    {
                        var options = await ResolveNodeSourceAsync(field.Source!, field.InputName!);

                        // Set default if not already set
                        if (options.Count > 0 && !fragment.HasValue(field.Parameter))
                        {
                            fragment.SetValue(field.Parameter, options[0]);
                            _logger.LogDebug("Set default for {FragmentId}.{Parameter} = {Value} (from field dynamic source)",
                                fragmentId, field.Parameter, options[0]);
                        }

                        StoreResolvedOptions(fragmentId, field.Parameter, options);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to pre-resolve field source for {FragmentId}.{Parameter}", fragmentId, field.Parameter);
                    }
                }

                // Recursively handle nested fields in groups
                if (field.Fields != null)
                {
                    await PreResolveFieldSourcesAsync(fragmentId, fragment, field.Fields);
                }
            }
        }

        /// <summary>
        /// Stores resolved options for later UI access.
        /// Stores in both the service-level cache and the fragment's ResolvedOptions.
        /// </summary>
        private void StoreResolvedOptions(string fragmentId, string parameterName, List<string> options)
        {
            // Store in service-level cache for GetResolvedOptions fallback
            var key = $"{fragmentId}.{parameterName}";
            _resolvedFragmentOptions[key] = options;

            // Also store directly in the fragment for easier access
            var fragment = Current.GetFragment(fragmentId);
            if (fragment != null)
            {
                fragment.ResolvedOptions[parameterName] = options;
            }
        }

        /// <inheritdoc />
        public List<string> GetResolvedOptions(string fragmentId, string parameterName)
        {
            // First check fragment's ResolvedOptions
            var fragment = Current.GetFragment(fragmentId);
            if (fragment?.ResolvedOptions.TryGetValue(parameterName, out var fragmentOptions) == true)
            {
                return fragmentOptions;
            }

            // Fall back to service-level cache
            var key = $"{fragmentId}.{parameterName}";
            return _resolvedFragmentOptions.GetValueOrDefault(key, new List<string>());
        }

        /// <inheritdoc />
        public async Task<List<string>> ResolveSourceOptionsAsync(ParameterConstraints constraints)
        {
            if (constraints == null)
                return new List<string>();

            // If static options are defined, return them directly
            if (constraints.Options?.Count > 0)
                return constraints.Options;

            // If no dynamic source is defined, return empty
            if (!constraints.HasDynamicSource)
                return new List<string>();

            // Source contains the node class_type, InputName contains the input field name
            return await ResolveNodeSourceAsync(constraints.Source!, constraints.InputName!);
        }

        /// <summary>
        /// Resolves a node source by querying ComfyUI's object_info API or backend service.
        /// </summary>
        /// <param name="classType">The node class_type (e.g., "SeedVR2LoadDiTModel") or Backend.* identifier</param>
        /// <param name="inputName">The input field name (e.g., "model") - optional for Backend.* sources</param>
        private async Task<List<string>> ResolveNodeSourceAsync(string classType, string inputName)
        {
            var cacheKey = $"{classType}:{inputName}";

            if (_sourceOptionsCache.TryGetValue(cacheKey, out var cached))
            {
                _logger.LogTrace("Returning cached options for {CacheKey}", cacheKey);
                return cached;
            }

            try
            {
                var options = await _comfyUIService.GetNodeInputOptionsAsync(classType, inputName);
                _sourceOptionsCache[cacheKey] = options;
                _logger.LogDebug("Resolved {Count} options for {ClassType}.{InputName}", options.Count, classType, inputName);
                return options;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve source {ClassType}.{InputName}", classType, inputName);
                return new List<string>();
            }
        }

        /// <summary>
        /// Creates a fragment with default values from the C# workflow builder.
        /// </summary>
        private FragmentParameters? CreateFragmentWithDefaults(string fragmentId)
        {
            // Get the current workflow - required to find fragment definition
            var workflowId = Current.WorkflowId;
            if (!workflowId.HasValue)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - no workflow selected", fragmentId);
                return null;
            }

            var builder = _workflowService.GetWorkflowBuilder(workflowId.Value);
            if (builder == null)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - no C# builder found", fragmentId);
                return null;
            }

            var fragmentBuilder = builder.GetFragments()
                .FirstOrDefault(f => f.Metadata.Id.Equals(fragmentId, StringComparison.OrdinalIgnoreCase));

            if (fragmentBuilder == null)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - not defined in workflow", fragmentId);
                return null;
            }

            var metadata = fragmentBuilder.Metadata;
            var fragment = new FragmentParameters
            {
                FragmentFile = $"fluent:{fragmentId}",
                IsActive = true,
                Order = Current.Fragments.Values.Any() ? Current.Fragments.Values.Max(f => f.Order) + 1 : 0
            };

            if (metadata.Parameters != null)
            {
                foreach (var param in metadata.Parameters)
                {
                    if (param.DefaultValue != null)
                    {
                        fragment.Values[param.Name] = param.DefaultValue;
                    }
                }
            }

            _logger.LogDebug("Created fragment '{FragmentId}' with {ValueCount} default values",
                fragmentId, fragment.Values.Count);

            return fragment;
        }

        #endregion
    }
}
