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

            wf.Pipeline = null;

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

            globalParams["ModelType"] = template.ModelType.ToString();

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
