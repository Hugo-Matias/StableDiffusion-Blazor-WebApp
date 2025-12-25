using System.Text.Json;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Templating.Pipeline;

/// <summary>
/// Processes $if markers in pipeline steps, conditionally including or excluding steps.
/// 
/// Example input:
/// {
///   "$if": "frame_interpolation.IsActive",
///   "id": "frame_interp",
///   "fragment": "frame-interpolation.liquid",
///   "parameters": { ... }
/// }
/// </summary>
public class ConditionalProcessor : IPipelineProcessor
{
    private readonly ILogger<ConditionalProcessor> _logger;

    public ConditionalProcessor(ILogger<ConditionalProcessor> logger)
    {
        _logger = logger;
    }

    public bool CanProcess(JsonElement step)
    {
        return step.TryGetProperty("$if", out _);
    }

    public IEnumerable<ExpandedPipelineStep> Process(
        JsonElement step,
        GenerationParameters parameters,
        ComputeRegistry computeRegistry)
    {
        if (!step.TryGetProperty("$if", out var conditionEl))
        {
            _logger.LogWarning("$if step missing condition property");
            yield break;
        }

        var conditionPath = conditionEl.GetString();
        if (string.IsNullOrEmpty(conditionPath))
        {
            _logger.LogWarning("$if condition is empty");
            yield break;
        }

        var conditionResult = EvaluateCondition(conditionPath, parameters);
        
        if (!conditionResult)
        {
            _logger.LogDebug("$if condition '{ConditionPath}' evaluated to false, skipping step", conditionPath);
            yield break;
        }

        _logger.LogDebug("$if condition '{ConditionPath}' evaluated to true, including step", conditionPath);

        // Extract the step without the $if property
        var expandedStep = ExtractStep(step, computeRegistry, parameters);
        if (expandedStep != null)
        {
            yield return expandedStep;
        }
    }

    private bool EvaluateCondition(string conditionPath, GenerationParameters parameters)
    {
        // Handle fragment.IsActive pattern (e.g., "frame_interpolation.IsActive")
        if (conditionPath.EndsWith(".IsActive", StringComparison.OrdinalIgnoreCase))
        {
            var fragmentId = conditionPath.Substring(0, conditionPath.Length - ".IsActive".Length);
            
            if (parameters.Fragments.TryGetValue(fragmentId, out var fragmentParams))
            {
                return fragmentParams.IsActive;
            }

            // Also try with underscores converted to match fragment IDs
            var normalizedId = fragmentId.Replace("-", "_");
            if (parameters.Fragments.TryGetValue(normalizedId, out fragmentParams))
            {
                return fragmentParams.IsActive;
            }

            _logger.LogDebug("Fragment '{FragmentId}' not found in parameters, condition is false", fragmentId);
            return false;
        }

        // Handle simple boolean property paths
        var parts = conditionPath.Split('.');
        object? current = parameters;

        foreach (var part in parts)
        {
            if (current == null) return false;

            var prop = current.GetType().GetProperty(part, 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.IgnoreCase);

            if (prop == null)
            {
                // Try dictionary access for Fragments
                if (current is IDictionary<string, object> dict && dict.TryGetValue(part, out var dictValue))
                {
                    current = dictValue;
                    continue;
                }

                _logger.LogDebug("Property '{Part}' not found in condition path '{ConditionPath}'", part, conditionPath);
                return false;
            }

            current = prop.GetValue(current);
        }

        return current is bool boolValue && boolValue;
    }

    private ExpandedPipelineStep? ExtractStep(
        JsonElement step,
        ComputeRegistry computeRegistry,
        GenerationParameters parameters)
    {
        // Get id
        var id = step.TryGetProperty("id", out var idEl)
            ? idEl.GetString() ?? ""
            : "";

        // Get fragment
        var fragment = step.TryGetProperty("fragment", out var fragEl)
            ? fragEl.GetString() ?? ""
            : "";

        if (string.IsNullOrEmpty(fragment))
        {
            _logger.LogWarning("$if step missing fragment property");
            return null;
        }

        // Extract parameters
        var expandedParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (step.TryGetProperty("parameters", out var paramsEl) &&
            paramsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in paramsEl.EnumerateObject())
            {
                var value = ExtractParameterValue(prop.Value, computeRegistry, parameters);
                expandedParams[prop.Name] = value;
            }
        }

        return new ExpandedPipelineStep
        {
            Id = id,
            Fragment = fragment,
            Parameters = expandedParams
        };
    }

    private object? ExtractParameterValue(
        JsonElement value,
        ComputeRegistry computeRegistry,
        GenerationParameters parameters)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var strValue = value.GetString() ?? "";

            // Check for $compute marker
            if (ComputeRegistry.IsComputeMarker(strValue))
            {
                return computeRegistry.ResolveValue(strValue, parameters);
            }

            return strValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var i) ? i : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }
}
