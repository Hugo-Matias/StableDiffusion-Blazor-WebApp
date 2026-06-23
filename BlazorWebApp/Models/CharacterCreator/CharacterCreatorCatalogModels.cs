namespace BlazorWebApp.Models.CharacterCreator;

public class CharacterCreatorCatalog
{
    public int SchemaVersion { get; set; } = 1;
    public List<CharacterRegionDefinition> Regions { get; set; } = new();
    public List<CharacterTraitDefinition> TraitDefinitions { get; set; } = new();
    public List<CharacterPresetDefinition> Presets { get; set; } = new();
    public List<CharacterPromptRuleDefinition> PromptRules { get; set; } = new();
    public List<CharacterConflictRuleDefinition> ConflictRules { get; set; } = new();
    public List<CharacterCatalogWarning> Warnings { get; set; } = new();

    public static CharacterCreatorCatalog CreateFallback()
    {
        return new CharacterCreatorCatalog
        {
            Regions = CharacterRegionDefinition.CreateFallbackRegions(),
            TraitDefinitions = CharacterTraitDefinition.CreateFallbackTraits(),
            PromptRules = CharacterPromptRuleDefinition.CreateFallbackRules()
        };
    }
}

public class CharacterCatalogEnvelope<T>
{
    public int SchemaVersion { get; set; }
    public string CatalogId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<T> Items { get; set; } = new();
}

public class CharacterRegionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Group { get; set; } = "identity";
    public int SortOrder { get; set; }
    public string InputMode { get; set; } = "multi";
    public bool SupportsFreeformNotes { get; set; } = true;
    public bool SupportsAsymmetry { get; set; }
    public CharacterRegionDiagramBinding? Diagram { get; set; }
    public int PromptPriority { get; set; }
    public List<string> Tags { get; set; } = new();

    public static List<CharacterRegionDefinition> CreateFallbackRegions()
    {
        return new List<CharacterRegionDefinition>
        {
            new() { Id = "body", Label = "Body", SortOrder = 10, PromptPriority = 10 },
            new() { Id = "head", Label = "Head", SortOrder = 20, InputMode = "zoom", PromptPriority = 20 },
            new() { Id = "face_shape", Label = "Face Shape", ParentId = "head", SortOrder = 21, PromptPriority = 21 },
            new() { Id = "eyes", Label = "Eyes", ParentId = "head", SortOrder = 22, SupportsAsymmetry = true, PromptPriority = 22 },
            new() { Id = "brows", Label = "Brows", ParentId = "head", SortOrder = 23, SupportsAsymmetry = true, PromptPriority = 23 },
            new() { Id = "nose", Label = "Nose", ParentId = "head", SortOrder = 24, PromptPriority = 24 },
            new() { Id = "mouth", Label = "Mouth", ParentId = "head", SortOrder = 25, PromptPriority = 25 },
            new() { Id = "ears", Label = "Ears", ParentId = "head", SortOrder = 26, SupportsAsymmetry = true, PromptPriority = 26 },
            new() { Id = "hair", Label = "Hair", ParentId = "head", SortOrder = 27, SupportsAsymmetry = true, PromptPriority = 15 },
            new() { Id = "makeup", Label = "Makeup", ParentId = "head", SortOrder = 28, PromptPriority = 28 },
            new() { Id = "head_marks", Label = "Marks", ParentId = "head", SortOrder = 29, SupportsAsymmetry = true, PromptPriority = 29 },
            new() { Id = "torso", Label = "Torso", SortOrder = 30, PromptPriority = 30 },
            new() { Id = "arms", Label = "Arms", SortOrder = 40, SupportsAsymmetry = true, PromptPriority = 40 },
            new() { Id = "hands", Label = "Hands", SortOrder = 50, SupportsAsymmetry = true, PromptPriority = 50 },
            new() { Id = "legs", Label = "Legs", SortOrder = 60, SupportsAsymmetry = true, PromptPriority = 60 },
            new() { Id = "feet", Label = "Feet", SortOrder = 70, SupportsAsymmetry = true, PromptPriority = 70 },
            new() { Id = "clothing", Label = "Clothing", Group = "wardrobe", SortOrder = 80, PromptPriority = 80 },
            new() { Id = "marks", Label = "Marks", SortOrder = 90, SupportsAsymmetry = true, PromptPriority = 90 },
            new() { Id = "expression", Label = "Expression", Group = "expression", SortOrder = 100, PromptPriority = 100 }
        };
    }
}

public class CharacterRegionDiagramBinding
{
    public string DiagramId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}

public class CharacterTraitDefinition
{
    public string Id { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ValueType { get; set; } = "text";
    public string Scope { get; set; } = "identity";
    public bool AllowCustomValue { get; set; }
    public bool LockedByDefault { get; set; }
    public List<CharacterTraitOptionDefinition> Options { get; set; } = new();
    public List<string> PromptTerms { get; set; } = new();
    public List<string> ConflictsWith { get; set; } = new();

    public static List<CharacterTraitDefinition> CreateFallbackTraits()
    {
        return new List<CharacterTraitDefinition>
        {
            new()
            {
                Id = "hair.length",
                RegionId = "hair",
                Label = "Hair Length",
                ValueType = "option",
                Scope = "identity",
                AllowCustomValue = true,
                Options =
                [
                    new() { Id = "short", Label = "Short", Prompt = "short hair" },
                    new() { Id = "shoulder_length", Label = "Shoulder Length", Prompt = "shoulder-length hair" },
                    new() { Id = "long", Label = "Long", Prompt = "long hair" }
                ]
            },
            new()
            {
                Id = "expression.default",
                RegionId = "expression",
                Label = "Default Expression",
                ValueType = "option",
                Scope = "expression",
                AllowCustomValue = true,
                Options =
                [
                    new() { Id = "neutral", Label = "Neutral", Prompt = "neutral expression" },
                    new() { Id = "soft_smile", Label = "Soft Smile", Prompt = "soft smile" }
                ]
            }
        };
    }
}

public class CharacterTraitOptionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public class CharacterPresetDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string PresetType { get; set; } = "starter";
    public string Description { get; set; } = string.Empty;
    public List<CharacterTraitAssignment> TraitValues { get; set; } = new();
}

public class CharacterPromptRuleDefinition
{
    public string ProfileId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DetailBudget { get; set; } = "balanced";
    public List<string> RegionOrder { get; set; } = new();
    public string Template { get; set; } = string.Empty;
    public string NegativeTemplate { get; set; } = string.Empty;
    public bool RewriteAllowed { get; set; } = true;

    public static List<CharacterPromptRuleDefinition> CreateFallbackRules()
    {
        return new List<CharacterPromptRuleDefinition>
        {
            new()
            {
                ProfileId = "identity_plus_wardrobe",
                Label = "Identity + Wardrobe",
                DetailBudget = "balanced",
                RegionOrder = ["identity_core", "body", "head", "hair", "clothing", "marks"],
                Template = "{identity}. {body}. {head}. {wardrobe}. {marks}",
                NegativeTemplate = "{negative_guard}",
                RewriteAllowed = true
            }
        };
    }
}

public class CharacterConflictRuleDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Mode { get; set; } = "mutuallyExclusive";
    public List<string> Items { get; set; } = new();
}

public class CharacterCatalogWarning
{
    public string FileName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public static CharacterCatalogWarning Create(string fileName, string message, string? itemId = null)
    {
        return new CharacterCatalogWarning
        {
            FileName = fileName,
            ItemId = itemId ?? string.Empty,
            Message = message
        };
    }
}