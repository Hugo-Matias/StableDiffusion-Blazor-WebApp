using BlazorWebApp.Events;
using BlazorWebApp.Models;
using System.Collections.Concurrent;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Operates on StateService.GenerationParameters for state persistence.
    /// Integrates with WorkflowStateService for per-workflow parameter persistence.
    /// </summary>
    public class GenerationParameterService : IGenerationParameterService
    {
        private readonly ILogger<GenerationParameterService> _logger;
        private readonly IWorkflowService _workflowService;
        private readonly IWorkflowStateService _workflowStateService;
        private readonly IEventService _eventService;
        private readonly IStateService _stateService;
        private readonly IComfyUIService _comfyUIService;

        /// <summary>
        /// Cache for resolved source options. Key format: "{classType}:{inputName}"
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _sourceOptionsCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cache for resolved options per fragment parameter. Key format: "{fragmentId}.{parameterName}"
        /// Used by UI components to get pre-resolved options without async calls.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _resolvedFragmentOptions = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public GenerationParameters Current => _stateService.GenerationParameters;

        public GenerationParameterService(
            ILogger<GenerationParameterService> logger,
            IWorkflowService workflowService,
            IWorkflowStateService workflowStateService,
            IEventService eventService,
            IStateService stateService,
            IComfyUIService comfyUIService)
        {
            _logger = logger;
            _workflowService = workflowService;
            _workflowStateService = workflowStateService;
            _eventService = eventService;
            _stateService = stateService;
            _comfyUIService = comfyUIService;
        }

        /// <inheritdoc />
        public void InitializeFromWorkflow(Workflow workflow)
        {
            // Synchronous version - just initializes from template defaults
            // For full functionality including DB state, use InitializeFromWorkflowAsync
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

            // Pre-resolve dynamic source options for all fragments
            await PreResolveDynamicSourcesAsync(workflow);

            return Current;
        }

        /// <summary>
        /// Saves the current workflow's parameters to the database.
        /// Call this before switching workflows or when the user explicitly saves.
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

            PublishChange(GenerationParametersChangedEventArgs.WorkflowChanged(workflow.Id));
        }

        /// <summary>
        /// Restores parameters from saved state, merging with workflow template for any new fragments.
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
            current.Loras.AddRange(savedState.Loras.Select(l => new Lora
            {
                Name = l.Name,
                Path = l.Path,
                Strength = l.Strength,
                IsEnabled = l.IsEnabled,
                IsNegative = l.IsNegative
            }));

            // Check for any new fragments in the workflow that weren't in saved state
            var pipelineSteps = _workflowService.GetPipelineSteps(workflow);
            foreach (var step in pipelineSteps)
            {
                var fragmentId = step.Id;
                if (string.IsNullOrEmpty(fragmentId))
                {
                    fragmentId = Path.GetFileNameWithoutExtension(step.Fragment).Replace("-", "_");
                }

                if (!current.Fragments.ContainsKey(fragmentId))
                {
                    // New fragment not in saved state - initialize from template
                    var schema = _workflowService.GetFragmentSchema(step.Fragment);
                    var isOptional = schema?.DefaultCollapsed ?? false;

                    var fragment = new FragmentParameters
                    {
                        FragmentFile = step.Fragment,
                        IsActive = !isOptional,
                        Order = step.Order
                    };

                    // Priority 1: Pipeline step defaults
                    foreach (var kvp in step.DefaultValues)
                    {
                        fragment.Values[kvp.Key] = kvp.Value;
                    }

                    // Priority 2: Schema defaults
                    if (schema?.Parameters != null)
                    {
                        foreach (var (paramName, constraints) in schema.Parameters)
                        {
                            if (!fragment.Values.ContainsKey(paramName) && constraints.Default != null)
                            {
                                fragment.Values[paramName] = constraints.Default;
                            }
                        }
                    }

                    // Note: Fragment body defaults are NOT used - Scriban rendering fallbacks only

                    current.Fragments[fragmentId] = fragment;
                    _logger.LogDebug("Added new fragment '{FragmentId}' from updated workflow template", fragmentId);
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
                        _logger.LogDebug("Added new asset '{AssetName}' from updated workflow template", asset.Parameter);
                    }
                }
            }

            _logger.LogDebug("Restored {FragmentCount} fragments, {AssetCount} assets from saved state", 
                current.Fragments.Count, current.Assets.Count);
        }

        /// <summary>
        /// Initializes fresh from workflow template (no saved state).
        /// </summary>
        private void InitializeFreshFromWorkflow(Workflow workflow, GenerationParameters current)
        {
            _logger.LogDebug("Initializing fresh from workflow template '{WorkflowTitle}'", workflow.Title);

            current.Fragments.Clear();
            current.Assets.Clear();
            current.Sources.Clear();
            // Note: Loras are preserved across workflow changes

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
            InitializeSourcesFromWorkflow(workflow, current);

            // Parse pipeline to extract fragments
            try
            {
                InitializeFragmentsFromPipeline(workflow, current);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing pipeline for workflow {WorkflowTitle}", workflow.Title);
            }
        }

        /// <summary>
        /// Initializes source assets from workflow definition.
        /// Sources are parsed by WorkflowService using regex since RawJson contains Scriban templates.
        /// </summary>
        private void InitializeSourcesFromWorkflow(Workflow workflow, GenerationParameters parameters)
        {
            // Sources must come from the parsed workflow.Sources property
            // We cannot parse RawJson directly as it contains Scriban template syntax
            if (workflow.Sources == null || workflow.Sources.Count == 0)
            {
                _logger.LogDebug("No sources defined for workflow '{WorkflowTitle}'", workflow.Title);
                return;
            }

            _logger.LogDebug("Initializing {Count} sources from workflow.Sources for '{WorkflowTitle}'", 
                workflow.Sources.Count, workflow.Title);
                
            foreach (var source in workflow.Sources)
            {
                parameters.Sources[source.Id] = new SourceAsset
                {
                    Label = source.Label,
                    Type = source.Type
                };
                _logger.LogDebug("Initialized source '{SourceId}' ({Type}) from workflow definition", source.Id, source.Type);
            }
        }

        /// <summary>
        /// Initializes fragment parameters from the workflow's pipeline.
        /// Uses WorkflowService.GetPipelineSteps() for cached, consolidated parsing logic.
        /// 
        /// Default value priority (see IGenerationParameterService interface for full docs):
        /// 1. Workflow template's pipeline step parameters
        /// 2. Fragment schema defaults (#meta.ui.parameters.*.default)
        /// 3. Dynamic options - handled in PreResolveDynamicSourcesAsync
        /// 
        /// Note: Fragment body defaults ({{ param ?? "default" }}) are NOT used here.
        /// Those are Scriban rendering fallbacks only.
        /// </summary>
        private void InitializeFragmentsFromPipeline(Workflow workflow, GenerationParameters parameters)
        {
            if (string.IsNullOrEmpty(workflow.RawJson))
                return;

            // Use cached pipeline steps from WorkflowService
            var pipelineSteps = _workflowService.GetPipelineSteps(workflow);
            
            foreach (var step in pipelineSteps)
            {
                var fragmentId = step.Id;
                
                // If no ID, generate from fragment filename
                if (string.IsNullOrEmpty(fragmentId))
                {
                    fragmentId = Path.GetFileNameWithoutExtension(step.Fragment).Replace("-", "_");
                    
                    // Make unique if already exists
                    if (parameters.Fragments.ContainsKey(fragmentId))
                    {
                        fragmentId = $"{fragmentId}_{step.Order}";
                    }
                }
                
                // Get the fragment schema to determine if this is an optional fragment
                // and to get schema defaults
                var schema = _workflowService.GetFragmentSchema(step.Fragment);
                var isOptional = schema?.DefaultCollapsed ?? false;

                // Create fragment parameters
                // Optional fragments (defaultCollapsed = true) default to inactive (not included in generation)
                var fragment = new FragmentParameters
                {
                    FragmentFile = step.Fragment,
                    IsActive = !isOptional,
                    Order = step.Order
                };

                // Priority 1: Use defaults from workflow template's pipeline step parameters
                foreach (var kvp in step.DefaultValues)
                {
                    fragment.Values[kvp.Key] = kvp.Value;
                }

                // Priority 2: Fill in any missing values from schema defaults
                if (schema?.Parameters != null)
                {
                    foreach (var (paramName, constraints) in schema.Parameters)
                    {
                        if (!fragment.Values.ContainsKey(paramName) && constraints.Default != null)
                        {
                            fragment.Values[paramName] = constraints.Default;
                            _logger.LogTrace("Applied schema default for '{FragmentId}.{Param}' = {Value}", 
                                fragmentId, paramName, constraints.Default);
                        }
                    }
                }

                // Note: Priority 3 (dynamic options) is handled in PreResolveDynamicSourcesAsync
                // Note: Fragment body defaults ({{ param ?? "default" }}) are NOT applied here
                //       Those are Scriban rendering fallbacks only

                parameters.Fragments[fragmentId] = fragment;
                _logger.LogTrace("Initialized fragment '{FragmentId}' from {FragmentFile} (IsActive: {IsActive}, Values: {ValueCount})", 
                    fragmentId, step.Fragment, fragment.IsActive, fragment.Values.Count);
            }
            
            _logger.LogDebug("Initialized {Count} fragments for workflow '{WorkflowTitle}'", 
                parameters.Fragments.Count, workflow.Title);
        }

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

        // TODO: Phase 9 - Node Chaining Support
        // These methods are placeholders for chainable fragment functionality.
        
        /// <inheritdoc />
        public (string fragmentId, FragmentParameters parameters) AddFragmentInstance(string fragmentFile, string? baseId = null)
        {
            // Generate unique ID
            var baseName = baseId ?? Path.GetFileNameWithoutExtension(fragmentFile).Replace("-", "_");
            var index = 1;
            var fragmentId = baseName;
            
            while (Current.Fragments.ContainsKey(fragmentId))
            {
                fragmentId = $"{baseName}_{index++}";
            }

            // Get max order
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
            // Copy values into the State's GenerationParameters
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
                if (fragment == null) continue;

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
        /// Resolves a node source by querying ComfyUI's object_info API.
        /// </summary>
        /// <param name="classType">The node class_type (e.g., "SeedVR2LoadDiTModel")</param>
        /// <param name="inputName">The input field name (e.g., "model")</param>
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
        /// Creates a fragment with default values based on the fragment ID.
        /// 
        /// IMPORTANT: Only fragments defined in the current workflow's Pipeline can be created.
        /// This ensures we always have the correct FragmentFile and defaults.
        /// 
        /// Default priority:
        /// 1. Pipeline step defaults
        /// 2. Schema defaults (#meta.ui.parameters.*.default)
        /// 3. Dynamic source defaults (handled by PreResolveDynamicSourcesAsync)
        /// </summary>
        /// <param name="fragmentId">The fragment ID to create</param>
        /// <returns>A new FragmentParameters with defaults, or null if not found in pipeline</returns>
        private FragmentParameters? CreateFragmentWithDefaults(string fragmentId)
        {
            // Get the current workflow - required to find fragment definition
            var workflowId = Current.WorkflowId;
            if (!workflowId.HasValue)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - no workflow selected", fragmentId);
                return null;
            }

            var workflow = _workflowService.GetWorkflowById(workflowId.Value);
            if (workflow == null)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - workflow {WorkflowId} not found", fragmentId, workflowId);
                return null;
            }

            // Find the fragment in the workflow's pipeline
            var pipelineSteps = _workflowService.GetPipelineSteps(workflow);
            var matchingStep = pipelineSteps.FirstOrDefault(s => 
                s.Id.Equals(fragmentId, StringComparison.OrdinalIgnoreCase) || 
                Path.GetFileNameWithoutExtension(s.Fragment).Replace("-", "_").Equals(fragmentId, StringComparison.OrdinalIgnoreCase));
            
            if (matchingStep == null)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - not defined in workflow pipeline", fragmentId);
                return null;
            }

            // Get schema for defaults and constraints
            var schema = _workflowService.GetFragmentSchema(matchingStep.Fragment);

            // Create the fragment using the pipeline step's fragment file
            var fragment = new FragmentParameters
            {
                FragmentFile = matchingStep.Fragment,
                IsActive = true,
                Order = Current.Fragments.Values.Any() ? Current.Fragments.Values.Max(f => f.Order) + 1 : 0
            };

            // Priority 1: Apply pipeline defaults
            foreach (var kvp in matchingStep.DefaultValues)
            {
                fragment.Values[kvp.Key] = kvp.Value;
            }

            // Priority 2: Apply schema defaults for any missing values
            if (schema?.Parameters != null)
            {
                foreach (var (paramName, constraints) in schema.Parameters)
                {
                    if (!fragment.Values.ContainsKey(paramName) && constraints.Default != null)
                    {
                        fragment.Values[paramName] = constraints.Default;
                    }
                }
            }

            // Note: Fragment body defaults are NOT used - Scriban rendering fallbacks only
            // Note: Priority 3 (dynamic sources) would require async, handled separately

            _logger.LogDebug("Created fragment '{FragmentId}' with {ValueCount} default values from {FragmentFile}", 
                fragmentId, fragment.Values.Count, matchingStep.Fragment);
            
            return fragment;
        }

        #endregion
    }
}
