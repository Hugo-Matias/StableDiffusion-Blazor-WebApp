using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Two-pass tag builder: extracts visual concepts from natural language,
    /// resolves them against the Danbooru CSV catalog, and assembles a final
    /// comma-separated tag prompt respecting model-preset conventions.
    /// </summary>
    public class TagPromptService
    {
        private readonly CsvService _csvService;
        private readonly OllamaService _ollamaService;
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<TagPromptService> _logger;

        public TagPromptService(
            CsvService csvService,
            OllamaService ollamaService,
            IDatabaseService databaseService,
            ILogger<TagPromptService> logger)
        {
            _csvService = csvService;
            _ollamaService = ollamaService;
            _databaseService = databaseService;
            _logger = logger;
        }

        /// <summary>
        /// Pass 1: Extract visual concepts from user input using the LLM.
        /// Outputs one concept per line, optionally prefixed with [category].
        /// </summary>
        public async Task<List<ExtractedConcept>> ExtractConceptsAsync(
            string userInput,
            string modelName,
            OllamaOptions? options = null,
            CancellationToken ct = default)
        {
            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = @"You are a visual concept extractor for anime/illustration image-generation prompts. Decompose a natural-language description into ATOMIC visual concepts: single nouns or tight noun phrases that map cleanly to Danbooru tag vocabulary.

Category prefix (REQUIRED on every line; pick the closest): [subject], [character], [copyright], [artist], [setting], [background], [lighting], [time_of_day], [weather], [mood], [style], [composition], [color], [clothing], [accessory], [pose], [expression], [action].

Rules:
- One concept per line, atomic. Prefer 1-3 words. Plain English or snake_case both fine.
- Decompose multi-element phrases. ""cyberpunk woman at the beach at sunset"" -> 5 lines (subject, style, setting, time_of_day, mood/atmosphere).
- Skip filler words (""beautiful"", ""nice"", ""with a""). Skip articles. No commentary, no numbering, no bullets.
- If the user already names a Danbooru-style tag (e.g. 1girl, looking_at_viewer), keep it verbatim.
- Output ONLY the list of prefixed concepts, nothing else.

Examples:

Input: cyberpunk woman at the beach at sunset
[subject] woman
[style] cyberpunk
[setting] beach
[time_of_day] sunset
[mood] atmospheric

Input: 1girl, knight, holding sword in a forest, dramatic lighting
[subject] 1girl
[subject] knight
[action] holding sword
[setting] forest
[lighting] dramatic lighting"
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = userInput.Trim()
                }
            };

            // Pass 1 prefers determinism. Caller can override.
            var pass1Options = options ?? new OllamaOptions { Temperature = 0.3f };
            var response = await _ollamaService.SendChatMessage(modelName, messages, pass1Options);
            var rawContent = response?.Message?.Content ?? string.Empty;
            _lastRawPass1 = rawContent;

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                _logger.LogWarning("Empty response from LLM during concept extraction for input: {Input}", userInput);
                return new List<ExtractedConcept>();
            }

            // Try JSON parse first (some models output structured data)
            var concepts = TryParseJsonConcepts(rawContent);
            if (concepts != null)
                return concepts;

            // Fallback: line-based parsing
            return ParseLineBasedConcepts(rawContent);
        }

        /// <summary>
        /// Attempts to parse LLM output as JSON array of concept objects.
        /// Returns null if not valid JSON.
        /// </summary>
        private List<ExtractedConcept>? TryParseJsonConcepts(string raw)
        {
            try
            {
                // Handle markdown code blocks
                var json = raw.Trim();
                if (json.StartsWith("```"))
                {
                    var lines = json.Split('\n');
                    var startIdx = 1; // skip opening ```
                    var endIdx = lines.Length - 1;
                    if (lines[endIdx].Trim() == "```")
                        endIdx--;
                    json = string.Join("\n", lines[startIdx..(endIdx + 1)]);
                }

                using var doc = JsonDocument.Parse(json);
                var concepts = new List<ExtractedConcept>();

                // Try as array of objects with "text" field
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        string? text = null;
                        TagCategory? category = null;

                        if (elem.TryGetProperty("text", out var textProp))
                            text = textProp.GetString()?.Trim();
                        else if (elem.ValueKind == JsonValueKind.String)
                            text = elem.GetString()?.Trim();

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            string? hint = null;
                            if (elem.TryGetProperty("category", out var catProp))
                            {
                                (category, hint) = MapPrefix(catProp.GetString());
                            }
                            concepts.Add(new ExtractedConcept(text, category, hint));
                        }
                    }
                }

                return concepts.Count > 0 ? concepts : null;
            }
            catch (JsonException)
            {
                // Not JSON - fall through to line parsing
                return null;
            }
        }

        /// <summary>
        /// Parses LLM output as one concept per line, with optional [category] prefix.
        /// </summary>
        private List<ExtractedConcept> ParseLineBasedConcepts(string raw)
        {
            var concepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<ExtractedConcept>();

            foreach (var line in raw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                TagCategory? category = null;
                string? hint = null;
                string text = trimmed;

                // Check for [category] prefix
                if (trimmed.StartsWith('[') && trimmed.Contains(']'))
                {
                    var bracketEnd = trimmed.IndexOf(']', StringComparison.Ordinal);
                    var catName = trimmed[1..bracketEnd].Trim();
                    (category, hint) = MapPrefix(catName);

                    text = trimmed[(bracketEnd + 1)..].Trim().Trim(':', ' ');
                }

                // Remove leading bullet/number markers
                text = text.TrimStart('-', '*', '.', ')');
                if (char.IsDigit(text[0]))
                {
                    var dotIdx = text.IndexOf('.');
                    if (dotIdx > 0 && dotIdx < 3)
                        text = text[(dotIdx + 1)..].Trim();
                }

                text = text.Trim();

                if (!string.IsNullOrWhiteSpace(text) && concepts.Add(text))
                {
                    result.Add(new ExtractedConcept(text, category, hint));
                }
            }

            return result;
        }

        /// <summary>
        /// Maps a Pass-1 prefix label to a Danbooru <see cref="TagCategory"/> (when applicable)
        /// and a free-form Hint used for resolver biasing.
        /// </summary>
        private static (TagCategory? Category, string? Hint) MapPrefix(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (null, null);

            var lower = name.Trim().ToLowerInvariant();
            // Hard category mappings (Danbooru-native)
            switch (lower)
            {
                case "subject": return (TagCategory.General, "subject");
                case "character": case "char": return (TagCategory.Character, "character");
                case "artist": return (TagCategory.Artist, "artist");
                case "copyright": case "copy": return (TagCategory.Copyright, "copyright");
            }
            // Recognized hint-only labels (Danbooru lacks a native category, so leave Category null
            // and use Hint to bias scoring downstream).
            var known = new HashSet<string>
            {
                "setting", "background", "lighting", "time_of_day", "weather",
                "mood", "style", "composition", "color", "clothing",
                "accessory", "pose", "expression", "action"
            };
            return known.Contains(lower) ? (null, lower) : (null, null);
        }

        /// <summary>Stop-words removed before phrase tokenization in <see cref="ResolveAsync"/>.</summary>
        private static readonly HashSet<string> s_stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "of", "in", "on", "at", "to", "with",
            "from", "by", "for", "into", "over", "under", "is", "are", "as",
            "this", "that", "these", "those", "it", "its", "very", "some"
        };

        // Captured raw responses from the most recent pipeline run (per-call), surfaced via TagBuilderResult.
        [ThreadStatic] private static string? _lastRawPass1;
        [ThreadStatic] private static string? _lastRawPass2;

        /// <summary>
        /// Resolution layer: match each concept against the Danbooru CSV catalog.
        /// Filters by category toggles, returns top-K candidates per concept.
        /// </summary>
        public async Task<List<ResolvedConcept>> ResolveAsync(
            IEnumerable<ExtractedConcept> concepts,
            TagBuilderRequest request,
            CancellationToken ct = default)
        {
            const int maxCandidatesPerConcept = 10;
            var resolvedList = new List<ResolvedConcept>();

            foreach (var concept in concepts)
            {
                ct.ThrowIfCancellationRequested();

                // Build sub-queries: full phrase + significant tokens. This rescues phrases like
                // "neon lights" / "dramatic lighting" that fuzzy-fail when matched whole.
                var subQueries = new List<string> { concept.Text };
                foreach (var tok in TokenizeForSearch(concept.Text))
                {
                    if (!subQueries.Any(q => string.Equals(q, tok, StringComparison.OrdinalIgnoreCase)))
                        subQueries.Add(tok);
                }

                // Merge candidates across sub-queries; keep best fuzzy score per tag.
                var merged = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
                foreach (var q in subQueries)
                {
                    var hits = await _csvService.SearchTags(q, enableFuzzy: true);
                    foreach (var t in hits)
                    {
                        if (!merged.TryGetValue(t.Name, out var existing) || t.FuzzyScore > existing.FuzzyScore)
                            merged[t.Name] = t;
                    }
                }

                IEnumerable<Tag> candidates = merged.Values;

                // Filter by category toggles
                if (request.CategoryToggles.Any())
                {
                    candidates = candidates.Where(t =>
                    {
                        var cat = DanbooruCategory.FromColor(t.Color);
                        return request.CategoryToggles.GetValueOrDefault(cat, true);
                    });
                }

                // Bias scoring with the concept's category / hint (re-order only, no filter).
                var biased = candidates.Select(t => new { Tag = t, Score = t.FuzzyScore + CategoryBoost(concept, t) })
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Tag.Uses)
                    .Select(x => x.Tag);

                var topCandidates = biased.Take(maxCandidatesPerConcept).ToList();

                resolvedList.Add(new ResolvedConcept(concept, topCandidates.AsReadOnly()));
            }

            return resolvedList;
        }

        private static IEnumerable<string> TokenizeForSearch(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) yield break;
            var separators = new[] { ' ', ',', ';', ':', '/', '\\', '\t', '\n', '\r', '\"', '\'', '(', ')' };
            foreach (var raw in phrase.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = raw.Trim().Trim('-', '_', '.').ToLowerInvariant();
                if (t.Length < 3) continue;
                if (s_stopWords.Contains(t)) continue;
                yield return t;
            }
        }

        /// <summary>
        /// Adds a constant boost when a candidate tag matches the concept's declared Danbooru category
        /// or hint label. Never goes negative; only re-orders.
        /// </summary>
        private static int CategoryBoost(ExtractedConcept concept, Tag tag)
        {
            int boost = 0;
            var tagCat = DanbooruCategory.FromColor(tag.Color);
            if (concept.Category.HasValue && tagCat == concept.Category.Value)
                boost += 150;

            // Hint-based heuristics: prefer general-category tags for setting/style/etc.
            if (!string.IsNullOrEmpty(concept.Hint) && tagCat == TagCategory.General)
                boost += 50;
            return boost;
        }

        /// <summary>
        /// Quality-tag prefix conventions per model preset.
        /// Verify dialect at implementation time - conventions drift.
        /// </summary>
        private static readonly Dictionary<TagModelPreset, string> s_presetQualityTags = new()
        {
            // Verify dialect at implementation time - conventions drift. User can override via Pass-2 system prompt.
            [TagModelPreset.Pony] = "score_9, score_8_up, score_7_up, source_anime, rating_safe",
            [TagModelPreset.Illustrious] = "masterpiece, best quality, amazing quality",
            [TagModelPreset.NoobAI] = "masterpiece, best quality, very aesthetic",
            [TagModelPreset.Anima] = "masterpiece, best quality, very aesthetic, absurdres"
        };

        /// <summary>
        /// Verbosity profile: target tag count window, hard cap, and explicit composition guidance.
        /// The LLM is given the full profile text so the verbosity slider produces visibly different
        /// outputs rather than collapsing to similar lengths.
        /// <para>
        /// <c>Target</c> bounds are advisory (sent to the model); <c>Max</c> is the hard cap enforced
        /// in post-processing. Counts INCLUDE the preset quality prefix tokens.
        /// </para>
        /// </summary>
        private record VerbosityProfile(int TargetMin, int TargetMax, int Max, string Guidance);

        private static readonly Dictionary<TagVerbosity, VerbosityProfile> s_verbosityProfiles = new()
        {
            [TagVerbosity.Minimal] = new VerbosityProfile(
                TargetMin: 8, TargetMax: 12, Max: 15,
                Guidance: "Bare essentials only. Quality prefix + ONE subject tag + ONE setting/background tag + ONE lighting or style tag. " +
                          "Skip accessories, composition, color, mood. Output should feel like a tight headline, not a description."),
            [TagVerbosity.Standard] = new VerbosityProfile(
                TargetMin: 16, TargetMax: 22, Max: 28,
                Guidance: "Well-formed prompt. Quality prefix + 1-2 subject tags + 1-2 setting/background tags + 1 lighting + 1 mood/atmosphere + 1 style + 1-2 details (clothing OR pose OR expression). " +
                          "No more than one tag per minor slot. Skip composition unless it's central to the request."),
            [TagVerbosity.Detailed] = new VerbosityProfile(
                TargetMin: 28, TargetMax: 38, Max: 45,
                Guidance: "Rich prompt. Quality prefix + 2-3 subject tags + 2-3 setting/background + 2 lighting + 1-2 mood + 1-2 style + composition + 3-5 detail tags (clothing, pose, expression, accessories). " +
                          "Cover most slots once."),
            [TagVerbosity.Exhaustive] = new VerbosityProfile(
                TargetMin: 50, TargetMax: 65, Max: 75,
                Guidance: "Fully fleshed out. Quality prefix + 3-4 subject tags + 3-4 setting/background + 2-3 lighting + 2-3 mood + 2-3 style + 2 composition + many detail tags (clothing, pose, expression, accessories, color palette). " +
                          "Aim to cover every applicable slot multiple times. Avoid filler synonyms.")
        };

        // Backwards-compatible alias for code paths that only need the hard cap.
        private static int VerbosityMax(TagVerbosity v) => s_verbosityProfiles.TryGetValue(v, out var p) ? p.Max : 45;

        /// <summary>
        /// Pass 2: Assemble resolved tags into a final prompt respecting model preset conventions.
        /// </summary>
        public async Task<string> AssemblePromptAsync(
            IEnumerable<ResolvedConcept> resolved,
            TagBuilderRequest request,
            string modelName,
            OllamaOptions? options = null,
            CancellationToken ct = default)
        {
            var resolvedList = resolved.ToList();

            // Build tag payload grouped by category for the LLM
            var tagsByCategory = new Dictionary<string, List<string>>();
            foreach (var rc in resolvedList)
            {
                foreach (var tag in rc.Candidates)
                {
                    var catName = DanbooruCategory.FromColor(tag.Color).ToString().ToLowerInvariant();
                    if (!tagsByCategory.TryGetValue(catName, out var list))
                    {
                        list = new List<string>();
                        tagsByCategory[catName] = list;
                    }
                    list.Add(tag.Name);
                }
            }

            // Build the tag summary string for LLM context
            var tagSummary = string.Join("\n", tagsByCategory.Select(kvp =>
                $"[{kvp.Key}]: {string.Join(", ", kvp.Value.Distinct())}"));

            var qualityTags = s_presetQualityTags.GetValueOrDefault(request.Preset, "");
            var profile = s_verbosityProfiles[request.Verbosity];
            var maxTags = profile.Max;

            // Build grounding context if provided
            string groundingContext = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.GroundingPrompt))
            {
                groundingContext = $"\nUser's existing prompt (keep compatible elements): \"{request.GroundingPrompt}\"";
            }

            var systemContent = $"You are a Danbooru tag prompt assembler. Given resolved tag candidates grouped by category, assemble a final comma-separated tag prompt.\n" +
                $"Preset: {request.Preset}\n" +
                $"Verbosity level: {request.Verbosity}\n" +
                $"TARGET tag count: {profile.TargetMin}-{profile.TargetMax} tags (including the quality prefix). Hard cap: {profile.Max}.\n" +
                $"Composition guidance for this verbosity: {profile.Guidance}\n" +
                $"Quality prefix for this preset: {qualityTags}\n\n" +
                "Rules:\n" +
                "- Start with the quality tags for the preset, verbatim, comma-separated.\n" +
                "- Stay within the TARGET tag count. Do NOT just dump every candidate; trim to fit the verbosity.\n" +
                "- Pick the best 1-2 tags per concept from the candidates following the composition guidance above.\n" +
                "- If the candidates for a concept are weak, you MAY emit your own Danbooru-style snake_case tag for that concept, prefixed with `~` (tilde). Limit augmentations to 6 total. Do NOT invent character or artist names.\n" +
                "- Order: quality tags, subject/character, setting/background, lighting/atmosphere, style/art medium, composition/camera, accessories/details.\n" +
                "- Do NOT duplicate tags. No prose, no commentary, no markdown.\n" +
                "- Output ONLY the comma-separated tag prompt on a single line." +
                groundingContext;

            var userContent = $"Resolved tag candidates by category:\n\n{tagSummary}\n\nAssemble the final prompt now.";

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "system", Content = systemContent },
                new OllamaChatMessage { Role = "user", Content = userContent }
            };

            // Pass 2 prefers stable creative output; caller can override.
            var pass2Options = options ?? new OllamaOptions { Temperature = 0.4f };
            var response = await _ollamaService.SendChatMessage(modelName, messages, pass2Options);
            var rawOutput = response?.Message?.Content ?? string.Empty;
            _lastRawPass2 = rawOutput;

            if (string.IsNullOrWhiteSpace(rawOutput))
                return string.Empty;

            var (assembled, _) = PostProcessTags(rawOutput, maxTags, request.KeepUnverifiedAugmentations);
            return assembled;
        }

        /// <summary>
        /// Post-processes a raw comma-separated LLM tag list:
        /// trims, lower-cases, dedupes preserving order, clamps to verbosity limit, and verifies any
        /// `~`-prefixed augmentations against the CSV cache. Unverified augmentations are dropped
        /// unless <paramref name="keepUnverified"/> is true.
        /// </summary>
        private (string Assembled, List<string> Dropped) PostProcessTags(string raw, int maxTags, bool keepUnverified)
        {
            // Strip code fences / markdown wrappers if present
            var cleaned = raw.Trim();
            if (cleaned.StartsWith("```"))
            {
                var lines = cleaned.Split('\n');
                cleaned = string.Join("\n", lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```")));
            }
            // Take first non-empty line as the tag line if model added prose around it.
            var tagLine = cleaned.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains(',') && !string.IsNullOrWhiteSpace(l))
                          ?? cleaned;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();
            var dropped = new List<string>();

            foreach (var rawTag in tagLine.Split(','))
            {
                var t = rawTag.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(t)) continue;

                bool augmented = t.StartsWith('~');
                if (augmented) t = t[1..].Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;

                if (augmented && !keepUnverified)
                {
                    var underscored = t.Replace(' ', '_');
                    if (!_csvService.CheckTagExistsCached(underscored))
                    {
                        dropped.Add(t);
                        _logger.LogDebug("Dropped unverified augmentation tag: {Tag}", t);
                        continue;
                    }
                    t = underscored;
                }

                if (seen.Add(t)) ordered.Add(t);
            }

            if (ordered.Count > maxTags)
                ordered = ordered.Take(maxTags).ToList();

            return (string.Join(", ", ordered), dropped);
        }

        /// <summary>
        /// Orchestrates the full pipeline: Pass 1 -> Resolve -> Pass 2.
        /// </summary>
        public async Task<TagBuilderResult> BuildAsync(
            TagBuilderRequest request,
            string modelName,
            OllamaOptions? options = null,
            CancellationToken ct = default)
        {
            _lastRawPass1 = null;
            _lastRawPass2 = null;

            if (request.Mode == TagBuilderMode.Hybrid)
                return await BuildHybridAsync(request, modelName, options, ct);

            // Pass 1: Extract concepts
            var concepts = await ExtractConceptsAsync(request.UserInput, modelName, options, ct);
            if (!concepts.Any())
            {
                _logger.LogWarning("No concepts extracted from input: {Input}", request.UserInput);
                return new TagBuilderResult(
                    FinalPrompt: string.Empty,
                    ResolvedConcepts: Array.Empty<ResolvedConcept>().AsReadOnly(),
                    ModelUsed: modelName,
                    Preset: request.Preset,
                    Verbosity: request.Verbosity,
                    RawPass1: _lastRawPass1,
                    RawPass2: null,
                    Mode: TagBuilderMode.TwoPass);
            }

            // Resolution: match concepts to Danbooru tags
            var resolved = await ResolveAsync(concepts, request, ct);

            // Pass 2: Assemble final prompt
            var finalPrompt = await AssemblePromptAsync(resolved, request, modelName, options, ct);

            return new TagBuilderResult(
                FinalPrompt: finalPrompt,
                ResolvedConcepts: resolved.AsReadOnly(),
                ModelUsed: modelName,
                Preset: request.Preset,
                Verbosity: request.Verbosity,
                RawPass1: _lastRawPass1,
                RawPass2: _lastRawPass2,
                Mode: TagBuilderMode.TwoPass);
        }

        /// <summary>
        /// One-pass mode: ask the LLM to produce a Danbooru tag prompt directly, then verify each
        /// emitted tag against the CSV cache. Faster but requires a model that already speaks the
        /// Danbooru vocabulary.
        /// </summary>
        public async Task<TagBuilderResult> BuildHybridAsync(
            TagBuilderRequest request,
            string modelName,
            OllamaOptions? options = null,
            CancellationToken ct = default)
        {
            var qualityTags = s_presetQualityTags.GetValueOrDefault(request.Preset, "");
            var profile = s_verbosityProfiles[request.Verbosity];
            var maxTags = profile.Max;

            string groundingContext = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.GroundingPrompt))
                groundingContext = $"\nUser's existing prompt (keep compatible elements): \"{request.GroundingPrompt}\"";

            var systemContent = $"You are an expert Danbooru tag prompt writer for anime/illustration image models. Convert the user's natural-language description into a comma-separated Danbooru tag prompt.\n" +
                $"Preset: {request.Preset}\n" +
                $"Verbosity level: {request.Verbosity}\n" +
                $"TARGET tag count: {profile.TargetMin}-{profile.TargetMax} tags (including the quality prefix). Hard cap: {profile.Max}.\n" +
                $"Composition guidance for this verbosity: {profile.Guidance}\n" +
                $"Quality prefix: {qualityTags}\n\n" +
                "Rules:\n" +
                "- Use real Danbooru tags in snake_case where applicable (e.g. 1girl, looking_at_viewer, cityscape, neon_lights).\n" +
                "- Start with the quality prefix verbatim.\n" +
                "- Stay within the TARGET tag count. Resist the urge to over-tag at lower verbosity; resist sparse output at higher verbosity.\n" +
                "- Order: quality, subject/character, setting/background, lighting/atmosphere, style, composition, accessories/details.\n" +
                "- No duplicates. No prose, no commentary, no markdown, no code fence.\n" +
                "- Output ONLY the comma-separated tag prompt on a single line." +
                groundingContext;

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "system", Content = systemContent },
                new OllamaChatMessage { Role = "user", Content = request.UserInput.Trim() }
            };

            var hybridOptions = options ?? new OllamaOptions { Temperature = 0.4f };
            var response = await _ollamaService.SendChatMessage(modelName, messages, hybridOptions);
            var raw = response?.Message?.Content ?? string.Empty;
            _lastRawPass2 = raw;

            // Verify every emitted tag against the CSV cache. Quality-prefix tokens (e.g. score_9,
            // masterpiece) are kept regardless because they're preset-mandated, not Danbooru proper.
            var prefixSet = new HashSet<string>(
                qualityTags.Split(',').Select(t => t.Trim().ToLowerInvariant()).Where(t => !string.IsNullOrEmpty(t)),
                StringComparer.OrdinalIgnoreCase);

            var (assembled, dropped) = PostProcessHybridTags(raw, maxTags, prefixSet, request.KeepUnverifiedAugmentations);

            return new TagBuilderResult(
                FinalPrompt: assembled,
                ResolvedConcepts: Array.Empty<ResolvedConcept>().AsReadOnly(),
                ModelUsed: modelName,
                Preset: request.Preset,
                Verbosity: request.Verbosity,
                RawPass1: null,
                RawPass2: _lastRawPass2,
                Mode: TagBuilderMode.Hybrid,
                DroppedAugmentations: dropped);
        }

        private (string Assembled, List<string> Dropped) PostProcessHybridTags(string raw, int maxTags, HashSet<string> prefixSet, bool keepUnverified)
        {
            var cleaned = raw.Trim();
            if (cleaned.StartsWith("```"))
            {
                var lines = cleaned.Split('\n');
                cleaned = string.Join("\n", lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```")));
            }
            var tagLine = cleaned.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains(',') && !string.IsNullOrWhiteSpace(l))
                          ?? cleaned;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();
            var dropped = new List<string>();

            foreach (var rawTag in tagLine.Split(','))
            {
                var t = rawTag.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(t)) continue;

                // Strip ComfyUI-style weight syntax: "(tag:1.2)" -> "tag", "(tag)" -> "tag".
                // The LLM frequently emits weighted tags; we want to keep the visual weight in
                // the final output but verify against the unweighted tag name.
                var (core, weightSuffix, weightPrefix) = StripWeightSyntax(t);

                if (prefixSet.Contains(core))
                {
                    var key = string.IsNullOrEmpty(weightSuffix) ? core : weightPrefix + core + weightSuffix;
                    if (seen.Add(core)) ordered.Add(key);
                    continue;
                }

                var underscored = core.Replace(' ', '_');
                bool exists = _csvService.CheckTagExistsCached(underscored);
                if (!exists && !keepUnverified)
                {
                    dropped.Add(t);
                    _logger.LogDebug("Hybrid: dropped unverified tag: {Tag}", t);
                    continue;
                }

                var canonical = exists ? underscored : core.Replace(' ', '_');
                var keep = string.IsNullOrEmpty(weightSuffix) ? canonical : weightPrefix + canonical + weightSuffix;
                if (seen.Add(canonical)) ordered.Add(keep);
            }

            if (ordered.Count > maxTags)
                ordered = ordered.Take(maxTags).ToList();

            return (string.Join(", ", ordered), dropped);
        }

        /// <summary>
        /// Strips ComfyUI / A1111 weight syntax around a tag token so the inner name can be
        /// verified against the CSV catalogue.
        /// "(tag:1.2)" -> ("tag", ":1.2)", "("); "(tag)" -> ("tag", ")", "("); "tag" -> ("tag", "", "").
        /// The prefix/suffix carry the original surrounding characters so the caller can reconstruct
        /// the weighted token after verification swaps the inner core for its canonical (underscored) form.
        /// </summary>
        private static (string Core, string Suffix, string Prefix) StripWeightSyntax(string token)
        {
            if (string.IsNullOrEmpty(token)) return (token, string.Empty, string.Empty);

            // Only match the simple "(name:weight)" or "(name)" wrapper. We do not attempt to
            // unwrap nested brackets (those are uncommon in tag prompts and a literal verify is fine).
            if (token.Length >= 3 && token[0] == '(' && token[^1] == ')')
            {
                var inner = token.Substring(1, token.Length - 2);
                var colon = inner.LastIndexOf(':');
                if (colon > 0 && colon < inner.Length - 1)
                {
                    var weight = inner.Substring(colon); // includes the leading ':'
                    var name = inner.Substring(0, colon).Trim();
                    if (name.Length > 0)
                        return (name, weight + ")", "(");
                }
                else
                {
                    var name = inner.Trim();
                    if (name.Length > 0)
                        return (name, ")", "(");
                }
            }
            return (token, string.Empty, string.Empty);
        }
    }
}
