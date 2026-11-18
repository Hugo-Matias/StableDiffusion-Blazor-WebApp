namespace BlazorWebApp.Models.CharacterCreator;

public enum CharacterLlmAuthoringOperation
{
    ExpandSummary,
    Summarize,
    RewriteRegion,
    FillMissing
}

public class CharacterLlmAuthoringSuggestion
{
    public CharacterLlmAuthoringOperation Operation { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public string RegionNotes { get; set; } = string.Empty;
    public List<CharacterTraitAssignment> TraitUpdates { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string RawResponse { get; set; } = string.Empty;

    public bool HasChanges => !string.IsNullOrWhiteSpace(Summary)
        || !string.IsNullOrWhiteSpace(Notes)
        || !string.IsNullOrWhiteSpace(RegionNotes)
        || TraitUpdates.Count > 0;
}

public class CharacterLlmAuthoringApplyResult
{
    public int AppliedTraitCount { get; set; }
    public int SkippedTraitCount { get; set; }
    public List<string> Messages { get; set; } = new();
}