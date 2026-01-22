namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Defines a parameter for a workflow fragment with UI rendering hints.
/// </summary>
public class FragmentParameter
{
    /// <summary>
    /// Parameter name (key in the Values dictionary).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Display label for the UI control.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Parameter type (determines which UI control to render).
    /// </summary>
    public required ParameterType Type { get; init; }

    /// <summary>
    /// Default value for this parameter.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Minimum value (for numeric types).
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value (for numeric types).
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Step increment (for numeric sliders).
    /// </summary>
    public double? Step { get; init; }

    /// <summary>
    /// Dynamic source for populating select options (queries ComfyUI).
    /// </summary>
    public DynamicSource? Source { get; init; }

    /// <summary>
    /// Static options for select dropdowns.
    /// </summary>
    public IEnumerable<string>? Options { get; init; }

    /// <summary>
    /// Help text or description for this parameter.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Parameter types that determine UI control rendering.
/// </summary>
public enum ParameterType
{
    Text,
    Number,
    Slider,
    Select,
    Checkbox,
    Color
}

/// <summary>
/// Defines a dynamic source for populating select options from ComfyUI.
/// </summary>
public record DynamicSource
{
    /// <summary>
    /// ComfyUI node type to query (e.g., "KSampler").
    /// </summary>
    public required string NodeType { get; init; }

    /// <summary>
    /// Input name on the node type to get options for (e.g., "sampler_name").
    /// </summary>
    public required string InputName { get; init; }

    public DynamicSource() { }

    public DynamicSource(string nodeType, string inputName)
    {
        NodeType = nodeType;
        InputName = inputName;
    }
}
