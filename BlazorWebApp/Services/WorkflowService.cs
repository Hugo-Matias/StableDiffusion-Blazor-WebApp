using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using BlazorWebApp.Services.Templating;
using BlazorWebApp.Services.Templating.Pipeline;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for loading, composing, and managing workflow templates.
    /// Delegates parsing to WorkflowTemplateParser and FragmentSchemaService.
    /// Uses Fluid template engine for all template rendering.
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        private readonly string _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        private readonly IIOService _io;
        private readonly ILogger<WorkflowService> _logger;
        private readonly WorkflowTemplateParser _templateParser;
        private readonly IFragmentSchemaService _fragmentSchemaService;
        private readonly IFluidTemplateService _fluidService;
        private readonly FragmentConditionValidator _conditionValidator;
        private readonly PipelineExpander _pipelineExpander;
        private readonly Dictionary<Guid, List<ParsedPipelineStep>> _pipelineCache = new();
        private readonly object _pipelineCacheLock = new();

        public WorkflowService(
            IIOService io,
            ILogger<WorkflowService> logger,
            WorkflowTemplateParser templateParser,
            IFragmentSchemaService fragmentSchemaService,
            IFluidTemplateService fluidService,
            FragmentConditionValidator conditionValidator,
            PipelineExpander pipelineExpander)
        {
            _io = io;
            _logger = logger;
            _templateParser = templateParser;
            _fragmentSchemaService = fragmentSchemaService;
            _fluidService = fluidService;
            _conditionValidator = conditionValidator;
            _pipelineExpander = pipelineExpander;
        }

        #region Workflow Loading

        public List<Workflow> GetWorkflows()
        {
            var templatesPath = Path.Combine(_workflowPath, "Templates");
            
            // Get both .workflow and .liquid files
            var workflowFiles = _io.GetFilesRecursive(
                templatesPath,
                ignorePath: "utils",
                extensionsWhitelist: new() { ".workflow", ".liquid" });

            // Group by relative path (without extension) to handle duplicates across different folders
            var filesByRelativePath = workflowFiles
                .Select(f => new
                {
                    File = f,
                    RelativePath = Path.GetRelativePath(templatesPath, f.FullName)
                        .Replace(f.Extension, "", StringComparison.OrdinalIgnoreCase)
                })
                .GroupBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.File).ToList());

            List<Workflow> workflows = new();

            foreach (var group in filesByRelativePath)
            {
                FileInfo? fileToLoad = null;

                // Prefer .workflow extension, fall back to .liquid
                var workflowFile = group.Value.FirstOrDefault(f => f.Extension == ".workflow");
                var liquidFile = group.Value.FirstOrDefault(f => f.Extension == ".liquid");

                if (workflowFile != null)
                {
                    fileToLoad = workflowFile;
                    _logger.LogDebug("Loading workflow template: {RelativePath}.workflow", group.Key);
                }
                else if (liquidFile != null)
                {
                    fileToLoad = liquidFile;
                    _logger.LogWarning(
                        "Loading legacy .liquid workflow template: {RelativePath}. " +
                        "Consider renaming to .workflow for clarity.",
                        group.Key);
                }

                if (fileToLoad != null)
                {
                    try
                    {
                        var templateText = File.ReadAllText(fileToLoad.FullName);
                        
                        // Use async Fluid parsing to handle template syntax properly
                        var workflow = _templateParser.ParseWorkflowTemplateAsync(templateText).GetAwaiter().GetResult();
                        workflows.Add(workflow);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse workflow template: {FilePath}", fileToLoad.FullName);
                    }
                }
            }

            return workflows;
        }

        /// <inheritdoc />
        public Workflow? GetWorkflowById(Guid workflowId)
        {
            var workflows = GetWorkflows();
            return workflows.FirstOrDefault(w => w.Id == workflowId);
        }

        public (List<Workflow> workflows, ModelBase? suggestedBase, Guid? suggestedId) RefreshWorkflows(
            ModelBase? currentWorkflowBase = null,
            Guid? currentWorkflowId = null)
        {
            ClearSchemaCache();
            ClearPipelineCache();

            try
            {
                var workflows = GetWorkflows();

                if (workflows == null || workflows.Count == 0)
                {
                    _logger.LogWarning("No workflows found on disk");
                    return (new List<Workflow>(), null, null);
                }

                ModelBase? suggestedBase = null;
                Guid? suggestedId = null;

                if (currentWorkflowId.HasValue)
                {
                    var matchById = workflows.FirstOrDefault(w => w.Id == currentWorkflowId.Value);
                    if (matchById != null)
                    {
                        suggestedId = matchById.Id;
                        suggestedBase = matchById.Base;
                        _logger.LogDebug("Preserved workflow selection by ID: {WorkflowId}", suggestedId);
                        return (workflows, suggestedBase, suggestedId);
                    }
                }

                if (currentWorkflowBase.HasValue && currentWorkflowBase.Value != default)
                {
                    var matchByBase = workflows.FirstOrDefault(w => w.Base == currentWorkflowBase.Value);
                    if (matchByBase != null)
                    {
                        suggestedId = matchByBase.Id;
                        suggestedBase = matchByBase.Base;
                        _logger.LogDebug("Preserved workflow selection by base: {WorkflowBase}", suggestedBase);
                        return (workflows, suggestedBase, suggestedId);
                    }
                }

                var firstWorkflow = workflows.FirstOrDefault();
                if (firstWorkflow != null)
                {
                    suggestedId = firstWorkflow.Id;
                    suggestedBase = firstWorkflow.Base;
                    _logger.LogDebug("Using first available workflow: {WorkflowTitle}", firstWorkflow.Title);
                }

                return (workflows, suggestedBase, suggestedId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing workflows from disk");
                return (new List<Workflow>(), null, null);
            }
        }

        public Workflow LoadWorkflowTemplate(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Workflow>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        #endregion

        #region Workflow Composition

        /// <inheritdoc />
        public async Task<string> ComposeWorkflowFromGenerationParametersAsync(Workflow template, GenerationParameters parameters)
        {
            return await ComposeFluidWorkflowAsync(template, parameters);
        }

        /// <summary>
        /// Composes a workflow using the Fluid/PipelineExpander approach.
        /// Handles $foreach, $if, and $compute markers in the pipeline.
        /// </summary>
        private async Task<string> ComposeFluidWorkflowAsync(Workflow template, GenerationParameters parameters)
        {
            var composer = new WorkflowComposer();

            // Build global parameters from GenerationParameters
            var globalParams = parameters.FlattenForTemplateRendering();

            // Inject workflow asset defaults
            InjectAssetDefaults(template, globalParams);

            // Inject sources from GenerationParameters
            InjectSources(parameters, globalParams);

            // Render the template with Fluid to get the JSON structure
            var (renderedJson, _) = await _fluidService.RenderAsync(template.RawJson, globalParams);

            // Parse the rendered JSON
            using var doc = JsonDocument.Parse(renderedJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Pipeline", out var pipelineEl))
                throw new InvalidOperationException("No Pipeline found in rendered template");

            // Expand pipeline using PipelineExpander (handles $foreach, $if, $compute)
            var expandedSteps = _pipelineExpander.ExpandPipeline(pipelineEl, parameters);

            _logger.LogDebug("Expanded pipeline to {Count} steps", expandedSteps.Count);

            // Process each expanded step
            foreach (var step in expandedSteps.OrderBy(s => s.Order))
            {
                try
                {
                    // Check if this fragment should be skipped (inactive optional fragment)
                    if (!string.IsNullOrEmpty(step.Id) && parameters.Fragments.TryGetValue(step.Id, out var fragmentParams))
                    {
                        if (!fragmentParams.IsActive)
                        {
                            _logger.LogDebug("Skipping inactive fragment '{FragmentId}'", step.Id);
                            continue;
                        }
                    }

                    // Build merged parameters for fragment rendering
                    var mergedParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    // Copy global params
                    foreach (var kvp in globalParams)
                    {
                        if (kvp.Value != null)
                            mergedParams[kvp.Key] = kvp.Value;
                    }

                    // Override with expanded step parameters
                    foreach (var kvp in step.Parameters)
                    {
                        if (kvp.Value != null)
                            mergedParams[kvp.Key] = kvp.Value;
                    }

                    // Merge fragment-specific parameters from GenerationParameters.Fragments
                    if (!string.IsNullOrEmpty(step.Id) && parameters.Fragments.TryGetValue(step.Id, out var fragmentParamsForMerge))
                    {
                        if (fragmentParamsForMerge.Values != null && fragmentParamsForMerge.Values.Count > 0)
                        {
                            foreach (var kvp in fragmentParamsForMerge.Values)
                            {
                                if (kvp.Value != null)
                                {
                                    mergedParams[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }

                    var context = new SubgraphContext
                    {
                        Parameters = mergedParams,
                        Outputs = new NodeRegistry()
                    };

                    context.Outputs.Merge(composer.Registry);

                    // Render fragment
                    if (!string.IsNullOrWhiteSpace(step.Fragment))
                    {
                        var fragPath = Path.Combine(_workflowPath, "Fragments", step.Fragment.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(fragPath))
                        {
                            var fragmentText = await File.ReadAllTextAsync(fragPath);
                            var (rendered, outputs) = await RenderFragmentAsync(fragmentText, context, mergedParams);

                            if (string.IsNullOrWhiteSpace(rendered))
                            {
                                _logger.LogDebug("Fragment '{FragmentId}' rendered to empty string", step.Id);
                                continue;
                            }

                            rendered = Regex.Replace(rendered, @",\s*(\}|])", "$1", RegexOptions.Singleline);

                            if (outputs.Count > 0)
                            {
                                var outputKeys = string.Join(", ", outputs.Keys.Select(k => $"'{k}'"));
                                _logger.LogDebug("Fragment '{FragmentId}' declared {Count} outputs: {OutputKeys}",
                                    step.Id, outputs.Count, outputKeys);
                            }

                            foreach (var kvp in outputs)
                                context.Outputs.Register(kvp.Key, kvp.Value.nodeId, kvp.Value.index);

                            composer.AddRenderedFragment(rendered, context.Outputs);
                        }
                        else
                        {
                            _logger.LogWarning("Fragment file not found: {FragmentPath}", fragPath);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to process step '{step.Id}'. JSON error: {ex.Message}.", ex);
                }
            }

            return composer.BuildFinalWorkflow();
        }

        /// <summary>
        /// Injects asset default values into global parameters.
        /// </summary>
        private void InjectAssetDefaults(Workflow template, Dictionary<string, object?> globalParams)
        {
            if (template.Assets == null) return;

            foreach (var asset in template.Assets)
            {
                if (!globalParams.ContainsKey(asset.Parameter) ||
                    globalParams[asset.Parameter] == null ||
                    string.IsNullOrWhiteSpace(globalParams[asset.Parameter]?.ToString()))
                {
                    if (!string.IsNullOrWhiteSpace(asset.DefaultValue))
                    {
                        globalParams[asset.Parameter] = asset.DefaultValue;
                        _logger.LogDebug("Injected Asset default for '{Parameter}': '{Value}'", asset.Parameter, asset.DefaultValue);
                    }
                }
            }
        }

        /// <summary>
        /// Injects source data into global parameters.
        /// </summary>
        private void InjectSources(GenerationParameters parameters, Dictionary<string, object?> globalParams)
        {
            foreach (var source in parameters.Sources)
            {
                if (source.Value?.HasData == true && !string.IsNullOrWhiteSpace(source.Value.Data))
                {
                    globalParams[source.Key] = source.Value.Data;

                    if (source.Key.Equals("source_image", StringComparison.OrdinalIgnoreCase))
                    {
                        globalParams["Image"] = source.Value.Data;
                    }

                    _logger.LogDebug("Injected source '{SourceId}' into globalParams", source.Key);
                }
            }
        }

        #endregion

        #region Fragment Rendering

        /// <summary>
        /// Renders a fragment using the Fluid template engine.
        /// </summary>
        public async Task<(string rendered, Dictionary<string, (string nodeId, int index)> outputs)> RenderFragmentAsync(
            string fragmentText,
            SubgraphContext context,
            Dictionary<string, object> globalParams)
        {
            var outputs = new Dictionary<string, (string nodeId, int index)>();
            Dictionary<string, JsonElement>? conditions = null;

            // Build parameters dictionary for Fluid
            var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // Add context parameters
            if (context.Parameters != null)
            {
                foreach (var kvp in context.Parameters)
                {
                    parameters[kvp.Key] = kvp.Value;
                }
            }

            // Add global params (may override context params)
            foreach (var kvp in globalParams)
            {
                if (kvp.Value != null)
                {
                    parameters[kvp.Key] = kvp.Value;
                }
            }
            
            // Debug log the parameters being passed
            var parameterPreview = string.Join(", ", parameters.Take(10).Select(kvp => $"{kvp.Key}={kvp.Value?.ToString()?.Truncate(30)}"));
            _logger.LogDebug("RenderFragmentAsync: Rendering fragment with {ParamCount} parameters: {Preview}", 
                parameters.Count, parameterPreview);
            
            // Check for node_prefix specifically
            if (parameters.TryGetValue("node_prefix", out var nodePrefixValue))
            {
                _logger.LogDebug("RenderFragmentAsync: node_prefix = '{Value}' (type: {Type})", nodePrefixValue, nodePrefixValue?.GetType().Name);
            }
            else
            {
                _logger.LogWarning("RenderFragmentAsync: node_prefix NOT found in parameters!");
            }

            // Render with Fluid - meta block is automatically captured to side-channel
            var (rendered, metadata) = await _fluidService.RenderAsync(fragmentText, parameters, context.Outputs);

            // Process metadata if captured
            if (!string.IsNullOrEmpty(metadata))
            {
                (outputs, conditions) = ExtractMetadata(metadata);

                // Validate conditions if present
                if (conditions != null && conditions.Count > 0)
                {
                    var fragmentId = GetFragmentIdFromContext(context, globalParams);
                    if (!string.IsNullOrEmpty(fragmentId))
                    {
                        var validationErrors = _conditionValidator.ValidateConditions(fragmentId, conditions);
                        if (validationErrors.Count > 0)
                        {
                            _conditionValidator.LogValidationErrors(fragmentId, validationErrors);
                        }
                    }
                }

                // Evaluate conditions - if not met, return empty
                if (!EvaluateConditions(conditions, globalParams))
                {
                    return (string.Empty, outputs);
                }
            }

            // Clean up trailing commas in JSON
            rendered = Regex.Replace(rendered, @",\s*(\}|])", "$1", RegexOptions.Singleline);

            return (rendered, outputs);
        }

        /// <summary>
        /// Attempts to determine the fragment ID from context for validation.
        /// </summary>
        private string? GetFragmentIdFromContext(SubgraphContext context, Dictionary<string, object> globalParams)
        {
            // Try to get from scope parameter
            if (context.Parameters.TryGetValue("scope", out var scopeObj) && scopeObj is string scope)
            {
                return scope.TrimEnd('_');
            }

            // Try to get from fragment_id parameter
            if (globalParams.TryGetValue("fragment_id", out var fragmentIdObj) && fragmentIdObj is string fragmentId)
            {
                return fragmentId;
            }

            return null;
        }

        #endregion

        #region Condition Evaluation

        private (Dictionary<string, (string nodeId, int index)> outputs, Dictionary<string, JsonElement> conditions) ExtractMetadata(string renderedMeta)
        {
            var outputs = new Dictionary<string, (string, int)>();
            var conditions = new Dictionary<string, JsonElement>();

            renderedMeta = Regex.Replace(renderedMeta, @",\s*(\}|])", "$1", RegexOptions.Singleline);

            try
            {
                using var doc = JsonDocument.Parse(renderedMeta);
                var root = doc.RootElement;

                if (root.TryGetProperty("outputs", out var outputsEl) && outputsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in outputsEl.EnumerateObject())
                    {
                        var node = string.Empty;
                        var idx = 0;

                        if (prop.Value.TryGetProperty("node", out var nodeEl))
                        {
                            node = nodeEl.ValueKind == JsonValueKind.String
                                ? nodeEl.GetString() ?? string.Empty
                                : nodeEl.GetRawText()?.Trim().Trim('"') ?? string.Empty;
                        }

                        if (prop.Value.TryGetProperty("index", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number)
                            _ = idxEl.TryGetInt32(out idx);

                        outputs[prop.Name] = (node, idx);
                    }
                }
                else
                {
                    _logger.LogWarning("ExtractMetadata: No 'outputs' property found in rendered meta. Meta JSON: {RenderedMeta}", 
                        renderedMeta.Length > 500 ? renderedMeta.Substring(0, 500) + "..." : renderedMeta);
                }

                if (root.TryGetProperty("conditions", out var conditionsEl) && conditionsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in conditionsEl.EnumerateObject())
                        conditions[prop.Name] = prop.Value.Clone();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "ExtractMetadata: Failed to parse rendered meta as JSON. Meta text: {RenderedMeta}", 
                    renderedMeta.Length > 500 ? renderedMeta.Substring(0, 500) + "..." : renderedMeta);
            }

            return (outputs, conditions);
        }

        private bool EvaluateConditions(Dictionary<string, JsonElement>? conditions, Dictionary<string, object> parameters)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            if (conditions.TryGetValue("required", out var requiredEl) && requiredEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var condition in requiredEl.EnumerateArray())
                {
                    if (condition.ValueKind == JsonValueKind.String)
                    {
                        var conditionPath = condition.GetString();
                        var result = EvaluateCondition(conditionPath, parameters);

                        if (!result)
                        {
                            _logger.LogDebug("Fragment excluded: required condition '{ConditionPath}' evaluated to false", conditionPath);
                            return false;
                        }
                        else
                        {
                            _logger.LogDebug("Required condition '{ConditionPath}' satisfied", conditionPath);
                        }
                    }
                }
            }

            if (conditions.TryGetValue("excluded_if", out var excludedEl) && excludedEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var condition in excludedEl.EnumerateArray())
                {
                    if (condition.ValueKind == JsonValueKind.String)
                    {
                        var conditionPath = condition.GetString();
                        var result = EvaluateCondition(conditionPath, parameters);

                        if (result)
                        {
                            _logger.LogDebug("Fragment excluded: excluded_if condition '{ConditionPath}' evaluated to true", conditionPath);
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool EvaluateCondition(string? conditionPath, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(conditionPath))
                return false;

            var parts = conditionPath.Split('.');
            object current = parameters;

            foreach (var part in parts)
            {
                if (current is IDictionary<string, object> dict)
                {
                    // Case-insensitive key lookup
                    var match = dict.Keys.FirstOrDefault(k => k.Equals(part, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        current = dict[match];
                    }
                    else
                    {
                        // Try to find the key with different casing conventions if direct match fails
                        var snakeCase = ToSnakeCase(part);
                        var pascalCase = ToPascalCase(part);
                        var camelCase = ToCamelCase(part);

                        if (dict.TryGetValue(snakeCase, out var snakeVal)) current = snakeVal;
                        else if (dict.TryGetValue(pascalCase, out var pascalVal)) current = pascalVal;
                        else if (dict.TryGetValue(camelCase, out var camelVal)) current = camelVal;
                        else return false;
                    }
                }
                else if (current != null)
                {
                    // Try reflection for object properties (case-insensitive)
                    var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null)
                        return false;
                    current = prop.GetValue(current)!;
                }
                else
                {
                    return false;
                }
            }

            return current is bool boolValue && boolValue;
        }

        private static string ToSnakeCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return string.Concat(text.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
        }

        private static string ToPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Contains('_'))
            {
                return string.Concat(text.Split('_').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1).ToLower()));
            }
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static string ToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var pascal = ToPascalCase(text);
            return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        }

        #endregion

        #region Asset Defaults Saving

        public bool SaveAssetDefaults(Workflow workflow, Dictionary<string, string> assetValues, List<Workflow>? allWorkflows = null)
        {
            if (workflow == null || assetValues == null || assetValues.Count == 0)
                return false;

            var validAssetParams = workflow.Assets?.Select(a => a.Parameter).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (validAssetParams == null || validAssetParams.Count == 0)
                return false;

            try
            {
                var templatePath = FindWorkflowTemplatePath(workflow);
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                    return false;

                var templateText = File.ReadAllText(templatePath);
                var updatedText = templateText;

                var workflowToUpdate = allWorkflows?.FirstOrDefault(w => w.Id == workflow.Id) ?? workflow;

                foreach (var kvp in assetValues)
                {
                    if (!validAssetParams.Contains(kvp.Key))
                    {
                        _logger.LogDebug("Skipping asset '{AssetKey}' - not defined in workflow", kvp.Key);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(kvp.Value))
                        continue;

                    var escapedParam = Regex.Escape(kvp.Key);
                    var escapedValue = EscapeJsonString(kvp.Value);

                    var assetBlockPattern = @"\{[^{}]*""parameter""\s*:\s*""" + escapedParam + @"""[^{}]*\}";

                    updatedText = Regex.Replace(updatedText, assetBlockPattern, match =>
                    {
                        var assetBlock = match.Value;
                        var defaultPattern = @"""default""\s*:\s*""[^""]*""";
                        var newDefault = $@"""default"": ""{escapedValue}""";
                        return Regex.Replace(assetBlock, defaultPattern, newDefault, RegexOptions.IgnoreCase);
                    }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    var asset = workflowToUpdate.Assets?.FirstOrDefault(a =>
                        a.Parameter.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
                    if (asset != null)
                    {
                        asset.DefaultValue = kvp.Value;
                        _logger.LogDebug("Updated in-memory default for '{Parameter}': '{Value}' (workflow: {WorkflowTitle})",
                            kvp.Key, kvp.Value, workflowToUpdate.Title);
                    }
                }

                if (updatedText != templateText)
                {
                    File.WriteAllText(templatePath, updatedText);
                    _logger.LogDebug("Saved asset defaults to: {TemplatePath}", templatePath);
                    return true;
                }

                _logger.LogDebug("No changes made to asset defaults");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving asset defaults for workflow '{WorkflowTitle}'", workflow.Title);
                return false;
            }
        }

        private string? FindWorkflowTemplatePath(Workflow workflow)
        {
            var templatesPath = Path.Combine(_workflowPath, "Templates");
            var workflowFiles = _io.GetFilesRecursive(
                templatesPath,
                ignorePath: "utils",
                extensionsWhitelist: new() { ".workflow", ".liquid" });

            foreach (var file in workflowFiles)
            {
                try
                {
                    var templateText = File.ReadAllText(file.FullName);
                    
                    // Use async Fluid parsing
                    var parsedWorkflow = _templateParser.ParseWorkflowTemplateAsync(templateText).GetAwaiter().GetResult();

                    if (parsedWorkflow.Title == workflow.Title &&
                        parsedWorkflow.Base == workflow.Base &&
                        parsedWorkflow.Mode == workflow.Mode)
                    {
                        return file.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse workflow template: {FilePath}", file.FullName);
                    continue;
                }
            }

            return null;
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        #endregion

        #region Pipeline Cache & Schema Delegation

        public List<ParsedPipelineStep> GetPipelineSteps(Workflow workflow)
        {
            if (workflow == null || string.IsNullOrEmpty(workflow.RawJson))
                return new List<ParsedPipelineStep>();

            lock (_pipelineCacheLock)
            {
                if (_pipelineCache.TryGetValue(workflow.Id, out var cached))
                {
                    _logger.LogTrace("Returning cached pipeline steps for workflow {WorkflowId}", workflow.Id);
                    return cached;
                }
            }

            var steps = _templateParser.ParsePipelineSteps(workflow.RawJson);

            lock (_pipelineCacheLock)
            {
                _pipelineCache[workflow.Id] = steps;
            }

            _logger.LogDebug("Cached {Count} pipeline steps for workflow {WorkflowId}", steps.Count, workflow.Id);
            return steps;
        }

        public List<ParsedPipelineStep> ParsePipelineSteps(string rawJson)
            => _templateParser.ParsePipelineSteps(rawJson);

        public void ClearPipelineCache()
        {
            lock (_pipelineCacheLock)
            {
                _pipelineCache.Clear();
            }
            _logger.LogDebug("Pipeline cache cleared");
        }

        public FragmentSchema? ParseFragmentSchema(string fragmentText)
            => _fragmentSchemaService.ParseFragmentSchema(fragmentText);

        public FragmentSchema? GetFragmentSchema(string fragmentFile)
            => _fragmentSchemaService.GetFragmentSchema(fragmentFile);

        public Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow)
            => _fragmentSchemaService.GetWorkflowFragmentSchemas(workflow);

        public void ClearSchemaCache()
            => _fragmentSchemaService.ClearCache();

        #endregion
    }

    /// <summary>
    /// Composes workflow fragments into a final workflow JSON.
    /// </summary>
    public class WorkflowComposer
    {
        private readonly List<string> _renderedFragments = new();
        private readonly NodeRegistry _globalRegistry = new();

        public void AddRenderedFragment(string rendered, NodeRegistry outputs)
        {
            _renderedFragments.Add(rendered);
            _globalRegistry.Merge(outputs);
        }

        public string BuildFinalWorkflow()
        {
            if (_renderedFragments.Count == 0)
                return "{}";

            var mergedNodes = new Dictionary<string, JsonElement>();

            foreach (var fragment in _renderedFragments)
            {
                try
                {
                    using var doc = JsonDocument.Parse(fragment);
                    var root = doc.RootElement;

                    foreach (var prop in root.EnumerateObject())
                        mergedNodes[prop.Name] = prop.Value.Clone();
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to parse fragment as JSON: {fragment}", ex);
                }
            }

            return JsonSerializer.Serialize(mergedNodes, new JsonSerializerOptions { WriteIndented = false });
        }

        public NodeRegistry Registry => _globalRegistry;
    }
}
