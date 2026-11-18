using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services.WildcardForge.Models;

namespace BlazorWebApp.Services.WildcardForge
{
    /// <summary>
    /// Phase 10 - assembles the system + user messages for a single Forge LLM call.
    /// Honors the 8k token budget by slicing category data to the user's selection and
    /// trimming oversize sections (sample collections, verbosity_examples) gracefully.
    /// Token estimation uses the chars/4 heuristic.
    /// </summary>
    public class PromptComposer
    {
        // Per-call ceiling for the *input* side (input + output must stay under 8k).
        private const int MaxInputCharBudget = 5000 * 4; // ~5k tokens

        private readonly WildcardForgeKnowledge _knowledge;

        public PromptComposer(WildcardForgeKnowledge knowledge)
        {
            _knowledge = knowledge;
        }

        public List<OllamaChatMessage> Compose(ForgeRequest request)
        {
            // Resolve category once (Simple-mode classifier may have set it; otherwise look up).
            request.ResolvedCategory ??= _knowledge.FindCategory(request.CategoryId);

            return request.Operation switch
            {
                ForgeOperation.Generate => ComposeGenerate(request),
                ForgeOperation.Expand => ComposeExpand(request),
                ForgeOperation.Refine => ComposeRefine(request),
                ForgeOperation.Describe => ComposeDescribe(request),
                _ => ComposeGenerate(request)
            };
        }

        // -------------------- Operation builders --------------------

        private List<OllamaChatMessage> ComposeGenerate(ForgeRequest req)
        {
            var template = _knowledge.GetTemplate(ForgeOperation.Generate);
            var category = req.ResolvedCategory;
            var keywords = string.Join(", ", category?.Keywords.Take(8) ?? Enumerable.Empty<string>());
            var collectionName = string.IsNullOrWhiteSpace(req.Theme) ? "untitled-collection" : ToKebab(req.Theme);

            var system = SubstituteSlots(template.SystemPrompt, new Dictionary<string, string>
            {
                ["count"] = req.Count.ToString(),
                ["theme"] = req.Theme,
                ["category"] = category?.Name ?? "general",
                ["verbosity_level"] = req.Verbosity,
                ["keywords"] = keywords,
            });

            var user = SubstituteSlots(template.UserPrompt, new Dictionary<string, string>
            {
                ["count"] = req.Count.ToString(),
                ["collection_name"] = collectionName,
                ["theme"] = req.Theme,
                ["verbosity_level"] = req.Verbosity,
                ["category"] = category?.Name ?? "general",
                ["keywords_list"] = keywords,
            });

            // Append category card + verbosity rubric to system message, trimmed to budget.
            var contextBlock = BuildCategoryCardBlock(category, req.Verbosity);
            var seedsBlock = BuildSeedsBlock(req.SeedExamples);
            var diversityNote = req.DiversityMode
                ? "\n\nIMPORTANT: Maximize variety. Each entry should explore a distinct sub-aspect; avoid stylistic clustering."
                : string.Empty;

            var sb = new StringBuilder(system);
            sb.AppendLine();
            AppendIfBudget(sb, contextBlock);
            AppendIfBudget(sb, seedsBlock);
            sb.Append(diversityNote);

            return new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = TruncateInput(sb.ToString()) },
                new() { Role = "user", Content = user }
            };
        }

        private List<OllamaChatMessage> ComposeExpand(ForgeRequest req)
        {
            var template = _knowledge.GetTemplate(ForgeOperation.Expand);
            var category = req.ResolvedCategory;
            var keywords = string.Join(", ", category?.Keywords.Take(8) ?? Enumerable.Empty<string>());

            // Reference set capped at 25 entries per call.
            var referenceEntries = (req.ExistingEntries ?? new List<WildcardEntry>())
                .Take(25)
                .Select(e => Sanitize(e.Value))
                .ToList();
            var referenceBlock = BuildReferenceEntriesBlock(referenceEntries);

            var system = SubstituteSlots(template.SystemPrompt, new Dictionary<string, string>
            {
                ["count"] = req.Count.ToString(),
                ["theme"] = req.Theme,
                ["category"] = category?.Name ?? "general",
                ["verbosity_level"] = req.Verbosity,
                ["keywords"] = keywords,
                ["existing_entries"] = string.Join("\n", referenceEntries.Select(e => $"- {e}")),
                ["existing_count"] = referenceEntries.Count.ToString(),
                ["collection_name"] = req.TargetCollection?.Name ?? "untitled",
            });

            var user = SubstituteSlots(template.UserPrompt, new Dictionary<string, string>
            {
                ["count"] = req.Count.ToString(),
                ["theme"] = req.Theme,
                ["verbosity_level"] = req.Verbosity,
                ["category"] = category?.Name ?? "general",
                ["existing_entries"] = string.Join("\n", referenceEntries.Select(e => $"- {e}")),
                ["collection_name"] = req.TargetCollection?.Name ?? "untitled",
            });

            var sb = new StringBuilder(system);
            sb.AppendLine();
            AppendIfBudget(sb, BuildCategoryCardBlock(category, req.Verbosity));
            AppendIfBudget(sb, referenceBlock);

            return new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = TruncateInput(sb.ToString()) },
                new() { Role = "user", Content = user }
            };
        }

        private List<OllamaChatMessage> ComposeRefine(ForgeRequest req)
        {
            var template = _knowledge.GetTemplate(ForgeOperation.Refine);
            var category = req.ResolvedCategory;
            var entries = (req.ExistingEntries ?? new List<WildcardEntry>())
                .Take(25)
                .Select(e => Sanitize(e.Value))
                .ToList();

            var system = SubstituteSlots(template.SystemPrompt, new Dictionary<string, string>
            {
                ["theme"] = req.Theme,
                ["category"] = category?.Name ?? "general",
                ["verbosity_level"] = req.Verbosity,
                ["entries"] = string.Join("\n", entries.Select(e => $"- {e}")),
                ["entry_count"] = entries.Count.ToString(),
                ["collection_name"] = req.TargetCollection?.Name ?? "untitled",
            });

            var user = SubstituteSlots(template.UserPrompt, new Dictionary<string, string>
            {
                ["theme"] = req.Theme,
                ["verbosity_level"] = req.Verbosity,
                ["category"] = category?.Name ?? "general",
                ["entries"] = string.Join("\n", entries.Select(e => $"- {e}")),
                ["collection_name"] = req.TargetCollection?.Name ?? "untitled",
            });

            var sb = new StringBuilder(system);
            sb.AppendLine();
            AppendIfBudget(sb, BuildCategoryCardBlock(category, req.Verbosity));
            AppendIfBudget(sb, BuildReferenceEntriesBlock(entries));

            return new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = TruncateInput(sb.ToString()) },
                new() { Role = "user", Content = user }
            };
        }

        private List<OllamaChatMessage> ComposeDescribe(ForgeRequest req)
        {
            // Custom (no template). Tiny prompt: infer name + category + description.
            var sample = (req.SeedExamples ?? new List<string>())
                .Concat(req.ExistingEntries?.Select(e => e.Value) ?? Enumerable.Empty<string>())
                .Take(8)
                .Select(Sanitize)
                .ToList();

            var categoryNames = string.Join(", ", _knowledge.Categories.Select(c => c.Name));

            var system = "You analyze a list of wildcard entries and produce a short JSON object describing the collection. " +
                         "Respond with ONLY valid JSON, no prose. Schema: {\"name\": string (kebab-case), \"category\": string (one of provided), \"description\": string (one sentence)}. " +
                         $"Categories: {categoryNames}.";

            var user = "Entries:\n" + string.Join("\n", sample.Select(s => $"- {s}")) +
                       "\n\nProduce the JSON object.";

            return new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = system },
                new() { Role = "user", Content = user }
            };
        }

        // -------------------- Helpers --------------------

        /// <summary>
        /// Slices a <see cref="CategoryCard"/> into a compact context block. Drops
        /// <c>verbosity_examples</c> for non-selected levels and trims sample collections
        /// to keep the section under ~600 tokens.
        /// </summary>
        private string BuildCategoryCardBlock(CategoryCard? card, string verbosity)
        {
            if (card == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"\n=== Category: {card.Name} ===");
            if (!string.IsNullOrWhiteSpace(card.Description))
                sb.AppendLine(card.Description);

            // Verbosity slice: chosen level + one neighbor for contrast.
            if (card.VerbosityExamples.TryGetValue(verbosity, out var chosen) && chosen.Count > 0)
            {
                sb.AppendLine($"\nVerbosity examples ({verbosity}):");
                foreach (var ex in chosen.Take(3))
                    sb.AppendLine($"  - {ex}");
            }

            if (card.BestPractices.Count > 0)
            {
                sb.AppendLine("\nBest practices:");
                foreach (var p in card.BestPractices.Take(5)) sb.AppendLine($"  - {p}");
            }
            if (card.Avoid.Count > 0)
            {
                sb.AppendLine("\nAvoid:");
                foreach (var a in card.Avoid.Take(5)) sb.AppendLine($"  - {a}");
            }
            return sb.ToString();
        }

        private static string BuildSeedsBlock(List<string> seeds)
        {
            if (seeds == null || seeds.Count == 0) return string.Empty;
            var sb = new StringBuilder("\nUser-provided seed examples (style reference, do not duplicate):\n");
            foreach (var s in seeds.Take(5)) sb.AppendLine($"  - {Sanitize(s)}");
            return sb.ToString();
        }

        private static string BuildReferenceEntriesBlock(List<string> entries)
        {
            if (entries == null || entries.Count == 0) return string.Empty;
            var sb = new StringBuilder("\nExisting entries (do not repeat verbatim):\n");
            foreach (var e in entries) sb.AppendLine($"  <<entry>>{e}<</entry>>");
            return sb.ToString();
        }

        /// <summary>
        /// Wraps user-controlled text in fenced delimiters and strips control chars and
        /// instruction-like sequences to mitigate prompt injection from existing collection content.
        /// </summary>
        private static string Sanitize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var s = value.Replace("<<entry>>", string.Empty)
                         .Replace("<</entry>>", string.Empty)
                         .Replace("\r", " ")
                         .Replace("\n", " ")
                         .Trim();
            if (s.Length > 300) s = s.Substring(0, 300);
            return s;
        }

        private static string SubstituteSlots(string template, Dictionary<string, string> slots)
        {
            if (string.IsNullOrEmpty(template)) return template;
            var s = template;
            foreach (var kv in slots) s = s.Replace("{" + kv.Key + "}", kv.Value);
            return s;
        }

        private static void AppendIfBudget(StringBuilder sb, string block)
        {
            if (string.IsNullOrEmpty(block)) return;
            if (sb.Length + block.Length > MaxInputCharBudget) return;
            sb.Append(block);
        }

        private static string TruncateInput(string content)
            => content.Length <= MaxInputCharBudget ? content : content.Substring(0, MaxInputCharBudget);

        private static string ToKebab(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "untitled";
            var trimmed = s.Trim().ToLowerInvariant();
            var sb = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (ch is ' ' or '-' or '_' or '/')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
                }
            }
            return sb.Length == 0 ? "untitled" : sb.ToString().Trim('-');
        }
    }
}
