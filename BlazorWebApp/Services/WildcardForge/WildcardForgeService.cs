using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services.WildcardForge.Models;
using Microsoft.Extensions.Logging;

namespace BlazorWebApp.Services.WildcardForge
{
    /// <summary>
    /// Phase 10 - default implementation. Delegates compose to <see cref="PromptComposer"/>,
    /// LLM call to <see cref="OllamaService"/>, persistence to <see cref="IDatabaseService"/>.
    /// </summary>
    public class WildcardForgeService : IWildcardForgeService
    {
        private readonly WildcardForgeKnowledge _knowledge;
        private readonly PromptComposer _composer;
        private readonly OllamaService _ollama;
        private readonly IDatabaseService _db;
        private readonly ILogger<WildcardForgeService> _logger;

        public WildcardForgeService(
            WildcardForgeKnowledge knowledge,
            PromptComposer composer,
            OllamaService ollama,
            IDatabaseService db,
            ILogger<WildcardForgeService> logger)
        {
            _knowledge = knowledge;
            _composer = composer;
            _ollama = ollama;
            _db = db;
            _logger = logger;
        }

        public async Task<ForgeDraftDto> RunAsync(ForgeRequest request, string modelName)
        {
            // Convert is a special path; doesn't go through PromptComposer at all.
            if (request.Operation == ForgeOperation.Convert)
                return await ConvertAsync(request, modelName);

            var messages = _composer.Compose(request);

            var template = request.Operation == ForgeOperation.Describe
                ? null
                : _knowledge.GetTemplate(request.Operation);

            var options = new OllamaOptions
            {
                Temperature = template?.RecommendedTemperature ?? 0.7f,
                TopP = template?.RecommendedTopP ?? 0.9f,
            };

            var response = await _ollama.SendChatMessage(modelName, messages, options);
            var raw = response?.Message?.Content ?? string.Empty;

            return request.Operation switch
            {
                ForgeOperation.Describe => BuildDescribeDraft(request, raw),
                ForgeOperation.Refine => BuildRefineDraft(request, raw),
                _ => BuildGenerateOrExpandDraft(request, raw)
            };
        }

        public IReadOnlyList<string> PreviewMessages(ForgeRequest request)
            => _composer.Compose(request).Select(m => $"[{m.Role}]\n{m.Content}").ToList();

        public async Task<int> SaveDraftAsync(
            ForgeDraftDto draft,
            string mode,
            int? targetCollectionId = null,
            string? newName = null,
            string? newCategory = null,
            string? newDescription = null)
        {
            var accepted = draft.Entries.Where(e => e.Accepted && !string.IsNullOrWhiteSpace(e.Value)).ToList();
            if (accepted.Count == 0)
                throw new InvalidOperationException("Cannot save: no accepted entries in the draft.");

            WildcardCollection collection;

            if (string.Equals(mode, "new", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(newName))
                    throw new ArgumentException("New collection requires a name.", nameof(newName));

                collection = await _db.CreateWildcardCollection(new WildcardCollection
                {
                    Name = newName.Trim(),
                    Description = newDescription,
                    Category = newCategory ?? string.Empty,
                });

                await PersistEntries(collection.Id, accepted, startSortOrder: 0);
            }
            else if (string.Equals(mode, "append", StringComparison.OrdinalIgnoreCase))
            {
                if (targetCollectionId == null)
                    throw new ArgumentException("Append requires a targetCollectionId.", nameof(targetCollectionId));

                var existing = await _db.GetWildcardCollectionById(targetCollectionId.Value)
                    ?? throw new InvalidOperationException($"Collection {targetCollectionId} not found.");
                collection = existing;

                var existingEntries = await _db.GetEntriesByCollectionId(collection.Id);
                var nextSort = existingEntries.Count == 0 ? 0 : existingEntries.Max(e => e.SortOrder) + 1;
                await PersistEntries(collection.Id, accepted, startSortOrder: nextSort);
            }
            else if (string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase))
            {
                if (targetCollectionId == null)
                    throw new ArgumentException("Replace requires a targetCollectionId.", nameof(targetCollectionId));

                var existing = await _db.GetWildcardCollectionById(targetCollectionId.Value)
                    ?? throw new InvalidOperationException($"Collection {targetCollectionId} not found.");
                collection = existing;

                var existingEntries = await _db.GetEntriesByCollectionId(collection.Id);
                foreach (var e in existingEntries) await _db.DeleteWildcardEntry(e.Id);

                await PersistEntries(collection.Id, accepted, startSortOrder: 0);
            }
            else
            {
                throw new ArgumentException($"Unknown save mode '{mode}'.", nameof(mode));
            }

            return collection.Id;
        }

        // -------------------- Per-op draft builders --------------------

        private ForgeDraftDto BuildGenerateOrExpandDraft(ForgeRequest request, string raw)
        {
            var values = ParseEntriesTolerant(raw);
            var dedup = Dedupe(values, request.ExistingEntries?.Select(e => e.Value));

            return new ForgeDraftDto
            {
                Operation = request.Operation.ToString(),
                TargetCollectionId = request.TargetCollection?.Id,
                TargetCollectionName = request.TargetCollection?.Name,
                Entries = dedup.Select(v => new ForgeDraftEntryDto { Value = v, Status = "New", Accepted = true }).ToList(),
                UpdatedAt = DateTime.UtcNow
            };
        }

        private ForgeDraftDto BuildRefineDraft(ForgeRequest request, string raw)
        {
            var refined = ParseEntriesTolerant(raw);
            var originals = (request.ExistingEntries ?? new List<WildcardEntry>()).ToList();
            var entries = new List<ForgeDraftEntryDto>();

            // Best-effort 1:1 pairing by index. Extra refined items show as "New",
            // missing ones show as "Kept" (original preserved).
            for (var i = 0; i < originals.Count; i++)
            {
                var orig = originals[i].Value;
                if (i < refined.Count)
                {
                    var newVal = refined[i];
                    entries.Add(new ForgeDraftEntryDto
                    {
                        Value = newVal,
                        OriginalValue = orig,
                        Status = string.Equals(newVal.Trim(), orig.Trim(), StringComparison.OrdinalIgnoreCase) ? "Kept" : "Modified",
                        Accepted = true,
                    });
                }
                else
                {
                    entries.Add(new ForgeDraftEntryDto
                    {
                        Value = orig,
                        OriginalValue = orig,
                        Status = "Kept",
                        Accepted = true,
                    });
                }
            }
            for (var i = originals.Count; i < refined.Count; i++)
            {
                entries.Add(new ForgeDraftEntryDto { Value = refined[i], Status = "New", Accepted = true });
            }

            return new ForgeDraftDto
            {
                Operation = ForgeOperation.Refine.ToString(),
                TargetCollectionId = request.TargetCollection?.Id,
                TargetCollectionName = request.TargetCollection?.Name,
                Entries = entries,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private ForgeDraftDto BuildDescribeDraft(ForgeRequest request, string raw)
        {
            string? name = null, category = null, description = null;
            try
            {
                var json = ExtractJson(raw);
                if (!string.IsNullOrEmpty(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("name", out var n)) name = n.GetString();
                    if (doc.RootElement.TryGetProperty("category", out var c)) category = c.GetString();
                    if (doc.RootElement.TryGetProperty("description", out var d)) description = d.GetString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Describe op produced unparseable output");
            }

            // Carry forward the existing entries from the request unchanged - Describe doesn't
            // mutate entries; it only enriches the metadata that the Save dialog will consume.
            var entries = (request.ExistingEntries ?? new List<WildcardEntry>())
                .Select(e => new ForgeDraftEntryDto { Value = e.Value, Status = "Kept", Accepted = true })
                .ToList();

            return new ForgeDraftDto
            {
                Operation = ForgeOperation.Describe.ToString(),
                TargetCollectionId = request.TargetCollection?.Id,
                TargetCollectionName = request.TargetCollection?.Name,
                SuggestedName = name,
                SuggestedCategory = category,
                SuggestedDescription = description,
                Entries = entries,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        private async Task<ForgeDraftDto> ConvertAsync(ForgeRequest request, string modelName)
        {
            // v1 inline path: rewrite each entry to match a target verbosity level,
            // preserving meaning and visual descriptors. (Bridge to TagPromptService is a follow-up.)
            var entries = (request.ExistingEntries ?? new List<WildcardEntry>())
                .Take(25)
                .Select(e => e.Value).ToList();

            if (entries.Count == 0)
                return new ForgeDraftDto { Operation = "Convert", Entries = new(), UpdatedAt = DateTime.UtcNow };

            var verbosityRubric = request.Verbosity switch
            {
                "minimal" => "MINIMAL verbosity (1-3 words; compact comma-separated tags; remove articles, connectors, and prepositions; keep only the most distinctive visual descriptors).",
                "balanced" => "BALANCED verbosity (3-6 words; short descriptive phrase; one or two adjectives plus the noun; readable but compact).",
                "detailed" => "DETAILED verbosity (6-12 words; richer phrasing with material, color, or context cues; still a single phrase, no full sentences).",
                "verbose" => "VERBOSE verbosity (12+ words; cinematic, multi-clause description with mood, lighting, materials, and atmosphere).",
                _ => "BALANCED verbosity (3-6 words; readable descriptive phrase)."
            };

            var system = "You rewrite wildcard entries to match a target verbosity level for AI image-generation prompts. " +
                         $"Target: {verbosityRubric} " +
                         "Rules: preserve the core subject and visual meaning; do not invent new concepts; one rewritten entry per input, same order; output ONLY a JSON array of strings.";
            var user = "Rewrite these entries to the target verbosity:\n" +
                       string.Join("\n", entries.Select((e, i) => $"{i + 1}. {e}")) +
                       "\n\nReturn the JSON array.";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = system },
                new() { Role = "user", Content = user }
            };

            var response = await _ollama.SendChatMessage(modelName, messages,
                new OllamaOptions { Temperature = 0.3f, TopP = 0.9f });
            var raw = response?.Message?.Content ?? string.Empty;
            var converted = ParseEntriesTolerant(raw);

            var draftEntries = entries.Select((orig, i) => new ForgeDraftEntryDto
            {
                Value = i < converted.Count ? converted[i] : orig,
                OriginalValue = orig,
                Status = i < converted.Count ? "Modified" : "Kept",
                Accepted = true,
            }).ToList();

            return new ForgeDraftDto
            {
                Operation = ForgeOperation.Convert.ToString(),
                TargetCollectionId = request.TargetCollection?.Id,
                TargetCollectionName = request.TargetCollection?.Name,
                Entries = draftEntries,
                UpdatedAt = DateTime.UtcNow
            };
        }

        // -------------------- Helpers --------------------

        private async Task PersistEntries(int collectionId, List<ForgeDraftEntryDto> accepted, int startSortOrder)
        {
            var sort = startSortOrder;
            foreach (var e in accepted)
            {
                await _db.CreateWildcardEntry(new WildcardEntry
                {
                    CollectionId = collectionId,
                    Value = e.Value.Trim(),
                    Weight = 1.0f,
                    SortOrder = sort++
                });
            }
        }

        /// <summary>
        /// Tolerant parser: tries strict JSON array of objects, then JSON array of strings,
        /// then markdown-fenced JSON, then falls back to line-by-line extraction.
        /// </summary>
        internal static List<string> ParseEntriesTolerant(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();

            var json = ExtractJson(raw);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.ValueKind == JsonValueKind.String)
                            {
                                var s = el.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                            }
                            else if (el.ValueKind == JsonValueKind.Object)
                            {
                                if (el.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String)
                                {
                                    var s = v.GetString();
                                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                                }
                            }
                        }
                        if (list.Count > 0) return list;
                    }
                }
                catch
                {
                    // fall through to line-by-line
                }
            }

            // Line-by-line fallback. Strips bullets, numbering, quotes, JSON object fragments.
            var lines = raw.Split('\n');
            var result = new List<string>();
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("```") || line.StartsWith("//") || line == "[" || line == "]") continue;

                // "value": "..."
                var m = Regex.Match(line, "\"value\"\\s*:\\s*\"(.+?)\"", RegexOptions.IgnoreCase);
                if (m.Success) { result.Add(m.Groups[1].Value); continue; }

                // - "..." | 1. "..." | * ...
                line = Regex.Replace(line, "^[-*]\\s+", string.Empty);
                line = Regex.Replace(line, "^\\d+[.)]\\s+", string.Empty);
                line = line.Trim().TrimStart('"').TrimEnd(',').TrimEnd('"');

                if (line.Length > 0 && !line.StartsWith("{") && !line.StartsWith("}") && !line.StartsWith("["))
                    result.Add(line);
            }
            return result;
        }

        private static string ExtractJson(string raw)
        {
            // Strip ```json fences first.
            var fenceMatch = Regex.Match(raw, "```(?:json)?\\s*(.+?)```", RegexOptions.Singleline);
            if (fenceMatch.Success) raw = fenceMatch.Groups[1].Value;

            var firstArray = raw.IndexOf('[');
            var lastArray = raw.LastIndexOf(']');
            if (firstArray >= 0 && lastArray > firstArray)
                return raw.Substring(firstArray, lastArray - firstArray + 1);

            var firstObj = raw.IndexOf('{');
            var lastObj = raw.LastIndexOf('}');
            if (firstObj >= 0 && lastObj > firstObj)
                return raw.Substring(firstObj, lastObj - firstObj + 1);

            return string.Empty;
        }

        internal static List<string> Dedupe(IEnumerable<string> candidates, IEnumerable<string>? against = null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (against != null)
            {
                foreach (var a in against)
                    if (!string.IsNullOrWhiteSpace(a)) seen.Add(Normalize(a));
            }
            var result = new List<string>();
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                var key = Normalize(c);
                if (seen.Add(key)) result.Add(c.Trim());
            }
            return result;

            static string Normalize(string s) => Regex.Replace(s.Trim().Trim('"'), "\\s+", " ").ToLowerInvariant();
        }
    }
}
