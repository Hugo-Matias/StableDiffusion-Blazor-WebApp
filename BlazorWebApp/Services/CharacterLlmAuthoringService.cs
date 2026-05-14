using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services;

public sealed class CharacterLlmAuthoringService : ICharacterLlmAuthoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OllamaService _ollama;
    private readonly IStateService _state;
    private readonly ILogger<CharacterLlmAuthoringService> _logger;

    public CharacterLlmAuthoringService(OllamaService ollama, IStateService state, ILogger<CharacterLlmAuthoringService> logger)
    {
        _ollama = ollama;
        _state = state;
        _logger = logger;
    }

    public Task<CharacterLlmAuthoringSuggestion?> ExpandSummaryAsync(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompilation? compilation, CancellationToken cancellationToken = default)
    {
        return RequestSuggestionAsync(CharacterLlmAuthoringOperation.ExpandSummary, body, catalog, compilation?.PositivePrompt ?? string.Empty, null, cancellationToken);
    }

    public Task<CharacterLlmAuthoringSuggestion?> SummarizeAsync(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompilation? compilation, CancellationToken cancellationToken = default)
    {
        return RequestSuggestionAsync(CharacterLlmAuthoringOperation.Summarize, body, catalog, compilation?.PositivePrompt ?? string.Empty, null, cancellationToken);
    }

    public Task<CharacterLlmAuthoringSuggestion?> RewriteRegionAsync(CharacterBody body, CharacterCreatorCatalog catalog, string regionId, CancellationToken cancellationToken = default)
    {
        return RequestSuggestionAsync(CharacterLlmAuthoringOperation.RewriteRegion, body, catalog, string.Empty, regionId, cancellationToken);
    }

    public Task<CharacterLlmAuthoringSuggestion?> FillMissingAsync(CharacterBody body, CharacterCreatorCatalog catalog, CancellationToken cancellationToken = default)
    {
        return RequestSuggestionAsync(CharacterLlmAuthoringOperation.FillMissing, body, catalog, string.Empty, null, cancellationToken);
    }

    public CharacterLlmAuthoringApplyResult ApplySuggestion(CharacterBody body, CharacterLlmAuthoringSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(suggestion);

        var result = new CharacterLlmAuthoringApplyResult();
        if (!string.IsNullOrWhiteSpace(suggestion.Summary))
        {
            body.Identity.Summary = suggestion.Summary.Trim();
            result.Messages.Add("Updated summary.");
        }

        if (!string.IsNullOrWhiteSpace(suggestion.Notes))
        {
            body.Notes = suggestion.Notes.Trim();
            result.Messages.Add("Updated notes.");
        }

        if (!string.IsNullOrWhiteSpace(suggestion.RegionId) && !string.IsNullOrWhiteSpace(suggestion.RegionNotes))
        {
            var region = GetOrCreateRegion(body, suggestion.RegionId);
            region.FreeformNotes = suggestion.RegionNotes.Trim();
            result.Messages.Add($"Updated {suggestion.RegionId} notes.");
        }

        foreach (var update in suggestion.TraitUpdates)
        {
            if (string.IsNullOrWhiteSpace(update.RegionId) || string.IsNullOrWhiteSpace(update.TraitId) || string.IsNullOrWhiteSpace(update.Value))
            {
                result.SkippedTraitCount++;
                continue;
            }

            var region = GetOrCreateRegion(body, update.RegionId);
            if (region.SelectedTraits.TryGetValue(update.TraitId, out var existing)
                && !string.IsNullOrWhiteSpace(existing.Value)
                && (existing.Locked || existing.Source == CharacterTraitSource.Manual))
            {
                result.SkippedTraitCount++;
                result.Messages.Add($"Skipped {update.TraitId}; an existing manual or locked value is present.");
                continue;
            }

            region.SelectedTraits[update.TraitId] = new CharacterTraitValue
            {
                TraitId = update.TraitId,
                Value = update.Value.Trim(),
                Locked = update.Locked,
                Source = CharacterTraitSource.Llm
            };
            result.AppliedTraitCount++;
        }

        return result;
    }

    public bool TryParseSuggestion(string? rawJson, CharacterLlmAuthoringOperation operation, out CharacterLlmAuthoringSuggestion suggestion)
    {
        suggestion = new CharacterLlmAuthoringSuggestion { Operation = operation, RawResponse = rawJson ?? string.Empty };
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            var normalizedJson = NormalizeJson(rawJson);
            var dto = JsonSerializer.Deserialize<CharacterLlmAuthoringSuggestionDto>(normalizedJson, JsonOptions);
            if (dto is null)
            {
                return false;
            }

            suggestion = new CharacterLlmAuthoringSuggestion
            {
                Operation = operation,
                Label = (dto.Label ?? string.Empty).Trim(),
                Summary = (dto.Summary ?? string.Empty).Trim(),
                Notes = (dto.Notes ?? string.Empty).Trim(),
                RegionId = (dto.RegionId ?? string.Empty).Trim(),
                RegionNotes = (dto.RegionNotes ?? string.Empty).Trim(),
                TraitUpdates = dto.TraitUpdates?
                    .Where(update => !string.IsNullOrWhiteSpace(update.RegionId) && !string.IsNullOrWhiteSpace(update.TraitId) && !string.IsNullOrWhiteSpace(update.Value))
                    .Select(update => new CharacterTraitAssignment
                    {
                        RegionId = update.RegionId!.Trim(),
                        TraitId = update.TraitId!.Trim(),
                        Value = update.Value!.Trim(),
                        Locked = update.Locked
                    })
                    .Take(8)
                    .ToList() ?? new List<CharacterTraitAssignment>(),
                Warnings = dto.Warnings?.Where(warning => !string.IsNullOrWhiteSpace(warning)).Select(warning => warning.Trim()).Take(8).ToList() ?? new List<string>(),
                RawResponse = rawJson
            };

            return suggestion.HasChanges;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Character LLM authoring JSON: {Content}", rawJson);
            return false;
        }
    }

    private async Task<CharacterLlmAuthoringSuggestion?> RequestSuggestionAsync(
        CharacterLlmAuthoringOperation operation,
        CharacterBody body,
        CharacterCreatorCatalog catalog,
        string compiledPrompt,
        string? regionId,
        CancellationToken cancellationToken)
    {
        var modelName = _state.State.Generation.LLM.Model;
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        var llmOptions = _state.State.Generation.LLM.Options;
        var response = await _ollama.SendChatMessage(
            modelName,
            BuildMessages(operation, body, catalog, compiledPrompt, regionId),
            options: BuildAuthoringOptions(llmOptions),
            keepAlive: "15m",
            stream: false,
            format: "json",
            think: llmOptions.EnableThinking ? true : false);

        cancellationToken.ThrowIfCancellationRequested();
        var content = StripThinkTags(response?.Message?.Content ?? string.Empty, llmOptions).Trim();
        return TryParseSuggestion(content, operation, out var suggestion) ? suggestion : null;
    }

    private static List<OllamaChatMessage> BuildMessages(CharacterLlmAuthoringOperation operation, CharacterBody body, CharacterCreatorCatalog catalog, string compiledPrompt, string? regionId)
    {
        return new List<OllamaChatMessage>
        {
            new()
            {
                Role = "system",
                Content = SystemPrompt
            },
            new()
            {
                Role = "user",
                Content = BuildUserPrompt(operation, body, catalog, compiledPrompt, regionId)
            }
        };
    }

    private const string SystemPrompt = """
You are an assistant for a structured Character Creator editor.
Return only JSON. Never return markdown.
The JSON shape is:
{"label":"short title","summary":"optional polished identity summary","notes":"optional character notes","regionId":"optional region id","regionNotes":"optional rewritten notes for that region","traitUpdates":[{"regionId":"region id","traitId":"trait id","value":"suggested value","locked":false}],"warnings":["optional warning"]}
Rules:
- Suggestions are drafts for user review. Do not claim they were applied.
- Prefer structured traitUpdates over prose when catalog trait ids fit.
- Do not overwrite locked/manual traits; if a value already exists, mention the risk in warnings instead of returning that same trait update.
- Use concise natural language for summary and notes.
- Keep trait values short and imageable.
""";

    private static string BuildUserPrompt(CharacterLlmAuthoringOperation operation, CharacterBody body, CharacterCreatorCatalog catalog, string compiledPrompt, string? regionId)
    {
        var selectedRegion = string.IsNullOrWhiteSpace(regionId) ? null : body.Regions.GetValueOrDefault(regionId);
        var availableTraits = catalog.TraitDefinitions
            .Where(trait => string.IsNullOrWhiteSpace(regionId) || string.Equals(trait.RegionId, regionId, StringComparison.Ordinal))
            .Select(trait => new
            {
                trait.RegionId,
                trait.Id,
                trait.Label,
                Options = trait.Options.Select(option => new { option.Id, option.Label, option.Prompt }).ToList()
            })
            .Take(24)
            .ToList();

        var context = new
        {
            Operation = operation.ToString(),
            body.Identity,
            body.Notes,
            CompiledPrompt = compiledPrompt,
            RegionId = regionId ?? string.Empty,
            SelectedRegion = selectedRegion,
            ExistingRegions = body.Regions,
            AvailableTraits = availableTraits
        };

        var instruction = operation switch
        {
            CharacterLlmAuthoringOperation.ExpandSummary => "Polish or expand the character identity summary from the current traits and compiled prompt. Return summary and optionally notes. Avoid traitUpdates unless a critical missing trait is obvious and currently empty.",
            CharacterLlmAuthoringOperation.Summarize => "Compress the current character identity into a concise reusable summary. Return summary only unless notes are essential. Do not return traitUpdates.",
            CharacterLlmAuthoringOperation.RewriteRegion => "Rewrite the selected region notes and suggest missing traitUpdates only for the selected region. Do not change other regions.",
            CharacterLlmAuthoringOperation.FillMissing => "Suggest a small set of missing high-value traitUpdates for empty catalog traits. Do not return updates for traits that already have values.",
            _ => "Suggest a safe structured improvement."
        };

        return $"""
{instruction}

Current character context JSON:
{JsonSerializer.Serialize(context, JsonOptions)}
""";
    }

    private static OllamaOptions BuildAuthoringOptions(AppStateOllamaOptions options)
    {
        var ollamaOptions = options.ToOllamaOptions();
        ollamaOptions.Seed = null;
        ollamaOptions.Temperature = Math.Max(ollamaOptions.Temperature, 0.7f);
        ollamaOptions.TopP = Math.Max(ollamaOptions.TopP, 0.9f);
        ollamaOptions.NumPredict = Math.Max(ollamaOptions.NumPredict, 700);
        return ollamaOptions;
    }

    private static CharacterRegionState GetOrCreateRegion(CharacterBody body, string regionId)
    {
        if (!body.Regions.TryGetValue(regionId, out var region))
        {
            region = new CharacterRegionState { RegionId = regionId };
            body.Regions[regionId] = region;
        }

        return region;
    }

    private static string NormalizeJson(string rawJson)
    {
        var trimmed = rawJson.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = Regex.Replace(trimmed, "^```(?:json)?", string.Empty, RegexOptions.IgnoreCase).Trim();
            trimmed = Regex.Replace(trimmed, "```$", string.Empty).Trim();
        }

        return trimmed;
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

    private sealed class CharacterLlmAuthoringSuggestionDto
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("regionId")]
        public string? RegionId { get; set; }

        [JsonPropertyName("regionNotes")]
        public string? RegionNotes { get; set; }

        [JsonPropertyName("traitUpdates")]
        public List<CharacterTraitAssignmentDto>? TraitUpdates { get; set; }

        [JsonPropertyName("warnings")]
        public List<string>? Warnings { get; set; }
    }

    private sealed class CharacterTraitAssignmentDto
    {
        [JsonPropertyName("regionId")]
        public string? RegionId { get; set; }

        [JsonPropertyName("traitId")]
        public string? TraitId { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("locked")]
        public bool Locked { get; set; }
    }
}