using System.Text.Json;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Templating.Pipeline;

/// <summary>
/// Processes $foreach markers in pipeline steps, expanding them into multiple steps.
/// 
/// Example input:
/// {
///   "$foreach": "Loras",
///   "$as": "lora",
///   "$template": {
///     "id": "lora_{{ $index }}",
///     "fragment": "lora-loader.liquid",
///     "parameters": {
///       "lora_name": "{{ lora.Name }}",
///       "lora_strength": "{{ lora.Strength }}"
///     }
///   }
/// }
/// </summary>
public class ForeachProcessor : IPipelineProcessor
{
    private readonly ILogger<ForeachProcessor> _logger;

    public ForeachProcessor(ILogger<ForeachProcessor> logger)
    {
        _logger = logger;
    }

    public bool CanProcess(JsonElement step)
    {
        return step.TryGetProperty("$foreach", out _);
    }

    public IEnumerable<ExpandedPipelineStep> Process(
        JsonElement step,
        GenerationParameters parameters,
        ComputeRegistry computeRegistry)
    {
        if (!step.TryGetProperty("$foreach", out var foreachEl) ||
            !step.TryGetProperty("$as", out var asEl) ||
            !step.TryGetProperty("$template", out var templateEl))
        {
            _logger.LogWarning("$foreach step missing required properties ($foreach, $as, $template)");
            yield break;
        }

        var collectionName = foreachEl.GetString();
        var itemName = asEl.GetString();

        if (string.IsNullOrEmpty(collectionName) || string.IsNullOrEmpty(itemName))
        {
            _logger.LogWarning("$foreach or $as value is empty");
            yield break;
        }

        var collection = GetCollection(collectionName, parameters);
        if (collection == null || collection.Count == 0)
        {
            _logger.LogDebug("$foreach collection '{CollectionName}' is empty, skipping", collectionName);
            yield break;
        }

        _logger.LogDebug("Expanding $foreach over '{CollectionName}' with {Count} items", collectionName, collection.Count);

        for (int i = 0; i < collection.Count; i++)
        {
            var item = collection[i];
            var expandedStep = ExpandTemplate(templateEl, itemName, item, i, computeRegistry, parameters);
            if (expandedStep != null)
            {
                yield return expandedStep;
            }
        }
    }

    private IList<object>? GetCollection(string collectionName, GenerationParameters parameters)
    {
        // Handle known collection names
        return collectionName.ToLowerInvariant() switch
        {
            "loras" => parameters.Loras?.Cast<object>().ToList(),
            // WAN img2vid: LoRAs with HighPath
            "highloras" => parameters.Loras?
                .Where(l => !string.IsNullOrEmpty(l.HighPath))
                .Cast<object>()
                .ToList(),
            // WAN img2vid: LoRAs with LowPath
            "lowloras" => parameters.Loras?
                .Where(l => !string.IsNullOrEmpty(l.LowPath))
                .Cast<object>()
                .ToList(),
            _ => null
        };
    }

    private ExpandedPipelineStep? ExpandTemplate(
        JsonElement template,
        string itemName,
        object item,
        int index,
        ComputeRegistry computeRegistry,
        GenerationParameters parameters)
    {
        try
        {
            // Extract id - replace {{ $index }} placeholder
            var id = template.TryGetProperty("id", out var idEl)
                ? ReplaceIndexPlaceholder(idEl.GetString() ?? "", index)
                : $"foreach_item_{index}";

            // Extract fragment
            var fragment = template.TryGetProperty("fragment", out var fragEl)
                ? fragEl.GetString() ?? ""
                : "";

            if (string.IsNullOrEmpty(fragment))
            {
                _logger.LogWarning("$foreach template missing fragment at index {Index}", index);
                return null;
            }

            // Extract and expand parameters
            var expandedParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (template.TryGetProperty("parameters", out var paramsEl) &&
                paramsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in paramsEl.EnumerateObject())
                {
                    var value = ExpandParameterValue(prop.Value, itemName, item, index, computeRegistry, parameters);
                    expandedParams[prop.Name] = value;
                }
            }

            return new ExpandedPipelineStep
            {
                Id = id,
                Fragment = fragment,
                Parameters = expandedParams,
                Order = index
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error expanding $foreach template at index {Index}", index);
            return null;
        }
    }

    private object? ExpandParameterValue(
        JsonElement value,
        string itemName,
        object item,
        int index,
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

            // Replace item property placeholders (e.g., {{ lora.Name }})
            strValue = ReplaceItemPlaceholders(strValue, itemName, item);

            // Replace index placeholder
            strValue = ReplaceIndexPlaceholder(strValue, index);

            return strValue;
        }

        // For other value types, return as-is (converted to CLR types)
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var i) ? i : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static string ReplaceIndexPlaceholder(string template, int index)
    {
        // Replace {{ $index }} with the actual index
        return template
            .Replace("{{ $index }}", index.ToString())
            .Replace("{{$index}}", index.ToString())
            .Replace("{{ forloop.index0 }}", index.ToString())
            .Replace("{{ forloop.index }}", (index + 1).ToString());
    }

    private string ReplaceItemPlaceholders(string template, string itemName, object item)
    {
        if (item == null) return template;

        var itemType = item.GetType();
        var properties = itemType.GetProperties();

        foreach (var prop in properties)
        {
            var placeholder1 = $"{{{{ {itemName}.{prop.Name} }}}}";
            var placeholder2 = $"{{{{{itemName}.{prop.Name}}}}}";

            if (template.Contains(placeholder1) || template.Contains(placeholder2))
            {
                var propValue = prop.GetValue(item);
                var strValue = propValue?.ToString() ?? "";

                template = template
                    .Replace(placeholder1, strValue)
                    .Replace(placeholder2, strValue);
            }
        }

        return template;
    }
}
