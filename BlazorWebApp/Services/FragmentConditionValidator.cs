using System.Text.Json;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Validates and standardizes fragment condition checks.
    /// Ensures conditions always match fragment ID conventions.
    /// </summary>
    public class FragmentConditionValidator
    {
        private readonly ILogger<FragmentConditionValidator> _logger;

        public FragmentConditionValidator(ILogger<FragmentConditionValidator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates that all condition paths in a fragment match the expected fragment ID.
        /// Returns validation errors if mismatches are found.
        /// </summary>
        public List<string> ValidateConditions(string fragmentId, Dictionary<string, JsonElement>? conditions)
        {
            var errors = new List<string>();

            if (conditions == null || conditions.Count == 0)
                return errors;

            // Check required conditions
            if (conditions.TryGetValue("required", out var requiredEl) && requiredEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var condition in requiredEl.EnumerateArray())
                {
                    if (condition.ValueKind == JsonValueKind.String)
                    {
                        var conditionPath = condition.GetString();
                        var error = ValidateConditionPath(fragmentId, conditionPath, "required");
                        if (error != null)
                            errors.Add(error);
                    }
                }
            }

            // Check excluded_if conditions
            if (conditions.TryGetValue("excluded_if", out var excludedEl) && excludedEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var condition in excludedEl.EnumerateArray())
                {
                    if (condition.ValueKind == JsonValueKind.String)
                    {
                        var conditionPath = condition.GetString();
                        var error = ValidateConditionPath(fragmentId, conditionPath, "excluded_if");
                        if (error != null)
                            errors.Add(error);
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates a single condition path matches expected format.
        /// </summary>
        private string? ValidateConditionPath(string fragmentId, string? conditionPath, string conditionType)
        {
            if (string.IsNullOrEmpty(conditionPath))
                return $"Empty condition path in {conditionType}";

            // Check if it's a simple parameter condition (e.g., "append_preview")
            if (!conditionPath.Contains('.'))
            {
                // Simple boolean parameter - valid format
                // Should be snake_case
                if (conditionPath != conditionPath.ToLowerInvariant())
                {
                    return $"Parameter condition '{conditionPath}' should be lowercase snake_case";
                }
                return null; // Valid parameter condition
            }

            var parts = conditionPath.Split('.');
            if (parts.Length != 2)
                return $"Invalid condition format '{conditionPath}' in {conditionType}. Expected format: 'fragment_id.Property' or 'parameter_name'";

            var conditionFragmentId = parts[0];
            var conditionProperty = parts[1];

            // Check if condition uses snake_case
            if (conditionFragmentId != conditionFragmentId.ToLowerInvariant() || 
                conditionFragmentId.Contains('-'))
            {
                return $"Condition '{conditionPath}' should use snake_case (got '{conditionFragmentId}')";
            }

            // Property should be PascalCase
            if (conditionProperty != "IsActive" && !char.IsUpper(conditionProperty[0]))
            {
                return $"Condition property '{conditionProperty}' should be PascalCase";
            }

            return null;
        }

        /// <summary>
        /// Normalizes a fragment filename to its expected ID format.
        /// Example: "detailer-core.sbn" -> "detailer_core"
        /// </summary>
        public static string NormalizeFragmentId(string fragmentFile)
        {
            return Path.GetFileNameWithoutExtension(fragmentFile)
                .Replace("-", "_")
                .ToLowerInvariant();
        }

        /// <summary>
        /// Generates a standard condition dictionary for a fragment.
        /// </summary>
        public Dictionary<string, object> GenerateCondition(string fragmentId, bool isRequired = true)
        {
            var conditionPath = $"{fragmentId}.IsActive";
            
            return new Dictionary<string, object>
            {
                {
                    isRequired ? "required" : "excluded_if",
                    new List<string> { conditionPath }
                }
            };
        }

        /// <summary>
        /// Generates a scoped condition for fragments like load-diffusion-w-prompts.
        /// Returns null if scope doesn't match the parent fragment.
        /// </summary>
        public Dictionary<string, object>? GenerateScopedCondition(
            string? scope, 
            string parentFragmentId)
        {
            if (string.IsNullOrEmpty(scope))
                return null;

            // Check if scope relates to the parent fragment
            // E.g., scope="detailer_" relates to parentFragmentId="detailer"
            if (scope.TrimEnd('_').Equals(parentFragmentId, StringComparison.OrdinalIgnoreCase))
            {
                return GenerateCondition(parentFragmentId);
            }

            return null;
        }

        /// <summary>
        /// Logs validation errors for debugging.
        /// </summary>
        public void LogValidationErrors(string fragmentFile, List<string> errors)
        {
            if (errors.Count == 0)
                return;

            _logger.LogWarning(
                "Fragment '{FragmentFile}' has {ErrorCount} condition validation errors:\n{Errors}",
                fragmentFile,
                errors.Count,
                string.Join("\n  - ", errors));
        }
    }
}
