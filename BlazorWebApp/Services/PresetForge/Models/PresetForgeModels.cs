using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services.PresetForge.Models;

public enum PresetForgeOperation
{
    Generate,
    Expand
}

public enum PresetForgeKind
{
    FullPrompt,
    BuildingBlock,
    EditInstruction
}

public enum PresetPreservationPolicy
{
    None,
    Full,
    Body,
    PartialBody,
    IdentityAndComposition
}

public sealed class PresetForgeRequest
{
    public PresetForgeOperation Operation { get; set; } = PresetForgeOperation.Generate;
    public PresetForgeKind Kind { get; set; } = PresetForgeKind.BuildingBlock;
    public PresetPreservationPolicy PreservationPolicy { get; set; }
    public string Theme { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Facet { get; set; } = string.Empty;
    public string NamingPrefix { get; set; } = string.Empty;
    public string ExpansionDirection { get; set; } = string.Empty;
    public string RequiredConcepts { get; set; } = string.Empty;
    public string ExcludedConcepts { get; set; } = string.Empty;
    public string KeepUnchanged { get; set; } = string.Empty;
    public int Count { get; set; } = 12;
    public bool BroadCoverage { get; set; }
    public bool IncludeNegativePrompts { get; set; }
    public List<string> AdditionalTags { get; set; } = [];
    public List<string> SeedExamples { get; set; } = [];
    public List<Prompt> ReferencePresets { get; set; } = [];
}

public sealed class PresetForgeDraft
{
    public PresetForgeOperation Operation { get; set; }
    public PresetForgeKind Kind { get; set; }
    public string RawResponse { get; set; } = string.Empty;
    public List<PresetForgeDraftEntry> Entries { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PresetForgeDraftEntry
{
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? NegativePrompt { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public bool Accepted { get; set; } = true;
    public bool IsDuplicate { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
    public List<int> SourcePresetIds { get; set; } = [];

    public bool IsValid => ValidationErrors.Count == 0;
}