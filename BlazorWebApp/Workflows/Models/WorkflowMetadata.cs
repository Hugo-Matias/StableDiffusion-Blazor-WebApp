using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using System.Security.Cryptography;
using System.Text;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Metadata describing a workflow.
/// Defined as properties in C# IWorkflowBuilder implementations.
/// </summary>
public record WorkflowMetadata
{
    /// <summary>
    /// Unique identifier for this workflow.
    /// Deterministically generated from Base + Mode + Title using UUID v5 (SHA-1).
    /// This ensures stable IDs across restarts without manual GUID management.
    /// </summary>
    public Guid Id => GenerateDeterministicId(Base, Mode, Title);

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

    /// <summary>
    /// CivitAI base model strings that this workflow is compatible with.
    /// Used to filter asset selectors and LoRA lists to only show compatible resources.
    /// Valid values come from Data/CivitAI/basemodels.json.
    /// </summary>
    public List<string> CompatibleResourceBaseModels { get; init; } = [];

    /// <summary>
    /// Generates a deterministic GUID from workflow metadata using UUID v5 (RFC 4122).
    /// Same inputs always produce the same GUID, preventing copy-paste collisions.
    /// </summary>
    public static Guid GenerateDeterministicId(ModelBase modelBase, ModeType mode, string title)
    {
        var key = $"{modelBase}:{mode}:{title}";
        return GenerateUuidV5(WorkflowNamespace, key);
    }

    // UUID v5 namespace for workflow IDs (generated once, never changes)
    private static readonly Guid WorkflowNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"); // RFC 4122 DNS namespace

    /// <summary>
    /// Generates a UUID v5 (name-based, SHA-1) per RFC 4122.
    /// </summary>
    private static Guid GenerateUuidV5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        // Convert to big-endian (RFC 4122 requires network byte order)
        SwapGuidByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var hashInput = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, hashInput, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, hashInput, namespaceBytes.Length, nameBytes.Length);

        var hash = SHA1.HashData(hashInput);

        // Take first 16 bytes
        var result = new byte[16];
        Array.Copy(hash, 0, result, 0, 16);

        // Set version to 5 (0101xxxx in byte 6)
        result[6] = (byte)((result[6] & 0x0F) | 0x50);
        // Set variant to RFC 4122 (10xxxxxx in byte 8)
        result[8] = (byte)((result[8] & 0x3F) | 0x80);

        // Convert back to little-endian for .NET Guid
        SwapGuidByteOrder(result);
        return new Guid(result);
    }

    /// <summary>
    /// Swaps the byte order of the first three components of a GUID
    /// between little-endian (.NET) and big-endian (RFC 4122) formats.
    /// </summary>
    private static void SwapGuidByteOrder(byte[] guid)
    {
        // Swap first 4 bytes (Data1: uint32)
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        // Swap bytes 4-5 (Data2: uint16)
        (guid[4], guid[5]) = (guid[5], guid[4]);
        // Swap bytes 6-7 (Data3: uint16)
        (guid[6], guid[7]) = (guid[7], guid[6]);
        // Bytes 8-15 remain unchanged (already in big-endian)
    }
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

    /// <summary>
    /// Whether the user can add multiple instances of this source.
    /// When true, the UI shows a "+" button to add more inputs.
    /// </summary>
    public bool AllowMultiple { get; init; }

    /// <summary>
    /// Parameter name for template variable replacement.
    /// </summary>
    public string Parameter { get; init; } = "";
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
    ClipVision,
    Lora
}

/// <summary>
/// Source input types.
/// </summary>
public enum SourceType
{
    Image,
    Video
}
