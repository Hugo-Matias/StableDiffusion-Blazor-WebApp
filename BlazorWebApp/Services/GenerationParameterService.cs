using BlazorWebApp.Events;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Operates on StateService.GenerationParameters for state persistence.
    /// </summary>
    public class GenerationParameterService : IGenerationParameterService
    {
        private readonly ILogger<GenerationParameterService> _logger;
        private readonly IWorkflowService _workflowService;
        private readonly IEventService _eventService;
        private readonly IStateService _stateService;

        /// <inheritdoc />
        public GenerationParameters Current => _stateService.GenerationParameters;

        public GenerationParameterService(
            ILogger<GenerationParameterService> logger,
            IWorkflowService workflowService,
            IEventService eventService,
            IStateService stateService)
        {
            _logger = logger;
            _workflowService = workflowService;
            _eventService = eventService;
            _stateService = stateService;
        }

        /// <inheritdoc />
        public void InitializeFromWorkflow(Workflow workflow)
        {
            if (workflow == null)
            {
                _logger.LogWarning("Cannot initialize from null workflow");
                return;
            }

            _logger.LogDebug("Initializing parameters from workflow: {WorkflowTitle}", workflow.Title);

            // Clear existing parameters and reinitialize
            var current = Current;
            current.WorkflowId = workflow.Id;
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

            // Parse pipeline to extract fragments using regex (RawJson contains Scriban templates)
            try
            {
                InitializeFragmentsFromPipeline(workflow, current);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing pipeline for workflow {WorkflowTitle}", workflow.Title);
            }

            PublishChange(GenerationParametersChangedEventArgs.WorkflowChanged(workflow.Id));
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

        /// <inheritdoc />
        public Task<GenerationParameters> InitializeFromWorkflowAsync(Workflow workflow)
        {
            InitializeFromWorkflow(workflow);
            return Task.FromResult(Current);
        }

        /// <summary>
        /// Parses the workflow's RawJson to extract pipeline fragments using regex.
        /// RawJson contains Scriban templates so we cannot use JSON parsing directly.
        /// </summary>
        private void InitializeFragmentsFromPipeline(Workflow workflow, GenerationParameters parameters)
        {
            if (string.IsNullOrEmpty(workflow.RawJson))
                return;

            // Use regex to find Pipeline array entries since RawJson contains Scriban templates
            // Look for each step in the Pipeline that has an "id" and "fragment" field
            var pipelineSteps = ParsePipelineStepsWithRegex(workflow.RawJson);
            
            int order = 0;
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
                        fragmentId = $"{fragmentId}_{order}";
                    }
                }

                // Create fragment parameters
                var fragment = new FragmentParameters
                {
                    FragmentFile = step.Fragment,
                    IsActive = true,
                    Order = order++
                };

                parameters.Fragments[fragmentId] = fragment;
                _logger.LogTrace("Initialized fragment '{FragmentId}' from {FragmentFile}", fragmentId, step.Fragment);
            }
            
            _logger.LogDebug("Initialized {Count} fragments for workflow '{WorkflowTitle}'", 
                parameters.Fragments.Count, workflow.Title);
        }

        /// <summary>
        /// Parses Pipeline steps from RawJson using regex to handle Scriban template syntax.
        /// Returns a list of (Id, Fragment) tuples.
        /// </summary>
        private List<(string Id, string Fragment)> ParsePipelineStepsWithRegex(string rawJson)
        {
            var result = new List<(string Id, string Fragment)>();
            
            // Find the Pipeline array content
            // The Pipeline array contains objects with "id" and "fragment" fields
            // We need to handle that some content may contain Scriban syntax
            
            // Match each step object in Pipeline - look for "fragment": "..." patterns
            // This regex finds objects that have a "fragment" field
            var fragmentPattern = @"""fragment""\s*:\s*""([^""]+)""";
            var idPattern = @"""id""\s*:\s*""([^""]+)""";
            
            // First, try to isolate the Pipeline section
            var pipelineMatch = Regex.Match(rawJson, @"""Pipeline""\s*:\s*\[(.*?)\](?=\s*\})", RegexOptions.Singleline);
            if (!pipelineMatch.Success)
            {
                _logger.LogDebug("No Pipeline array found in workflow");
                return result;
            }
            
            var pipelineContent = pipelineMatch.Groups[1].Value;
            
            // Find all step objects by matching opening and closing braces
            // This is a simplified approach - we look for { ... } blocks that contain "fragment"
            var stepMatches = Regex.Matches(pipelineContent, @"\{[^{}]*""fragment""[^{}]*\}", RegexOptions.Singleline);
            
            foreach (Match stepMatch in stepMatches)
            {
                var stepContent = stepMatch.Value;
                
                // Extract fragment
                var fragMatch = Regex.Match(stepContent, fragmentPattern);
                var fragment = fragMatch.Success ? fragMatch.Groups[1].Value : "";
                
                // Extract id (optional)
                var idMatch = Regex.Match(stepContent, idPattern);
                var id = idMatch.Success ? idMatch.Groups[1].Value : "";
                
                if (!string.IsNullOrEmpty(fragment))
                {
                    result.Add((id, fragment));
                }
            }
            
            return result;
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
    }
}
