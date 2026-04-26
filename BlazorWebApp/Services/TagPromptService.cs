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
                    Content = @"You are a visual concept extractor for image-generation prompts. Given a natural-language description, extract the key visual concepts as one concept per line.

Optional category prefix: [subject], [setting], [lighting], [clothing], [mood], [style], [composition], [color], [time_of_day], [weather], [action], [accessory], [pose], [expression], [background].

Rules:
- Output one concept per line.
- Keep concepts short (1-4 words).
- Do NOT output Danbooru tags - use plain English.
- Skip filler words and generic descriptors like 'beautiful' or 'nice'.
- If the input is already very simple, just return it as a single line.
- Output ONLY the list of concepts, nothing else."
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = userInput.Trim()
                }
            };

            var response = await _ollamaService.SendChatMessage(modelName, messages, options);
            var rawContent = response?.Message?.Content ?? string.Empty;

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
                            if (elem.TryGetProperty("category", out var catProp))
                            {
                                category = ParseCategoryFromName(catProp.GetString());
                            }
                            concepts.Add(new ExtractedConcept(text, category));
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
                string text = trimmed;

                // Check for [category] prefix
                if (trimmed.StartsWith('[') && trimmed.Contains(']'))
                {
                    var bracketEnd = trimmed.IndexOf(']', StringComparison.Ordinal);
                    var catName = trimmed[1..bracketEnd].Trim();
                    category = ParseCategoryFromName(catName);

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
                    result.Add(new ExtractedConcept(text, category));
                }
            }

            return result;
        }

        /// <summary>
        /// Maps a category name string to TagCategory enum.
        /// </summary>
        private static TagCategory? ParseCategoryFromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var lower = name.ToLowerInvariant();
            return lower switch
            {
                "subject" => TagCategory.General,
                "character" or "char" => TagCategory.Character,
                "artist" => TagCategory.Artist,
                "copyright" or "copy" => TagCategory.Copyright,
                _ => null // setting, lighting, mood, style, etc. map to null (no Danbooru category)
            };
        }

        /// <summary>
        /// Resolution layer: match each concept against the Danbooru CSV catalog.
        /// Filters by category toggles, returns top-K candidates per concept.
        /// </summary>
        public async Task<List<ResolvedConcept>> ResolveAsync(
            IEnumerable<ExtractedConcept> concepts,
            TagBuilderRequest request,
            CancellationToken ct = default)
        {
            const int maxCandidatesPerConcept = 5;
            var resolvedList = new List<ResolvedConcept>();

            foreach (var concept in concepts)
            {
                ct.ThrowIfCancellationRequested();

                // Search the CSV catalog for this concept
                var candidates = await _csvService.SearchTags(concept.Text, enableFuzzy: true);

                // Filter by category toggles
                if (request.CategoryToggles.Any())
                {
                    candidates = candidates.Where(t =>
                    {
                        var cat = DanbooruCategory.FromColor(t.Color);
                        return request.CategoryToggles.GetValueOrDefault(cat, true);
                    });
                }

                var topCandidates = candidates.Take(maxCandidatesPerConcept).ToList();

                resolvedList.Add(new ResolvedConcept(concept, topCandidates.AsReadOnly()));
            }

            return resolvedList;
        }

        /// <summary>
        /// Quality-tag prefix conventions per model preset.
        /// Verify dialect at implementation time - conventions drift.
        /// </summary>
        private static readonly Dictionary<TagModelPreset, string> s_presetQualityTags = new()
        {
            [TagModelPreset.Pony] = "score_9,score_8_up,score_7_up",
            [TagModelPreset.Illustrious] = "masterpiece,best quality,amazing quality",
            [TagModelPreset.NoobAI] = "masterpiece,best quality,very aesthetic",
            [TagModelPreset.Anima] = "masterpiece,best quality,very aesthetic,absurdres"
        };

        /// <summary>
        /// Max tag count per verbosity level.
        /// </summary>
        private static readonly Dictionary<TagVerbosity, int> s_verbosityLimits = new()
        {
            [TagVerbosity.Minimal] = 15,
            [TagVerbosity.Standard] = 30,
            [TagVerbosity.Detailed] = 50,
            [TagVerbosity.Exhaustive] = 80
        };

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
            var maxTags = s_verbosityLimits.GetValueOrDefault(request.Verbosity, 50);

            // Build grounding context if provided
            string groundingContext = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.GroundingPrompt))
            {
                groundingContext = $"\nUser's existing prompt (keep compatible elements): \"{request.GroundingPrompt}\"";
            }

            var systemContent = $"You are a Danbooru tag prompt assembler. Given resolved tag candidates grouped by category, assemble a final comma-separated tag prompt.\n" +
                $"Preset: {request.Preset}\n" +
                $"Verbosity: {request.Verbosity} (max {maxTags} tags)\n" +
                $"Quality prefix for this preset: {qualityTags}\n\n" +
                "Rules:\n" +
                "- Start with the quality tags for the preset.\n" +
                "- Select ONE tag per concept from the candidates (pick the best match).\n" +
                "- Order: quality tags, subject/character, setting/background, lighting/atmosphere, style/art medium, composition/camera, accessories/details.\n" +
                "- Do NOT duplicate tags.\n" +
                "- Output ONLY the comma-separated tag prompt, nothing else." +
                groundingContext;

            var userContent = $"Resolved tag candidates by category:\n\n{tagSummary}\n\nAssemble the final prompt now.";

            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "system", Content = systemContent },
                new OllamaChatMessage { Role = "user", Content = userContent }
            };

            var response = await _ollamaService.SendChatMessage(modelName, messages, options);
            var rawOutput = response?.Message?.Content ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawOutput))
                return string.Empty;

            // Post-process: split on comma, trim, dedupe preserving order, clamp length
            var tags = rawOutput.Split(',')
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tags.Count > maxTags)
                tags = tags.Take(maxTags).ToList();

            return string.Join(", ", tags);
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
                    RawPass1: null,
                    RawPass2: null);
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
                RawPass1: null,
                RawPass2: null);
        }
    }
}
