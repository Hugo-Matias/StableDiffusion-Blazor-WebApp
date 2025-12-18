using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Provides CRUD operations and workflow initialization.
    /// </summary>
    public class GenerationParameterService : IGenerationParameterService
    {
        private readonly ILogger<GenerationParameterService> _logger;
        private readonly IWorkflowService _workflowService;
        private GenerationParameters _current = new();

        /// <inheritdoc />
        public event Action? OnParametersChanged;

        /// <inheritdoc />
        public GenerationParameters Current => _current;

        public GenerationParameterService(
            ILogger<GenerationParameterService> logger,
            IWorkflowService workflowService)
        {
            _logger = logger;
            _workflowService = workflowService;
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

            // Create new parameters instance
            var newParams = new GenerationParameters
            {
                WorkflowId = workflow.Id
            };

            // Initialize assets from workflow defaults
            if (workflow.Assets != null)
            {
                foreach (var asset in workflow.Assets)
                {
                    if (!string.IsNullOrWhiteSpace(asset.DefaultValue))
                    {
                        newParams.Assets[asset.Parameter] = asset.DefaultValue;
                    }
                }
            }

            // Parse pipeline to extract fragments and their defaults
            try
            {
                InitializeFragmentsFromPipeline(workflow, newParams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing pipeline for workflow {WorkflowTitle}", workflow.Title);
            }

            _current = newParams;
            NotifyChanged();
        }

        /// <summary>
        /// Parses the workflow's RawJson to extract pipeline fragments and their default values.
        /// </summary>
        private void InitializeFragmentsFromPipeline(Workflow workflow, GenerationParameters parameters)
        {
            if (string.IsNullOrEmpty(workflow.RawJson))
                return;

            try
            {
                using var doc = JsonDocument.Parse(workflow.RawJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Pipeline", out var pipelineEl) || 
                    pipelineEl.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                int order = 0;
                foreach (var stepEl in pipelineEl.EnumerateArray())
                {
                    // Get fragment ID (optional, we generate if missing)
                    string fragmentId;
                    if (stepEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    {
                        fragmentId = idEl.GetString() ?? $"fragment_{order}";
                    }
                    else
                    {
                        // Generate ID from fragment filename
                        if (stepEl.TryGetProperty("fragment", out var fragEl) && fragEl.ValueKind == JsonValueKind.String)
                        {
                            var fragName = fragEl.GetString() ?? "";
                            fragmentId = Path.GetFileNameWithoutExtension(fragName).Replace("-", "_");
                            
                            // Make unique if already exists
                            if (parameters.Fragments.ContainsKey(fragmentId))
                            {
                                fragmentId = $"{fragmentId}_{order}";
                            }
                        }
                        else
                        {
                            fragmentId = $"fragment_{order}";
                        }
                    }

                    // Get fragment file
                    string fragmentFile = "";
                    if (stepEl.TryGetProperty("fragment", out var fragmentEl) && fragmentEl.ValueKind == JsonValueKind.String)
                    {
                        fragmentFile = fragmentEl.GetString() ?? "";
                    }

                    // Create fragment parameters
                    var fragment = new FragmentParameters
                    {
                        FragmentFile = fragmentFile,
                        IsActive = true,
                        Order = order++
                    };

                    // Extract default parameter values
                    // Note: These are Scriban templates with {{ }}, we need to evaluate them
                    // For now, we extract literal values and skip template expressions
                    if (stepEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var param in paramsEl.EnumerateObject())
                        {
                            var value = ExtractParameterValue(param.Value);
                            if (value != null)
                            {
                                fragment.Values[param.Name] = value;
                            }
                        }
                    }

                    parameters.Fragments[fragmentId] = fragment;
                    _logger.LogTrace("Initialized fragment '{FragmentId}' from {FragmentFile}", fragmentId, fragmentFile);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse workflow pipeline JSON");
            }
        }

        /// <summary>
        /// Extracts a parameter value from JSON, handling different value types.
        /// Skips Scriban template expressions.
        /// </summary>
        private object? ExtractParameterValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intVal) 
                    ? intVal 
                    : (element.TryGetInt64(out var longVal) 
                        ? longVal 
                        : element.GetDouble()),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                // Skip arrays and objects for now (complex types)
                _ => null
            };
        }

        /// <inheritdoc />
        public void SetFragmentValue(string fragmentId, string parameter, object? value)
        {
            var fragment = _current.GetOrCreateFragment(fragmentId);
            fragment.SetValue(parameter, value);
            _logger.LogTrace("Set {FragmentId}.{Parameter} = {Value}", fragmentId, parameter, value);
        }

        /// <inheritdoc />
        public T? GetFragmentValue<T>(string fragmentId, string parameter)
        {
            var fragment = _current.GetFragment(fragmentId);
            if (fragment == null)
                return default;
            return fragment.GetValue<T>(parameter);
        }

        /// <inheritdoc />
        public void SetFragmentActive(string fragmentId, bool isActive)
        {
            var fragment = _current.GetFragment(fragmentId);
            if (fragment != null)
            {
                fragment.IsActive = isActive;
                _logger.LogDebug("Set fragment '{FragmentId}' active = {IsActive}", fragmentId, isActive);
            }
        }

        /// <inheritdoc />
        public bool IsFragmentActive(string fragmentId)
        {
            var fragment = _current.GetFragment(fragmentId);
            return fragment?.IsActive ?? false;
        }

        /// <inheritdoc />
        public (string fragmentId, FragmentParameters parameters) AddFragmentInstance(string fragmentFile, string? baseId = null)
        {
            // Generate unique ID
            var baseName = baseId ?? Path.GetFileNameWithoutExtension(fragmentFile).Replace("-", "_");
            var index = 1;
            var fragmentId = baseName;
            
            while (_current.Fragments.ContainsKey(fragmentId))
            {
                fragmentId = $"{baseName}_{index++}";
            }

            // Get max order
            var maxOrder = _current.Fragments.Values.Any() 
                ? _current.Fragments.Values.Max(f => f.Order) 
                : 0;

            var parameters = new FragmentParameters
            {
                FragmentFile = fragmentFile,
                IsActive = true,
                Order = maxOrder + 1
            };

            _current.Fragments[fragmentId] = parameters;
            _logger.LogDebug("Added fragment instance '{FragmentId}' for {FragmentFile}", fragmentId, fragmentFile);

            return (fragmentId, parameters);
        }

        /// <inheritdoc />
        public bool RemoveFragmentInstance(string fragmentId)
        {
            var removed = _current.Fragments.Remove(fragmentId);
            if (removed)
            {
                _logger.LogDebug("Removed fragment instance '{FragmentId}'", fragmentId);
            }
            return removed;
        }

        /// <inheritdoc />
        public void ReorderFragments(IEnumerable<string> fragmentIds)
        {
            var order = 0;
            foreach (var id in fragmentIds)
            {
                if (_current.Fragments.TryGetValue(id, out var fragment))
                {
                    fragment.Order = order++;
                }
            }
        }

        /// <inheritdoc />
        public void SetAsset(string assetName, string value)
        {
            _current.Assets[assetName] = value;
            _logger.LogTrace("Set asset {AssetName} = {Value}", assetName, value);
        }

        /// <inheritdoc />
        public string? GetAsset(string assetName)
        {
            return _current.Assets.GetValueOrDefault(assetName);
        }

        /// <inheritdoc />
        public void SetSource(string sourceId, SourceAsset source)
        {
            _current.Sources[sourceId] = source;
            _logger.LogTrace("Set source {SourceId}", sourceId);
        }

        /// <inheritdoc />
        public SourceAsset? GetSource(string sourceId)
        {
            return _current.Sources.GetValueOrDefault(sourceId);
        }

        /// <inheritdoc />
        public void ClearSource(string sourceId)
        {
            if (_current.Sources.TryGetValue(sourceId, out var source))
            {
                source.Clear();
            }
        }

        /// <inheritdoc />
        public void LoadParameters(GenerationParameters parameters)
        {
            _current = parameters ?? new GenerationParameters();
            _logger.LogDebug("Loaded generation parameters (WorkflowId: {WorkflowId})", _current.WorkflowId);
            NotifyChanged();
        }

        /// <inheritdoc />
        public GenerationParameters CreateSnapshot()
        {
            return _current.Clone();
        }

        /// <inheritdoc />
        public void NotifyChanged()
        {
            OnParametersChanged?.Invoke();
        }
    }
}
