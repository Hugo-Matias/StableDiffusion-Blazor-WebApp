using System.Text.Json;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Templating.Pipeline;

/// <summary>
/// Orchestrates pipeline expansion by applying all registered processors.
/// Handles the full pipeline array, expanding $foreach, $if, and $compute markers.
/// </summary>
public class PipelineExpander
{
    private readonly ILogger<PipelineExpander> _logger;
    private readonly IEnumerable<IPipelineProcessor> _processors;
    private readonly ComputeRegistry _computeRegistry;

    public PipelineExpander(
        ILogger<PipelineExpander> logger,
        IEnumerable<IPipelineProcessor> processors,
        ComputeRegistry computeRegistry)
    {
        _logger = logger;
        _processors = processors;
        _computeRegistry = computeRegistry;
    }

    /// <summary>
    /// Expands a pipeline array, processing all markers.
    /// </summary>
    /// <param name="pipelineJson">The Pipeline array as a JSON string.</param>
    /// <param name="parameters">The generation parameters.</param>
    /// <returns>List of expanded pipeline steps ready for fragment rendering.</returns>
    public List<ExpandedPipelineStep> ExpandPipeline(string pipelineJson, GenerationParameters parameters)
    {
        var result = new List<ExpandedPipelineStep>();

        try
        {
            using var doc = JsonDocument.Parse(pipelineJson);
            return ExpandPipeline(doc.RootElement, parameters);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse pipeline JSON");
            return result;
        }
    }

    /// <summary>
    /// Expands a pipeline array from a JsonElement.
    /// </summary>
    /// <param name="pipelineArray">The Pipeline array as a JsonElement.</param>
    /// <param name="parameters">The generation parameters.</param>
    /// <returns>List of expanded pipeline steps ready for fragment rendering.</returns>
    public List<ExpandedPipelineStep> ExpandPipeline(JsonElement pipelineArray, GenerationParameters parameters)
    {
        var result = new List<ExpandedPipelineStep>();

        if (pipelineArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Pipeline is not an array");
            return result;
        }

        int orderCounter = 0;

        foreach (var step in pipelineArray.EnumerateArray())
        {
            var expandedSteps = ExpandStep(step, parameters);
            
            foreach (var expanded in expandedSteps)
            {
                expanded.Order = orderCounter++;
                result.Add(expanded);
            }
        }

        _logger.LogDebug("Expanded pipeline to {Count} steps", result.Count);
        return result;
    }

    private IEnumerable<ExpandedPipelineStep> ExpandStep(JsonElement step, GenerationParameters parameters)
    {
        // Find a processor that can handle this step
        foreach (var processor in _processors)
        {
            if (processor.CanProcess(step))
            {
                _logger.LogDebug("Using {ProcessorType} for step", processor.GetType().Name);
                return processor.Process(step, parameters, _computeRegistry);
            }
        }

        // No special processor - treat as a regular step
        return ExpandRegularStep(step, parameters);
    }

    private IEnumerable<ExpandedPipelineStep> ExpandRegularStep(JsonElement step, GenerationParameters parameters)
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
            _logger.LogWarning("Pipeline step missing fragment property (id: {Id})", id);
            yield break;
        }

        // Extract parameters, resolving $compute markers
        var expandedParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (step.TryGetProperty("parameters", out var paramsEl) &&
            paramsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in paramsEl.EnumerateObject())
            {
                var value = ExtractAndResolveValue(prop.Value, parameters);
                expandedParams[prop.Name] = value;
            }
        }

        yield return new ExpandedPipelineStep
        {
            Id = id,
            Fragment = fragment,
            Parameters = expandedParams
        };
    }

    private object? ExtractAndResolveValue(JsonElement value, GenerationParameters parameters)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var strValue = value.GetString() ?? "";

            // Check for $compute marker
            if (ComputeRegistry.IsComputeMarker(strValue))
            {
                return _computeRegistry.ResolveValue(strValue, parameters);
            }

            return strValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var i) ? i : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ExtractObject(value, parameters),
            JsonValueKind.Array => ExtractArray(value, parameters),
            _ => value.GetRawText()
        };
    }

    private Dictionary<string, object?> ExtractObject(JsonElement obj, GenerationParameters parameters)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var prop in obj.EnumerateObject())
        {
            result[prop.Name] = ExtractAndResolveValue(prop.Value, parameters);
        }
        
        return result;
    }

    private List<object?> ExtractArray(JsonElement arr, GenerationParameters parameters)
    {
        var result = new List<object?>();
        
        foreach (var item in arr.EnumerateArray())
        {
            result.Add(ExtractAndResolveValue(item, parameters));
        }
        
        return result;
    }
}
