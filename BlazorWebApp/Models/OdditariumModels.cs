using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Single button option rendered to the user. <see cref="Label"/> is the display text;
    /// <see cref="Hint"/> is optional secondary text. <see cref="Payload"/> may carry a
    /// richer string sent back to the LLM as the chosen value (defaults to <see cref="Label"/>).
    /// </summary>
    public sealed class OdditariumOption
    {
        public string Label { get; set; } = string.Empty;
        public string? Hint { get; set; }
        public string? Payload { get; set; }
    }

    /// <summary>
    /// One completed round of the game (question asked, 6 options shown, user choice recorded).
    /// Stored on <see cref="OdditariumBody.History"/> for deterministic Undo and the rolling
    /// last-N context window sent to the LLM.
    /// </summary>
    public sealed class OdditariumTurn
    {
        /// <summary>"round" | "more" | "skip"</summary>
        public string Source { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;
        public List<OdditariumOption> Options { get; set; } = new();
        public string Choice { get; set; } = string.Empty;

        /// <summary>The descriptive layer added by this turn (the chosen option payload/label).</summary>
        public string LayerAdded { get; set; } = string.Empty;

        /// <summary>Snapshot of <c>OdditariumBody.PendingQuestion</c> taken before this turn applied. Restored by Undo.</summary>
        public string? PendingQuestionBefore { get; set; }

        /// <summary>Snapshot of <c>OdditariumBody.PendingOptions</c> taken before this turn applied. Restored by Undo.</summary>
        public List<OdditariumOption>? PendingOptionsBefore { get; set; }

        /// <summary>Snapshot of <c>OdditariumBody.CollectedLayers</c> count before this turn. Used for undo validation.</summary>
        public int LayersCountBefore { get; set; }
    }

    /// <summary>
    /// JSON-backed body of an <see cref="Data.Entities.OdditariumSession"/> row.
    /// Persisted as a single complex column via <see cref="OdditariumJsonOptions.Compact"/>.
    /// </summary>
    public sealed class OdditariumBody
    {
        /// <summary>The selected persona ID for this session.</summary>
        public string? PersonaId { get; set; }

        /// <summary>The vibe lock chosen by the user (persona-suggested, user-picked).</summary>
        public string? Vibe { get; set; }

        /// <summary>Total rounds completed since session start. No hard cap - freestyle pacing.</summary>
        public int RoundCount { get; set; }

        /// <summary>The final assembled prompt (populated at Commit time by the LLM).</summary>
        public string? CommittedDraft { get; set; }

        /// <summary>Currently active question from the LLM.</summary>
        public string? PendingQuestion { get; set; }

        /// <summary>6 options currently shown to the user.</summary>
        public List<OdditariumOption> PendingOptions { get; set; } = new();

        /// <summary>
        /// The growing collection of descriptive layers picked by the user across rounds.
        /// Each pick appends one layer. At Commit time, the LLM assembles these into a coherent prompt.
        /// </summary>
        public List<string> CollectedLayers { get; set; } = new();

        /// <summary>Full history of turns for deterministic Undo.</summary>
        public List<OdditariumTurn> History { get; set; } = new();

        /// <summary>True when the session has been committed (draft assembled).</summary>
        public bool IsCommitted { get; set; }

        /// <summary>True when the game has started (persona + vibe selected, first round loaded).</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Shape of a single LLM response in Odditarium. Both round and commit prompts
    /// must conform. Commit responses may additionally return an assembled <see cref="Draft"/> string.
    /// </summary>
    public sealed class OdditariumLLMResponse
    {
        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("options")]
        public List<OdditariumOption>? Options { get; set; }

        [JsonPropertyName("draft")]
        public string? Draft { get; set; }
    }

    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> for Odditarium persistence and LLM I/O.
    /// camelCase, ignore null, no formatting.
    /// </summary>
    public static class OdditariumJsonOptions
    {
        public static readonly JsonSerializerOptions Compact = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
    }

    /// <summary>
    /// Parses + sanitizes raw LLM output into a <see cref="OdditariumLLMResponse"/>.
    /// Trims labels/hints, drops malformed entries, and clamps option count to 3-6.
    /// </summary>
    public static class OdditariumResponseValidator
    {
        public const int MinOptions = 3;
        public const int MaxOptions = 6;
        public const int MaxLabelLength = 60;
        public const int MaxHintLength = 80;

        /// <summary>
        /// Attempts to deserialize <paramref name="rawJson"/> and produce a sanitized response.
        /// Returns <c>true</c> when the result has a non-empty <c>Question</c> and at least
        /// <see cref="MinOptions"/> usable options.
        /// </summary>
        public static bool TryParse(string? rawJson, out OdditariumLLMResponse response)
        {
            response = new OdditariumLLMResponse();
            if (string.IsNullOrWhiteSpace(rawJson)) return false;

            var trimmed = ExtractJsonObject(rawJson);
            if (trimmed is null) return false;

            OdditariumLLMResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<OdditariumLLMResponse>(trimmed, OdditariumJsonOptions.Compact);
            }
            catch (JsonException)
            {
                return false;
            }
            if (parsed is null) return false;

            response = Sanitize(parsed);
            return !string.IsNullOrWhiteSpace(response.Question)
                && response.Options is { Count: >= MinOptions };
        }

        /// <summary>
        /// Trims, deduplicates, and clamps option count. Drops entries whose label is empty.
        /// Returns a new instance; does not mutate <paramref name="raw"/>.
        /// </summary>
        public static OdditariumLLMResponse Sanitize(OdditariumLLMResponse raw)
        {
            var question = (raw.Question ?? string.Empty).Trim();
            var draft = string.IsNullOrWhiteSpace(raw.Draft) ? null : raw.Draft.Trim();

            var options = new List<OdditariumOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (raw.Options is { Count: > 0 })
            {
                foreach (var opt in raw.Options)
                {
                    if (opt is null) continue;

                    var label = (opt.Label ?? string.Empty).Trim().TrimEnd('.', ',', ';', ':', '!', '?');
                    if (string.IsNullOrWhiteSpace(label)) continue;
                    if (label.Length > MaxLabelLength) label = label.Substring(0, MaxLabelLength).TrimEnd();
                    if (!seen.Add(label)) continue;

                    string? hint = null;
                    if (!string.IsNullOrWhiteSpace(opt.Hint))
                    {
                        hint = opt.Hint.Trim();
                        if (hint.Length > MaxHintLength) hint = hint.Substring(0, MaxHintLength).TrimEnd();
                    }

                    options.Add(new OdditariumOption
                    {
                        Label = label,
                        Hint = hint,
                        Payload = string.IsNullOrWhiteSpace(opt.Payload) ? null : opt.Payload.Trim(),
                    });

                    if (options.Count >= MaxOptions) break;
                }
            }

            return new OdditariumLLMResponse
            {
                Question = question,
                Options = options,
                Draft = draft,
            };
        }

        /// <summary>
        /// Some local models prepend prose or wrap output in code fences. Extract the
        /// first balanced JSON object substring; returns null if none found.
        /// </summary>
        private static string? ExtractJsonObject(string raw)
        {
            var start = raw.IndexOf('{');
            if (start < 0) return null;

            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = start; i < raw.Length; i++)
            {
                var c = raw[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return raw.Substring(start, i - start + 1);
                }
            }
            return null;
        }
    }
}
