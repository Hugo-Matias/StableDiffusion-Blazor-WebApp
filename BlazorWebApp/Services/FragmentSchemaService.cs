using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for parsing and caching fragment UI schemas.
    /// Handles extraction of #meta blocks and UI configuration from fragment files.
    /// </summary>
    public class FragmentSchemaService : IFragmentSchemaService
    {
        private readonly string _fragmentsPath;
        private readonly ILogger<FragmentSchemaService> _logger;
        private readonly Dictionary<string, FragmentSchema?> _schemaCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _schemaCacheLock = new();

        public FragmentSchemaService(ILogger<FragmentSchemaService> logger)
        {
            _fragmentsPath = Path.Combine(AppContext.BaseDirectory, "Workflows", "Fragments");
            _logger = logger;
        }

        /// <inheritdoc />
        public FragmentSchema? ParseFragmentSchema(string fragmentText)
        {
            if (string.IsNullOrWhiteSpace(fragmentText))
                return null;

            // Match #meta ... #end block - use [\s\S] to match any character including newlines
            var metaMatch = Regex.Match(fragmentText, @"#meta\s*([\s\S]*?)\s*#end");
            if (!metaMatch.Success)
                return null;

            var metaJson = metaMatch.Groups[1].Value.Trim();

            try
            {
                // Pre-process to handle Scriban templating in #meta blocks
                // This allows fragments to use dynamic outputs/conditions while still having parseable UI schemas
                metaJson = PreprocessMetaJson(metaJson);
                
                // Clean up trailing commas before parsing
                metaJson = Regex.Replace(metaJson, @",\s*(\}|])", "$1", RegexOptions.Singleline);
                
                using var doc = JsonDocument.Parse(metaJson);
                var root = doc.RootElement;

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

        /// <summary>
        /// Pre-processes #meta JSON content to handle Scriban templating.
        /// Removes or neutralizes Scriban expressions so the JSON can be parsed.
        /// </summary>
        private static string PreprocessMetaJson(string metaJson)
        {
            // Remove Scriban conditional blocks entirely (they add optional properties like "conditions")
            // Pattern: {{~ if ... ~}} ... {{~ end ~}} or {{ if ... }} ... {{ end }}
            metaJson = Regex.Replace(metaJson, @"\{\{~?\s*if\s+[\s\S]*?\{\{~?\s*end\s*~?\}\}", "", RegexOptions.Singleline);
            
            // Replace Scriban expressions in string values with placeholder
            // Pattern: {{ scope ?? '' }} or {{ variable | filter }} etc.
            // This handles dynamic keys like "{{ scope ?? '' }}model_output"
            metaJson = Regex.Replace(metaJson, @"\{\{[^}]+\}\}", "", RegexOptions.None);
            
            // Clean up any resulting empty string concatenations in keys
            // e.g., "model_output" instead of "{{ scope ?? '' }}model_output"
            // The keys will be different at runtime but we only need to validate structure
            
            // Remove any leading commas that might result from removed conditional blocks
            metaJson = Regex.Replace(metaJson, @",(\s*\})", "$1", RegexOptions.Singleline);
            metaJson = Regex.Replace(metaJson, @"\{(\s*),", "{$1", RegexOptions.Singleline);
            
            return metaJson;
        }

        /// <inheritdoc />
        public FragmentSchema? GetFragmentSchema(string fragmentFile)
        {
            if (string.IsNullOrWhiteSpace(fragmentFile))
                return null;

            lock (_schemaCacheLock)
            {
                if (_schemaCache.TryGetValue(fragmentFile, out var cached))
                    return cached;
            }

            var fragPath = Path.Combine(_fragmentsPath, fragmentFile.Replace('/', Path.DirectorySeparatorChar));
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

            if (workflow?.Pipeline == null)
                return result;

            foreach (var step in workflow.Pipeline)
            {
                var fragmentId = step.Id;

                if (string.IsNullOrEmpty(fragmentId))
                    fragmentId = Path.GetFileNameWithoutExtension(step.Fragment).Replace("-", "_");

                if (!string.IsNullOrEmpty(step.Fragment))
                {
                    var schema = GetFragmentSchema(step.Fragment);
                    if (schema != null)
                        result[fragmentId] = schema;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            lock (_schemaCacheLock)
            {
                _schemaCache.Clear();
            }
            _logger.LogDebug("Fragment schema cache cleared");
        }

        /// <inheritdoc />
        public List<string> ValidateFragmentSchema(string fragmentFile)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(fragmentFile))
            {
                errors.Add("Fragment file path is empty");
                return errors;
            }

            var fragPath = Path.Combine(_fragmentsPath, fragmentFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fragPath))
            {
                errors.Add($"Fragment file not found: {fragmentFile}");
                return errors;
            }

            // Parse the schema
            var schema = GetFragmentSchema(fragmentFile);
            if (schema == null)
            {
                // No schema is valid for utility fragments
                return errors;
            }

            // Validate the schema
            var schemaErrors = schema.Validate(fragmentFile);
            errors.AddRange(schemaErrors);

            return errors;
        }

        /// <inheritdoc />
        public List<string> GetAllFragmentFiles()
        {
            var fragments = new List<string>();

            if (!Directory.Exists(_fragmentsPath))
            {
                _logger.LogWarning("Fragments directory not found: {Path}", _fragmentsPath);
                return fragments;
            }

            try
            {
                var files = Directory.GetFiles(_fragmentsPath, "*.sbn", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Get path relative to fragments folder
                    var relativePath = Path.GetRelativePath(_fragmentsPath, file);
                    // Normalize to forward slashes for consistency
                    fragments.Add(relativePath.Replace(Path.DirectorySeparatorChar, '/'));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating fragment files in {Path}", _fragmentsPath);
            }

            return fragments;
        }

        /// <inheritdoc />
        public Dictionary<string, List<string>> ValidateAllFragmentSchemas()
        {
            var results = new Dictionary<string, List<string>>();
            var fragmentFiles = GetAllFragmentFiles();

            foreach (var fragmentFile in fragmentFiles)
            {
                var errors = ValidateFragmentSchema(fragmentFile);
                if (errors.Count > 0)
                {
                    results[fragmentFile] = errors;
                }
            }

            if (results.Count > 0)
            {
                _logger.LogWarning("Fragment schema validation found errors in {Count} fragments", results.Count);
            }
            else
            {
                _logger.LogDebug("All {Count} fragment schemas validated successfully", fragmentFiles.Count);
            }

            return results;
        }

        /// <inheritdoc />
        public Dictionary<string, object?> ParseFragmentDefaults(string fragmentFile)
        {
            var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(fragmentFile))
                return defaults;

            var fragPath = Path.Combine(_fragmentsPath, fragmentFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fragPath))
            {
                _logger.LogWarning("Fragment file not found for defaults parsing: {FragmentFile}", fragmentFile);
                return defaults;
            }

            var fragmentText = File.ReadAllText(fragPath);

            // Remove #meta block - use [\s\S] to match any character including newlines
            fragmentText = Regex.Replace(fragmentText, @"#meta\s*[\s\S]*?\s*#end", "", RegexOptions.None);

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

        #region Private Parsing Methods

        private FragmentSchema ParseUiSchema(JsonElement uiEl)
        {
            var schema = new FragmentSchema();

            if (uiEl.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            {
                var typeStr = typeEl.GetString();
                if (!string.IsNullOrEmpty(typeStr) && Enum.TryParse<FragmentType>(typeStr, ignoreCase: true, out var fragmentType))
                    schema.Type = fragmentType;
            }

            if (uiEl.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
                schema.Title = titleEl.GetString() ?? string.Empty;

            if (uiEl.TryGetProperty("component", out var componentEl))
                schema.Component = componentEl.ValueKind == JsonValueKind.String ? componentEl.GetString() : null;

            if (uiEl.TryGetProperty("icon", out var iconEl) && iconEl.ValueKind == JsonValueKind.String)
                schema.Icon = iconEl.GetString();

            if (uiEl.TryGetProperty("collapsible", out var collapsibleEl) && collapsibleEl.ValueKind == JsonValueKind.False)
                schema.Collapsible = false;

            if (uiEl.TryGetProperty("defaultCollapsed", out var defaultCollapsedEl) && defaultCollapsedEl.ValueKind == JsonValueKind.True)
                schema.DefaultCollapsed = true;

            if (uiEl.TryGetProperty("chainable", out var chainableEl) && chainableEl.ValueKind == JsonValueKind.True)
                schema.Chainable = true;

            if (uiEl.TryGetProperty("order", out var orderEl) && orderEl.ValueKind == JsonValueKind.Number)
                schema.Order = orderEl.GetInt32();

            if (uiEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
                schema.Parameters = ParseParameterConstraints(paramsEl);

            if (uiEl.TryGetProperty("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Array)
                schema.Fields = ParseFieldSchemas(fieldsEl);

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
                        constraints.Min = minEl.GetDouble();

                    if (prop.Value.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number)
                        constraints.Max = maxEl.GetDouble();

                    if (prop.Value.TryGetProperty("step", out var stepEl) && stepEl.ValueKind == JsonValueKind.Number)
                        constraints.Step = stepEl.GetDouble();

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

                    if (prop.Value.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind == JsonValueKind.String)
                        constraints.Source = sourceEl.GetString();

                    if (prop.Value.TryGetProperty("input_name", out var inputNameEl) && inputNameEl.ValueKind == JsonValueKind.String)
                        constraints.InputName = inputNameEl.GetString();

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
                    field.Parameter = paramEl.GetString() ?? string.Empty;

                if (fieldEl.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String)
                    field.Label = labelEl.GetString() ?? string.Empty;

                if (fieldEl.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                    field.Type = typeEl.GetString() ?? string.Empty;

                if (fieldEl.TryGetProperty("column", out var columnEl) && columnEl.ValueKind == JsonValueKind.Number)
                    field.Column = columnEl.GetInt32();

                if (fieldEl.TryGetProperty("tooltip", out var tooltipEl) && tooltipEl.ValueKind == JsonValueKind.String)
                    field.Tooltip = tooltipEl.GetString();

                if (fieldEl.TryGetProperty("visible", out var visibleEl) && visibleEl.ValueKind == JsonValueKind.String)
                    field.Visible = visibleEl.GetString();

                if (fieldEl.TryGetProperty("min", out var minEl) && minEl.ValueKind == JsonValueKind.Number)
                    field.Min = minEl.GetDouble();

                if (fieldEl.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number)
                    field.Max = maxEl.GetDouble();

                if (fieldEl.TryGetProperty("step", out var stepEl) && stepEl.ValueKind == JsonValueKind.Number)
                    field.Step = stepEl.GetDouble();

                if (fieldEl.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind == JsonValueKind.String)
                    field.Source = sourceEl.GetString();

                if (fieldEl.TryGetProperty("input_name", out var inputNameEl) && inputNameEl.ValueKind == JsonValueKind.String)
                    field.InputName = inputNameEl.GetString();

                if (fieldEl.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
                {
                    field.Options = optionsEl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();
                }

                if (fieldEl.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Number)
                    field.Rows = rowsEl.GetInt32();

                if (fieldEl.TryGetProperty("collapsible", out var collapsibleEl))
                    field.Collapsible = collapsibleEl.ValueKind == JsonValueKind.True;

                if (fieldEl.TryGetProperty("fields", out var nestedFieldsEl) && nestedFieldsEl.ValueKind == JsonValueKind.Array)
                    field.Fields = ParseFieldSchemas(nestedFieldsEl);

                result.Add(field);
            }

            return result;
        }

        private object? ParseScribanDefaultValue(string valueStr)
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

        #endregion
    }

    /// <summary>
    /// Interface for fragment schema parsing and caching.
    /// </summary>
    public interface IFragmentSchemaService
    {
        /// <summary>
        /// Parses a fragment text to extract its UI schema.
        /// </summary>
        FragmentSchema? ParseFragmentSchema(string fragmentText);

        /// <summary>
        /// Gets the cached schema for a fragment file, parsing if not cached.
        /// </summary>
        FragmentSchema? GetFragmentSchema(string fragmentFile);

        /// <summary>
        /// Gets all fragment schemas for a workflow's pipeline.
        /// </summary>
        Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow);

        /// <summary>
        /// Parses default values from a fragment file body.
        /// </summary>
        Dictionary<string, object?> ParseFragmentDefaults(string fragmentFile);

        /// <summary>
        /// Clears the schema cache.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Validates a fragment's schema and returns any errors found.
        /// </summary>
        /// <param name="fragmentFile">The fragment file path (relative to Fragments folder).</param>
        /// <returns>List of validation error messages, empty if valid.</returns>
        List<string> ValidateFragmentSchema(string fragmentFile);

        /// <summary>
        /// Gets all fragment files in the Fragments directory.
        /// </summary>
        /// <returns>List of fragment file paths relative to the Fragments folder.</returns>
        List<string> GetAllFragmentFiles();

        /// <summary>
        /// Validates all fragment schemas in the Fragments directory.
        /// </summary>
        /// <returns>Dictionary of fragment file paths to their validation errors.</returns>
        Dictionary<string, List<string>> ValidateAllFragmentSchemas();
    }
}
