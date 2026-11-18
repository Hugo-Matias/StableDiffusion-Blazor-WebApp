using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.CharacterCreator;

public static class CharacterCreatorJsonOptions
{
    public static readonly JsonSerializerOptions Compact = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}

public class CharacterBody
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public CharacterIdentity Identity { get; set; } = new();
    public Dictionary<string, CharacterRegionState> Regions { get; set; } = new(StringComparer.Ordinal);
    public List<CharacterWardrobePreset> Wardrobes { get; set; } = new();
    public List<CharacterPromptProfile> PromptProfiles { get; set; } = CharacterPromptProfile.CreateDefaults();
    public List<CharacterReferenceSheetBody> ReferenceSheets { get; set; } = new();
    public string? ActiveReferenceSheetId { get; set; }
    public string Notes { get; set; } = string.Empty;

    public static CharacterBody CreateDefault(string? displayName = null)
    {
        return new CharacterBody
        {
            Identity = new CharacterIdentity
            {
                DisplayName = displayName?.Trim() ?? string.Empty
            }
        };
    }
}

public class CharacterIdentity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
}

public class CharacterRegionState
{
    public string RegionId { get; set; } = string.Empty;
    public Dictionary<string, CharacterTraitValue> SelectedTraits { get; set; } = new(StringComparer.Ordinal);
    public string FreeformNotes { get; set; } = string.Empty;
    public Dictionary<string, double> ConfidenceByTrait { get; set; } = new(StringComparer.Ordinal);
}

public class CharacterTraitValue
{
    public string TraitId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public CharacterTraitSource Source { get; set; } = CharacterTraitSource.Manual;
    public bool Locked { get; set; }
}

public enum CharacterTraitSource
{
    Manual,
    Llm,
    Image,
    Imported
}

public class CharacterWardrobePreset
{
    public string Id { get; set; } = CharacterIdFactory.CreateId("wardrobe");
    public string Label { get; set; } = "Default";
    public string Description { get; set; } = string.Empty;
    public List<CharacterTraitAssignment> TraitValues { get; set; } = new();
}

public class CharacterPromptProfile
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public CharacterPromptDetailBudget DetailBudget { get; set; } = CharacterPromptDetailBudget.Balanced;
    public bool IsDefault { get; set; }

    public static List<CharacterPromptProfile> CreateDefaults()
    {
        return new List<CharacterPromptProfile>
        {
            new() { Id = "identity_only", Label = "Identity Only", DetailBudget = CharacterPromptDetailBudget.Concise },
            new() { Id = "identity_plus_wardrobe", Label = "Identity + Wardrobe", DetailBudget = CharacterPromptDetailBudget.Balanced, IsDefault = true },
            new() { Id = "image_edit_preservation", Label = "Image Edit Preserve", DetailBudget = CharacterPromptDetailBudget.Balanced },
            new() { Id = "rich_portrait", Label = "Rich Portrait", DetailBudget = CharacterPromptDetailBudget.Rich },
            new() { Id = "negative_guard", Label = "Negative Guard", DetailBudget = CharacterPromptDetailBudget.Concise }
        };
    }
}

public enum CharacterPromptDetailBudget
{
    Concise,
    Balanced,
    Rich
}

public class CharacterTraitAssignment
{
    public string RegionId { get; set; } = string.Empty;
    public string TraitId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Locked { get; set; }
}

public static class CharacterIdFactory
{
    public static string CreateId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }
}