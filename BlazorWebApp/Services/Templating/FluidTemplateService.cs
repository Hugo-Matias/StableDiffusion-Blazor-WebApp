using BlazorWebApp.Models;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    
    // Disable cache for debugging - set to true to diagnose cache issues
    private const bool DisableCacheForDebugging = true;

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
        
        // Log startup
        _logger.LogInformation("FluidTemplateService initialized. Cache disabled: {CacheDisabled}", DisableCacheForDebugging);
        
        // Self-test the meta block registration
        TestMetaBlockRegistration();
    }
    
    /// <summary>
    /// Tests that the meta block extraction works correctly.
    /// Uses the regex-based approach that handles multi-line content.
    /// </summary>
    private void TestMetaBlockRegistration()
    {
        // Test 1: Simple meta block
        TestMetaExtraction("Simple", 
            @"{% meta %}{ ""test"": ""value"" }{% endmeta %}BODY", 
            new Dictionary<string, object?>(),
            expectedMetaContains: "test",
            expectedBodyContains: "BODY");
        
        // Test 2: Multi-line nested JSON (no expressions)
        TestMetaExtraction("MultiLineNested", 
            @"{% meta %}
{
  ""outputs"": {
    ""output_key"": { ""node"": ""node_id"", ""index"": 0 }
  }
}
{% endmeta %}

BODY",
            new Dictionary<string, object?>(),
            expectedMetaContains: "outputs",
            expectedBodyContains: "BODY");
        
        // Test 3: With assign + expressions (the correct pattern)
        TestMetaExtraction("AssignPattern",
            @"{%- assign _prefix = prefix | default: ""model"" -%}
{%- assign _output = output_name | default: ""out"" -%}
{% meta %}
{
  ""outputs"": {
    ""{{ _output }}"": { ""node"": ""{{ _prefix }}_torch"", ""index"": 0 }
  }
}
{% endmeta %}

{
  ""{{ _prefix }}_loader"": { ""class_type"": ""Loader"" }
}",
            new Dictionary<string, object?> { ["prefix"] = "high", ["output_name"] = "high_output" },
            expectedMetaContains: "high_output",
            expectedBodyContains: "high_loader");
    }
    
    private void TestMetaExtraction(string testName, string template, Dictionary<string, object?> parameters, 
        string? expectedMetaContains, string? expectedBodyContains)
    {
        try
        {
            var context = CreateContext(parameters, null);
            var (rendered, metadata) = RenderAsync(template, context).GetAwaiter().GetResult();
            
            var metaOk = expectedMetaContains == null || (metadata?.Contains(expectedMetaContains) ?? false);
            var bodyOk = expectedBodyContains == null || rendered.Contains(expectedBodyContains);
            
            var metaPreview = metadata?.Length > 80 ? metadata.Substring(0, 80).Replace("\n", " ") + "..." : metadata?.Replace("\n", " ");
            var bodyPreview = rendered.Length > 80 ? rendered.Substring(0, 80).Replace("\n", " ") + "..." : rendered.Replace("\n", " ");
            
            if (metaOk && bodyOk)
            {
                _logger.LogInformation("Meta test '{TestName}' PASSED. Meta: {Meta}, Body: {Body}", 
                    testName, metaPreview, bodyPreview);
            }
            else
            {
                _logger.LogError("Meta test '{TestName}' FAILED. MetaOK={MetaOk}, BodyOK={BodyOk}. Meta: {Meta}, Body: {Body}", 
                    testName, metaOk, bodyOk, metaPreview, bodyPreview);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meta test '{TestName}' EXCEPTION", testName);
        }
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
        // WORKAROUND: Fluid's RegisterEmptyBlock has a bug with multi-line content.
        // Extract the meta block using regex and render it with context that includes
        // any {% assign %} statements that may precede it.
        string? preExtractedMetadata = null;
        var bodyTemplate = templateText;
        
        var metaMatch = Regex.Match(templateText, 
            @"\{%\s*meta\s*%\}(.*?)\{%\s*endmeta\s*%\}", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        if (metaMatch.Success)
        {
            // Get everything BEFORE the meta block (may contain {% assign %} statements)
            var preMetaContent = templateText.Substring(0, metaMatch.Index);
            
            // Get the meta block content
            var metaContent = metaMatch.Groups[1].Value.Trim();
            
            // Get everything AFTER the meta block (the body)
            bodyTemplate = templateText.Substring(metaMatch.Index + metaMatch.Length).Trim();
            
            // Combine pre-meta content (assigns) with meta content for rendering
            // This ensures variables defined before {% meta %} are available
            var metaWithAssigns = preMetaContent + metaContent;
            
            // Render the meta content (with preceding assigns) to resolve Fluid expressions
            if (!string.IsNullOrEmpty(metaWithAssigns.Trim()))
            {
                try
                {
                    if (_parser.TryParse(metaWithAssigns, out var metaTemplate, out var metaError))
                    {
                        var metaContext = BuildTemplateContext(context);
                        var renderedMetaWithAssigns = await metaTemplate.RenderAsync(metaContext);
                        
                        // The assigns don't produce output, so the result should be just the meta JSON
                        preExtractedMetadata = renderedMetaWithAssigns?.Trim();
                        
                        _logger.LogDebug("Pre-extracted metadata ({Length} chars): {Preview}", 
                            preExtractedMetadata?.Length ?? 0,
                            (preExtractedMetadata?.Length ?? 0) > 200 
                                ? preExtractedMetadata!.Substring(0, 200).Replace("\n", "\\n") + "..." 
                                : preExtractedMetadata?.Replace("\n", "\\n") ?? "(null)");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse meta content: {Error}. Meta with assigns: {Content}", 
                            metaError, metaWithAssigns.Length > 200 ? metaWithAssigns.Substring(0, 200) + "..." : metaWithAssigns);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rendering meta content");
                }
            }
            
            // Also prepend the assigns to the body template so body can use the same variables
            bodyTemplate = preMetaContent + bodyTemplate;
        }
        
        // Now parse and render the body template (without meta block, but with assigns)
        IFluidTemplate template;
        if (DisableCacheForDebugging)
        {
            if (!_parser.TryParse(bodyTemplate, out template, out var error))
            {
                _logger.LogError("Failed to parse Fluid template: {Error}. Template preview: {Preview}", 
                    error, 
                    bodyTemplate.Length > 200 ? bodyTemplate.Substring(0, 200) + "..." : bodyTemplate);
                throw new InvalidOperationException($"Fluid template parse error: {error}");
            }
        }
        else
        {
            var cacheKey = ComputeCacheKey(bodyTemplate);
            template = GetOrCompileTemplate(cacheKey, bodyTemplate);
        }

        // Build Fluid context
        var templateContext = BuildTemplateContext(context);

        // Render body
        var rendered = await template.RenderAsync(templateContext);

        // Return pre-extracted metadata (from regex) instead of relying on custom block
        context.CapturedMetadata = preExtractedMetadata;

        return (rendered.Trim(), preExtractedMetadata);
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
        // Note: RegisterEmptyBlock requires the block name and a delegate
        parser.RegisterEmptyBlock("meta", RenderMetaBlock);

        // Register {% get_ref "key" %} custom tag
        parser.RegisterExpressionTag("get_ref", RenderGetRefTag);
        
        _logger.LogInformation("FluidParser configured with custom 'meta' block and 'get_ref' tag");

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

        _logger.LogDebug("RenderMetaBlock: Processing {StatementCount} statements in meta block", statements.Count);
        
        // Log each statement type for debugging
        for (int i = 0; i < statements.Count; i++)
        {
            _logger.LogTrace("RenderMetaBlock: Statement[{Index}] type: {Type}", i, statements[i].GetType().Name);
        }
        
        try
        {
            foreach (var statement in statements)
            {
                await statement.WriteToAsync(metaWriter, encoder, context);
            }

            var metaContent = metaWriter.ToString().Trim();
            
            _logger.LogDebug("RenderMetaBlock: Successfully captured metadata ({Length} chars): {Preview}", 
                metaContent.Length, 
                metaContent.Length > 300 ? metaContent.Substring(0, 300).Replace("\n", "\\n") + "..." : metaContent.Replace("\n", "\\n"));

            // Check for signs of body content leaking into meta
            if (metaContent.Contains("class_type") || metaContent.Contains("_unet_loader"))
            {
                _logger.LogError("RenderMetaBlock: BODY CONTENT LEAKED INTO META! This indicates {% endmeta %} was not recognized.");
            }

            // Validate that it looks like JSON
            if (!metaContent.StartsWith("{"))
            {
                _logger.LogWarning("RenderMetaBlock: Captured metadata doesn't start with '{{'. Content: {Content}", metaContent);
            }

            // Store in context ambient values (NOT in main output)
            context.AmbientValues[MetadataContextKey] = metaContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenderMetaBlock: Error rendering meta block statements");
            throw;
        }

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
        // Use SHA256 for reliable cache key (GetHashCode is not stable across runs)
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(templateText);
        var hash = sha256.ComputeHash(bytes);
        return $"fluid_{Convert.ToHexString(hash).Substring(0, 16)}";
    }

    #endregion
}
