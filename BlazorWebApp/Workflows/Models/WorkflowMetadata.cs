using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Metadata describing a workflow template.
/// This replaces the parsed metadata from Scriban .sbn files.
/// </summary>
public record WorkflowMetadata
{
    /// <summary>
    /// Unique identifier for this workflow.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Display title for the workflow.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The model base this workflow is designed for (Flux, SD, SDXL, etc.).
    /// </summary>
    public required ModelBase Base { get; init; }

    /// <summary>
    /// The generation mode (Txt2Img, Img2Img, Img2Vid, etc.).
    /// </summary>
    public required ModeType Mode { get; init; }

    /// <summary>
    /// Asset requirements (models, VAEs, CLIPs, etc.) with UI configuration.
    /// </summary>
    public IEnumerable<WorkflowAsset> Assets { get; init; } = Array.Empty<WorkflowAsset>();

    /// <summary>
    /// Source asset requirements (input images, videos) with UI configuration.
    /// </summary>
    public IEnumerable<WorkflowSource> Sources { get; init; } = Array.Empty<WorkflowSource>();
}

/// <summary>
/// Represents a model/resource asset required by a workflow.
/// </summary>
public record WorkflowAsset
{
    /// <summary>
    /// The parameter name used in template variable replacement.
    /// </summary>
    public required string Parameter { get; init; }

    /// <summary>
    /// Display label for the UI dropdown.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The type of asset (determines which models are loaded).
    /// </summary>
    public required AssetType Type { get; init; }

    /// <summary>
    /// Default filename/value for this asset.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Display order in the UI (lower values appear first).
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Grid column size (1-12, MudBlazor grid system).
    /// </summary>
    public int ColumnSize { get; init; } = 6;

    public WorkflowAsset() { }

    public WorkflowAsset(string parameter, string label, AssetType type, string? defaultValue, int order, int columnSize)
    {
        Parameter = parameter;
        Label = label;
        Type = type;
        DefaultValue = defaultValue;
        Order = order;
        ColumnSize = columnSize;
    }
}

/// <summary>
/// Represents a source input (image/video) required by a workflow.
/// </summary>
public record WorkflowSource
{
    /// <summary>
    /// Unique identifier for this source.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display label for the UI.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Type of source (Image, Video).
    /// </summary>
    public required SourceType Type { get; init; }

    /// <summary>
    /// Whether this source is required.
    /// </summary>
    public bool Required { get; init; }
}

/// <summary>
/// Asset types supported by workflows.
/// </summary>
public enum AssetType
{
    CheckpointModel,
    DiffusionModel,
    Vae,
    Clip,
    ClipVision
}

/// <summary>
/// Source input types.
/// </summary>
public enum SourceType
{
    Image,
    Video
}
