using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Scriban;
using Scriban.Runtime;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for loading, composing, and managing workflow templates.
    /// Delegates parsing to WorkflowTemplateParser and FragmentSchemaService.
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        private readonly string _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        private readonly IIOService _io;
        private readonly ILogger<WorkflowService> _logger;
        private readonly WorkflowTemplateParser _templateParser;
        private readonly IFragmentSchemaService _fragmentSchemaService;
        private readonly Dictionary<Guid, List<ParsedPipelineStep>> _pipelineCache = new();
        private readonly object _pipelineCacheLock = new();

        public WorkflowService(
            IIOService io,
            ILogger<WorkflowService> logger,
            WorkflowTemplateParser templateParser,
            IFragmentSchemaService fragmentSchemaService)
        {
            _io = io;
            _logger = logger;
            _templateParser = templateParser;
            _fragmentSchemaService = fragmentSchemaService;
        }

        #region Workflow Loading

        public List<Workflow> GetWorkflows()
        {
            var workflowFiles = _io.GetFilesRecursive(Path.Combine(_workflowPath, "Templates"), ignorePath: "utils", extensionsWhitelist: new() { ".sbn" });
            List<Workflow> workflows = new();

            foreach (var filePath in workflowFiles)
            {
                var templateText = File.ReadAllText(filePath.FullName);
                var workflow = _templateParser.ParseWorkflowTemplate(templateText);
                workflows.Add(workflow);
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
        public string ComposeWorkflowFromGenerationParameters(Workflow template, GenerationParameters parameters)
        {
            var composer = new WorkflowComposer();
            
            // Build global parameters from GenerationParameters
            var globalParams = parameters.FlattenForTemplateRendering();
            
            // Inject any remaining workflow asset defaults that aren't in parameters
            if (template.Assets != null)
            {
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

            // Inject sources from GenerationParameters
            foreach (var source in parameters.Sources)
            {
                if (source.Value?.HasData == true && !string.IsNullOrWhiteSpace(source.Value.Data))
                {
                    // Use source ID as parameter name, also add common aliases
                    globalParams[source.Key] = source.Value.Data;
                    
                    // Add "Image" alias for the first source_image
                    if (source.Key.Equals("source_image", StringComparison.OrdinalIgnoreCase))
                    {
                        globalParams["Image"] = source.Value.Data;
                    }
                    
                    _logger.LogDebug("Injected source '{SourceId}' into globalParams", source.Key);
                }
            }

            var templateContext = new TemplateContext
            {
                MemberRenamer = member => member.Name,
                MemberFilter = member => true,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedFunctionAccess = true,
                EnableRelaxedTargetAccess = true,
                StrictVariables = false
            };

            var scriptObject = new ScriptObject();
            foreach (var kvp in globalParams)
                scriptObject[kvp.Key] = kvp.Value;

            scriptObject.Import("json", new Func<object, string>(value =>
            {
                if (value == null) return "null";
                if (value is string str) return JsonSerializer.Serialize(str);
                if (value is bool b) return b ? "true" : "false";
                if (value is int || value is long || value is double || value is float || value is decimal)
                    return value.ToString()!;
                return JsonSerializer.Serialize(value);
            }));

            templateContext.PushGlobal(scriptObject);

            var fullTemplate = Template.Parse(template.RawJson);
            var renderedTemplate = fullTemplate.Render(templateContext);

            using var doc = JsonDocument.Parse(renderedTemplate);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Pipeline", out var pipelineEl))
                throw new InvalidOperationException("No Pipeline found in rendered template");

            foreach (var stepEl in pipelineEl.EnumerateArray())
            {
                try
                {
                    if (!stepEl.TryGetProperty("fragment", out var fragmentEl))
                        continue;

                    var fragmentName = fragmentEl.GetString();
                    
                    // Check if this fragment should be skipped (inactive optional fragment)
                    var fragmentId = GetFragmentIdFromStep(stepEl, fragmentName);
                    if (!string.IsNullOrEmpty(fragmentId) && parameters.Fragments.TryGetValue(fragmentId, out var fragmentParams))
                    {
                        if (!fragmentParams.IsActive)
                        {
                            _logger.LogDebug("Skipping inactive fragment '{FragmentId}'", fragmentId);
                            continue;
                        }
                    }
                    
                    var mergedParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    
                    // Copy global params
                    foreach (var kvp in globalParams)
                    {
                        if (kvp.Value != null)
                            mergedParams[kvp.Key] = kvp.Value;
                    }

                    // Override with step parameters from rendered template
                    if (stepEl.TryGetProperty("parameters", out var paramsEl))
                    {
                        foreach (var prop in paramsEl.EnumerateObject())
                        {
                            object value = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString()!,
                                JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal)
                                    ? intVal
                                    : (prop.Value.TryGetInt64(out var longVal)
                                        ? (object)longVal
                                        : prop.Value.GetDouble()),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null!,
                                _ => prop.Value.GetRawText()
                            };
                            mergedParams[prop.Name] = value;
                        }
                    }

                    var context = new SubgraphContext
                    {
                        Parameters = mergedParams,
                        Outputs = new NodeRegistry()
                    };

                    context.Outputs.Merge(composer.Registry);

                    if (!string.IsNullOrWhiteSpace(fragmentName))
                    {
                        var fragPath = Path.Combine(_workflowPath, "Fragments", fragmentName.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(fragPath))
                        {
                            var fragmentText = File.ReadAllText(fragPath);
                            var (rendered, outputs) = RenderFragment(fragmentText, context, mergedParams);

                            if (string.IsNullOrWhiteSpace(rendered))
                                continue;

                            rendered = Regex.Replace(rendered, @",\s*(\}|])", "$1", RegexOptions.Singleline);

                            foreach (var kvp in outputs)
                                context.Outputs.Register(kvp.Key, kvp.Value.nodeId, kvp.Value.index);

                            composer.AddRenderedFragment(rendered, context.Outputs);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to parse step parameters. JSON error: {ex.Message}.", ex);
                }
            }

            return composer.BuildFinalWorkflow();
        }

        /// <summary>
        /// Extracts the fragment ID from a pipeline step.
        /// </summary>
        private static string? GetFragmentIdFromStep(JsonElement stepEl, string? fragmentName)
        {
            // First try to get explicit ID
            if (stepEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
            {
                return idEl.GetString();
            }
            
            // Fall back to generating ID from fragment filename
            if (!string.IsNullOrEmpty(fragmentName))
            {
                return Path.GetFileNameWithoutExtension(fragmentName).Replace("-", "_");
            }
            
            return null;
        }

        #endregion

        #region Fragment Rendering

        public (string rendered, Dictionary<string, (string nodeId, int index)> outputs) RenderFragment(
            string fragmentText,
            SubgraphContext context,
            Dictionary<string, object> globalParams,
            Func<string, Task<string>>? loraPathResolver = null)
        {
            Dictionary<string, (string, int)> outputs = new();
            Dictionary<string, JsonElement>? conditions = null;

            // Match #meta ... #end block - use [\s\S] to match any character including newlines
            // The pattern captures everything between #meta and #end
            var metaMatch = Regex.Match(fragmentText, @"#meta\s*([\s\S]*?)\s*#end");
            
            if (metaMatch.Success)
            {
                var metaJson = metaMatch.Groups[1].Value.Trim();

                string renderedMeta;
                try
                {
                    renderedMeta = RenderTemplate(metaJson, context, preserveFormatting: true, loraPathResolver);
                }
                catch
                {
                    renderedMeta = metaJson;
                }

                (outputs, conditions) = ExtractMetadata(renderedMeta);

                if (!EvaluateConditions(conditions, globalParams))
                    return (string.Empty, outputs);

                fragmentText = fragmentText.Replace(metaMatch.Value, "").Trim();
            }

            var rendered = RenderTemplate(fragmentText, context, preserveFormatting: false, loraPathResolver);
            rendered = Regex.Replace(rendered, @",\s*(\}|])", "$1", RegexOptions.Singleline);
            return (rendered, outputs);
        }

        private string RenderTemplate(string templateText, SubgraphContext context, bool preserveFormatting, Func<string, Task<string>>? loraPathResolver = null)
        {
            var template = Template.Parse(templateText);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                if (!preserveFormatting)
                    throw new InvalidOperationException($"Template parse errors: {errors}");
            }

            var templateContext = new TemplateContext
            {
                MemberRenamer = member => member.Name,
                MemberFilter = member => true,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedFunctionAccess = true,
                EnableRelaxedTargetAccess = true,
                StrictVariables = false
            };

            var scriptObject = new ScriptObject();

            if (context.Parameters != null)
            {
                foreach (var kvp in context.Parameters)
                {
                    scriptObject[kvp.Key] = kvp.Value;

                    var alternateKey = ConvertCasing(kvp.Key);
                    if (alternateKey != kvp.Key)
                        scriptObject[alternateKey] = kvp.Value;
                }
            }

            scriptObject.Import("get_ref", new Func<string, string>(key => context.Outputs.GetReference(key)));

            scriptObject.Import("json", new Func<object, string>(value =>
            {
                if (value == null) return "null";
                if (value is string str) return JsonSerializer.Serialize(str);
                if (value is bool b) return b ? "true" : "false";
                if (value is int || value is long || value is double || value is float || value is decimal)
                    return value.ToString()!;
                return JsonSerializer.Serialize(value);
            }));

            if (loraPathResolver != null)
            {
                scriptObject.Import("resolve_lora_path", new Func<string, object>(loraName =>
                {
                    if (string.IsNullOrWhiteSpace(loraName))
                        return loraName;

                    var task = loraPathResolver(loraName);
                    task.Wait();
                    return task.Result;
                }));
            }
            else
            {
                scriptObject.Import("resolve_lora_path", new Func<string, string>(loraName => loraName));
            }

            templateContext.PushGlobal(scriptObject);

            var rendered = template.Render(templateContext);

            if (!preserveFormatting)
            {
                rendered = rendered.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                rendered = Regex.Replace(rendered, @"\s+", " ");
                rendered = Regex.Replace(rendered, @"\s*([{}[\]:,])\s*", "$1");
                rendered = rendered.Trim();
            }

            return rendered;
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

                if (root.TryGetProperty("conditions", out var conditionsEl) && conditionsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in conditionsEl.EnumerateObject())
                        conditions[prop.Name] = prop.Value.Clone();
                }
            }
            catch { }

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
                        if (!EvaluateCondition(conditionPath, parameters))
                            return false;
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
                        if (EvaluateCondition(conditionPath, parameters))
                            return false;
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
            var workflowFiles = _io.GetFilesRecursive(templatesPath, ignorePath: "utils", extensionsWhitelist: new() { ".sbn" });

            foreach (var file in workflowFiles)
            {
                var templateText = File.ReadAllText(file.FullName);
                var parsedWorkflow = _templateParser.ParseWorkflowTemplate(templateText);

                if (parsedWorkflow.Title == workflow.Title &&
                    parsedWorkflow.Base == workflow.Base &&
                    parsedWorkflow.Mode == workflow.Mode)
                {
                    return file.FullName;
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

        // Delegate to FragmentSchemaService
        public FragmentSchema? ParseFragmentSchema(string fragmentText)
            => _fragmentSchemaService.ParseFragmentSchema(fragmentText);

        public FragmentSchema? GetFragmentSchema(string fragmentFile)
            => _fragmentSchemaService.GetFragmentSchema(fragmentFile);

        public Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow)
            => _fragmentSchemaService.GetWorkflowFragmentSchemas(workflow);

        public Dictionary<string, object?> ParseFragmentDefaults(string fragmentFile)
            => _fragmentSchemaService.ParseFragmentDefaults(fragmentFile);

        public void ClearSchemaCache()
            => _fragmentSchemaService.ClearCache();

        #endregion

        #region Utilities

        private static string ConvertCasing(string key)
        {
            if (key.Contains('_'))
            {
                return string.Concat(key.Split('_').Select(part =>
                    char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1) : "")));
            }
            else if (char.IsUpper(key[0]))
            {
                return string.Concat(key.Select((c, i) =>
                    i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
            }
            return key;
        }

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
