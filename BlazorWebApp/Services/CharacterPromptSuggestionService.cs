using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services;

public sealed class CharacterPromptSuggestionService
{
    private static readonly IReadOnlyDictionary<CharacterReferenceSlotKind, string[]> CreativeDirections = new Dictionary<CharacterReferenceSlotKind, string[]>
    {
        [CharacterReferenceSlotKind.BodyAngle] = ["unexpected camera height", "lens and perspective", "dynamic framing", "silhouette read"],
        [CharacterReferenceSlotKind.Expression] = ["subtle asymmetry", "emotional contradiction", "eye and brow detail", "mouth shape and cheek tension"],
        [CharacterReferenceSlotKind.Pose] = ["movement before impact", "weight shift", "hand gesture", "rhythm and balance"],
        [CharacterReferenceSlotKind.Body] = ["proportion clarity", "posture anatomy", "shape-language emphasis", "clean reference readability"],
        [CharacterReferenceSlotKind.Outfit] = ["layering and fasteners", "material contrast", "weathered costume detail", "distinct silhouette accessory"],
        [CharacterReferenceSlotKind.Landscape] = ["foreground depth cue", "weather and atmosphere", "architectural framing", "scale contrast"],
        [CharacterReferenceSlotKind.Custom] = ["fresh visual hook", "unexpected constraint", "specific prop or detail", "composition twist"]
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OllamaService _ollama;
    private readonly IStateService _state;
    private readonly ILogger<CharacterPromptSuggestionService> _logger;

    public CharacterPromptSuggestionService(
        OllamaService ollama,
        IStateService state,
        ILogger<CharacterPromptSuggestionService> logger)
    {
        _ollama = ollama;
        _state = state;
        _logger = logger;
    }

    public async Task<CharacterPromptSuggestion?> SuggestAsync(CharacterReferenceSlotState slot)
    {
        if (slot.PresetKey == CharacterReferenceSlotPresetKey.Blank)
        {
            return null;
        }

        var modelName = _state.State.Generation.LLM.Model;
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        var llmOptions = _state.State.Generation.LLM.Options;
        var creativeDirection = PickCreativeDirection(slot.Kind);
        var messages = new List<OllamaChatMessage>
        {
            new()
            {
                Role = "system",
                Content = """
You write concise JSON suggestions for a character reference sheet slot.
Return only JSON with string fields label and promptExtension.
The label must be a short replacement UI label, 1 to 4 title-case words, and should not simply repeat the current label.
The promptExtension must be an additive instruction appended after a hidden base prompt, so do not repeat the base prompt. Be verbose enough to clearly convey a distinct visual idea, but do not add generic quality or mood modifiers that don't change the image details. Focus on one concrete visual change that respects the slot kind.
    Prioritize novelty over polish. If the current prompt extension already contains an idea, generate a different idea instead of rephrasing it.
    Avoid synonym swaps, reordered wording, and generic upgrades like more detailed, cinematic, dramatic, beautiful, intricate, or high quality.
    Use one concrete visual hook that would noticeably change the image while respecting the slot kind. Examples below have short prompt extensions for clarity, but your suggestions should be more detailed to clearly convey the visual idea.
Examples:
{"label":"Low Angle Hero","promptExtension":"low angle perspective, heroic silhouette, foreshortened limbs, dramatic upward view"}
{"label":"Windblown Smile","promptExtension":"soft open smile, windblown hair motion, bright relaxed eyes"}
{"label":"Hooded Travel Coat","promptExtension":"long hooded travel coat, layered belts, sturdy boots, same color harmony"}
"""
            },
            new()
            {
                Role = "user",
                Content = BuildUserPrompt(slot, creativeDirection)
            }
        };

        var response = await _ollama.SendChatMessage(
            modelName,
            messages,
            options: BuildSuggestionOptions(llmOptions),
            keepAlive: "15m",
            stream: false,
            format: "json",
            think: llmOptions.EnableThinking ? true : false);

        var content = StripThinkTags(response?.Message?.Content ?? string.Empty, llmOptions).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<CharacterPromptSuggestionDto>(content, JsonOptions);
            var label = (dto?.Label ?? string.Empty).Trim();
            var promptExtension = (dto?.PromptExtension ?? string.Empty).Trim();

            return string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(promptExtension)
                ? null
                : new CharacterPromptSuggestion(label, promptExtension);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Character prompt suggestion JSON: {Content}", content);
            return null;
        }
    }

    private static OllamaOptions BuildSuggestionOptions(AppStateOllamaOptions options)
    {
        var ollamaOptions = options.ToOllamaOptions();

        ollamaOptions.Seed = null;
        ollamaOptions.Temperature = Math.Max(ollamaOptions.Temperature, 1.25f);
        ollamaOptions.TopK = Math.Max(ollamaOptions.TopK, 80);
        ollamaOptions.TopP = Math.Max(ollamaOptions.TopP, 0.95f);
        ollamaOptions.MinP = ollamaOptions.MinP.HasValue
            ? Math.Min(ollamaOptions.MinP.Value, 0.03f)
            : 0.03f;

        return ollamaOptions;
    }

    private static string PickCreativeDirection(CharacterReferenceSlotKind kind)
    {
        var directions = CreativeDirections.TryGetValue(kind, out var kindDirections)
            ? kindDirections
            : CreativeDirections[CharacterReferenceSlotKind.Custom];

        return directions[Random.Shared.Next(directions.Length)];
    }

    private static string BuildUserPrompt(CharacterReferenceSlotState slot, string creativeDirection)
    {
        var promptTemplate = CharacterReferenceSlotCatalog.ResolvePromptTemplate(slot);
        var promptExtension = CharacterReferenceSlotCatalog.ResolvePromptExtension(slot);

        return $"""
Slot kind: {slot.Kind}
Current label: {slot.Label}
Hidden base prompt: {promptTemplate}
Current prompt extension: {promptExtension}
Creative direction to explore: {creativeDirection}

Suggest a more surprising label and a verbose promptExtension for this slot.
The label must be the card title the user will see after applying the suggestion.
Treat the current label and current prompt extension as an avoid list. Do not preserve their main concept unless it is required by the slot kind.
The new promptExtension must introduce a distinct visual concept, not a clearer wording of the current one. Be descriptive about the visual details to change, not just the mood or quality. Use concrete, specific details that create a noticeable visual change while respecting the slot kind. If relevant you can use instructions for direction but focus on description.
Prefer one specific, imageable detail over several vague adjectives.
For Camera slots, suggest camera/framing only, focus on camera angles. For Expression slots, suggest facial expression details only. For Pose slots, suggest pose/motion only. For Outfit slots, suggest clothing changes only. For Body slots, suggest body/reference anatomy details only. For Landscape slots, suggest wide composition/background details only.
""";
    }

    private static string StripThinkTags(string value, AppStateOllamaOptions options)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.IsNullOrWhiteSpace(options.ThinkOpenTag)
            || string.IsNullOrWhiteSpace(options.ThinkCloseTag))
        {
            return value;
        }

        var pattern = Regex.Escape(options.ThinkOpenTag) + @"[\s\S]*?" + Regex.Escape(options.ThinkCloseTag);
        return Regex.Replace(value, pattern, string.Empty, RegexOptions.IgnoreCase);
    }

    private sealed class CharacterPromptSuggestionDto
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("promptExtension")]
        public string? PromptExtension { get; set; }
    }
}

public sealed record CharacterPromptSuggestion(string Label, string PromptExtension);