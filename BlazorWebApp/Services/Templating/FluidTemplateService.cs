using BlazorWebApp.Models;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BlazorWebApp.Services.Templating;

/// <summary>
/// Fluid (Liquid) template service with custom blocks for fragment rendering.
/// Eliminates regex-based meta extraction by using native {% meta %}...{% endmeta %} blocks.
/// </summary>
public class FluidTemplateService : IFluidTemplateService
{
    private readonly FluidParser _parser;
    private readonly ConcurrentDictionary<string, IFluidTemplate> _templateCache = new();
    private readonly ILogger<FluidTemplateService> _logger;
    private readonly TemplateOptions _options;

    // Context key for storing captured metadata from {% meta %} block
    private const string MetadataContextKey = "__fluid_fragment_metadata__";

    public FluidTemplateService(ILogger<FluidTemplateService> logger)
    {
        _logger = logger;
        _options = new TemplateOptions();

        // Configure member access (relaxed access mode)
        _options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.Default;

        // Register custom filters
        RegisterFilters();

        // Create parser with custom tags
        _parser = CreateParser();
    }

    #region Public API

    public async Task<(string rendered, string? metadata)> RenderAsync(
        string templateText,
        Dictionary<string, object?> parameters,
        NodeRegistry? nodeRegistry = null)
    {
        var context = CreateContext(parameters, nodeRegistry);
        return await RenderAsync(templateText, context);
    }

    public async Task<(string rendered, string? metadata)> RenderAsync(
        string templateText,
        FluidRenderContext context)
    {
        // Get or compile template
        var cacheKey = ComputeCacheKey(templateText);
        var template = GetOrCompileTemplate(cacheKey, templateText);

        // Build Fluid context
        var templateContext = BuildTemplateContext(context);

        // Render
        var rendered = await template.RenderAsync(templateContext);

        // Extract captured metadata
        string? metadata = null;
        if (templateContext.AmbientValues.TryGetValue(MetadataContextKey, out var metaObj) && metaObj is string metaStr)
        {
            metadata = metaStr;
            context.CapturedMetadata = metaStr;
        }

        return (rendered, metadata);
    }

    public FluidRenderContext CreateContext(
        Dictionary<string, object?> parameters,
        NodeRegistry? nodeRegistry = null)
    {
        return new FluidRenderContext
        {
            Parameters = new Dictionary<string, object?>(parameters, StringComparer.OrdinalIgnoreCase),
            NodeRegistry = nodeRegistry
        };
    }

    public void ClearCache()
    {
        _templateCache.Clear();
        _logger.LogDebug("Fluid template cache cleared");
    }

    #endregion

    #region Parser Configuration

    private FluidParser CreateParser()
    {
        var parser = new FluidParser();

        // Register {% meta %}...{% endmeta %} custom block (no expression argument)
        parser.RegisterEmptyBlock("meta", RenderMetaBlock);

        // Register {% get_ref "key" %} custom tag
        parser.RegisterExpressionTag("get_ref", RenderGetRefTag);

        return parser;
    }

    /// <summary>
    /// Handles {% meta %}...{% endmeta %} block.
    /// Renders content but captures it to context instead of outputting to main stream.
    /// </summary>
    private async ValueTask<Completion> RenderMetaBlock(
        IReadOnlyList<Statement> statements,
        TextWriter writer,
        TextEncoder encoder,
        TemplateContext context)
    {
        // Render meta content to separate writer
        using var metaWriter = new StringWriter();

        foreach (var statement in statements)
        {
            await statement.WriteToAsync(metaWriter, encoder, context);
        }

        var metaContent = metaWriter.ToString().Trim();

        // Store in context ambient values (NOT in main output)
        context.AmbientValues[MetadataContextKey] = metaContent;

        _logger.LogTrace("Meta block captured: {Length} chars", metaContent.Length);

        // Return Normal to continue processing, but we wrote nothing to main writer
        return Completion.Normal;
    }

    /// <summary>
    /// Handles {% get_ref "key" %} tag.
    /// Outputs the node reference string for the given output key.
    /// </summary>
    private async ValueTask<Completion> RenderGetRefTag(
        Expression expression,
        TextWriter writer,
        TextEncoder encoder,
        TemplateContext context)
    {
        // Evaluate the expression to get the key
        var keyValue = await expression.EvaluateAsync(context);
        var key = keyValue.ToStringValue();

        // Get node registry from ambient values
        if (context.AmbientValues.TryGetValue("__node_registry__", out var registryObj) && registryObj is NodeRegistry registry)
        {
            var reference = registry.GetReference(key);
            await writer.WriteAsync(reference);
        }
        else
        {
            _logger.LogWarning("get_ref called but no NodeRegistry in context. Key: {Key}", key);
            await writer.WriteAsync($"[\"UNRESOLVED:{key}\", 0]");
        }

        return Completion.Normal;
    }

    #endregion

    #region Filter Registration

    private void RegisterFilters()
    {
        // JSON encoding filter: {{ value | json }}
        _options.Filters.AddFilter("json", JsonFilter);

        // String contains filter for compatibility: {{ str | string_contains: "substring" }}
        _options.Filters.AddFilter("string_contains", StringContainsFilter);
    }

    /// <summary>
    /// JSON encoding filter that properly handles all value types.
    /// Usage: {{ value | json }}
    /// </summary>
    private static ValueTask<FluidValue> JsonFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var value = input.ToObjectValue();
        string jsonResult;

        if (value == null)
        {
            jsonResult = "null";
        }
        else if (value is string str)
        {
            jsonResult = JsonSerializer.Serialize(str);
        }
        else if (value is bool b)
        {
            jsonResult = b ? "true" : "false";
        }
        else if (value is int or long or double or float or decimal)
        {
            jsonResult = value.ToString()!;
        }
        else
        {
            // For complex objects, serialize to JSON
            jsonResult = JsonSerializer.Serialize(value);
        }

        return ValueTask.FromResult<FluidValue>(new StringValue(jsonResult, encode: false));
    }

    /// <summary>
    /// String contains filter for checking substrings.
    /// Usage: {{ str | string_contains: "substring" }}
    /// </summary>
    private static ValueTask<FluidValue> StringContainsFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var str = input.ToStringValue();
        var substring = arguments.At(0).ToStringValue();

        if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(substring))
        {
            return ValueTask.FromResult<FluidValue>(BooleanValue.False);
        }

        var contains = str.Contains(substring, StringComparison.OrdinalIgnoreCase);
        return ValueTask.FromResult<FluidValue>(contains ? BooleanValue.True : BooleanValue.False);
    }

    #endregion

    #region Template Context Building

    private TemplateContext BuildTemplateContext(FluidRenderContext renderContext)
    {
        var templateContext = new TemplateContext(_options);

        // Add all parameters to the context
        foreach (var kvp in renderContext.Parameters)
        {
            if (kvp.Value != null)
            {
                templateContext.SetValue(kvp.Key, kvp.Value);

                // Also add alternate casing for compatibility
                var alternateKey = ConvertCasing(kvp.Key);
                if (alternateKey != kvp.Key)
                {
                    templateContext.SetValue(alternateKey, kvp.Value);
                }
            }
        }

        // Store node registry in ambient values for get_ref tag
        if (renderContext.NodeRegistry != null)
        {
            templateContext.AmbientValues["__node_registry__"] = renderContext.NodeRegistry;
        }

        return templateContext;
    }

    /// <summary>
    /// Converts between snake_case and PascalCase for parameter compatibility.
    /// </summary>
    private static string ConvertCasing(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        if (key.Contains('_'))
        {
            // snake_case to PascalCase
            return string.Concat(key.Split('_').Select(part =>
                part.Length > 0 ? char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1) : "") : ""));
        }
        else if (char.IsUpper(key[0]))
        {
            // PascalCase to snake_case
            return string.Concat(key.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
        }

        return key;
    }

    #endregion

    #region Template Caching

    private IFluidTemplate GetOrCompileTemplate(string cacheKey, string templateText)
    {
        return _templateCache.GetOrAdd(cacheKey, _ =>
        {
            if (_parser.TryParse(templateText, out var template, out var error))
            {
                _logger.LogTrace("Compiled and cached template: {CacheKey}", cacheKey);
                return template;
            }

            _logger.LogError("Failed to parse Fluid template: {Error}", error);
            throw new InvalidOperationException($"Fluid template parse error: {error}");
        });
    }

    private static string ComputeCacheKey(string templateText)
    {
        // Simple hash-based cache key
        return $"fluid_{templateText.GetHashCode():X8}";
    }

    #endregion
}
