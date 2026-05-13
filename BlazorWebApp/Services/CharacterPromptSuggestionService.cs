using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services;

public sealed class CharacterPromptSuggestionService
{
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
        var messages = new List<OllamaChatMessage>
        {
            new()
            {
                Role = "system",
                Content = """
You write concise JSON suggestions for a character reference sheet slot.
Return only JSON with string fields label and promptExtension.
The label must be a short replacement UI label, 1 to 4 title-case words, and should not simply repeat the current label.
The promptExtension must be an additive instruction appended after a hidden base prompt, so do not repeat the base prompt.
Examples:
{"label":"Low Angle Hero","promptExtension":"low-angle camera, slight upward perspective"}
{"label":"Windblown Smile","promptExtension":"soft open smile, windblown hair motion, bright relaxed eyes"}
{"label":"Hooded Travel Coat","promptExtension":"long hooded travel coat, layered belts, sturdy boots, same color harmony"}
"""
            },
            new()
            {
                Role = "user",
                Content = BuildUserPrompt(slot)
            }
        };

        var response = await _ollama.SendChatMessage(
            modelName,
            messages,
            options: llmOptions.ToOllamaOptions(),
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

    private static string BuildUserPrompt(CharacterReferenceSlotState slot)
    {
        var promptTemplate = CharacterReferenceSlotCatalog.ResolvePromptTemplate(slot);
        var promptExtension = CharacterReferenceSlotCatalog.ResolvePromptExtension(slot);

        return $"""
Slot kind: {slot.Kind}
Current label: {slot.Label}
Hidden base prompt: {promptTemplate}
Current prompt extension: {promptExtension}

Suggest a better label and a compact promptExtension for this slot.
The label must be the card title the user will see after applying the suggestion.
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