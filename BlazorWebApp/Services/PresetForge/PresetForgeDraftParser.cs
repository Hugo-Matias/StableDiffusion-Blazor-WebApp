using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services.PresetForge.Models;

namespace BlazorWebApp.Services.PresetForge;

public static class PresetForgeDraftParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<PresetForgeDraftEntry> Parse(
        string raw,
        PresetForgeRequest request,
        IReadOnlyCollection<Prompt> existingPrompts)
    {
        var json = ExtractJsonArray(raw);
        if (json == null)
            return [InvalidEntry("The LLM response did not contain a JSON array.")];

        List<PresetOutput>? outputs;
        try
        {
            outputs = JsonSerializer.Deserialize<List<PresetOutput>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return [InvalidEntry($"The LLM response contained invalid JSON: {ex.Message}")];
        }

        if (outputs == null || outputs.Count == 0)
            return [InvalidEntry("The LLM response contained no presets.")];

        var existingNames = existingPrompts
            .Select(prompt => Normalize(prompt.Title))
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var existingValues = existingPrompts
            .Select(prompt => Normalize(prompt.Positive))
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var draftNames = new HashSet<string>(StringComparer.Ordinal);
        var draftValues = new HashSet<string>(StringComparer.Ordinal);

        return outputs.Select(output => BuildEntry(
            output,
            request,
            existingNames,
            existingValues,
            draftNames,
            draftValues)).ToList();
    }

    private static PresetForgeDraftEntry BuildEntry(
        PresetOutput output,
        PresetForgeRequest request,
        HashSet<string> existingNames,
        HashSet<string> existingValues,
        HashSet<string> draftNames,
        HashSet<string> draftValues)
    {
        var entry = new PresetForgeDraftEntry
        {
            Name = output.Name?.Trim() ?? string.Empty,
            Prompt = output.Prompt?.Trim() ?? string.Empty,
            NegativePrompt = string.IsNullOrWhiteSpace(output.NegativePrompt) ? null : output.NegativePrompt.Trim(),
            Category = string.IsNullOrWhiteSpace(output.Category) ? request.Category.Trim() : output.Category.Trim(),
            Tags = NormalizeTags(output.Tags, request),
            SourcePresetIds = request.ReferencePresets.Select(prompt => prompt.Id).Where(id => id > 0).ToList()
        };

        if (entry.Name.Length == 0)
            entry.ValidationErrors.Add("Name is required.");
        if (entry.Prompt.Length == 0)
            entry.ValidationErrors.Add("Prompt is required.");
        if (entry.Category.Length == 0)
            entry.ValidationErrors.Add("Category is required.");

        var normalizedName = Normalize(entry.Name);
        var normalizedPrompt = Normalize(entry.Prompt);
        entry.IsDuplicate = normalizedName.Length > 0 && (!draftNames.Add(normalizedName) || existingNames.Contains(normalizedName));
        entry.IsDuplicate |= normalizedPrompt.Length > 0 && (!draftValues.Add(normalizedPrompt) || existingValues.Contains(normalizedPrompt));
        if (entry.IsDuplicate)
            entry.ValidationErrors.Add("Duplicates an existing or generated preset.");

        entry.Accepted = entry.IsValid;
        return entry;
    }

    private static List<string> NormalizeTags(IEnumerable<string>? outputTags, PresetForgeRequest request)
    {
        var semanticTags = request.Kind switch
        {
            PresetForgeKind.FullPrompt => new[] { "preset-forge", "t2i", "full-prompt" },
            PresetForgeKind.BuildingBlock => new[] { "preset-forge", "t2i", "building-block" },
            _ => new[] { "preset-forge", "i2i", "targeted-edit", PreservationTag(request.PreservationPolicy) }
        };

        return (outputTags ?? [])
            .Concat(request.AdditionalTags)
            .Concat(semanticTags)
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string PreservationTag(PresetPreservationPolicy policy) => policy switch
    {
        PresetPreservationPolicy.Full => "preserve-full",
        PresetPreservationPolicy.Body => "preserve-body",
        PresetPreservationPolicy.PartialBody => "preserve-partial-body",
        PresetPreservationPolicy.IdentityAndComposition => "preserve-identity",
        _ => "preserve-custom"
    };

    private static string NormalizeTag(string? value) => string.Join(
        '-',
        (value ?? string.Empty).Trim().ToLowerInvariant()
            .Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries));

    private static string Normalize(string? value) => string.Join(
        ' ',
        (value ?? string.Empty).Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static PresetForgeDraftEntry InvalidEntry(string message) => new()
    {
        Accepted = false,
        ValidationErrors = [message]
    };

    private static string? ExtractJsonArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var start = raw.IndexOf('[');
        var end = raw.LastIndexOf(']');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private sealed class PresetOutput
    {
        public string? Name { get; set; }
        public string? Prompt { get; set; }

        [JsonPropertyName("negative_prompt")]
        public string? NegativePrompt { get; set; }

        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
    }
}