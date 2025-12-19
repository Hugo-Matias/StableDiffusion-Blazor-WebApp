using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Handles parsing of workflow template files (.sbn) including:
    /// - Workflow metadata (title, base, mode)
    /// - Assets array parsing
    /// - Sources array parsing
    /// - Pipeline steps parsing with parameter defaults
    /// </summary>
    public class WorkflowTemplateParser
    {
        private readonly ILogger<WorkflowTemplateParser> _logger;

        public WorkflowTemplateParser(ILogger<WorkflowTemplateParser> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Parses a workflow template text into a Workflow object.
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
        /// </summary>
        public object? ParseScribanDefaultValue(string valueStr)
        {
            if (string.IsNullOrWhiteSpace(valueStr))
                return null;

            valueStr = valueStr.Trim();

            if ((valueStr.StartsWith("\"") && valueStr.EndsWith("\"")) ||
                (valueStr.StartsWith("'") && valueStr.EndsWith("'")))
                return valueStr.Substring(1, valueStr.Length - 2);

            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                valueStr.Equals("nil", StringComparison.OrdinalIgnoreCase))
                return null;

            if (long.TryParse(valueStr, out var longVal))
                return longVal;

            if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                return doubleVal;

            return valueStr;
        }

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
