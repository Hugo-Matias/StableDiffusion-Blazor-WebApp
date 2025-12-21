using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Models.Fragments;
using BlazorWebApp.Services;
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
        private readonly IFragmentRegistry _fragmentRegistry;

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
            IComfyUIService comfyUIService,
            IFragmentRegistry fragmentRegistry)
        {
            _logger = logger;
            _workflowService = workflowService;
            _workflowStateService = workflowStateService;
            _eventService = eventService;
            _stateService = stateService;
            _comfyUIService = comfyUIService;
            _fragmentRegistry = fragmentRegistry;
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

            var currentWorkflowId = Current.WorkflowId;
            if (currentWorkflowId.HasValue && currentWorkflowId.Value != workflow.Id)
            {
                await SaveCurrentWorkflowStateAsync();
            }

            var savedState = await _workflowStateService.LoadWorkflowStateAsync(workflow.Id);
            InitializeFromWorkflowInternal(workflow, savedState);

            return Current;
        }

        /// <inheritdoc />
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

        private void InitializeFromWorkflowInternal(Workflow workflow, GenerationParameters? savedState)
        {
            if (workflow == null)
            {
                _logger.LogWarning("Cannot initialize from null workflow");
                return;
            }

            _logger.LogDebug("Initializing parameters from workflow: {WorkflowTitle} (hasSavedState: {HasSaved})", 
                workflow.Title, savedState != null);

            ClearSourceCache();

            var current = Current;
            current.WorkflowId = workflow.Id;

            if (savedState != null && savedState.Fragments.Count > 0)
            {
                RestoreFromSavedState(workflow, current, savedState);
            }
            else
            {
                InitializeFreshFromWorkflow(workflow, current);
            }

            PublishChange(GenerationParametersChangedEventArgs.WorkflowChanged(workflow.Id));
        }

        private void RestoreFromSavedState(Workflow workflow, GenerationParameters current, GenerationParameters savedState)
        {
            _logger.LogDebug("Restoring saved state for workflow '{WorkflowTitle}'", workflow.Title);

            current.Fragments.Clear();
            foreach (var kvp in savedState.Fragments)
            {
                current.Fragments[kvp.Key] = kvp.Value.Clone(kvp.Key);
            }

            current.Assets.Clear();
            foreach (var kvp in savedState.Assets)
            {
                current.Assets[kvp.Key] = kvp.Value;
            }

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

            current.Loras.Clear();
            current.Loras.AddRange(savedState.Loras.Select(l => new Lora
            {
                Name = l.Name,
                Path = l.Path,
                Strength = l.Strength,
                IsEnabled = l.IsEnabled,
                IsNegative = l.IsNegative
            }));

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
                    var fragment = CreateTypedFragment(fragmentId, step.Fragment);
                    if (fragment != null)
                    {
                        var schema = _workflowService.GetFragmentSchema(step.Fragment);
                        fragment.IsActive = !(schema?.DefaultCollapsed ?? false);
                        fragment.Order = step.Order;
                        ApplyDefaultsToFragment(fragment, step);
                        current.Fragments[fragmentId] = fragment;
                        _logger.LogDebug("Added new fragment '{FragmentId}' from updated workflow template", fragmentId);
                    }
                }
            }

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

        private void InitializeFreshFromWorkflow(Workflow workflow, GenerationParameters current)
        {
            _logger.LogDebug("Initializing fresh from workflow template '{WorkflowTitle}'", workflow.Title);

            current.Fragments.Clear();
            current.Assets.Clear();
            current.Sources.Clear();

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

            InitializeSourcesFromWorkflow(workflow, current);

            try
            {
                InitializeFragmentsFromPipeline(workflow, current);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing pipeline for workflow {WorkflowTitle}", workflow.Title);
            }
        }

        private void InitializeSourcesFromWorkflow(Workflow workflow, GenerationParameters parameters)
        {
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

        private void InitializeFragmentsFromPipeline(Workflow workflow, GenerationParameters parameters)
        {
            if (string.IsNullOrEmpty(workflow.RawJson))
                return;

            var pipelineSteps = _workflowService.GetPipelineSteps(workflow);
            
            foreach (var step in pipelineSteps)
            {
                var fragmentId = step.Id;
                
                if (string.IsNullOrEmpty(fragmentId))
                {
                    fragmentId = Path.GetFileNameWithoutExtension(step.Fragment).Replace("-", "_");
                    
                    if (parameters.Fragments.ContainsKey(fragmentId))
                    {
                        fragmentId = $"{fragmentId}_{step.Order}";
                    }
                }
                
                var schema = _workflowService.GetFragmentSchema(step.Fragment);
                var isOptional = schema?.DefaultCollapsed ?? false;

                // Create typed fragment using registry
                var fragment = CreateTypedFragment(fragmentId, step.Fragment);
                if (fragment == null)
                {
                    _logger.LogWarning("Could not create fragment for '{FragmentFile}'", step.Fragment);
                    continue;
                }

                fragment.IsActive = !isOptional;
                fragment.Order = step.Order;

                // Apply defaults
                ApplyDefaultsToFragment(fragment, step);

                parameters.Fragments[fragmentId] = fragment;
                _logger.LogTrace("Initialized fragment '{FragmentId}' from {FragmentFile} (IsActive: {IsActive})", 
                    fragmentId, step.Fragment, fragment.IsActive);
            }
            
            _logger.LogDebug("Initialized {Count} fragments for workflow '{WorkflowTitle}'", 
                parameters.Fragments.Count, workflow.Title);
        }

        /// <summary>
        /// Creates a typed fragment using the FragmentRegistry.
        /// </summary>
        private FragmentBase? CreateTypedFragment(string fragmentId, string fragmentFile)
        {
            var fragment = _fragmentRegistry.CreateFragment(fragmentFile, fragmentId);
            return fragment;
        }

        /// <summary>
        /// Applies default values to a fragment from pipeline step and fragment template.
        /// </summary>
        private void ApplyDefaultsToFragment(FragmentBase fragment, ParsedPipelineStep step)
        {
            // Priority 1: Step default values
            foreach (var kvp in step.DefaultValues)
            {
                fragment.SetPropertyFromDictionary(kvp.Key, kvp.Value);
            }

            // Priority 2: Fragment template defaults (for values not already set)
            var fragmentDefaults = _workflowService.ParseFragmentDefaults(step.Fragment);
            foreach (var kvp in fragmentDefaults)
            {
                // Check if value is already set (non-default)
                var currentDict = fragment.ToDictionary();
                if (!currentDict.ContainsKey(kvp.Key) || currentDict[kvp.Key] == null)
                {
                    fragment.SetPropertyFromDictionary(kvp.Key, kvp.Value);
                }
            }
        }

        /// <inheritdoc />
        public void SetFragmentValue(string fragmentId, string parameter, object? value)
        {
            var fragment = Current.GetFragment(fragmentId);
            if (fragment != null)
            {
                fragment.SetPropertyFromDictionary(parameter, value);
                _logger.LogTrace("Set {FragmentId}.{Parameter} = {Value}", fragmentId, parameter, value);
            }
            else
            {
                _logger.LogWarning("Cannot set value - fragment '{FragmentId}' not found", fragmentId);
            }
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
            
            var dict = fragment.ToDictionary();
            if (dict.TryGetValue(parameter, out var value) && value is T typedValue)
                return typedValue;
            
            return default;
        }

        /// <inheritdoc />
        public void SetFragmentActive(string fragmentId, bool isActive)
        {
            var fragment = Current.GetFragment(fragmentId);
            
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
        public (string fragmentId, FragmentBase parameters) AddFragmentInstance(string fragmentFile, string? baseId = null)
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

            var fragment = CreateTypedFragment(fragmentId, fragmentFile);
            if (fragment == null)
            {
                throw new InvalidOperationException($"Cannot create fragment for '{fragmentFile}'");
            }

            fragment.IsActive = true;
            fragment.Order = maxOrder + 1;

            Current.Fragments[fragmentId] = fragment;
            _logger.LogDebug("Added fragment instance '{FragmentId}' for {FragmentFile}", fragmentId, fragmentFile);
            
            PublishChange(GenerationParametersChangedEventArgs.FragmentAdded(fragmentId));

            return (fragmentId, fragment);
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

            if (constraints.Options?.Count > 0)
                return constraints.Options;

            if (!constraints.HasDynamicSource)
                return new List<string>();

            return await ResolveNodeSourceAsync(constraints.Source!, constraints.InputName!);
        }

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

        private FragmentBase? CreateFragmentWithDefaults(string fragmentId)
        {
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

            var pipelineSteps = _workflowService.GetPipelineSteps(workflow);
            var matchingStep = pipelineSteps.FirstOrDefault(s => 
                s.Id.Equals(fragmentId, StringComparison.OrdinalIgnoreCase) || 
                Path.GetFileNameWithoutExtension(s.Fragment).Replace("-", "_").Equals(fragmentId, StringComparison.OrdinalIgnoreCase));
            
            if (matchingStep == null)
            {
                _logger.LogWarning("Cannot create fragment '{FragmentId}' - not defined in workflow pipeline", fragmentId);
                return null;
            }

            var fragment = CreateTypedFragment(fragmentId, matchingStep.Fragment);
            if (fragment == null)
            {
                _logger.LogWarning("Cannot create typed fragment for '{FragmentFile}'", matchingStep.Fragment);
                return null;
            }

            fragment.Order = Current.Fragments.Values.Any() ? Current.Fragments.Values.Max(f => f.Order) + 1 : 0;
            fragment.IsActive = true;
            
            ApplyDefaultsToFragment(fragment, matchingStep);

            _logger.LogDebug("Created fragment '{FragmentId}' with defaults from {FragmentFile}", 
                fragmentId, matchingStep.Fragment);
            
            return fragment;
        }

        #endregion
    }
}
