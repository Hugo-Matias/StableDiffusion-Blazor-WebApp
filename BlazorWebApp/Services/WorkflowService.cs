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

        public WorkflowService(IOService io)
        {
            _io = io;
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

            var modelTypeMatch = Regex.Match(templateText, @"""modeltype""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (modelTypeMatch.Success && Enum.TryParse<ModelType>(modelTypeMatch.Groups[1].Value, true, out var mtype))
                wf.ModelType = mtype;

            var uniqueString = $"{wf.Title}_{wf.Base}_{wf.Mode}_{wf.ModelType}";
            wf.Id = GenerateDeterministicGuid(uniqueString);

            // Extract pipeline structure - only fragment names, NO parameter parsing
            var pipelineMatch = Regex.Match(templateText, @"""pipeline""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (pipelineMatch.Success)
            {
                var pipeline = new List<WorkflowStep>();
                var pipelineContent = pipelineMatch.Groups[1].Value;

                // Extract each pipeline step block using a Scriban-aware brace-matching algorithm
                var steps = ExtractJsonObjectsScribanAware(pipelineContent);

                foreach (var stepJson in steps)
                {
                    var fragMatch = Regex.Match(stepJson, @"""fragment""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (!fragMatch.Success) continue;

                    var step = new WorkflowStep
                    {
                        Fragment = fragMatch.Groups[1].Value,
                        RawParameters = stepJson
                    };

                    pipeline.Add(step);
                }

                wf.Pipeline = pipeline;
            }

            return wf;
        }

        private Guid GenerateDeterministicGuid(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
        }

        // Helper method to extract JSON objects while preserving Scriban {{ }} syntax
        private List<string> ExtractJsonObjectsScribanAware(string content)
        {
            var objects = new List<string>();
            var depth = 0;
            var startIndex = -1;
            var inString = false;
            var escapeNext = false;

            for (int i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                // Check for Scriban {{ or }} and skip them
                if (c == '{')
                {
                    // Look ahead to see if this is {{ (Scriban opening)
                    if (i + 1 < content.Length && content[i + 1] == '{')
                    {
                        i++; // Skip the next brace
                        continue;
                    }

                    // Regular JSON brace
                    if (depth == 0)
                        startIndex = i;
                    depth++;
                }
                else if (c == '}')
                {
                    // Look ahead to see if this is }} (Scriban closing)
                    if (i + 1 < content.Length && content[i + 1] == '}')
                    {
                        i++; // Skip the next brace
                        continue;
                    }

                    // Regular JSON brace
                    depth--;
                    if (depth == 0 && startIndex >= 0)
                    {
                        objects.Add(content.Substring(startIndex, i - startIndex + 1));
                        startIndex = -1;
                    }
                }
            }

            return objects;
        }

        // Separate method: extract outputs from rendered meta block
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

        // Helper method to evaluate conditions
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
        public (string rendered, Dictionary<string, (string nodeId, int index)> outputs) RenderFragment(string fragmentText, SubgraphContext context, Dictionary<string, object> globalParams)
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
                    renderedMeta = RenderTemplate(metaJson, context, preserveFormatting: true);
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
            var rendered = RenderTemplate(fragmentText, context, preserveFormatting: false);
            // Remove trailing commas from rendered fragment
            rendered = Regex.Replace(rendered, @",\s*(\}|])", "$1", RegexOptions.Singleline);
            return (rendered, outputs);
        }

        private string RenderTemplate(string templateText, SubgraphContext context, bool preserveFormatting)
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
                MemberFilter = member => true, // Allow all members
                EnableRelaxedMemberAccess = true, // Allow null propagation
                EnableRelaxedFunctionAccess = true,
                EnableRelaxedTargetAccess = true
            };

            var scriptObject = new ScriptObject();

            if (context.Parameters != null)
            {
                foreach (var kvp in context.Parameters)
                {
                    scriptObject.SetValue(kvp.Key, kvp.Value, false);

                    // ALSO store with alternative casing to support both snake_case and PascalCase
                    // This handles cases where step parameters use snake_case but fragments expect PascalCase
                    var alternateKey = ConvertCasing(kvp.Key);
                    if (alternateKey != kvp.Key)
                    {
                        scriptObject.SetValue(alternateKey, kvp.Value, false);
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
            var composer = new WorkflowComposer();

            var globalParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Use reflection to get all properties from param
            foreach (var prop in param.GetType().GetProperties())
            {
                var value = prop.GetValue(param);
                globalParams[prop.Name] = value;
            }

            var templateContext = new TemplateContext
            {
                MemberRenamer = member => member.Name,
                MemberFilter = member => true,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedFunctionAccess = true,
                EnableRelaxedTargetAccess = true
            };

            var scriptObject = new ScriptObject();
            foreach (var kvp in globalParams)
            {
                scriptObject.SetValue(kvp.Key, kvp.Value, false);
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

            foreach (var step in template.Pipeline)
            {
                try
                {
                    // Render the step's raw parameters JSON to resolve Scriban expressions
                    var parametersTemplate = Template.Parse(step.RawParameters);
                    var renderedStepJson = parametersTemplate.Render(templateContext);

                    // DEBUG: Write to file to inspect
                    //File.WriteAllText($"debug_step_{step.Fragment}.json", renderedStepJson);

                    // Parse the rendered JSON to extract actual parameter values
                    using var doc = JsonDocument.Parse(renderedStepJson);
                    var root = doc.RootElement;

                    var mergedParams = new Dictionary<string, object>(globalParams, StringComparer.OrdinalIgnoreCase);

                    if (root.TryGetProperty("parameters", out var paramsEl))
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

                    if (!string.IsNullOrWhiteSpace(step.Fragment))
                    {
                        var fragPath = Path.Combine(_workflowPath, "Fragments", step.Fragment.Replace('/', Path.DirectorySeparatorChar));
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

                            if (step.Outputs != null)
                            {
                                foreach (var kvp in step.Outputs)
                                {
                                    context.Outputs.Register(kvp.Key, kvp.Value.Node, kvp.Value.Index);
                                }
                            }

                            composer.AddRenderedFragment(rendered, context.Outputs);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to parse step parameters for fragment '{step.Fragment}'. " +
                        $"JSON error: {ex.Message}. " +
                        $"Check debug_step_{step.Fragment}.json for the rendered output.",
                        ex);
                }
            }

            return composer.BuildFinalWorkflow();
        }

        public Workflow LoadWorkflowTemplate(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Workflow>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        // Helper method to convert between snake_case and PascalCase
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

            // Parse all fragments as JSON and merge them into a single object
            var mergedNodes = new Dictionary<string, JsonElement>();

            foreach (var fragment in _renderedFragments)
            {
                try
                {
                    using var doc = JsonDocument.Parse(fragment);
                    var root = doc.RootElement;

                    // Each fragment should be a JSON object with node IDs as keys
                    foreach (var prop in root.EnumerateObject())
                    {
                        // Clone the JsonElement to avoid document disposal issues
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
