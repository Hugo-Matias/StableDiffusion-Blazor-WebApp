namespace BlazorWebApp.Models.CharacterCreator;

public class CharacterImageDraftRequest
{
    public string ModelName { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public string? ImagePath { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public CharacterCreatorCatalog Catalog { get; set; } = CharacterCreatorCatalog.CreateFallback();
}

public class CharacterImageDraft
{
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public CharacterReferenceSourceImage? SourceImage { get; set; }
    public List<CharacterImageDraftRegion> Regions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string RawResponse { get; set; } = string.Empty;

    public bool HasChanges => !string.IsNullOrWhiteSpace(Summary)
        || !string.IsNullOrWhiteSpace(Species)
        || !string.IsNullOrWhiteSpace(Archetype)
        || !string.IsNullOrWhiteSpace(Notes)
        || Regions.Any(region => !string.IsNullOrWhiteSpace(region.Notes) || region.Traits.Count > 0);
}

public class CharacterImageDraftRegion
{
    public string RegionId { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<CharacterImageDraftTrait> Traits { get; set; } = new();
}

public class CharacterImageDraftTrait
{
    public string TraitId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double Confidence { get; set; } = 0.5;
}

public class CharacterImageDraftApplyResult
{
    public int AppliedTraitCount { get; set; }
    public int SkippedTraitCount { get; set; }
    public bool AddedReferenceSheet { get; set; }
    public bool SelectedReferenceSheet { get; set; }
    public List<string> Messages { get; set; } = new();
}