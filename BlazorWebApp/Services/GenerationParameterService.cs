using BlazorWebApp.Events;
using BlazorWebApp.Models;
using System.Collections.Concurrent;
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
            IEventService eventService,
            IStateService stateService,
            IComfyUIService comfyUIService)
        {
            _logger = logger;
            _workflowService = workflowService;
            _eventService = eventService;
            _stateService = stateService;
            _comfyUIService = comfyUIService;
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

            // Clear source options cache on workflow change
            ClearSourceCache();

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
        /// Also populates fragment Values with defaults from both:
        /// 1. The workflow template's pipeline step parameters (e.g., {{ SeedVR2.Model ?? "default" | json }})
        /// 2. The fragment template itself (fallback)
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
                    Order = order++
                };

                // Priority 1: Use defaults from workflow template's pipeline step parameters
                // These are the {{ SeedVR2.Model ?? "default" | json }} expressions
                foreach (var kvp in step.Parameters)
                {
                    fragment.Values[kvp.Key] = kvp.Value;
                }

                // Priority 2: Fill in any missing values from fragment template defaults
                // These are the {{ param ?? "default" | json }} expressions in the fragment itself
                var fragmentDefaults = _workflowService.ParseFragmentDefaults(step.Fragment);
                foreach (var kvp in fragmentDefaults)
                {
                    // Only add if not already set by step parameters
                    if (!fragment.Values.ContainsKey(kvp.Key))
                    {
                        fragment.Values[kvp.Key] = kvp.Value;
                    }
                }

                parameters.Fragments[fragmentId] = fragment;
                _logger.LogTrace("Initialized fragment '{FragmentId}' from {FragmentFile} (IsActive: {IsActive}, DefaultCollapsed: {DefaultCollapsed}, Values: {ValueCount})", 
                    fragmentId, step.Fragment, fragment.IsActive, isOptional, fragment.Values.Count);
            }
            
            _logger.LogDebug("Initialized {Count} fragments for workflow '{WorkflowTitle}'", 
                parameters.Fragments.Count, workflow.Title);
        }

        /// <summary>
        /// Parses Pipeline steps from RawJson using regex to handle Scriban template syntax.
        /// Returns a list of (Id, Fragment, Parameters) tuples.
        /// Parameters contains default values extracted from the pipeline step.
        /// </summary>
        private List<(string Id, string Fragment, Dictionary<string, object?> Parameters)> ParsePipelineStepsWithRegex(string rawJson)
        {
            var result = new List<(string Id, string Fragment, Dictionary<string, object?> Parameters)>();
            
            // Match each step object in Pipeline - look for "fragment": "..." patterns
            var fragmentPattern = @"""fragment""\s*:\s*""([^""]+)""";
            var idPattern = @"""id""\s*:\s*""([^""]+)""";
            
            // First, try to isolate the Pipeline section
            var pipelineMatch = Regex.Match(rawJson, @"""Pipeline""\s*:\s*\[", RegexOptions.Singleline);
            if (!pipelineMatch.Success)
            {
                _logger.LogDebug("No Pipeline array found in workflow");
                return result;
            }
            
            // Find the Pipeline array content by counting brackets
            var startIndex = pipelineMatch.Index + pipelineMatch.Length;
            var bracketCount = 1;
            var endIndex = startIndex;
            
            for (var i = startIndex; i < rawJson.Length && bracketCount > 0; i++)
            {
                if (rawJson[i] == '[') bracketCount++;
                else if (rawJson[i] == ']') bracketCount--;
                endIndex = i;
            }
            
            var pipelineContent = rawJson.Substring(startIndex, endIndex - startIndex);
            
            // Find each step by using brace counting
            var stepStart = -1;
            var braceDepth = 0;
            
            for (var i = 0; i < pipelineContent.Length; i++)
            {
                var c = pipelineContent[i];
                
                if (c == '{')
                {
                    if (braceDepth == 0)
                    {
                        stepStart = i;
                    }
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0 && stepStart >= 0)
                    {
                        // Extract the step content
                        var stepContent = pipelineContent.Substring(stepStart, i - stepStart + 1);
                        
                        // Extract fragment (required)
                        var fragMatch = Regex.Match(stepContent, fragmentPattern);
                        if (fragMatch.Success)
                        {
                            var fragment = fragMatch.Groups[1].Value;
                            
                            // Extract id (optional)
                            var idMatch = Regex.Match(stepContent, idPattern);
                            var id = idMatch.Success ? idMatch.Groups[1].Value : "";
                            
                            // Extract parameter defaults from step parameters
                            var parameters = ParseStepParameterDefaults(stepContent);
                            
                            result.Add((id, fragment, parameters));
                            _logger.LogTrace("Parsed pipeline step: id='{Id}', fragment='{Fragment}', params={ParamCount}", 
                                id, fragment, parameters.Count);
                        }
                        
                        stepStart = -1;
                    }
                }
            }
            
            _logger.LogDebug("Parsed {Count} pipeline steps from workflow", result.Count);
            return result;
        }

        /// <summary>
        /// Parses default values from a pipeline step's parameters object.
        /// Handles Scriban template expressions like {{ Param ?? "default" | json }}
        /// </summary>
        private Dictionary<string, object?> ParseStepParameterDefaults(string stepContent)
        {
            var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            
            // Find the parameters block within the step
            var paramsMatch = Regex.Match(stepContent, @"""parameters""\s*:\s*\{", RegexOptions.Singleline);
            if (!paramsMatch.Success)
                return defaults;
            
            // Extract parameters block content using brace counting
            var startIndex = paramsMatch.Index + paramsMatch.Length;
            var braceCount = 1;
            var endIndex = startIndex;
            
            for (var i = startIndex; i < stepContent.Length && braceCount > 0; i++)
            {
                if (stepContent[i] == '{') braceCount++;
                else if (stepContent[i] == '}') braceCount--;
                endIndex = i;
            }
            
            var paramsContent = stepContent.Substring(startIndex, endIndex - startIndex);
            
            // Pattern to match parameter definitions with defaults:
            // "param_name": {{ SomeVar ?? "default" | json }}
            // "param_name": {{ SomeVar ?? 123 | json }}
            var paramPattern = @"""(\w+)""\s*:\s*\{\{\s*[\w.]+\s*\?\?\s*([^|]+?)\s*\|";
            
            var matches = Regex.Matches(paramsContent, paramPattern);
            foreach (Match match in matches)
            {
                var paramName = match.Groups[1].Value.Trim();
                var defaultValueStr = match.Groups[2].Value.Trim();
                
                if (string.IsNullOrEmpty(paramName) || defaults.ContainsKey(paramName))
                    continue;
                
                var parsedValue = ParseScribanDefaultValue(defaultValueStr);
                if (parsedValue != null)
                {
                    defaults[paramName] = parsedValue;
                    _logger.LogTrace("Parsed step parameter default for '{Param}': {Value}", paramName, parsedValue);
                }
            }
            
            return defaults;
        }

        /// <summary>
        /// Parses a Scriban default value expression into a CLR object.
        /// Handles strings ("value"), numbers (123, 1.5), booleans (true/false), and null.
        /// </summary>
        private object? ParseScribanDefaultValue(string valueStr)
        {
            if (string.IsNullOrWhiteSpace(valueStr))
                return null;

            valueStr = valueStr.Trim();

            // Handle quoted strings
            if ((valueStr.StartsWith("\"") && valueStr.EndsWith("\"")) ||
                (valueStr.StartsWith("'") && valueStr.EndsWith("'")))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }

            // Handle booleans
            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            // Handle null
            if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                valueStr.Equals("nil", StringComparison.OrdinalIgnoreCase))
                return null;

            // Handle integers
            if (long.TryParse(valueStr, out var longVal))
                return longVal;

            // Handle decimals/floats
            if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                return doubleVal;

            // If nothing else, return as string (could be a variable reference)
            return valueStr;
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

        #endregion
    }
}
