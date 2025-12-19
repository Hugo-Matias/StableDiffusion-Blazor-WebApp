using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
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
    public class WorkflowService : IWorkflowService
    {
        private readonly string _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        private readonly IIOService _io;
        private readonly ILogger<WorkflowService> _logger;
        private readonly Dictionary<string, FragmentSchema?> _schemaCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, List<ParsedPipelineStep>> _pipelineCache = new();
        private readonly object _schemaCacheLock = new();
        private readonly object _pipelineCacheLock = new();

        public WorkflowService(IIOService io, ILogger<WorkflowService> logger)
        {
            _io = io;
            _logger = logger;
        }

        public List<Workflow> GetWorkflows()
        {
            var workflowFiles = _io.GetFilesRecursive(Path.Combine(_workflowPath, "Templates"), ignorePath: "utils", extensionsWhitelist: new() { ".sbn" });
            List<Workflow> workflows = new();

            foreach (var filePath in workflowFiles)
            {
                var templateText = File.ReadAllText(filePath.FullName);
                var workflow = ParseWorkflowTemplate(templateText);
                workflows.Add(workflow);
            }

            return workflows;
        }

        /// <summary>
        /// Refreshes workflows from disk template files and attempts to preserve the current selection.
        /// This ensures that any changes to workflow templates are picked up.
        /// Also clears schema and pipeline caches to ensure fresh data is loaded.
        /// </summary>
        /// <param name="currentWorkflowBase">The currently selected workflow base (to preserve selection)</param>
        /// <param name="currentWorkflowId">The currently selected workflow ID (to preserve selection)</param>
        /// <returns>A tuple containing: (workflows list, suggested workflow base, suggested workflow ID)</returns>
        public (List<Workflow> workflows, ModelBase? suggestedBase, Guid? suggestedId) RefreshWorkflows(
            ModelBase? currentWorkflowBase = null,
            Guid? currentWorkflowId = null)
        {
            // Clear caches when refreshing workflows to pick up any template changes
            ClearSchemaCache();
            ClearPipelineCache();

            try
            {
                // Load workflows from disk
                var workflows = GetWorkflows();

                if (workflows == null || workflows.Count == 0)
                {
                    _logger.LogWarning("No workflows found on disk");
                    return (new List<Workflow>(), null, null);
                }

                ModelBase? suggestedBase = null;
                Guid? suggestedId = null;

                // Priority 1: Try to restore by ID (most specific)
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

                // Priority 2: Fallback to matching by base
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

                // Priority 3: Use first available workflow
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

        private Workflow ParseWorkflowTemplate(string templateText)
        {
            var wf = new Workflow
            {
                RawJson = templateText,
            };

            var titleMatch = Regex.Match(templateText, @"""title""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
                wf.Title = titleMatch.Groups[1].Value;

            var baseMatch = Regex.Match(templateText, @"""base""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (baseMatch.Success && Enum.TryParse<ModelBase>(baseMatch.Groups[1].Value, true, out var mb))
                wf.Base = mb;

            var modeMatch = Regex.Match(templateText, @"""mode""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (modeMatch.Success && Enum.TryParse<ModeType>(modeMatch.Groups[1].Value, true, out var mt))
                wf.Mode = mt;

            // Parse Assets from workflow template
            wf.Assets = ParseAssetsFromTemplate(templateText);

            // Parse Sources from workflow template
            wf.Sources = ParseSourcesFromTemplate(templateText);

            // Generate a unique ID based on available properties
            var uniqueString = $"{wf.Title}_{wf.Base}_{wf.Mode}";
            wf.Id = GenerateDeterministicGuid(uniqueString);

            // Parse Pipeline from workflow template
            wf.Pipeline = ParsePipelineFromTemplate(templateText);

            return wf;
        }

        /// <summary>
        /// Parses Pipeline steps from the workflow template using regex.
        /// Since templates contain Scriban syntax, we can't use JSON parsing directly.
        /// </summary>
        private List<WorkflowStep>? ParsePipelineFromTemplate(string templateText)
        {
            var steps = ParsePipelineStepsFromRawJson(templateText);
            if (steps.Count == 0)
                return null;

            return steps.Select(s => new WorkflowStep
            {
                Id = s.Id,
                Fragment = s.Fragment
            }).ToList();
        }

        /// <summary>
        /// Parses the Sources array from a workflow template.
        /// Sources define input images/videos required by the workflow.
        /// </summary>
        private List<WorkflowSource>? ParseSourcesFromTemplate(string templateText)
        {
            // Look for "Sources": [...] in the template
            var sourcesMatch = Regex.Match(templateText, @"""Sources""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!sourcesMatch.Success)
            {
                _logger.LogTrace("No 'Sources' array found in template");
                return null;
            }

            var sourcesArrayContent = sourcesMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(sourcesArrayContent))
            {
                _logger.LogTrace("'Sources' array is empty in template");
                return null;
            }

            _logger.LogDebug("Found Sources array content: {Content}", sourcesArrayContent.Substring(0, Math.Min(100, sourcesArrayContent.Length)));

            var sources = new List<WorkflowSource>();

            // Parse each source object in the array
            var sourceMatches = Regex.Matches(sourcesArrayContent, @"\{([^{}]*)\}", RegexOptions.Singleline);

            foreach (Match sourceMatch in sourceMatches)
            {
                var sourceContent = sourceMatch.Groups[1].Value;
                var source = ParseSingleSource(sourceContent);
                if (source != null)
                {
                    sources.Add(source);
                    _logger.LogDebug("Parsed source: id='{Id}', label='{Label}', type='{Type}'", source.Id, source.Label, source.Type);
                }
            }

            _logger.LogDebug("Parsed {Count} sources from template", sources.Count);
            return sources.Count > 0 ? sources : null;
        }

        /// <summary>
        /// Parses a single source definition from JSON-like content.
        /// </summary>
        private WorkflowSource? ParseSingleSource(string sourceContent)
        {
            var source = new WorkflowSource();

            // Parse id
            var idMatch = Regex.Match(sourceContent, @"""id""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (idMatch.Success)
                source.Id = idMatch.Groups[1].Value;
            else
                return null; // ID is required

            // Parse label
            var labelMatch = Regex.Match(sourceContent, @"""label""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (labelMatch.Success)
                source.Label = labelMatch.Groups[1].Value;
            else
                source.Label = source.Id; // Default to ID

            // Parse type
            var typeMatch = Regex.Match(sourceContent, @"""type""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
                source.Type = typeMatch.Groups[1].Value;
            else
                source.Type = "image"; // Default to image

            // Parse required
            var requiredMatch = Regex.Match(sourceContent, @"""required""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
            if (requiredMatch.Success)
                source.Required = requiredMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            else
                source.Required = true; // Default to required

            // Parse parameter
            var paramMatch = Regex.Match(sourceContent, @"""parameter""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (paramMatch.Success)
                source.Parameter = paramMatch.Groups[1].Value;

            return source;
        }

        /// <summary>
        /// Parses the Assets array from a workflow template.
        /// Assets define the models/resources required by the workflow.
        /// </summary>
        private List<WorkflowAsset>? ParseAssetsFromTemplate(string templateText)
        {
            // Look for "Assets": [...] in the template
            var assetsMatch = Regex.Match(templateText, @"""Assets""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!assetsMatch.Success)
                return null;

            var assetsArrayContent = assetsMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(assetsArrayContent))
                return null;

            var assets = new List<WorkflowAsset>();

            // Parse each asset object in the array
            var assetMatches = Regex.Matches(assetsArrayContent, @"\{([^{}]*)\}", RegexOptions.Singleline);

            foreach (Match assetMatch in assetMatches)
            {
                var assetContent = assetMatch.Groups[1].Value;
                var asset = ParseSingleAsset(assetContent);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets.Count > 0 ? assets : null;
        }

        /// <summary>
        /// Parses a single asset definition from JSON-like content.
        /// </summary>
        private WorkflowAsset? ParseSingleAsset(string assetContent)
        {
            var asset = new WorkflowAsset();

            // Parse parameter
            var paramMatch = Regex.Match(assetContent, @"""parameter""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (paramMatch.Success)
                asset.Parameter = paramMatch.Groups[1].Value;
            else
                return null; // Parameter is required

            // Parse label
            var labelMatch = Regex.Match(assetContent, @"""label""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (labelMatch.Success)
                asset.Label = labelMatch.Groups[1].Value;
            else
                asset.Label = asset.Parameter; // Default to parameter name

            // Parse type
            var typeMatch = Regex.Match(assetContent, @"""type""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (typeMatch.Success && Enum.TryParse<AssetType>(typeMatch.Groups[1].Value, true, out var assetType))
                asset.Type = assetType;
            else
                return null; // Type is required

            // Parse default value
            var defaultMatch = Regex.Match(assetContent, @"""default""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (defaultMatch.Success)
                asset.DefaultValue = defaultMatch.Groups[1].Value;

            // Parse order
            var orderMatch = Regex.Match(assetContent, @"""order""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (orderMatch.Success && int.TryParse(orderMatch.Groups[1].Value, out var order))
                asset.Order = order;

            // Parse column size
            var columnMatch = Regex.Match(assetContent, @"""columnSize""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (columnMatch.Success && int.TryParse(columnMatch.Groups[1].Value, out var columnSize))
                asset.ColumnSize = columnSize;

            return asset;
        }

        private Guid GenerateDeterministicGuid(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
        }

        private (Dictionary<string, (string nodeId, int index)> outputs, Dictionary<string, JsonElement> conditions) ExtractMetadata(string renderedMeta)
        {
            var outputs = new Dictionary<string, (string, int)>();
            var conditions = new Dictionary<string, JsonElement>();

            // Remove trailing commas
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
                            if (nodeEl.ValueKind == JsonValueKind.String)
                                node = nodeEl.GetString() ?? string.Empty;
                            else
                                node = nodeEl.GetRawText()?.Trim().Trim('"') ?? string.Empty;
                        }

                        if (prop.Value.TryGetProperty("index", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number)
                            _ = idxEl.TryGetInt32(out idx);

                        outputs[prop.Name] = (node, idx);
                    }
                }

                if (root.TryGetProperty("conditions", out var conditionsEl) && conditionsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in conditionsEl.EnumerateObject())
                    {
                        conditions[prop.Name] = prop.Value.Clone();
                    }
                }
            }
            catch
            {
                // Tolerant: return empty if parsing fails
            }

            return (outputs, conditions);
        }

        private bool EvaluateConditions(Dictionary<string, JsonElement> conditions, Dictionary<string, object> parameters)
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

        private bool EvaluateCondition(string conditionPath, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(conditionPath))
                return false;

            var parts = conditionPath.Split('.');
            object current = parameters;

            foreach (var part in parts)
            {
                if (current is IDictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                    {
                        // Try case-insensitive lookup for dictionaries that don't have a case-insensitive comparer
                        var match = dict.Keys.FirstOrDefault(k => k.Equals(part, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            current = dict[match];
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else if (current != null)
                {
                    var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null)
                    {
                        return false;
                    }
                    current = prop.GetValue(current);
                }
                else
                {
                    return false;
                }
            }

            if (current is bool boolValue)
            {
                return boolValue;
            }

            return false;
        }

        // Core rendering method: handles both meta and body
        public (string rendered, Dictionary<string, (string nodeId, int index)> outputs) RenderFragment(string fragmentText, SubgraphContext context, Dictionary<string, object> globalParams, Func<string, Task<string>>? loraPathResolver = null)
        {
            Dictionary<string, (string, int)> outputs = new();
            Dictionary<string, JsonElement> conditions = null;

            // Extract and render #meta block if present
            var metaMatch = Regex.Match(fragmentText, @"#meta\s*(\{.*?\})\s*#end", RegexOptions.Singleline);
            if (metaMatch.Success)
            {
                var metaJson = metaMatch.Groups[1].Value;

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
                {
                    return (string.Empty, outputs);
                }

                // Remove meta block before rendering fragment body
                fragmentText = fragmentText.Replace(metaMatch.Value, "").Trim();
            }

            // Render fragment body (collapsed for workflow JSON)
            var rendered = RenderTemplate(fragmentText, context, preserveFormatting: false, loraPathResolver);
            // Remove trailing commas from rendered fragment
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
                    {
                        scriptObject[alternateKey] = kvp.Value;
                    }
                }
            }

            scriptObject.Import("get_ref", new Func<string, string>(key => context.Outputs.GetReference(key)));

            scriptObject.Import("json", new Func<object, string>(value =>
            {
                if (value == null) return "null";
                if (value is string str) return System.Text.Json.JsonSerializer.Serialize(str);
                if (value is bool b) return b ? "true" : "false";
                if (value is int || value is long || value is double || value is float || value is decimal)
                    return value.ToString();
                return System.Text.Json.JsonSerializer.Serialize(value);
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
                // Remove all line breaks and extra whitespace
                rendered = rendered.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                // Collapse multiple spaces
                rendered = Regex.Replace(rendered, @"\s+", " ");
                // Remove spaces around JSON structural characters
                rendered = Regex.Replace(rendered, @"\s*([{}[\]:,])\s*", "$1");
                rendered = rendered.Trim();
            }

            return rendered;
        }

        public string ComposeWorkflowFromTemplate(Workflow template, Txt2ImgComfyUI param)
        {
            return ComposeWorkflowFromTemplateInternal(template, param);
        }

        public string ComposeWorkflowFromTemplate(Workflow template, Img2ImgComfyUI param)
        {
            return ComposeWorkflowFromTemplateInternal(template, param);
        }

        public string ComposeWorkflowFromTemplate(Workflow template, Img2VidComfyUI param)
        {
            return ComposeWorkflowFromTemplateInternal(template, param);
        }

        private string ComposeWorkflowFromTemplateInternal<T>(Workflow template, T param) where T : class
        {
            var composer = new WorkflowComposer();

            var globalParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Use reflection to get all properties from param
            foreach (var prop in param.GetType().GetProperties())
            {
                var value = prop.GetValue(param);

                if (prop.Name.Equals("Loras", StringComparison.OrdinalIgnoreCase) && value is List<Lora> loras)
                {
                    value = loras.Where(l => l.IsEnabled && !l.IsNegative).ToList();
                }

                globalParams[prop.Name] = value;
            }

            // Inject WorkflowAssets directly into global params for template access
            // This allows templates to use {{ HighModel }}, {{ Vae }}, etc. directly
            InjectWorkflowAssets(param, template, globalParams);

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
            {
                scriptObject[kvp.Key] = kvp.Value;
            }

            scriptObject.Import("json", new Func<object, string>(value =>
            {
                if (value == null) return "null";
                if (value is string str) return System.Text.Json.JsonSerializer.Serialize(str);
                if (value is bool b) return b ? "true" : "false";
                if (value is int || value is long || value is double || value is float || value is decimal)
                    return value.ToString();
                return System.Text.Json.JsonSerializer.Serialize(value);
            }));

            templateContext.PushGlobal(scriptObject);

            var fullTemplateText = template.RawJson;
            var fullTemplate = Template.Parse(fullTemplateText);
            var renderedTemplate = fullTemplate.Render(templateContext);

            using var doc = JsonDocument.Parse(renderedTemplate);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Pipeline", out var pipelineEl))
            {
                throw new InvalidOperationException("No Pipeline found in rendered template");
            }

            foreach (var stepEl in pipelineEl.EnumerateArray())
            {
                try
                {
                    if (!stepEl.TryGetProperty("fragment", out var fragmentEl))
                        continue;

                    var fragmentName = fragmentEl.GetString();
                    var mergedParams = new Dictionary<string, object>(globalParams, StringComparer.OrdinalIgnoreCase);

                    if (stepEl.TryGetProperty("parameters", out var paramsEl))
                    {
                        foreach (var prop in paramsEl.EnumerateObject())
                        {
                            object value = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString(),
                                JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal)
                                    ? intVal
                                    : (prop.Value.TryGetInt64(out var longVal)
                                        ? (object)longVal
                                        : prop.Value.GetDouble()),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
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

                            var (rendered, outputs) = RenderFragment(fragmentText, context, globalParams);

                            if (string.IsNullOrWhiteSpace(rendered))
                                continue;

                            rendered = Regex.Replace(rendered, @",\s*(\}|])", "$1", RegexOptions.Singleline);

                            foreach (var kvp in outputs)
                            {
                                context.Outputs.Register(kvp.Key, kvp.Value.nodeId, kvp.Value.index);
                            }

                            composer.AddRenderedFragment(rendered, context.Outputs);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to parse step parameters. " +
                        $"JSON error: {ex.Message}.",
                        ex);
                }
            }

            return composer.BuildFinalWorkflow();
        }

        /// <summary>
        /// Saves the current asset values as defaults in the workflow template file.
        /// Updates the "default" field for each asset in the Assets array.
        /// Also updates the in-memory workflow to reflect the new defaults.
        /// </summary>
        /// <param name="workflow">The workflow whose template should be updated</param>
        /// <param name="assetValues">Dictionary of asset parameter names to their current values</param>
        /// <param name="allWorkflows">Optional: The full list of workflows to update in-memory (typically State.Generation.Workflows)</param>
        /// <returns>True if the file was updated successfully, false otherwise</returns>
        public bool SaveAssetDefaults(Workflow workflow, Dictionary<string, string> assetValues, List<Workflow>? allWorkflows = null)
        {
            if (workflow == null || assetValues == null || assetValues.Count == 0)
                return false;

            // Get the list of valid asset parameters from the workflow
            var validAssetParams = workflow.Assets?.Select(a => a.Parameter).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (validAssetParams == null || validAssetParams.Count == 0)
                return false;

            try
            {
                // Find the template file for this workflow
                var templatePath = FindWorkflowTemplatePath(workflow);
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                    return false;

                var templateText = File.ReadAllText(templatePath);
                var updatedText = templateText;

                // Find the workflow in the allWorkflows list to ensure we update the correct instance
                var workflowToUpdate = allWorkflows?.FirstOrDefault(w => w.Id == workflow.Id) ?? workflow;

                // Update each asset's default value in the template (only for assets that exist in the workflow)
                foreach (var kvp in assetValues)
                {
                    // Skip if this asset parameter doesn't exist in the workflow
                    if (!validAssetParams.Contains(kvp.Key))
                    {
                        _logger.LogDebug("Skipping asset '{AssetKey}' - not defined in workflow", kvp.Key);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(kvp.Value))
                        continue;

                    var escapedParam = Regex.Escape(kvp.Key);
                    var escapedValue = EscapeJsonString(kvp.Value);

                    // Pattern to find the asset object with matching parameter and update its default
                    // Uses a callback to handle the replacement
                    var assetBlockPattern = @"\{[^{}]*""parameter""\s*:\s*""" + escapedParam + @"""[^{}]*\}";

                    updatedText = Regex.Replace(updatedText, assetBlockPattern, match =>
                    {
                        var assetBlock = match.Value;
                        // Update the default value within this asset block
                        var defaultPattern = @"""default""\s*:\s*""[^""]*""";
                        var newDefault = $@"""default"": ""{escapedValue}""";
                        return Regex.Replace(assetBlock, defaultPattern, newDefault, RegexOptions.IgnoreCase);
                    }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    // Update the in-memory workflow asset default (on the workflow from the list)
                    var asset = workflowToUpdate.Assets?.FirstOrDefault(a =>
                        a.Parameter.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
                    if (asset != null)
                    {
                        asset.DefaultValue = kvp.Value;
                        _logger.LogDebug("Updated in-memory default for '{Parameter}': '{Value}' (workflow: {WorkflowTitle})",
                            kvp.Key, kvp.Value, workflowToUpdate.Title);
                    }
                }

                // Only write if changes were made
                if (updatedText != templateText)
                {
                    File.WriteAllText(templatePath, updatedText);
                    _logger.LogDebug("Saved asset defaults to: {TemplatePath}", templatePath);
                    return true;
                }
                else
                {
                    _logger.LogDebug("No changes made to asset defaults");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving asset defaults for workflow '{WorkflowTitle}'", workflow.Title);
                return false;
            }
        }

        /// <summary>
        /// Finds the template file path for a given workflow.
        /// </summary>
        private string? FindWorkflowTemplatePath(Workflow workflow)
        {
            var templatesPath = Path.Combine(_workflowPath, "Templates");
            var workflowFiles = _io.GetFilesRecursive(templatesPath, ignorePath: "utils", extensionsWhitelist: new() { ".sbn" });

            foreach (var file in workflowFiles)
            {
                var templateText = File.ReadAllText(file.FullName);
                var parsedWorkflow = ParseWorkflowTemplate(templateText);

                // Match by title, base, and mode
                if (parsedWorkflow.Title == workflow.Title &&
                    parsedWorkflow.Base == workflow.Base &&
                    parsedWorkflow.Mode == workflow.Mode)
                {
                    return file.FullName;
                }
            }

            return null;
        }

        /// <summary>
        /// Escapes a string for use in JSON.
        /// </summary>
        private string EscapeJsonString(string value)
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

        /// <summary>
        /// Injects WorkflowAssets from the parameter object into the global params dictionary.
        /// This allows templates to access assets like {{ HighModel }}, {{ Vae }}, etc. directly.
        /// Also injects default values from workflow Assets when parameter values are not set.
        /// </summary>
        /// <param name="param">The parameter object containing WorkflowAssets</param>
        /// <param name="workflow">The workflow template being composed (used for Asset defaults)</param>
        /// <param name="globalParams">The global parameters dictionary to inject into</param>
        private void InjectWorkflowAssets<T>(T param, Workflow workflow, Dictionary<string, object> globalParams) where T : class
        {
            // Try to get WorkflowAssets dictionary via reflection
            var workflowAssetsProp = param.GetType().GetProperty("WorkflowAssets");
            var workflowAssets = workflowAssetsProp?.GetValue(param) as Dictionary<string, string>;

            if (workflowAssets != null)
            {
                foreach (var kvp in workflowAssets)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        // Add to global params so they're accessible in templates
                        globalParams[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Also inject defaults from workflow Assets if parameter is not already set
            // This ensures Assets defaults are used when WorkflowAssets doesn't have a value
            if (workflow?.Assets != null)
            {
                foreach (var asset in workflow.Assets)
                {
                    // Only inject default if parameter not already in global params with a valid value
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
        }

        public Workflow LoadWorkflowTemplate(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Workflow>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private string ConvertCasing(string key)
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

        #region Fragment Schema Parsing

        /// <inheritdoc />
        public FragmentSchema? ParseFragmentSchema(string fragmentText)
        {
            if (string.IsNullOrWhiteSpace(fragmentText))
                return null;

            // Extract #meta block
            var metaMatch = Regex.Match(fragmentText, @"#meta\s*(\{.*?\})\s*#end", RegexOptions.Singleline);
            if (!metaMatch.Success)
                return null;

            var metaJson = metaMatch.Groups[1].Value;

            try
            {
                using var doc = JsonDocument.Parse(metaJson);
                var root = doc.RootElement;

                // Check if UI schema exists
                if (!root.TryGetProperty("ui", out var uiEl))
                    return null;

                return ParseUiSchema(uiEl);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse fragment UI schema");
                return null;
            }
        }

        private FragmentSchema ParseUiSchema(JsonElement uiEl)
        {
            var schema = new FragmentSchema();

            // Optional: type (fragment purpose/category)
            if (uiEl.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            {
                var typeStr = typeEl.GetString();
                if (!string.IsNullOrEmpty(typeStr) && Enum.TryParse<FragmentType>(typeStr, ignoreCase: true, out var fragmentType))
                {
                    schema.Type = fragmentType;
                }
            }

            // Required: title
            if (uiEl.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
            {
                schema.Title = titleEl.GetString() ?? string.Empty;
            }

            // Optional: component
            if (uiEl.TryGetProperty("component", out var componentEl))
            {
                schema.Component = componentEl.ValueKind == JsonValueKind.String 
                    ? componentEl.GetString() 
                    : null;
            }

            // Optional: icon
            if (uiEl.TryGetProperty("icon", out var iconEl) && iconEl.ValueKind == JsonValueKind.String)
            {
                schema.Icon = iconEl.GetString();
            }

            // Optional: collapsible (default true)
            if (uiEl.TryGetProperty("collapsible", out var collapsibleEl) && collapsibleEl.ValueKind == JsonValueKind.False)
            {
                schema.Collapsible = false;
            }

            // Optional: defaultCollapsed
            if (uiEl.TryGetProperty("defaultCollapsed", out var defaultCollapsedEl) && defaultCollapsedEl.ValueKind == JsonValueKind.True)
            {
                schema.DefaultCollapsed = true;
            }

            // Optional: chainable
            if (uiEl.TryGetProperty("chainable", out var chainableEl) && chainableEl.ValueKind == JsonValueKind.True)
            {
                schema.Chainable = true;
            }

            // Optional: order
            if (uiEl.TryGetProperty("order", out var orderEl) && orderEl.ValueKind == JsonValueKind.Number)
            {
                schema.Order = orderEl.GetInt32();
            }

            // Optional: parameters (for designed components)
            if (uiEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
            {
                schema.Parameters = ParseParameterConstraints(paramsEl);
            }

            // Optional: fields (for dynamic rendering)
            if (uiEl.TryGetProperty("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Array)
            {
                schema.Fields = ParseFieldSchemas(fieldsEl);
            }

            return schema;
        }

        private Dictionary<string, ParameterConstraints> ParseParameterConstraints(JsonElement paramsEl)
        {
            var result = new Dictionary<string, ParameterConstraints>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in paramsEl.EnumerateObject())
            {
                var constraints = new ParameterConstraints();

                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (prop.Value.TryGetProperty("min", out var minEl) && minEl.ValueKind == JsonValueKind.Number)
                    {
                        constraints.Min = minEl.GetDouble();
                    }

                    if (prop.Value.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number)
                    {
                        constraints.Max = maxEl.GetDouble();
                    }

                    if (prop.Value.TryGetProperty("step", out var stepEl) && stepEl.ValueKind == JsonValueKind.Number)
                    {
                        constraints.Step = stepEl.GetDouble();
                    }

                    // Parse default value - can be string, number, or boolean
                    if (prop.Value.TryGetProperty("default", out var defaultEl))
                    {
                        constraints.Default = defaultEl.ValueKind switch
                        {
                            JsonValueKind.String => defaultEl.GetString(),
                            JsonValueKind.Number => defaultEl.TryGetInt64(out var intVal) ? intVal : defaultEl.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => null
                        };
                    }

                    // Parse source - contains node class_type for dynamic options
                    if (prop.Value.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind == JsonValueKind.String)
                    {
                        constraints.Source = sourceEl.GetString();
                    }

                    // Parse input_name - the node input field to query
                    if (prop.Value.TryGetProperty("input_name", out var inputNameEl) && inputNameEl.ValueKind == JsonValueKind.String)
                    {
                        constraints.InputName = inputNameEl.GetString();
                    }

                    if (prop.Value.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
                    {
                        constraints.Options = optionsEl.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .ToList();
                    }
                }

                result[prop.Name] = constraints;
            }

            return result;
        }

        private List<FieldSchema> ParseFieldSchemas(JsonElement fieldsEl)
        {
            var result = new List<FieldSchema>();

            foreach (var fieldEl in fieldsEl.EnumerateArray())
            {
                if (fieldEl.ValueKind != JsonValueKind.Object)
                    continue;

                var field = new FieldSchema();

                if (fieldEl.TryGetProperty("parameter", out var paramEl) && paramEl.ValueKind == JsonValueKind.String)
                {
                    field.Parameter = paramEl.GetString() ?? string.Empty;
                }

                if (fieldEl.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String)
                {
                    field.Label = labelEl.GetString() ?? string.Empty;
                }

                if (fieldEl.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                {
                    field.Type = typeEl.GetString() ?? string.Empty;
                }

                if (fieldEl.TryGetProperty("column", out var columnEl) && columnEl.ValueKind == JsonValueKind.Number)
                {
                    field.Column = columnEl.GetInt32();
                }

                if (fieldEl.TryGetProperty("tooltip", out var tooltipEl) && tooltipEl.ValueKind == JsonValueKind.String)
                {
                    field.Tooltip = tooltipEl.GetString();
                }

                if (fieldEl.TryGetProperty("visible", out var visibleEl) && visibleEl.ValueKind == JsonValueKind.String)
                {
                    field.Visible = visibleEl.GetString();
                }

                if (fieldEl.TryGetProperty("min", out var minEl) && minEl.ValueKind == JsonValueKind.Number)
                {
                    field.Min = minEl.GetDouble();
                }

                if (fieldEl.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number)
                {
                    field.Max = maxEl.GetDouble();
                }

                if (fieldEl.TryGetProperty("step", out var stepEl) && stepEl.ValueKind == JsonValueKind.Number)
                {
                    field.Step = stepEl.GetDouble();
                }

                // Parse source - contains node class_type for dynamic options
                if (fieldEl.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind == JsonValueKind.String)
                {
                    field.Source = sourceEl.GetString();
                }

                // Parse input_name - the node input field to query
                if (fieldEl.TryGetProperty("input_name", out var inputNameEl) && inputNameEl.ValueKind == JsonValueKind.String)
                {
                    field.InputName = inputNameEl.GetString();
                }

                if (fieldEl.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
                {
                    field.Options = optionsEl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();
                }

                if (fieldEl.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Number)
                {
                    field.Rows = rowsEl.GetInt32();
                }

                if (fieldEl.TryGetProperty("collapsible", out var collapsibleEl))
                {
                    field.Collapsible = collapsibleEl.ValueKind == JsonValueKind.True;
                }

                // Nested fields for group type
                if (fieldEl.TryGetProperty("fields", out var nestedFieldsEl) && nestedFieldsEl.ValueKind == JsonValueKind.Array)
                {
                    field.Fields = ParseFieldSchemas(nestedFieldsEl);
                }

                result.Add(field);
            }

            return result;
        }

        /// <inheritdoc />
        public FragmentSchema? GetFragmentSchema(string fragmentFile)
        {
            if (string.IsNullOrWhiteSpace(fragmentFile))
                return null;

            lock (_schemaCacheLock)
            {
                if (_schemaCache.TryGetValue(fragmentFile, out var cached))
                {
                    return cached;
                }
            }

            // Load and parse fragment
            var fragPath = Path.Combine(_workflowPath, "Fragments", fragmentFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fragPath))
            {
                _logger.LogWarning("Fragment file not found: {FragmentFile}", fragmentFile);
                return null;
            }

            var fragmentText = File.ReadAllText(fragPath);
            var schema = ParseFragmentSchema(fragmentText);

            lock (_schemaCacheLock)
            {
                _schemaCache[fragmentFile] = schema;
            }

            return schema;
        }

        /// <inheritdoc />
        public Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow)
        {
            var result = new Dictionary<string, FragmentSchema>(StringComparer.OrdinalIgnoreCase);

            if (workflow == null || string.IsNullOrEmpty(workflow.RawJson))
                return result;

            // Use regex to parse Pipeline since RawJson contains Scriban templates
            var pipelineSteps = ParsePipelineStepsFromRawJson(workflow.RawJson);
            
            int index = 0;
            foreach (var step in pipelineSteps)
            {
                var fragmentId = step.Id;
                
                // If no ID, generate from fragment filename
                if (string.IsNullOrEmpty(fragmentId))
                {
                    fragmentId = Path.GetFileNameWithoutExtension(step.Fragment).Replace("-", "_");
                }

                // Get fragment schema
                if (!string.IsNullOrEmpty(step.Fragment))
                {
                    var schema = GetFragmentSchema(step.Fragment);
                    if (schema != null)
                    {
                        result[fragmentId] = schema;
                    }
                }

                index++;
            }

            return result;
        }

        /// <summary>
        /// Parses Pipeline steps from RawJson using regex to handle Scriban template syntax.
        /// Returns a list of (Id, Fragment) tuples.
        /// Uses bracket counting to properly handle nested objects in step parameters.
        /// </summary>
        private List<(string Id, string Fragment)> ParsePipelineStepsFromRawJson(string rawJson)
        {
            // Delegate to the public method for consistency
            var steps = ParsePipelineSteps(rawJson);
            return steps.Select(s => (s.Id, s.Fragment)).ToList();
        }

        /// <inheritdoc />
        public List<ParsedPipelineStep> ParsePipelineSteps(string rawJson)
        {
            var result = new List<ParsedPipelineStep>();
            
            if (string.IsNullOrEmpty(rawJson))
                return result;
            
            // Match each step object in Pipeline - look for "fragment": "..." patterns
            var fragmentPattern = @"""fragment""\s*:\s*""([^""]+)""";
            var idPattern = @"""id""\s*:\s*""([^""]+)""";
            
            // First, find the start of the Pipeline array
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
            
            // Find each step by using brace counting to handle nested objects
            var stepStart = -1;
            var braceDepth = 0;
            var order = 0;
            
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
                            
                            // Extract parameter defaults from step
                            var defaults = ParseStepParameterDefaults(stepContent);
                            
                            result.Add(new ParsedPipelineStep(id, fragment, defaults, order++));
                            _logger.LogTrace("Parsed pipeline step: id='{Id}', fragment='{Fragment}', defaults={DefaultCount}", 
                                id, fragment, defaults.Count);
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
            // "param_name": {{ SomeVar ?? true | json }}
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
                }
            }
            
            return defaults;
        }

        /// <inheritdoc />
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

            var steps = ParsePipelineSteps(workflow.RawJson);

            lock (_pipelineCacheLock)
            {
                _pipelineCache[workflow.Id] = steps;
            }

            _logger.LogDebug("Cached {Count} pipeline steps for workflow {WorkflowId}", steps.Count, workflow.Id);
            return steps;
        }

        /// <inheritdoc />
        public void ClearPipelineCache()
        {
            lock (_pipelineCacheLock)
            {
                _pipelineCache.Clear();
            }
            _logger.LogDebug("Pipeline cache cleared");
        }

        /// <inheritdoc />
        public void ClearSchemaCache()
        {
            lock (_schemaCacheLock)
            {
                _schemaCache.Clear();
            }
            _logger.LogDebug("Fragment schema cache cleared");
        }

        /// <inheritdoc />
        public Dictionary<string, object?> ParseFragmentDefaults(string fragmentFile)
        {
            var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(fragmentFile))
                return defaults;

            // Load fragment file
            var fragPath = Path.Combine(_workflowPath, "Fragments", fragmentFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fragPath))
            {
                _logger.LogWarning("Fragment file not found for defaults parsing: {FragmentFile}", fragmentFile);
                return defaults;
            }

            var fragmentText = File.ReadAllText(fragPath);

            // Remove #meta block to avoid parsing its content
            fragmentText = Regex.Replace(fragmentText, @"#meta\s*\{.*?\}\s*#end", "", RegexOptions.Singleline);

            // Pattern to match Scriban expressions with defaults:
            // {{ param ?? default_value | json }}
            // {{ param ?? "string_default" | json }}
            // {{ param ?? 123 | json }}
            // {{ param ?? true | json }}
            var defaultPattern = @"\{\{\s*(\w+)\s*\?\?\s*([^|]+?)\s*\|";

            var matches = Regex.Matches(fragmentText, defaultPattern);

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
                    _logger.LogTrace("Parsed default for '{Param}': {Value}", paramName, parsedValue);
                }
            }

            _logger.LogDebug("Parsed {Count} defaults from fragment '{FragmentFile}'", defaults.Count, fragmentFile);
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

        #endregion
    }

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
                    {
                        mergedNodes[prop.Name] = prop.Value.Clone();
                    }
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to parse fragment as JSON: {fragment}", ex);
                }
            }

            return JsonSerializer.Serialize(mergedNodes, new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }

        public NodeRegistry Registry => _globalRegistry;
    }
}
