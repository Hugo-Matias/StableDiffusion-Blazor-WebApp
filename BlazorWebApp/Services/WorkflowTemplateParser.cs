using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services.Templating;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Handles parsing of workflow template files including:
    /// - Workflow metadata (title, base, mode)
    /// - Assets array parsing
    /// - Sources array parsing
    /// - Pipeline steps parsing with parameter defaults
    /// 
    /// Supports two parsing modes:
    /// 1. Async (preferred): Uses Fluid rendering with safe defaults to produce valid JSON
    /// 2. Sync (legacy): Uses regex parsing for templates with Scriban syntax
    /// </summary>
    public class WorkflowTemplateParser
    {
        private readonly ILogger<WorkflowTemplateParser> _logger;
        private readonly IFluidTemplateService? _fluidService;

        /// <summary>
        /// Safe default values for Fluid rendering during metadata extraction.
        /// These values ensure the template renders to valid JSON even without
        /// actual generation parameters.
        /// </summary>
        private static readonly Dictionary<string, object?> SafeDefaults = new(StringComparer.OrdinalIgnoreCase)
        {
            // Collections - empty to skip loops
            ["Loras"] = new List<object>(),
            
            // Common generation parameters
            ["steps"] = 20,
            ["seed"] = 42,
            ["cfg"] = 7.0,
            ["width"] = 512,
            ["height"] = 768,
            ["batch_size"] = 1,
            ["denoise"] = 1.0,
            
            // Prompts
            ["positive"] = "",
            ["negative"] = "",
            
            // Sampler settings
            ["sampler_name"] = "euler",
            ["scheduler"] = "simple",
            
            // Video settings
            ["video_length"] = 81,
            ["frame_rate"] = 16,
            ["motion_amplitude"] = 1.1,
            ["shift"] = 5,
            
            // Frame interpolation
            ["frame_interpolation_scale_by"] = 2.0,
            ["frame_interpolation_multiplier"] = 2,
            ["frame_interpolation_rife_model"] = "rife49.pth",
            ["frame_interpolation_is_active"] = false,
            
            // Upscale settings
            ["upscale_model"] = "4x-UltraSharpV2.safetensors",
            ["upscale_width"] = 1024,
            ["upscale_height"] = 1536,
            ["upscale_steps"] = 20,
            ["upscale_denoise"] = 1.0,
            
            // Detailer settings
            ["detailer_model"] = "bbox/face_yolov8m.pt",
            ["detailer_sampler"] = "dpmpp_2m",
            ["detailer_scheduler"] = "beta",
            ["detailer_seed"] = 42,
            ["detailer_steps"] = 20,
            ["detailer_cfg"] = 8.0,
            ["detailer_denoise"] = 0.65,
            
            // SeedVR2 settings
            ["seed_vr2_model"] = "seedvr2_ema_7b-Q4_K_M.gguf",
            ["seed_vr2_vae_model"] = "ema_vae_fp16.safetensors",
            
            // Asset placeholders (will be overridden by actual assets)
            ["Model"] = "model.safetensors",
            ["HighModel"] = "high_model.safetensors",
            ["LowModel"] = "low_model.safetensors",
            ["Clip"] = "clip.safetensors",
            ["ClipVision"] = "clip_vision.safetensors",
            ["Vae"] = "vae.safetensors",
            
            // Source placeholders
            ["image"] = "/tmp/placeholder.png",
        };

        public WorkflowTemplateParser(ILogger<WorkflowTemplateParser> logger)
        {
            _logger = logger;
            _fluidService = null;
        }

        public WorkflowTemplateParser(ILogger<WorkflowTemplateParser> logger, IFluidTemplateService fluidService)
        {
            _logger = logger;
            _fluidService = fluidService;
        }

        /// <summary>
        /// Parses a workflow template asynchronously using Fluid rendering.
        /// This is the preferred method for .liquid templates.
        /// </summary>
        /// <param name="templateText">The raw template text.</param>
        /// <returns>A parsed Workflow object.</returns>
        public async Task<Workflow> ParseWorkflowTemplateAsync(string templateText)
        {
            if (_fluidService == null)
            {
                _logger.LogWarning("FluidTemplateService not available, falling back to sync parsing");
                return ParseWorkflowTemplate(templateText);
            }

            var wf = new Workflow
            {
                RawJson = templateText,
            };

            try
            {
                // Render template with safe defaults to produce valid JSON
                var (renderedJson, _) = await _fluidService.RenderAsync(templateText, SafeDefaults, null);

                // Parse the rendered JSON
                using var doc = JsonDocument.Parse(renderedJson);
                var root = doc.RootElement;

                // Extract metadata
                wf.Title = GetStringProperty(root, "Title") ?? "Untitled";
                
                var baseStr = GetStringProperty(root, "Base");
                if (!string.IsNullOrEmpty(baseStr) && Enum.TryParse<ModelBase>(baseStr, true, out var mb))
                    wf.Base = mb;

                var modeStr = GetStringProperty(root, "Mode");
                if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse<ModeType>(modeStr, true, out var mt))
                    wf.Mode = mt;

                // Parse Assets
                wf.Assets = ParseAssetsFromJson(root);

                // Parse Sources
                wf.Sources = ParseSourcesFromJson(root);

                // Generate deterministic ID
                var uniqueString = $"{wf.Title}_{wf.Base}_{wf.Mode}";
                wf.Id = GenerateDeterministicGuid(uniqueString);

                // Parse Pipeline steps (just structure, not expansion)
                wf.Pipeline = ParsePipelineFromJson(root);

                _logger.LogDebug("Parsed workflow template: Title='{Title}', Base={Base}, Mode={Mode}, Assets={AssetCount}, Pipeline={StepCount}",
                    wf.Title, wf.Base, wf.Mode, wf.Assets?.Count ?? 0, wf.Pipeline?.Count ?? 0);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse rendered workflow template as JSON. Falling back to regex parsing.");
                return ParseWorkflowTemplate(templateText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing workflow template with Fluid. Falling back to regex parsing.");
                return ParseWorkflowTemplate(templateText);
            }

            return wf;
        }

        /// <summary>
        /// Parses Assets array from a JSON document.
        /// </summary>
        private List<WorkflowAsset>? ParseAssetsFromJson(JsonElement root)
        {
            if (!root.TryGetProperty("Assets", out var assetsEl) || assetsEl.ValueKind != JsonValueKind.Array)
                return null;

            var assets = new List<WorkflowAsset>();

            foreach (var assetEl in assetsEl.EnumerateArray())
            {
                if (assetEl.ValueKind != JsonValueKind.Object)
                    continue;

                var parameter = GetStringProperty(assetEl, "parameter");
                if (string.IsNullOrEmpty(parameter))
                    continue;

                var typeStr = GetStringProperty(assetEl, "type");
                if (string.IsNullOrEmpty(typeStr) || !Enum.TryParse<AssetType>(typeStr, true, out var assetType))
                    continue;

                var asset = new WorkflowAsset
                {
                    Parameter = parameter,
                    Label = GetStringProperty(assetEl, "label") ?? parameter,
                    Type = assetType,
                    DefaultValue = GetStringProperty(assetEl, "default"),
                    Order = GetIntProperty(assetEl, "order") ?? 0,
                    ColumnSize = GetIntProperty(assetEl, "columnSize") ?? 12
                };

                assets.Add(asset);
            }

            return assets.Count > 0 ? assets : null;
        }

        /// <summary>
        /// Parses Sources array from a JSON document.
        /// </summary>
        private List<WorkflowSource>? ParseSourcesFromJson(JsonElement root)
        {
            if (!root.TryGetProperty("Sources", out var sourcesEl) || sourcesEl.ValueKind != JsonValueKind.Array)
                return null;

            var sources = new List<WorkflowSource>();

            foreach (var sourceEl in sourcesEl.EnumerateArray())
            {
                if (sourceEl.ValueKind != JsonValueKind.Object)
                    continue;

                var id = GetStringProperty(sourceEl, "id");
                if (string.IsNullOrEmpty(id))
                    continue;

                var source = new WorkflowSource
                {
                    Id = id,
                    Label = GetStringProperty(sourceEl, "label") ?? id,
                    Type = GetStringProperty(sourceEl, "type") ?? "image",
                    Required = GetBoolProperty(sourceEl, "required") ?? true,
                    Parameter = GetStringProperty(sourceEl, "parameter")
                };

                sources.Add(source);
                _logger.LogDebug("Parsed source from JSON: id='{Id}', label='{Label}', type='{Type}'", source.Id, source.Label, source.Type);
            }

            _logger.LogDebug("Parsed {Count} sources from JSON", sources.Count);
            return sources.Count > 0 ? sources : null;
        }

        /// <summary>
        /// Parses Pipeline array from a JSON document.
        /// Note: This captures the static structure. Dynamic steps ($foreach, $if) are expanded later by PipelineExpander.
        /// </summary>
        private List<WorkflowStep>? ParsePipelineFromJson(JsonElement root)
        {
            if (!root.TryGetProperty("Pipeline", out var pipelineEl) || pipelineEl.ValueKind != JsonValueKind.Array)
                return null;

            var steps = new List<WorkflowStep>();

            foreach (var stepEl in pipelineEl.EnumerateArray())
            {
                if (stepEl.ValueKind != JsonValueKind.Object)
                    continue;

                // Check for special markers
                if (stepEl.TryGetProperty("$foreach", out _) || stepEl.TryGetProperty("$if", out _))
                {
                    // Skip dynamic steps in static pipeline parsing
                    // These will be handled by PipelineExpander during composition
                    _logger.LogTrace("Skipping dynamic pipeline step (has $foreach or $if marker)");
                    continue;
                }

                var id = GetStringProperty(stepEl, "id") ?? "";
                var fragment = GetStringProperty(stepEl, "fragment") ?? "";

                if (string.IsNullOrEmpty(fragment))
                    continue;

                steps.Add(new WorkflowStep
                {
                    Id = id,
                    Fragment = fragment
                });
            }

            return steps.Count > 0 ? steps : null;
        }

        #region JSON Helper Methods

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        private static int? GetIntProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();
            return null;
        }

        private static bool? GetBoolProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
            }
            return null;
        }

        #endregion

        #region Legacy Regex-based Parsing (for .sbn templates)

        /// <summary>
        /// Parses a workflow template text into a Workflow object using regex.
        /// This is the legacy method for .sbn templates with Scriban syntax.
        /// </summary>
        public Workflow ParseWorkflowTemplate(string templateText)
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

            wf.Assets = ParseAssetsFromTemplate(templateText);
            wf.Sources = ParseSourcesFromTemplate(templateText);

            var uniqueString = $"{wf.Title}_{wf.Base}_{wf.Mode}";
            wf.Id = GenerateDeterministicGuid(uniqueString);

            wf.Pipeline = ParsePipelineFromTemplate(templateText);

            return wf;
        }

        /// <summary>
        /// Parses Pipeline steps from the workflow template.
        /// </summary>
        private List<WorkflowStep>? ParsePipelineFromTemplate(string templateText)
        {
            var steps = ParsePipelineSteps(templateText);
            if (steps.Count == 0)
                return null;

            return steps.Select(s => new WorkflowStep
            {
                Id = s.Id,
                Fragment = s.Fragment
            }).ToList();
        }

        /// <summary>
        /// Parses the Assets array from a workflow template.
        /// </summary>
        public List<WorkflowAsset>? ParseAssetsFromTemplate(string templateText)
        {
            var assetsMatch = Regex.Match(templateText, @"""Assets""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!assetsMatch.Success)
                return null;

            var assetsArrayContent = assetsMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(assetsArrayContent))
                return null;

            var assets = new List<WorkflowAsset>();
            var assetMatches = Regex.Matches(assetsArrayContent, @"\{([^{}]*)\}", RegexOptions.Singleline);

            foreach (Match assetMatch in assetMatches)
            {
                var asset = ParseSingleAsset(assetMatch.Groups[1].Value);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets.Count > 0 ? assets : null;
        }

        /// <summary>
        /// Parses a single asset definition from JSON-like content.
        /// </summary>
        private WorkflowAsset? ParseSingleAsset(string assetContent)
        {
            var asset = new WorkflowAsset();

            var paramMatch = Regex.Match(assetContent, @"""parameter""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (paramMatch.Success)
                asset.Parameter = paramMatch.Groups[1].Value;
            else
                return null;

            var labelMatch = Regex.Match(assetContent, @"""label""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            asset.Label = labelMatch.Success ? labelMatch.Groups[1].Value : asset.Parameter;

            var typeMatch = Regex.Match(assetContent, @"""type""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (typeMatch.Success && Enum.TryParse<AssetType>(typeMatch.Groups[1].Value, true, out var assetType))
                asset.Type = assetType;
            else
                return null;

            var defaultMatch = Regex.Match(assetContent, @"""default""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (defaultMatch.Success)
                asset.DefaultValue = defaultMatch.Groups[1].Value;

            var orderMatch = Regex.Match(assetContent, @"""order""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (orderMatch.Success && int.TryParse(orderMatch.Groups[1].Value, out var order))
                asset.Order = order;

            var columnMatch = Regex.Match(assetContent, @"""columnSize""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (columnMatch.Success && int.TryParse(columnMatch.Groups[1].Value, out var columnSize))
                asset.ColumnSize = columnSize;

            return asset;
        }

        /// <summary>
        /// Parses the Sources array from a workflow template.
        /// </summary>
        public List<WorkflowSource>? ParseSourcesFromTemplate(string templateText)
        {
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

            var sources = new List<WorkflowSource>();
            var sourceMatches = Regex.Matches(sourcesArrayContent, @"\{([^{}]*)\}", RegexOptions.Singleline);

            foreach (Match sourceMatch in sourceMatches)
            {
                var source = ParseSingleSource(sourceMatch.Groups[1].Value);
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

            var idMatch = Regex.Match(sourceContent, @"""id""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (idMatch.Success)
                source.Id = idMatch.Groups[1].Value;
            else
                return null;

            var labelMatch = Regex.Match(sourceContent, @"""label""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            source.Label = labelMatch.Success ? labelMatch.Groups[1].Value : source.Id;

            var typeMatch = Regex.Match(sourceContent, @"""type""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            source.Type = typeMatch.Success ? typeMatch.Groups[1].Value : "image";

            var requiredMatch = Regex.Match(sourceContent, @"""required""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
            source.Required = !requiredMatch.Success || requiredMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

            var paramMatch = Regex.Match(sourceContent, @"""parameter""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (paramMatch.Success)
                source.Parameter = paramMatch.Groups[1].Value;

            return source;
        }

        /// <summary>
        /// Parses Pipeline steps from RawJson with parameter defaults.
        /// </summary>
        public List<ParsedPipelineStep> ParsePipelineSteps(string rawJson)
        {
            var result = new List<ParsedPipelineStep>();

            if (string.IsNullOrEmpty(rawJson))
                return result;

            var fragmentPattern = @"""fragment""\s*:\s*""([^""]+)""";
            var idPattern = @"""id""\s*:\s*""([^""]+)""";

            var pipelineMatch = Regex.Match(rawJson, @"""Pipeline""\s*:\s*\[", RegexOptions.Singleline);
            if (!pipelineMatch.Success)
            {
                _logger.LogDebug("No Pipeline array found in workflow");
                return result;
            }

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

            var stepStart = -1;
            var braceDepth = 0;
            var order = 0;

            for (var i = 0; i < pipelineContent.Length; i++)
            {
                var c = pipelineContent[i];

                if (c == '{')
                {
                    if (braceDepth == 0)
                        stepStart = i;
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0 && stepStart >= 0)
                    {
                        var stepContent = pipelineContent.Substring(stepStart, i - stepStart + 1);

                        var fragMatch = Regex.Match(stepContent, fragmentPattern);
                        if (fragMatch.Success)
                        {
                            var fragment = fragMatch.Groups[1].Value;
                            var idMatch = Regex.Match(stepContent, idPattern);
                            var id = idMatch.Success ? idMatch.Groups[1].Value : "";
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
        /// </summary>
        public Dictionary<string, object?> ParseStepParameterDefaults(string stepContent)
        {
            var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var paramsMatch = Regex.Match(stepContent, @"""parameters""\s*:\s*\{", RegexOptions.Singleline);
            if (!paramsMatch.Success)
                return defaults;

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
                    defaults[paramName] = parsedValue;
            }

            return defaults;
        }

        /// <summary>
        /// Parses a Scriban default value expression into a CLR object.
        /// 
        /// Only parses unambiguous literals:
        /// - Quoted strings: "euler", 'simple'
        /// - Numbers: 20, 1.5, -1
        /// - Booleans: true, false
        /// - Null: null, nil
        /// 
        /// Unquoted identifiers (like positive, width, Model) are NOT stored as defaults.
        /// They are variable references that Scriban will resolve at render time.
        /// </summary>
        public object? ParseScribanDefaultValue(string valueStr)
        {
            if (string.IsNullOrWhiteSpace(valueStr))
                return null;

            valueStr = valueStr.Trim();

            // 1. Quoted string literals - these are unambiguous defaults
            if ((valueStr.StartsWith("\"") && valueStr.EndsWith("\"")) ||
                (valueStr.StartsWith("'") && valueStr.EndsWith("'")))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }

            // 2. Boolean literals
            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            // 3. Null literals
            if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                valueStr.Equals("nil", StringComparison.OrdinalIgnoreCase))
                return null;

            // 4. Numeric literals - try integer first, then floating point
            if (long.TryParse(valueStr, out var longVal))
                return longVal;

            if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                return doubleVal;

            // 5. Anything else is an unquoted identifier - treat as variable reference
            // Don't store it as a default; let Scriban resolve it at render time
            _logger.LogTrace("Unquoted identifier '{Value}' treated as variable reference, not storing as default", valueStr);
            return null;
        }

        #endregion

        /// <summary>
        /// Generates a deterministic GUID from a string input.
        /// </summary>
        public static Guid GenerateDeterministicGuid(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return new Guid(hash);
        }
    }
}
