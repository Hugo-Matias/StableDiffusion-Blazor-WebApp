namespace BlazorWebApp.Services
{
    /// <summary>
    /// Represents a validation error found in a workflow or fragment template.
    /// </summary>
    public class TemplateValidationError
    {
        /// <summary>
        /// The file path where the error was found.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// The type of error (Workflow, Fragment, Schema, Scriban).
        /// </summary>
        public TemplateErrorType ErrorType { get; init; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Optional line number where the error occurred.
        /// </summary>
        public int? LineNumber { get; init; }

        /// <summary>
        /// Optional exception that caused the error.
        /// </summary>
        public Exception? Exception { get; init; }

        public override string ToString()
        {
            var location = LineNumber.HasValue ? $":{LineNumber}" : "";
            return $"[{ErrorType}] {FilePath}{location}: {Message}";
        }
    }

    /// <summary>
    /// Types of template validation errors.
    /// </summary>
    public enum TemplateErrorType
    {
        /// <summary>
        /// Error in workflow template structure (missing Title, Base, Mode, etc.).
        /// </summary>
        WorkflowStructure,

        /// <summary>
        /// Error in Pipeline definition (missing fragment, invalid ID, etc.).
        /// </summary>
        Pipeline,

        /// <summary>
        /// Error in Assets array definition.
        /// </summary>
        Assets,

        /// <summary>
        /// Error in Sources array definition.
        /// </summary>
        Sources,

        /// <summary>
        /// Referenced fragment file not found.
        /// </summary>
        FragmentNotFound,

        /// <summary>
        /// Error parsing fragment #meta block.
        /// </summary>
        FragmentSchema,

        /// <summary>
        /// Error in fragment UI schema (invalid constraints, missing component, etc.).
        /// </summary>
        SchemaConstraints,

        /// <summary>
        /// Scriban template syntax error.
        /// </summary>
        ScribanSyntax,

        /// <summary>
        /// Component referenced in schema not registered.
        /// </summary>
        ComponentNotFound
    }

    /// <summary>
    /// Result of template validation containing all errors found.
    /// </summary>
    public class TemplateValidationResult
    {
        /// <summary>
        /// List of all validation errors found.
        /// </summary>
        public List<TemplateValidationError> Errors { get; } = new();

        /// <summary>
        /// List of all validation warnings (non-critical issues).
        /// </summary>
        public List<TemplateValidationError> Warnings { get; } = new();

        /// <summary>
        /// Whether validation passed with no errors.
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Total number of workflow templates validated.
        /// </summary>
        public int WorkflowCount { get; set; }

        /// <summary>
        /// Total number of fragment files validated.
        /// </summary>
        public int FragmentCount { get; set; }

        /// <summary>
        /// Adds an error to the result.
        /// </summary>
        public void AddError(string filePath, TemplateErrorType errorType, string message, int? lineNumber = null, Exception? exception = null)
        {
            Errors.Add(new TemplateValidationError
            {
                FilePath = filePath,
                ErrorType = errorType,
                Message = message,
                LineNumber = lineNumber,
                Exception = exception
            });
        }

        /// <summary>
        /// Adds a warning to the result.
        /// </summary>
        public void AddWarning(string filePath, TemplateErrorType errorType, string message, int? lineNumber = null)
        {
            Warnings.Add(new TemplateValidationError
            {
                FilePath = filePath,
                ErrorType = errorType,
                Message = message,
                LineNumber = lineNumber
            });
        }
    }

    /// <summary>
    /// Service for validating workflow and fragment templates at startup.
    /// </summary>
    public interface IWorkflowValidationService
    {
        /// <summary>
        /// Validates all workflow templates and their referenced fragments.
        /// </summary>
        /// <returns>Validation result containing all errors and warnings.</returns>
        TemplateValidationResult ValidateAllTemplates();

        /// <summary>
        /// Validates a single workflow template.
        /// </summary>
        /// <param name="templatePath">Full path to the workflow template file.</param>
        /// <returns>Validation result for this template.</returns>
        TemplateValidationResult ValidateWorkflowTemplate(string templatePath);

        /// <summary>
        /// Validates a single fragment file.
        /// </summary>
        /// <param name="fragmentPath">Full path to the fragment file.</param>
        /// <returns>Validation result for this fragment.</returns>
        TemplateValidationResult ValidateFragment(string fragmentPath);

        /// <summary>
        /// Pre-compiles all Scriban templates and caches them.
        /// Reports syntax errors with line numbers.
        /// </summary>
        /// <returns>Validation result containing any Scriban syntax errors.</returns>
        TemplateValidationResult PrecompileTemplates();
    }
}
