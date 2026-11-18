using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services;

public sealed class CharacterImageDraftService : ICharacterImageDraftService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OllamaService _ollama;
    private readonly IStateService _state;
    private readonly ILogger<CharacterImageDraftService> _logger;

    public CharacterImageDraftService(OllamaService ollama, IStateService state, ILogger<CharacterImageDraftService> logger)
    {
        _ollama = ollama;
        _state = state;
        _logger = logger;
    }

    public async Task<CharacterImageDraft?> CreateDraftAsync(CharacterImageDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ModelName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.ImagePath) && request.ImageBytes is not { Length: > 0 })
        {
            return null;
        }

        string base64;
        CharacterReferenceSourceImage sourceImage;
        if (!string.IsNullOrWhiteSpace(request.ImagePath))
        {
            base64 = await _ollama.EncodeImageBase64Async(request.ImagePath!, cancellationToken);
            sourceImage = CharacterReferenceSourceImage.FromPath(request.ImagePath!, request.SourceLabel);
        }
        else
        {
            base64 = Convert.ToBase64String(request.ImageBytes!);
            sourceImage = CharacterReferenceSourceImage.FromBytes(request.ImageBytes!, request.SourceLabel, request.SourceLabel);
        }

        var llmOptions = _state.State.Generation.LLM.Options;
        var response = await _ollama.SendChatMessage(
            request.ModelName,
            BuildMessages(request.Catalog, base64),
            options: BuildDraftOptions(llmOptions),
            keepAlive: "15m",
            stream: false,
            format: "json",
            think: llmOptions.EnableThinking ? true : false);

        cancellationToken.ThrowIfCancellationRequested();
        var content = StripThinkTags(response?.Message?.Content ?? string.Empty, llmOptions).Trim();
        return TryParseDraft(content, request.Catalog, sourceImage, request.SourceLabel, out var draft) ? draft : null;
    }

    public bool TryParseDraft(string? rawJson, CharacterCreatorCatalog catalog, CharacterReferenceSourceImage? sourceImage, string? sourceLabel, out CharacterImageDraft draft)
    {
        draft = new CharacterImageDraft
        {
            SourceImage = sourceImage,
            SourceLabel = sourceLabel ?? string.Empty,
            RawResponse = rawJson ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<CharacterImageDraftDto>(NormalizeJson(rawJson), JsonOptions);
            if (dto is null)
            {
                return false;
            }

            draft.DisplayName = (dto.DisplayName ?? string.Empty).Trim();
            draft.Summary = (dto.Summary ?? string.Empty).Trim();
            draft.Species = (dto.Species ?? string.Empty).Trim();
            draft.Archetype = (dto.Archetype ?? string.Empty).Trim();
            draft.Notes = (dto.Notes ?? string.Empty).Trim();
            draft.Warnings = dto.Warnings?
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Select(warning => warning.Trim())
                .Take(8)
                .ToList() ?? new List<string>();

            var validRegions = catalog.Regions.Select(region => region.Id).ToHashSet(StringComparer.Ordinal);
            var validTraits = catalog.TraitDefinitions.ToDictionary(trait => trait.Id, StringComparer.Ordinal);
            foreach (var regionDto in dto.Regions ?? [])
            {
                var regionId = (regionDto.RegionId ?? string.Empty).Trim();
                if (!validRegions.Contains(regionId))
                {
                    continue;
                }

                var region = new CharacterImageDraftRegion
                {
                    RegionId = regionId,
                    Notes = (regionDto.Notes ?? string.Empty).Trim()
                };

                foreach (var traitDto in regionDto.Traits ?? [])
                {
                    var traitId = (traitDto.TraitId ?? string.Empty).Trim();
                    var value = (traitDto.Value ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(value)
                        || !validTraits.TryGetValue(traitId, out var traitDefinition)
                        || !string.Equals(traitDefinition.RegionId, regionId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    region.Traits.Add(new CharacterImageDraftTrait
                    {
                        TraitId = traitId,
                        Value = value,
                        Confidence = Math.Clamp(traitDto.Confidence ?? 0.5, 0, 1)
                    });
                }

                if (!string.IsNullOrWhiteSpace(region.Notes) || region.Traits.Count > 0)
                {
                    draft.Regions.Add(region);
                }
            }

            return draft.HasChanges;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Character image draft JSON: {Content}", rawJson);
            return false;
        }
    }

    public CharacterImageDraftApplyResult ApplyDraft(CharacterBody body, CharacterImageDraft draft)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(draft);

        var result = new CharacterImageDraftApplyResult();
        if (!string.IsNullOrWhiteSpace(draft.DisplayName) && string.IsNullOrWhiteSpace(body.Identity.DisplayName))
        {
            body.Identity.DisplayName = draft.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(draft.Summary))
        {
            body.Identity.Summary = draft.Summary.Trim();
            result.Messages.Add("Updated summary.");
        }

        if (!string.IsNullOrWhiteSpace(draft.Species))
        {
            body.Identity.Species = draft.Species.Trim();
        }

        if (!string.IsNullOrWhiteSpace(draft.Archetype))
        {
            body.Identity.Archetype = draft.Archetype.Trim();
        }

        if (!string.IsNullOrWhiteSpace(draft.Notes))
        {
            body.Notes = draft.Notes.Trim();
        }

        foreach (var draftRegion in draft.Regions)
        {
            var region = GetOrCreateRegion(body, draftRegion.RegionId);
            if (!string.IsNullOrWhiteSpace(draftRegion.Notes))
            {
                region.FreeformNotes = draftRegion.Notes.Trim();
            }

            foreach (var trait in draftRegion.Traits)
            {
                if (region.SelectedTraits.TryGetValue(trait.TraitId, out var existing)
                    && !string.IsNullOrWhiteSpace(existing.Value)
                    && (existing.Locked || existing.Source == CharacterTraitSource.Manual))
                {
                    result.SkippedTraitCount++;
                    continue;
                }

                region.SelectedTraits[trait.TraitId] = new CharacterTraitValue
                {
                    TraitId = trait.TraitId,
                    Value = trait.Value.Trim(),
                    Source = CharacterTraitSource.Image
                };
                region.ConfidenceByTrait[trait.TraitId] = Math.Clamp(trait.Confidence, 0, 1);
                result.AppliedTraitCount++;
            }
        }

        if (draft.SourceImage is not null && !string.IsNullOrWhiteSpace(draft.SourceImage.SourceFingerprint))
        {
            var sheet = body.ReferenceSheets.FirstOrDefault(sheet => string.Equals(sheet.SourceImage.SourceFingerprint, draft.SourceImage.SourceFingerprint, StringComparison.Ordinal));
            if (sheet is null)
            {
                sheet = CharacterReferenceSheetBody.Create(draft.SourceImage, draft.SourceLabel);
                body.ReferenceSheets.Add(sheet);
                result.AddedReferenceSheet = true;
            }

            body.ActiveReferenceSheetId = sheet.Id;
            result.SelectedReferenceSheet = true;
        }

        if (result.AppliedTraitCount > 0)
        {
            result.Messages.Add($"Applied {result.AppliedTraitCount} image trait{(result.AppliedTraitCount == 1 ? string.Empty : "s")}.");
        }

        if (result.SkippedTraitCount > 0)
        {
            result.Messages.Add($"Skipped {result.SkippedTraitCount} manual or locked trait{(result.SkippedTraitCount == 1 ? string.Empty : "s")}.");
        }

        if (result.AddedReferenceSheet)
        {
            result.Messages.Add("Added source image reference sheet.");
        }

        return result;
    }

    private static List<OllamaChatMessage> BuildMessages(CharacterCreatorCatalog catalog, string base64Image)
    {
        var catalogShape = new
        {
            Regions = catalog.Regions.Select(region => new { region.Id, region.Label }).ToList(),
            Traits = catalog.TraitDefinitions.Select(trait => new
            {
                trait.Id,
                trait.RegionId,
                trait.Label,
                Options = trait.Options.Select(option => new { option.Id, option.Label, option.Prompt }).ToList()
            }).ToList()
        };

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
                Content = $"""
Extract an editable character identity draft from the attached image.

Use only regionId and traitId values from this catalog JSON:
{JsonSerializer.Serialize(catalogShape, JsonOptions)}
""",
                Images = new List<string> { base64Image }
            }
        };
    }

    private const string SystemPrompt = """
You create structured character identity drafts from images for a Character Creator editor.
Return only JSON. Never return markdown.
The JSON shape is:
{"displayName":"optional short name","summary":"1-2 sentence visual identity summary","species":"optional species","archetype":"optional archetype","notes":"optional editor notes","regions":[{"regionId":"catalog region id","notes":"optional visible notes","traits":[{"traitId":"catalog trait id","value":"short visible value","confidence":0.0}]}],"warnings":["optional ambiguity"]}
Rules:
- Only describe visible identity details. Use warnings for uncertainty.
- Use catalog regionId and traitId values exactly.
- Prefer concise values that can become prompt fragments.
- Confidence must be between 0 and 1.
- Do not invent backstory, personality, age, or unseen anatomy.
""";

    private static OllamaOptions BuildDraftOptions(AppStateOllamaOptions options)
    {
        var ollamaOptions = options.ToOllamaOptions();
        ollamaOptions.Seed = null;
        ollamaOptions.Temperature = Math.Min(Math.Max(ollamaOptions.Temperature, 0.2f), 0.6f);
        ollamaOptions.TopP = Math.Max(ollamaOptions.TopP, 0.8f);
        ollamaOptions.NumPredict = Math.Max(ollamaOptions.NumPredict, 900);
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

    private sealed class CharacterImageDraftDto
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("species")]
        public string? Species { get; set; }

        [JsonPropertyName("archetype")]
        public string? Archetype { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("regions")]
        public List<CharacterImageDraftRegionDto>? Regions { get; set; }

        [JsonPropertyName("warnings")]
        public List<string>? Warnings { get; set; }
    }

    private sealed class CharacterImageDraftRegionDto
    {
        [JsonPropertyName("regionId")]
        public string? RegionId { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("traits")]
        public List<CharacterImageDraftTraitDto>? Traits { get; set; }
    }

    private sealed class CharacterImageDraftTraitDto
    {
        [JsonPropertyName("traitId")]
        public string? TraitId { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }
    }
}