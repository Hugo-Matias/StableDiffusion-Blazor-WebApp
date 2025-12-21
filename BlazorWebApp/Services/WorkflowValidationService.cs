using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Scriban;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for validating workflow and fragment templates at startup.
    /// Catches template errors early to provide better developer experience.
    /// </summary>
    public class WorkflowValidationService : IWorkflowValidationService
    {
        private readonly string _workflowPath;
        private readonly string _templatesPath;
        private readonly string _fragmentsPath;
        private readonly ILogger<WorkflowValidationService> _logger;
        private readonly IComponentRegistry _componentRegistry;
        private readonly IFragmentSchemaService _fragmentSchemaService;
        private readonly IIOService _io;

        // Cache of compiled Scriban templates: path -> Template
        private readonly Dictionary<string, Template> _compiledTemplates = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _templateCacheLock = new();

        public WorkflowValidationService(
            ILogger<WorkflowValidationService> logger,
            IComponentRegistry componentRegistry,
            IFragmentSchemaService fragmentSchemaService,
            IIOService io)
        {
            _logger = logger;
            _componentRegistry = componentRegistry;
            _fragmentSchemaService = fragmentSchemaService;
            _io = io;

            _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
            _templatesPath = Path.Combine(_workflowPath, "Templates");
            _fragmentsPath = Path.Combine(_workflowPath, "Fragments");
        }

        /// <inheritdoc />
        public TemplateValidationResult ValidateAllTemplates()
        {
            var result = new TemplateValidationResult();
            var validatedFragments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Starting workflow template validation...");

            // Validate all workflow templates
            var workflowFiles = _io.GetFilesRecursive(_templatesPath, ignorePath: "utils", extensionsWhitelist: new() { ".sbn" });
            result.WorkflowCount = workflowFiles.Count();

            foreach (var file in workflowFiles)
            {
                var workflowResult = ValidateWorkflowTemplate(file.FullName);
                result.Errors.AddRange(workflowResult.Errors);
                result.Warnings.AddRange(workflowResult.Warnings);

                // Track fragments referenced by this workflow
                var fragments = ExtractFragmentReferences(file.FullName);
                foreach (var frag in fragments)
                {
                    validatedFragments.Add(frag);
                }
            }

            // Validate all referenced fragments
            foreach (var fragmentFile in validatedFragments)
            {
                var fragPath = Path.Combine(_fragmentsPath, fragmentFile.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fragPath))
                {
                    var fragResult = ValidateFragment(fragPath);
                    result.Errors.AddRange(fragResult.Errors);
                    result.Warnings.AddRange(fragResult.Warnings);
                    result.FragmentCount++;
                }
            }

            // Pre-compile all templates
            var precompileResult = PrecompileTemplates();
            result.Errors.AddRange(precompileResult.Errors);
            result.Warnings.AddRange(precompileResult.Warnings);

            // Log summary
            if (result.IsValid)
            {
                _logger.LogInformation(
                    "Template validation passed: {WorkflowCount} workflows, {FragmentCount} fragments, {WarningCount} warnings",
                    result.WorkflowCount, result.FragmentCount, result.Warnings.Count);
            }
            else
            {
                _logger.LogError(
                    "Template validation failed: {ErrorCount} errors, {WarningCount} warnings",
                    result.Errors.Count, result.Warnings.Count);

                foreach (var error in result.Errors)
                {
                    _logger.LogError("{Error}", error.ToString());
                }
            }

            return result;
        }

        /// <inheritdoc />
        public TemplateValidationResult ValidateWorkflowTemplate(string templatePath)
        {
            var result = new TemplateValidationResult();
            var relativePath = GetRelativePath(templatePath);

            if (!File.Exists(templatePath))
            {
                result.AddError(relativePath, TemplateErrorType.WorkflowStructure, "Template file not found");
                return result;
            }

            string templateText;
            try
            {
                templateText = File.ReadAllText(templatePath);
            }
            catch (Exception ex)
            {
                result.AddError(relativePath, TemplateErrorType.WorkflowStructure, $"Failed to read file: {ex.Message}", exception: ex);
                return result;
            }

            // Validate required metadata fields
            ValidateWorkflowMetadata(templateText, relativePath, result);

            // Validate Assets array
            ValidateAssetsArray(templateText, relativePath, result);

            // Validate Sources array
            ValidateSourcesArray(templateText, relativePath, result);

            // Validate Pipeline array
            ValidatePipelineArray(templateText, relativePath, result);

            return result;
        }

        /// <inheritdoc />
        public TemplateValidationResult ValidateFragment(string fragmentPath)
        {
            var result = new TemplateValidationResult();
            var relativePath = GetRelativePath(fragmentPath);

            if (!File.Exists(fragmentPath))
            {
                result.AddError(relativePath, TemplateErrorType.FragmentNotFound, "Fragment file not found");
                return result;
            }

            string fragmentText;
            try
            {
                fragmentText = File.ReadAllText(fragmentPath);
            }
            catch (Exception ex)
            {
                result.AddError(relativePath, TemplateErrorType.FragmentSchema, $"Failed to read file: {ex.Message}", exception: ex);
                return result;
            }

            // Validate #meta block if present
            var metaMatch = Regex.Match(fragmentText, @"#meta\s*([\s\S]*?)\s*#end");
            if (metaMatch.Success)
            {
                ValidateMetaBlock(metaMatch.Groups[1].Value, relativePath, result);
            }

            // Use FragmentSchemaService for deeper schema validation
            var fragmentFile = GetFragmentRelativePath(fragmentPath);
            if (!string.IsNullOrEmpty(fragmentFile))
            {
                var schemaErrors = _fragmentSchemaService.ValidateFragmentSchema(fragmentFile);
                foreach (var error in schemaErrors)
                {
                    result.AddError(relativePath, TemplateErrorType.SchemaConstraints, error);
                }
            }

            // Validate Scriban syntax in fragment body
            var bodyText = metaMatch.Success
                ? fragmentText.Replace(metaMatch.Value, "")
                : fragmentText;

            ValidateScribanSyntax(bodyText, relativePath, result);

            return result;
        }

        /// <inheritdoc />
        public TemplateValidationResult PrecompileTemplates()
        {
            var result = new TemplateValidationResult();

            // Pre-compile workflow templates
            var workflowFiles = _io.GetFilesRecursive(_templatesPath, ignorePath: "utils", extensionsWhitelist: new() { ".sbn" });
            foreach (var file in workflowFiles)
            {
                PrecompileTemplate(file.FullName, result);
            }

            // Pre-compile fragment templates
            var fragmentFiles = _io.GetFilesRecursive(_fragmentsPath, extensionsWhitelist: new() { ".sbn" });
            foreach (var file in fragmentFiles)
            {
                PrecompileTemplate(file.FullName, result);
            }

            return result;
        }

        #region Private Validation Methods

        private void ValidateWorkflowMetadata(string templateText, string relativePath, TemplateValidationResult result)
        {
            // Check for Title
            if (!Regex.IsMatch(templateText, @"""title""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase))
            {
                result.AddError(relativePath, TemplateErrorType.WorkflowStructure, "Missing required 'title' property");
            }

            // Check for Base
            var baseMatch = Regex.Match(templateText, @"""base""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (!baseMatch.Success)
            {
                result.AddError(relativePath, TemplateErrorType.WorkflowStructure, "Missing required 'base' property");
            }
            else
            {
                var baseValue = baseMatch.Groups[1].Value;
                if (!Enum.TryParse<ModelBase>(baseValue, true, out _))
                {
                    result.AddError(relativePath, TemplateErrorType.WorkflowStructure,
                        $"Invalid 'base' value: '{baseValue}'. Must be one of: {string.Join(", ", Enum.GetNames<ModelBase>())}");
                }
            }

            // Check for Mode
            var modeMatch = Regex.Match(templateText, @"""mode""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (!modeMatch.Success)
            {
                result.AddError(relativePath, TemplateErrorType.WorkflowStructure, "Missing required 'mode' property");
            }
            else
            {
                var modeValue = modeMatch.Groups[1].Value;
                // ModeType is in Data.Entities namespace
                if (!Enum.TryParse<ModeType>(modeValue, true, out _))
                {
                    result.AddError(relativePath, TemplateErrorType.WorkflowStructure,
                        $"Invalid 'mode' value: '{modeValue}'. Must be one of: {string.Join(", ", Enum.GetNames<ModeType>())}");
                }
            }

            // Check for Pipeline array
            if (!Regex.IsMatch(templateText, @"""Pipeline""\s*:\s*\[", RegexOptions.IgnoreCase))
            {
                result.AddError(relativePath, TemplateErrorType.Pipeline, "Missing required 'Pipeline' array");
            }
        }

        private void ValidateAssetsArray(string templateText, string relativePath, TemplateValidationResult result)
        {
            var assetsMatch = Regex.Match(templateText, @"""Assets""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!assetsMatch.Success)
            {
                // Assets are optional for some workflows
                return;
            }

            var assetsContent = assetsMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(assetsContent))
                return;

            // Validate each asset entry
            var assetMatches = Regex.Matches(assetsContent, @"\{([^{}]*)\}", RegexOptions.Singleline);
            var assetIndex = 0;

            foreach (Match assetMatch in assetMatches)
            {
                assetIndex++;
                var assetContent = assetMatch.Groups[1].Value;

                // Check for required parameter
                if (!Regex.IsMatch(assetContent, @"""parameter""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase))
                {
                    result.AddError(relativePath, TemplateErrorType.Assets, $"Asset #{assetIndex} missing required 'parameter' property");
                }

                // Check for required type
                var typeMatch = Regex.Match(assetContent, @"""type""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (!typeMatch.Success)
                {
                    result.AddError(relativePath, TemplateErrorType.Assets, $"Asset #{assetIndex} missing required 'type' property");
                }
                else
                {
                    var typeValue = typeMatch.Groups[1].Value;
                    if (!Enum.TryParse<AssetType>(typeValue, true, out _))
                    {
                        result.AddWarning(relativePath, TemplateErrorType.Assets,
                            $"Asset #{assetIndex} has unknown type '{typeValue}'");
                    }
                }
            }
        }

        private void ValidateSourcesArray(string templateText, string relativePath, TemplateValidationResult result)
        {
            var sourcesMatch = Regex.Match(templateText, @"""Sources""\s*:\s*\[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!sourcesMatch.Success)
            {
                // Sources are optional
                return;
            }

            var sourcesContent = sourcesMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(sourcesContent))
                return;

            // Validate each source entry
            var sourceMatches = Regex.Matches(sourcesContent, @"\{([^{}]*)\}", RegexOptions.Singleline);
            var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceIndex = 0;

            foreach (Match sourceMatch in sourceMatches)
            {
                sourceIndex++;
                var sourceContent = sourceMatch.Groups[1].Value;

                // Check for required id
                var idMatch = Regex.Match(sourceContent, @"""id""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (!idMatch.Success)
                {
                    result.AddError(relativePath, TemplateErrorType.Sources, $"Source #{sourceIndex} missing required 'id' property");
                }
                else
                {
                    var sourceId = idMatch.Groups[1].Value;
                    if (!sourceIds.Add(sourceId))
                    {
                        result.AddError(relativePath, TemplateErrorType.Sources, $"Duplicate source id: '{sourceId}'");
                    }
                }
            }
        }

        private void ValidatePipelineArray(string templateText, string relativePath, TemplateValidationResult result)
        {
            // Extract Pipeline array content
            var pipelineMatch = Regex.Match(templateText, @"""Pipeline""\s*:\s*\[", RegexOptions.IgnoreCase);
            if (!pipelineMatch.Success)
                return;

            var startIndex = pipelineMatch.Index + pipelineMatch.Length;
            var bracketCount = 1;
            var endIndex = startIndex;

            for (var i = startIndex; i < templateText.Length && bracketCount > 0; i++)
            {
                if (templateText[i] == '[') bracketCount++;
                else if (templateText[i] == ']') bracketCount--;
                endIndex = i;
            }

            var pipelineContent = templateText.Substring(startIndex, endIndex - startIndex);

            // Find all pipeline steps
            var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stepStart = -1;
            var braceDepth = 0;
            var stepIndex = 0;

            for (var i = 0; i < pipelineContent.Length; i++)
            {
                var c = pipelineContent[i];

                if (c == '{')
                {
                    if (braceDepth == 0) stepStart = i;
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0 && stepStart >= 0)
                    {
                        stepIndex++;
                        var stepContent = pipelineContent.Substring(stepStart, i - stepStart + 1);

                        // Validate this step
                        ValidatePipelineStep(stepContent, stepIndex, relativePath, result, stepIds);
                        stepStart = -1;
                    }
                }
            }

            if (stepIndex == 0)
            {
                result.AddWarning(relativePath, TemplateErrorType.Pipeline, "Pipeline array is empty");
            }
        }

        private void ValidatePipelineStep(string stepContent, int stepIndex, string relativePath, TemplateValidationResult result, HashSet<string> stepIds)
        {
            // Check for fragment reference
            var fragmentMatch = Regex.Match(stepContent, @"""fragment""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (!fragmentMatch.Success)
            {
                result.AddError(relativePath, TemplateErrorType.Pipeline, $"Pipeline step #{stepIndex} missing required 'fragment' property");
                return;
            }

            var fragmentFile = fragmentMatch.Groups[1].Value;
            var fragmentPath = Path.Combine(_fragmentsPath, fragmentFile.Replace('/', Path.DirectorySeparatorChar));

            // Check fragment exists
            if (!File.Exists(fragmentPath))
            {
                result.AddError(relativePath, TemplateErrorType.FragmentNotFound,
                    $"Pipeline step #{stepIndex} references non-existent fragment: '{fragmentFile}'");
            }

            // Check for step ID (optional but recommended)
            var idMatch = Regex.Match(stepContent, @"""id""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                var stepId = idMatch.Groups[1].Value;
                if (!stepIds.Add(stepId))
                {
                    result.AddError(relativePath, TemplateErrorType.Pipeline, $"Duplicate pipeline step id: '{stepId}'");
                }
            }
            else
            {
                // Generate ID from fragment filename for warning
                var inferredId = Path.GetFileNameWithoutExtension(fragmentFile).Replace("-", "_");
                result.AddWarning(relativePath, TemplateErrorType.Pipeline,
                    $"Pipeline step #{stepIndex} (fragment: {fragmentFile}) has no explicit 'id'. Will use inferred ID: '{inferredId}'");
            }
        }

        private void ValidateMetaBlock(string metaContent, string relativePath, TemplateValidationResult result)
        {
            // Clean up trailing commas before parsing
            metaContent = Regex.Replace(metaContent, @",\s*(\}|])", "$1", RegexOptions.Singleline);

            try
            {
                using var doc = JsonDocument.Parse(metaContent);
                var root = doc.RootElement;

                // Validate ui section if present
                if (root.TryGetProperty("ui", out var uiEl))
                {
                    ValidateUiSchema(uiEl, relativePath, result);
                }
            }
            catch (JsonException ex)
            {
                // Find approximate line number
                var lineNumber = CountLines(metaContent, ex.BytePositionInLine ?? 0);
                result.AddError(relativePath, TemplateErrorType.FragmentSchema,
                    $"Invalid JSON in #meta block: {ex.Message}", lineNumber, ex);
            }
        }

        private void ValidateUiSchema(JsonElement uiEl, string relativePath, TemplateValidationResult result)
        {
            // Validate component reference if specified
            if (uiEl.TryGetProperty("component", out var componentEl) && componentEl.ValueKind == JsonValueKind.String)
            {
                var componentName = componentEl.GetString();
                if (!string.IsNullOrEmpty(componentName) && !_componentRegistry.IsRegistered(componentName))
                {
                    result.AddWarning(relativePath, TemplateErrorType.ComponentNotFound,
                        $"UI schema references unregistered component: '{componentName}'");
                }
            }

            // Validate parameter constraints
            if (uiEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var param in paramsEl.EnumerateObject())
                {
                    ValidateParameterConstraints(param.Name, param.Value, relativePath, result);
                }
            }

            // Validate fields if present
            if (uiEl.TryGetProperty("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Array)
            {
                var fieldIndex = 0;
                foreach (var field in fieldsEl.EnumerateArray())
                {
                    fieldIndex++;
                    ValidateFieldSchema(field, fieldIndex, relativePath, result);
                }
            }
        }

        private void ValidateParameterConstraints(string paramName, JsonElement constraints, string relativePath, TemplateValidationResult result)
        {
            if (constraints.ValueKind != JsonValueKind.Object)
                return;

            double? min = null, max = null, step = null;

            if (constraints.TryGetProperty("min", out var minEl) && minEl.ValueKind == JsonValueKind.Number)
                min = minEl.GetDouble();

            if (constraints.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number)
                max = maxEl.GetDouble();

            if (constraints.TryGetProperty("step", out var stepEl) && stepEl.ValueKind == JsonValueKind.Number)
                step = stepEl.GetDouble();

            // Validate min < max
            if (min.HasValue && max.HasValue && min.Value >= max.Value)
            {
                result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                    $"Parameter '{paramName}' has invalid constraints: min ({min}) must be less than max ({max})");
            }

            // Validate step > 0
            if (step.HasValue && step.Value <= 0)
            {
                result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                    $"Parameter '{paramName}' has invalid step value: {step} (must be greater than 0)");
            }

            // Validate dynamic source has input_name
            if (constraints.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind == JsonValueKind.String)
            {
                if (!constraints.TryGetProperty("input_name", out _))
                {
                    result.AddWarning(relativePath, TemplateErrorType.SchemaConstraints,
                        $"Parameter '{paramName}' has 'source' but no 'input_name' for dynamic resolution");
                }
            }
        }

        private void ValidateFieldSchema(JsonElement field, int fieldIndex, string relativePath, TemplateValidationResult result)
        {
            if (field.ValueKind != JsonValueKind.Object)
                return;

            // Check for parameter (required for non-group fields)
            var hasParameter = field.TryGetProperty("parameter", out var paramEl) && 
                               paramEl.ValueKind == JsonValueKind.String && 
                               !string.IsNullOrEmpty(paramEl.GetString());

            // Check for type (required)
            var hasType = field.TryGetProperty("type", out var typeEl) && 
                          typeEl.ValueKind == JsonValueKind.String;
            var fieldType = hasType ? typeEl.GetString() : null;

            if (fieldType != "group" && !hasParameter)
            {
                result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                    $"Field #{fieldIndex} missing required 'parameter' property");
            }

            if (!hasType)
            {
                result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                    $"Field #{fieldIndex} missing required 'type' property");
            }

            // Validate select fields have options or source
            if (fieldType == "select")
            {
                var hasOptions = field.TryGetProperty("options", out var optionsEl) && 
                                 optionsEl.ValueKind == JsonValueKind.Array && 
                                 optionsEl.GetArrayLength() > 0;
                var hasSource = field.TryGetProperty("source", out _);

                if (!hasOptions && !hasSource)
                {
                    result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                        $"Select field #{fieldIndex} requires 'options' array or 'source' for dynamic loading");
                }
            }

            // Validate slider fields have min/max
            if (fieldType == "slider")
            {
                if (!field.TryGetProperty("min", out _))
                {
                    result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                        $"Slider field #{fieldIndex} requires 'min' property");
                }
                if (!field.TryGetProperty("max", out _))
                {
                    result.AddError(relativePath, TemplateErrorType.SchemaConstraints,
                        $"Slider field #{fieldIndex} requires 'max' property");
                }
            }
        }

        private void ValidateScribanSyntax(string templateText, string relativePath, TemplateValidationResult result)
        {
            var template = Template.Parse(templateText);

            if (template.HasErrors)
            {
                foreach (var message in template.Messages)
                {
                    if (message.Type == Scriban.Parsing.ParserMessageType.Error)
                    {
                        result.AddError(relativePath, TemplateErrorType.ScribanSyntax,
                            message.Message, message.Span.Start.Line);
                    }
                    else
                    {
                        result.AddWarning(relativePath, TemplateErrorType.ScribanSyntax,
                            message.Message, message.Span.Start.Line);
                    }
                }
            }
        }

        private void PrecompileTemplate(string templatePath, TemplateValidationResult result)
        {
            var relativePath = GetRelativePath(templatePath);

            try
            {
                var templateText = File.ReadAllText(templatePath);

                // For fragments, remove #meta block before compiling
                if (templatePath.Contains("Fragments"))
                {
                    templateText = Regex.Replace(templateText, @"#meta\s*[\s\S]*?\s*#end", "", RegexOptions.None);
                }

                var template = Template.Parse(templateText);

                if (template.HasErrors)
                {
                    foreach (var message in template.Messages.Where(m => m.Type == Scriban.Parsing.ParserMessageType.Error))
                    {
                        result.AddError(relativePath, TemplateErrorType.ScribanSyntax,
                            message.Message, message.Span.Start.Line);
                    }
                }
                else
                {
                    // Cache the compiled template
                    lock (_templateCacheLock)
                    {
                        _compiledTemplates[templatePath] = template;
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError(relativePath, TemplateErrorType.ScribanSyntax,
                    $"Failed to pre-compile template: {ex.Message}", exception: ex);
            }
        }

        #endregion

        #region Helper Methods

        private List<string> ExtractFragmentReferences(string templatePath)
        {
            var fragments = new List<string>();

            try
            {
                var templateText = File.ReadAllText(templatePath);
                var matches = Regex.Matches(templateText, @"""fragment""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    fragments.Add(match.Groups[1].Value);
                }
            }
            catch
            {
                // Ignore read errors - will be caught in validation
            }

            return fragments;
        }

        private string GetRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(_workflowPath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(_workflowPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            return Path.GetFileName(fullPath);
        }

        private string GetFragmentRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(_fragmentsPath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fullPath.Substring(_fragmentsPath.Length).TrimStart(Path.DirectorySeparatorChar);
                return relativePath.Replace(Path.DirectorySeparatorChar, '/');
            }
            return string.Empty;
        }

        private static int CountLines(string text, long position)
        {
            var lineCount = 1;
            var pos = 0;
            foreach (var c in text)
            {
                if (pos >= position) break;
                if (c == '\n') lineCount++;
                pos++;
            }
            return lineCount;
        }

        #endregion
    }
}
