using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
        /// <summary>Inherited from the round response - which anchor this option belongs to.</summary>
        public string? Anchor { get; set; }
        /// <summary>Inherited from the round response - which facet this option explores.</summary>
        public string? Facet { get; set; }
    }

    /// <summary>
    /// One collected descriptive layer, carrying structural metadata (anchor + facet + mode)
    /// alongside the display label and hint. Replaces the flat <c>List<string></c> of v1.
    /// </summary>
    public sealed class OdditariumLayer
    {
        /// <summary>One of the 13 fixed anchors in 3 tiers — Core: subject, setting; Structural: action, lighting, framing, atmosphere; Enrichment: mood, style, detail, time, scale, color, movement.</summary>
        public string Anchor { get; set; } = string.Empty;

        /// <summary>Lowercase-kebab facet identifier invented by the LLM (e.g. "material-and-patina"). Null on first expand of an anchor.</summary>
        public string? Facet { get; set; }

        /// <summary>"expand" | "deepen" | "pivot"</summary>
        public string Mode { get; set; } = "expand";

        /// <summary>The bare noun concept shown to the user (e.g. "Marble Bust").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Persona-flavored hint text.</summary>
        public string? Hint { get; set; }

        /// <summary>The question that produced this layer.</summary>
        public string? Question { get; set; }
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

        /// <summary>The typed layer record added by this turn. Null for skip turns.</summary>
        public OdditariumLayer? Layer { get; set; }

        // --- Undo snapshots (captured before this turn was applied) ---
        public string? PendingQuestionBefore { get; set; }
        public List<OdditariumOption>? PendingOptionsBefore { get; set; }
        public string? PendingAnchorBefore { get; set; }
        public string? PendingFacetBefore { get; set; }
        public string? PendingModeBefore { get; set; }
        public List<string>? ShownLabelsBefore { get; set; }

        /// <summary>Total layers in CollectedLayers before this turn. Used for undo validation.</summary>
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
        /// Each pick appends one typed layer carrying anchor, facet, mode, label, hint, question.
        /// At Commit time, the LLM assembles these into a coherent prompt.
        /// </summary>
        public List<OdditariumLayer> CollectedLayers { get; set; } = new();

        /// <summary>Full history of turns for deterministic Undo.</summary>
        public List<OdditariumTurn> History { get; set; } = new();

        /// <summary>True when the session has been committed (draft assembled).</summary>
        public bool IsCommitted { get; set; }

        /// <summary>True when the game has started (persona + vibe selected, first round loaded).</summary>
        public bool IsActive { get; set; }

        // --- Phase 7: Structural scaffolding ---

        /// <summary>Schema version for CollectedLayers. v1 = List[string], v2 = List[OdditariumLayer].</summary>
        [JsonPropertyName("bodySchemaVersion")]
        public int BodySchemaVersion { get; set; } = 2;

        /// <summary>The anchor targeted by the currently pending question.</summary>
        public string? PendingAnchor { get; set; }

        /// <summary>The facet explored by the currently pending question.</summary>
        public string? PendingFacet { get; set; }

        /// <summary>The round mode of the currently pending question (expand/deepen/pivot).</summary>
        public string? PendingMode { get; set; }

        /// <summary>Which facets the LLM has already explored per anchor. Key = anchor, Value = list of visited facet IDs.</summary>
        public Dictionary<string, List<string>> VisitedFacets { get; set; } = new();

        /// <summary>Cumulative labels shown for the current pending question. Cleared when the question changes. Used by More... exclusion list.</summary>
        public List<string> ShownLabelsForCurrentQuestion { get; set; } = new();

        // --- Phase 7.5: Service-driven anchor queue ---

        /// <summary>Which anchor the service is currently targeting (expand or deepening). Null until first round.</summary>
        [JsonPropertyName("currentAnchorTarget")]
        public string? CurrentAnchorTarget { get; set; }

        /// <summary>How many more deepen rounds remain for CurrentAnchorTarget. 0 = advance to next anchor on next call.</summary>
        [JsonPropertyName("remainingDeepensForAnchor")]
        public int RemainingDeepensForAnchor { get; set; }

        /// <summary>Ordered list of unvisited anchors remaining in this session's queue. Built once at session start; mutated by resonance picks.</summary>
        [JsonPropertyName("anchorQueue")]
        public List<string> AnchorQueue { get; set; } = new();
    }

    /// <summary>
    /// Shape of a single LLM response in Odditarium. Both round and commit prompts
    /// must conform. Commit responses may additionally return an assembled <see cref="Draft"/> string.
    /// </summary>
    public sealed class OdditariumLLMResponse
    {
        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("anchor")]
        public string? Anchor { get; set; }

        [JsonPropertyName("facet")]
        public string? Facet { get; set; }

        [JsonPropertyName("options")]
        public List<OdditariumOption>? Options { get; set; }

        [JsonPropertyName("draft")]
        public string? Draft { get; set; }

        [JsonPropertyName("expansions")]
        public Dictionary<string, string>? Expansions { get; set; }
    }

    /// <summary>
    /// Fixed 13-anchor vocabulary for Odditarium. Anchors are the structural backbone of the assembled prompt.
    /// Facets are invented by the LLM at round time and tracked per anchor via <see cref="OdditariumBody.VisitedFacets"/>.
    /// </summary>
    public static class OdditariumAnchors
    {
        public static readonly string[] Core = { "subject", "setting" };
        public static readonly string[] Structural = { "action", "lighting", "framing", "atmosphere" };
        public static readonly string[] Enrichment = { "mood", "style", "detail", "time", "scale", "color", "movement" };

        /// <summary>All 13 anchors in presentation order (Core → Structural → Enrichment).</summary>
        public static readonly string[] All = Core.Concat(Structural).Concat(Enrichment).ToArray();

        /// <summary>Anchors eligible for Round 1 (Core + Structural only — no Enrichment).</summary>
        public static readonly string[] Round1Eligible = Core.Concat(Structural).ToArray();

        /// <summary>Anchors that generate structural debt when empty (Core + Structural).</summary>
        public static readonly string[] DebtTracked = Core.Concat(Structural).ToArray();

        private static readonly HashSet<string> _allSet = new(All, StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns true if <paramref name="anchor"/> is one of the 13 known anchors (case-insensitive).</summary>
        public static bool IsKnown(string anchor) => _allSet.Contains(anchor);
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
        /// Coerces unknown anchors to "detail", kebab-normalizes facets, and propagates mode/anchor/facet/expansions.
        /// Returns a new instance; does not mutate <paramref name="raw"/>.
        /// </summary>
        public static OdditariumLLMResponse Sanitize(OdditariumLLMResponse raw)
        {
            var question = (raw.Question ?? string.Empty).Trim();
            var draft = string.IsNullOrWhiteSpace(raw.Draft) ? null : raw.Draft.Trim();

            // Mode normalization
            var mode = raw.Mode?.Trim().ToLowerInvariant();
            if (mode != "expand" && mode != "deepen" && mode != "pivot")
                mode = null;

            // Anchor coercion — unknown values fall back to "detail"
            var anchor = raw.Anchor?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(anchor) && !OdditariumAnchors.IsKnown(anchor))
                anchor = "detail";

            // Facet kebab normalization
            var facet = raw.Facet?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(facet))
            {
                facet = Regex.Replace(facet, @"\s+", "-");
                facet = Regex.Replace(facet, @"[^a-z0-9\-]", "");
                facet = facet.Trim('-');
                if (string.IsNullOrWhiteSpace(facet)) facet = null;
            }

            // Expansions normalization (lowercase keys, trim values)
            Dictionary<string, string>? expansions = null;
            if (raw.Expansions is { Count: > 0 })
            {
                expansions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in raw.Expansions)
                {
                    var key = kv.Key?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(kv.Value))
                        expansions[key] = kv.Value.Trim();
                }
                if (expansions.Count == 0) expansions = null;
            }

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

                    // Normalize to Title Case — LLMs often return labels in lowercase.
                    label = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(label.ToLowerInvariant());

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
                        Anchor = anchor,
                        Facet = facet,
                    });

                    if (options.Count >= MaxOptions) break;
                }
            }

            return new OdditariumLLMResponse
            {
                Question = question,
                Mode = mode,
                Anchor = anchor,
                Facet = facet,
                Options = options,
                Draft = draft,
                Expansions = expansions,
            };
        }

        /// <summary>
        /// Attempts to parse a commit pass-1 response containing an "expansions" map.
        /// Returns true when at least one expansion entry is present.
        /// </summary>
        public static bool TryParseExpansions(string? rawJson, out Dictionary<string, string> expansions)
        {
            expansions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawJson)) return false;

            var trimmed = ExtractJsonObject(rawJson);
            if (trimmed is null) return false;

            OdditariumLLMResponse? parsed;
            try { parsed = JsonSerializer.Deserialize<OdditariumLLMResponse>(trimmed, OdditariumJsonOptions.Compact); }
            catch (JsonException) { return false; }

            if (parsed?.Expansions is not { Count: > 0 }) return false;

            foreach (var kv in parsed.Expansions)
            {
                var key = kv.Key?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(kv.Value))
                    expansions[key] = kv.Value.Trim();
            }
            return expansions.Count > 0;
        }

        /// <summary>
        /// Attempts to parse a commit pass-2 response containing a "draft" string.
        /// Returns true when draft is non-empty.
        /// </summary>
        public static bool TryParseDraft(string? rawJson, out string? draft)
        {
            draft = null;
            if (string.IsNullOrWhiteSpace(rawJson)) return false;

            var trimmed = ExtractJsonObject(rawJson);
            if (trimmed is null) return false;

            OdditariumLLMResponse? parsed;
            try { parsed = JsonSerializer.Deserialize<OdditariumLLMResponse>(trimmed, OdditariumJsonOptions.Compact); }
            catch (JsonException) { return false; }

            if (string.IsNullOrWhiteSpace(parsed?.Draft)) return false;
            draft = parsed.Draft.Trim();
            return true;
        }

        /// <summary>
        /// Some local models prepend prose or wrap output in code fences. Extract the
        /// first balanced JSON object substring; returns null if none found.
        /// Thinking models (Qwen3.5, QwQ, DeepSeek-R1, etc.) emit &lt;think&gt;...&lt;/think&gt;
        /// before the answer; those blocks are stripped first so the inner JSON-like
        /// content does not fool the brace-scanning logic.
        /// </summary>
        private static string? ExtractJsonObject(string raw)
        {
            // Strip <think>...</think> blocks produced by reasoning models.
            raw = Regex.Replace(raw, @"<think>[\s\S]*?</think>", string.Empty, RegexOptions.IgnoreCase);

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
