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
    public class WorkflowService
    {
        private readonly string _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        private readonly IOService _io;
        private readonly ILogger<WorkflowService> _logger;

        public WorkflowService(IOService io, ILogger<WorkflowService> logger)
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

            // Generate a unique ID based on available properties
            var uniqueString = $"{wf.Title}_{wf.Base}_{wf.Mode}";
            wf.Id = GenerateDeterministicGuid(uniqueString);

            wf.Pipeline = null;

            return wf;
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
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                        return false;
                }
                else if (current != null)
                {
                    var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null)
                        return false;
                    current = prop.GetValue(current);
                }
                else
                {
                    return false;
                }
            }

            if (current is bool boolValue)
                return boolValue;

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
