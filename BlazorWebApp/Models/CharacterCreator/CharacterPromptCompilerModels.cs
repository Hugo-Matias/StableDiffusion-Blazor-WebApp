namespace BlazorWebApp.Models.CharacterCreator;

public class CharacterPromptCompileOptions
{
    public string? ProfileId { get; set; }
    public string? WardrobeId { get; set; }
    public CharacterPromptApplicationContext? ApplicationContext { get; set; }
    public string? ExistingPrompt { get; set; }
    public CharacterPromptCompositionMode CompositionMode { get; set; } = CharacterPromptCompositionMode.Append;
    public CharacterPromptDetailBudget? DetailBudgetOverride { get; set; }
}

public class CharacterPromptApplicationContext
{
    public string Pose { get; set; } = string.Empty;
    public string Camera { get; set; } = string.Empty;
    public string Scene { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string ExpressionOverride { get; set; } = string.Empty;
    public string EditIntent { get; set; } = string.Empty;
    public string AdditionalPositive { get; set; } = string.Empty;
    public string AdditionalNegative { get; set; } = string.Empty;
}

public enum CharacterPromptCompositionMode
{
    Replace,
    Prepend,
    Append
}

public enum CharacterPromptFragmentKind
{
    Identity,
    Wardrobe,
    Expression,
    Application,
    NegativeGuard
}

public class CharacterPromptFragment
{
    public CharacterPromptFragmentKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public IReadOnlyList<string> SourceKeys { get; set; } = [];
}

public class CharacterPromptCompilation
{
    public string ProfileId { get; set; } = string.Empty;
    public CharacterPromptDetailBudget DetailBudget { get; set; } = CharacterPromptDetailBudget.Balanced;
    public string PositivePrompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public string? ComposedPrompt { get; set; }
    public List<CharacterPromptFragment> Fragments { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}