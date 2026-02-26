using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;

namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Metadata describing a workflow fragment.
/// Defined as properties in C# IFragmentBuilder implementations.
/// </summary>
public record FragmentMetadata
{
    /// <summary>
    /// Unique identifier for this fragment (used for parameter storage and UI rendering).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Fragment type classification.
    /// </summary>
    public required FragmentType Type { get; init; }

    /// <summary>
    /// Display title for the fragment in the UI.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional Blazor component name for rendering this fragment.
    /// If null, dynamic field rendering is used based on Parameters.
    /// Example: "LatentForm", "SamplerForm", "DetailerForm"
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// Optional icon class (Font Awesome) for UI display.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Display order in the UI (lower values appear first).
    /// </summary>
    public int Order { get; init; } = 100;

    /// <summary>
    /// Whether this fragment can be collapsed in the UI.
    /// </summary>
    public bool Collapsible { get; init; } = true;

    /// <summary>
    /// Whether this fragment should start collapsed by default.
    /// </summary>
    public bool DefaultCollapsed { get; init; } = false;

    /// <summary>
    /// Whether this fragment is hidden from the UI (utility fragments).
    /// </summary>
    public bool IsHidden { get; init; } = false;

    /// <summary>
    /// Parameter definitions for UI generation and validation.
    /// </summary>
    public IEnumerable<FragmentParameter> Parameters { get; init; } = Array.Empty<FragmentParameter>();

    /// <summary>
    /// Optional condition that determines if this fragment should be included.
    /// If null, fragment is always included (controlled by IsActive flag).
    /// </summary>
    public Func<GenerationParameters, bool>? InclusionCondition { get; init; }
}
