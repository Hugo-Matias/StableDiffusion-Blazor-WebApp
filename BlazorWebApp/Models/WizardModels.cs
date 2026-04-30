using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// High-level stage of the Workshop Wizard. Drives which prompts and UI surface are active.
    /// </summary>
    public enum WizardStage
    {
        /// <summary>Walking the hardcoded section scaffold (Subject -> Scenery -> Lighting -> Mood -> Style).</summary>
        Intro = 0,

        /// <summary>Free iteration on a built draft using LLM-suggested options + persistent verbs.</summary>
        Iteration = 1,

        /// <summary>Draft has been pushed into the composer; waiting on user to start a new wizard.</summary>
        Committed = 2,
    }

    /// <summary>
    /// Single button option rendered to the user. <see cref="Label"/> is the display text;
    /// <see cref="Hint"/> is optional secondary text. <see cref="Payload"/> may carry a
    /// richer string sent back to the LLM as the chosen value (defaults to <see cref="Label"/>).
    /// </summary>
    public sealed class WizardOption
    {
        public string Label { get; set; } = string.Empty;
        public string? Hint { get; set; }
        public string? Payload { get; set; }
    }

    /// <summary>
    /// One completed turn of the wizard (intro section answered, verb applied, iteration choice, etc.).
    /// Stored on <see cref="WizardBody.History"/> for deterministic Undo and the rolling
    /// last-N context window sent to the LLM.
    /// </summary>
    public sealed class WizardTurn
    {
        /// <summary>"intro:{sectionId}" | "iterate" | "verb:{verbId}" | "more"</summary>
        public string Source { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;
        public List<WizardOption> Options { get; set; } = new();
        public string Choice { get; set; } = string.Empty;
        public string? ActionVerb { get; set; }
        public string DraftBefore { get; set; } = string.Empty;
        public string DraftAfter { get; set; } = string.Empty;

        /// <summary>Snapshot of <c>WizardBody.PendingQuestion</c> taken before this turn applied. Restored by Undo.</summary>
        public string? PendingQuestionBefore { get; set; }

        /// <summary>Snapshot of <c>WizardBody.PendingOptions</c> taken before this turn applied. Restored by Undo.</summary>
        public List<WizardOption>? PendingOptionsBefore { get; set; }

        /// <summary>Snapshot of <c>WizardBody.LastShownLabels</c> taken before this turn applied. Restored by Undo.</summary>
        public List<string>? LastShownLabelsBefore { get; set; }
    }

    /// <summary>
    /// JSON-backed body of a <see cref="Data.Entities.WorkshopWizardSession"/> row.
    /// Persisted as a single complex column via <see cref="WizardJsonOptions.Compact"/>.
    /// </summary>
    public sealed class WizardBody
    {
        public WizardStage Stage { get; set; } = WizardStage.Intro;

        /// <summary>0..(IntroSections.Count - 1) while in <see cref="WizardStage.Intro"/>.</summary>
        public int IntroSectionIndex { get; set; }

        /// <summary>Total turns recorded since the last reset/commit. Hard-capped at 50.</summary>
        public int TurnCount { get; set; }

        public string CurrentDraft { get; set; } = string.Empty;

        public string? PendingQuestion { get; set; }
        public List<WizardOption> PendingOptions { get; set; } = new();

        /// <summary>
        /// Labels currently / previously shown for the active question. Used as
        /// <c>excludeLabels</c> when the user clicks the More... button so the
        /// model rotates suggestions instead of repeating.
        /// </summary>
        public List<string> LastShownLabels { get; set; } = new();

        public List<WizardTurn> History { get; set; } = new();

        public string? LastActionVerb { get; set; }
    }

    /// <summary>
    /// Shape of a single LLM response in the wizard. Both intro and iteration prompts
    /// must conform; iteration / verb prompts may additionally return an updated
    /// <see cref="Draft"/> string. Validation happens in Step 2.
    /// </summary>
    public sealed class WizardLLMResponse
    {
        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("options")]
        public List<WizardOption>? Options { get; set; }

        [JsonPropertyName("draft")]
        public string? Draft { get; set; }
    }

    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> for wizard persistence and LLM I/O.
    /// Mirrors <c>SchedulerJsonOptions.Compact</c>: camelCase, ignore null, no formatting.
    /// </summary>
    public static class WizardJsonOptions
    {
        public static readonly JsonSerializerOptions Compact = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
    }

    /// <summary>
    /// Parses + sanitizes raw LLM output into a <see cref="WizardLLMResponse"/>.
    /// Trims labels/hints, drops malformed entries, and clamps option count to 3-6.
    /// </summary>
    public static class WizardResponseValidator
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
        public static bool TryParse(string? rawJson, out WizardLLMResponse response)
        {
            response = new WizardLLMResponse();
            if (string.IsNullOrWhiteSpace(rawJson)) return false;

            var trimmed = ExtractJsonObject(rawJson);
            if (trimmed is null) return false;

            WizardLLMResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<WizardLLMResponse>(trimmed, WizardJsonOptions.Compact);
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
        public static WizardLLMResponse Sanitize(WizardLLMResponse raw)
        {
            var question = (raw.Question ?? string.Empty).Trim();
            var draft = string.IsNullOrWhiteSpace(raw.Draft) ? null : raw.Draft.Trim();

            var options = new List<WizardOption>();
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

                    options.Add(new WizardOption
                    {
                        Label = label,
                        Hint = hint,
                        Payload = string.IsNullOrWhiteSpace(opt.Payload) ? null : opt.Payload.Trim(),
                    });

                    if (options.Count >= MaxOptions) break;
                }
            }

            return new WizardLLMResponse
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
