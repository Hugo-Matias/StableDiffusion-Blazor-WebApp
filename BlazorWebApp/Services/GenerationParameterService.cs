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

                    foreach (var kvp in step.DefaultValues)
                    {
                        fragment.Values[kvp.Key] = kvp.Value;
                    }

                    var fragmentDefaults = _workflowService.ParseFragmentDefaults(step.Fragment);
                    foreach (var kvp in fragmentDefaults)
                    {
                        if (!fragment.Values.ContainsKey(kvp.Key))
                        {
                            fragment.Values[kvp.Key] = kvp.Value;
                        }
                    }

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
        /// 1. Workflow template's pipeline step parameters ({{ Param ?? "default" | json }})
        /// 2. Fragment template defaults ({{ param ?? "fallback" | json }} in fragment body)
        /// 3. Dynamic options - NOT handled here (requires async UI calls)
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
                // Optional fragments have defaultCollapsed = true in their UI schema
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

                // Priority 2: Fill in any missing values from fragment template defaults
                var fragmentDefaults = _workflowService.ParseFragmentDefaults(step.Fragment);
                foreach (var kvp in fragmentDefaults)
                {
                    // Only add if not already set by step parameters
                    if (!fragment.Values.ContainsKey(kvp.Key))
                    {
                        fragment.Values[kvp.Key] = kvp.Value;
                    }
                }

                // Note: Priority 3 (dynamic options) is handled by UI components
                // because it requires async calls to ComfyUI API

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
            _logger.LogDebug("Cleared source options cache");
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

            // Create the fragment using the pipeline step's fragment file
            var fragment = new FragmentParameters
            {
                FragmentFile = matchingStep.Fragment,
                IsActive = true,
                Order = Current.Fragments.Values.Any() ? Current.Fragments.Values.Max(f => f.Order) + 1 : 0
            };

            // Apply pipeline defaults
            foreach (var kvp in matchingStep.DefaultValues)
            {
                fragment.Values[kvp.Key] = kvp.Value;
            }

            // Apply fragment template defaults for any missing values
            var fragmentDefaults = _workflowService.ParseFragmentDefaults(matchingStep.Fragment);
            foreach (var kvp in fragmentDefaults)
            {
                if (!fragment.Values.ContainsKey(kvp.Key))
                {
                    fragment.Values[kvp.Key] = kvp.Value;
                }
            }

            _logger.LogDebug("Created fragment '{FragmentId}' with {ValueCount} default values from {FragmentFile}", 
                fragmentId, fragment.Values.Count, matchingStep.Fragment);
            
            return fragment;
        }

        #endregion
    }
}
