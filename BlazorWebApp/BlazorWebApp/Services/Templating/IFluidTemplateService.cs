using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Templating;

/// <summary>
/// Service for rendering Fluid (Liquid) templates with custom blocks and filters.
/// Replaces regex-based meta extraction with native block parsing.
/// </summary>
public interface IFluidTemplateService
{
    /// <summary>
    /// Renders a template string with the provided parameters.
    /// </summary>
    /// <param name="templateText">The Liquid template text to render.</param>
    /// <param name="parameters">Dictionary of parameters available to the template.</param>
    /// <param name="nodeRegistry">Optional node registry for get_ref lookups.</param>
    /// <returns>Tuple containing (rendered output, metadata JSON if {% meta %} block was present).</returns>
    Task<(string rendered, string? metadata)> RenderAsync(
        string templateText,
        Dictionary<string, object?> parameters,
        NodeRegistry? nodeRegistry = null);

    /// <summary>
    /// Renders a template with a pre-built context for advanced scenarios.
    /// </summary>
    /// <param name="templateText">The Liquid template text to render.</param>
    /// <param name="context">Pre-configured Fluid template context.</param>
    /// <returns>Tuple containing (rendered output, metadata JSON if {% meta %} block was present).</returns>
    Task<(string rendered, string? metadata)> RenderAsync(
        string templateText,
        FluidRenderContext context);

    /// <summary>
    /// Creates a render context with the provided parameters and node registry.
    /// Useful when you need to inspect or modify the context before rendering.
    /// </summary>
    FluidRenderContext CreateContext(
        Dictionary<string, object?> parameters,
        NodeRegistry? nodeRegistry = null);

    /// <summary>
    /// Clears the template cache. Call after template files are modified.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Encapsulates the context for a Fluid template render operation.
/// Provides access to captured metadata after rendering.
/// </summary>
public class FluidRenderContext
{
    /// <summary>
    /// Parameters available to the template during rendering.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Node registry for resolving get_ref lookups.
    /// </summary>
    public NodeRegistry? NodeRegistry { get; set; }

    /// <summary>
    /// After rendering, contains the JSON captured by {% meta %}...{% endmeta %} block.
    /// Null if no meta block was present in the template.
    /// </summary>
    public string? CapturedMetadata { get; internal set; }
}
