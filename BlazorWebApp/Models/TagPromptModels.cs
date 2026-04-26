using BlazorWebApp.Models;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Danbooru tag category mapped to CSV color codes.
    /// </summary>
    public enum TagCategory
    {
        General = 0,
        Artist = 1,
        Copyright = 3,
        Character = 4,
        Meta = 5
    }

    /// <summary>
    /// Model preset for tag assembly conventions.
    /// Verify dialect at implementation time - conventions drift.
    /// </summary>
    public enum TagModelPreset
    {
        Pony,
        Illustrious,
        NoobAI,
        Anima
    }

    /// <summary>
    /// Controls tag count and detail level in the final prompt.
    /// </summary>
    public enum TagVerbosity
    {
        Minimal,    // <= 15 tags
        Standard,   // <= 30 tags
        Detailed,   // <= 50 tags
        Exhaustive  // <= 80 tags
    }

    /// <summary>
    /// Helper to convert between CSV color codes and TagCategory.
    /// </summary>
    public static class DanbooruCategory
    {
        public static TagCategory FromColor(int color)
        {
            return color switch
            {
                1 => TagCategory.Artist,
                3 => TagCategory.Copyright,
                4 => TagCategory.Character,
                5 => TagCategory.Meta,
                _ => TagCategory.General
            };
        }

        public static int ToColor(TagCategory category)
        {
            return (int)category;
        }
    }

    /// <summary>
    /// Request passed to TagPromptService.BuildAsync.
    /// </summary>
    public record TagBuilderRequest(
        string UserInput,
        TagVerbosity Verbosity,
        TagModelPreset Preset,
        Dictionary<TagCategory, bool> CategoryToggles,
        string? GroundingPrompt);

    /// <summary>
    /// Single visual concept extracted from user input (Pass 1 output).
    /// </summary>
    public record ExtractedConcept(
        string Text,
        TagCategory? Category);

    /// <summary>
    /// Concept with resolved Danbooru tag candidates.
    /// </summary>
    public record ResolvedConcept(
        ExtractedConcept Concept,
        IReadOnlyList<Tag> Candidates);

    /// <summary>
    /// Complete result from TagPromptService.BuildAsync.
    /// </summary>
    public record TagBuilderResult(
        string FinalPrompt,
        IReadOnlyList<ResolvedConcept> ResolvedConcepts,
        string ModelUsed,
        TagModelPreset Preset,
        TagVerbosity Verbosity,
        string? RawPass1,
        string? RawPass2);
}
