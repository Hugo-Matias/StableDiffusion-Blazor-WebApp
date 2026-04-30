using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Persisted, button-driven prompt builder that lives above the Workshop composer. State is
    /// scoped per <c>PromptWorkshopSession</c>; an unbound (null-session) row is also supported.
    /// All public mutations persist <c>Body</c> via the JSON converter and publish events.
    /// </summary>
    public interface IWorkshopWizardService
    {
        Task<WorkshopWizardSession> LoadOrCreateAsync(int? sessionId);
        Task<WorkshopWizardSession> EnsurePendingAsync(int? sessionId, string modelName);
        Task<WorkshopWizardSession> AdvanceIntroAsync(int? sessionId, WizardOption choice, string modelName);
        Task<WorkshopWizardSession> SkipIntroAsync(int? sessionId, string modelName);
        Task<WorkshopWizardSession> AdvanceIterationAsync(int? sessionId, WizardOption choice, string modelName);
        Task<WorkshopWizardSession> ApplyVerbAsync(int? sessionId, string verb, string modelName);
        Task<WorkshopWizardSession> RequestMoreAsync(int? sessionId, string modelName);
        Task<WorkshopWizardSession> UndoAsync(int? sessionId);
        Task<WorkshopWizardSession> CommitAsync(int? sessionId);
        Task<WorkshopWizardSession> ResetAsync(int? sessionId);
    }

    public class WorkshopWizardService : IWorkshopWizardService
    {
        public const int SoftCap = 30;
        public const int HardCap = 50;
        public const int ContextWindowTurns = 2;

        public static readonly string[] Verbs = { "improve", "change", "add", "remove", "surprise" };

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly OllamaService _ollama;
        private readonly WizardIntroCatalog _intro;
        private readonly IEventService _events;
        private readonly ILogger<WorkshopWizardService> _logger;

        public WorkshopWizardService(
            IDbContextFactory<AppDbContext> factory,
            OllamaService ollama,
            WizardIntroCatalog intro,
            IEventService events,
            ILogger<WorkshopWizardService> logger)
        {
            _factory = factory;
            _ollama = ollama;
            _intro = intro;
            _events = events;
            _logger = logger;
        }

        // ---------------- Load / Reset / Commit ----------------

        public async Task<WorkshopWizardSession> LoadOrCreateAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId);
            if (row != null) return row;

            row = new WorkshopWizardSession
            {
                SessionId = sessionId,
                Body = new WizardBody { Stage = WizardStage.Intro, IntroSectionIndex = 0 },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ctx.WorkshopWizardSessions.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        public async Task<WorkshopWizardSession> ResetAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");

            row.Body = new WizardBody { Stage = WizardStage.Intro, IntroSectionIndex = 0 };
            await PersistAsync(ctx, row);

            _events.Publish(new WizardResetEventArgs { SessionId = sessionId });
            return row;
        }

        public async Task<WorkshopWizardSession> CommitAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");

            row.Body.Stage = WizardStage.Committed;
            await PersistAsync(ctx, row);

            _events.Publish(new WizardCommittedEventArgs
            {
                SessionId = sessionId,
                Draft = row.Body.CurrentDraft ?? string.Empty,
            });
            return row;
        }

        // ---------------- Intro stage ----------------

        public async Task<WorkshopWizardSession> EnsurePendingAsync(int? sessionId, string modelName)
        {
            await _intro.LoadAsync().ConfigureAwait(false);

            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");

            // Already populated, nothing to do.
            if (row.Body.PendingOptions != null && row.Body.PendingOptions.Count > 0) return row;
            if (row.Body.Stage == WizardStage.Committed) return row;

            if (row.Body.Stage == WizardStage.Intro)
            {
                var sections = _intro.Sections;
                if (sections.Count == 0) throw new InvalidOperationException("Wizard intro catalog is empty");
                var idx = Math.Clamp(row.Body.IntroSectionIndex, 0, sections.Count - 1);
                var section = sections[idx];
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: $"intro:{section.Id}", introSection: section)
                    .ConfigureAwait(false);
            }
            else
            {
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: "iterate").ConfigureAwait(false);
            }

            await PersistAsync(ctx, row);
            return row;
        }

        public async Task<WorkshopWizardSession> SkipIntroAsync(int? sessionId, string modelName)
        {
            await _intro.LoadAsync().ConfigureAwait(false);
            var sections = _intro.Sections;
            if (sections.Count == 0) throw new InvalidOperationException("Wizard intro catalog is empty");

            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");
            EnsureStage(row, WizardStage.Intro);
            EnforceHardCap(row);

            var idx = Math.Clamp(row.Body.IntroSectionIndex, 0, sections.Count - 1);
            var section = sections[idx];
            var draft = row.Body.CurrentDraft ?? string.Empty;
            var pendingBefore = SnapshotPending(row.Body);

            row.Body.History.Add(new WizardTurn
            {
                Source = $"intro:{section.Id}:skip",
                Question = row.Body.PendingQuestion,
                Choice = "(skipped)",
                DraftBefore = draft,
                DraftAfter = draft,
                PendingQuestionBefore = pendingBefore.q,
                PendingOptionsBefore = pendingBefore.opts,
                LastShownLabelsBefore = pendingBefore.shown,
            });
            row.Body.TurnCount++;
            row.Body.IntroSectionIndex = idx + 1;

            if (row.Body.IntroSectionIndex >= sections.Count)
            {
                row.Body.Stage = WizardStage.Iteration;
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: "iterate").ConfigureAwait(false);
            }
            else
            {
                var next = sections[row.Body.IntroSectionIndex];
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: $"intro:{next.Id}", introSection: next)
                    .ConfigureAwait(false);
            }

            await PersistAsync(ctx, row);
            PublishTurn(row);
            return row;
        }

        public async Task<WorkshopWizardSession> AdvanceIntroAsync(int? sessionId, WizardOption choice, string modelName)
        {
            await _intro.LoadAsync().ConfigureAwait(false);
            var sections = _intro.Sections;
            if (sections.Count == 0) throw new InvalidOperationException("Wizard intro catalog is empty");

            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");
            EnsureStage(row, WizardStage.Intro);
            EnforceHardCap(row);

            var idx = Math.Clamp(row.Body.IntroSectionIndex, 0, sections.Count - 1);
            var section = sections[idx];

            var draftBefore = row.Body.CurrentDraft ?? string.Empty;
            var draftAfter = StitchIntroDraft(draftBefore, section, choice);

            var pendingBefore = SnapshotPending(row.Body);

            row.Body.History.Add(new WizardTurn
            {
                Source = $"intro:{section.Id}",
                Question = row.Body.PendingQuestion,
                Choice = choice.Label,
                DraftBefore = draftBefore,
                DraftAfter = draftAfter,
                PendingQuestionBefore = pendingBefore.q,
                PendingOptionsBefore = pendingBefore.opts,
                LastShownLabelsBefore = pendingBefore.shown,
            });
            row.Body.CurrentDraft = draftAfter;
            row.Body.TurnCount++;
            row.Body.IntroSectionIndex = idx + 1;

            // Transition to Iteration when we are past the last hardcoded section.
            if (row.Body.IntroSectionIndex >= sections.Count)
            {
                row.Body.Stage = WizardStage.Iteration;
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: "iterate")
                    .ConfigureAwait(false);
            }
            else
            {
                // Still in intro; ask LLM for options for the *next* section using its guidance.
                var next = sections[row.Body.IntroSectionIndex];
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: $"intro:{next.Id}", introSection: next)
                    .ConfigureAwait(false);
            }

            await PersistAsync(ctx, row);
            PublishTurn(row);
            return row;
        }

        // ---------------- Iteration stage ----------------

        public async Task<WorkshopWizardSession> AdvanceIterationAsync(int? sessionId, WizardOption choice, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");
            EnsureStage(row, WizardStage.Iteration);
            EnforceHardCap(row);

            await ApplyLLMTurnAsync(row.Body, modelName, source: "iterate", actionLabel: choice.Label).ConfigureAwait(false);
            await PersistAsync(ctx, row);
            PublishTurn(row);
            return row;
        }

        public async Task<WorkshopWizardSession> ApplyVerbAsync(int? sessionId, string verb, string modelName)
        {
            if (string.IsNullOrWhiteSpace(verb)) throw new ArgumentException("Verb is required", nameof(verb));
            var normalized = verb.Trim().ToLowerInvariant();
            if (Array.IndexOf(Verbs, normalized) < 0)
                throw new ArgumentException($"Unsupported wizard verb '{verb}'", nameof(verb));

            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");
            EnsureStage(row, WizardStage.Iteration);
            EnforceHardCap(row);

            row.Body.LastActionVerb = normalized;
            await ApplyLLMTurnAsync(row.Body, modelName, source: $"verb:{normalized}", actionLabel: normalized).ConfigureAwait(false);
            await PersistAsync(ctx, row);
            PublishTurn(row);
            return row;
        }

        public async Task<WorkshopWizardSession> RequestMoreAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");
            // "More..." is option re-roll only; do NOT consume a turn or record history.
            // Allowed in both Intro and Iteration so users can keep asking for fresher options
            // before they pick anything (Phase 13 user feedback).
            if (row.Body.Stage == WizardStage.Committed)
            {
                throw new InvalidOperationException("Cannot request more options after commit");
            }

            if (row.Body.Stage == WizardStage.Intro)
            {
                await _intro.LoadAsync().ConfigureAwait(false);
                var sections = _intro.Sections;
                if (sections.Count == 0) throw new InvalidOperationException("Wizard intro catalog is empty");
                var idx = Math.Clamp(row.Body.IntroSectionIndex, 0, sections.Count - 1);
                var section = sections[idx];
                await PopulatePendingFromLLMAsync(
                        row.Body, modelName,
                        source: $"intro:{section.Id}:more",
                        introSection: section,
                        excludeLabels: row.Body.LastShownLabels)
                    .ConfigureAwait(false);
            }
            else
            {
                await PopulatePendingFromLLMAsync(row.Body, modelName, source: "more", excludeLabels: row.Body.LastShownLabels)
                    .ConfigureAwait(false);
            }

            await PersistAsync(ctx, row);
            return row;
        }

        // ---------------- Undo ----------------

        public async Task<WorkshopWizardSession> UndoAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Wizard row not found");
            if (row.Body.History.Count == 0) return row;

            var last = row.Body.History[^1];
            row.Body.History.RemoveAt(row.Body.History.Count - 1);
            row.Body.CurrentDraft = last.DraftBefore;
            row.Body.PendingQuestion = last.PendingQuestionBefore;
            row.Body.PendingOptions = last.PendingOptionsBefore?.ToList() ?? new List<WizardOption>();
            row.Body.LastShownLabels = last.LastShownLabelsBefore?.ToList() ?? new List<string>();
            row.Body.TurnCount = Math.Max(0, row.Body.TurnCount - 1);

            // Walk back the intro index when the popped turn was an intro step and we have not
            // already crossed into iteration past it.
            if (last.Source.StartsWith("intro:", StringComparison.Ordinal))
            {
                row.Body.Stage = WizardStage.Intro;
                row.Body.IntroSectionIndex = Math.Max(0, row.Body.IntroSectionIndex - 1);
            }

            await PersistAsync(ctx, row);
            PublishTurn(row);
            return row;
        }

        // ---------------- Helpers ----------------

        internal static List<WizardTurn> BuildContextWindow(WizardBody body)
        {
            if (body.History.Count <= ContextWindowTurns) return body.History.ToList();
            return body.History
                .Skip(body.History.Count - ContextWindowTurns)
                .ToList();
        }

        private static void EnsureStage(WorkshopWizardSession row, WizardStage required)
        {
            if (row.Body.Stage != required)
                throw new InvalidOperationException($"Wizard stage is {row.Body.Stage}; expected {required}.");
        }

        private static void EnforceHardCap(WorkshopWizardSession row)
        {
            if (row.Body.TurnCount >= HardCap)
                throw new InvalidOperationException($"Wizard hard cap of {HardCap} turns reached. Commit, undo, or reset.");
        }

        private static (string? q, List<WizardOption>? opts, List<string>? shown) SnapshotPending(WizardBody body)
        {
            return (
                body.PendingQuestion,
                body.PendingOptions?.Select(o => new WizardOption { Label = o.Label, Hint = o.Hint, Payload = o.Payload }).ToList(),
                body.LastShownLabels?.ToList()
            );
        }

        private static string StitchIntroDraft(string current, WizardIntroSection section, WizardOption choice)
        {
            var label = (choice.Label ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(label)) return current;
            if (string.IsNullOrWhiteSpace(current)) return label;
            // Comma-separated stitching keeps the running draft compatible with diffusion prompts
            // while remaining trivially editable. Iteration stage will rewrite this freely.
            return $"{current}, {label}";
        }

        private static Task<WorkshopWizardSession?> FindRowAsync(AppDbContext ctx, int? sessionId)
        {
            return sessionId.HasValue
                ? ctx.WorkshopWizardSessions.FirstOrDefaultAsync(r => r.SessionId == sessionId.Value)
                : ctx.WorkshopWizardSessions.FirstOrDefaultAsync(r => r.SessionId == null);
        }

        private async Task PersistAsync(AppDbContext ctx, WorkshopWizardSession row)
        {
            row.UpdatedAt = DateTime.UtcNow;
            // Force the JSON converter to re-serialize the in-place mutated body.
            ctx.Entry(row).Property(e => e.Body).IsModified = true;
            await ctx.SaveChangesAsync();
        }

        private void PublishTurn(WorkshopWizardSession row)
        {
            var soft = row.Body.TurnCount == SoftCap; // first time we hit it
            var hard = row.Body.TurnCount >= HardCap;
            _events.Publish(new WizardTurnAdvancedEventArgs
            {
                SessionId = row.SessionId,
                Stage = row.Body.Stage,
                TurnCount = row.Body.TurnCount,
                SoftCapReached = soft,
                HardCapReached = hard,
            });
        }

        // ---------------- LLM I/O ----------------

        /// <summary>
        /// Records a history turn for the iteration verb/choice action, calls the LLM with the
        /// iteration system prompt, applies the returned draft, and refreshes pending options.
        /// </summary>
        private async Task ApplyLLMTurnAsync(WizardBody body, string modelName, string source, string actionLabel)
        {
            var draftBefore = body.CurrentDraft ?? string.Empty;
            var pendingBefore = SnapshotPending(body);

            var (draft, question, options) = await CallIterationAsync(body, modelName, actionLabel, excludeLabels: null)
                .ConfigureAwait(false);

            body.History.Add(new WizardTurn
            {
                Source = source,
                Question = body.PendingQuestion,
                Choice = actionLabel,
                DraftBefore = draftBefore,
                DraftAfter = draft,
                PendingQuestionBefore = pendingBefore.q,
                PendingOptionsBefore = pendingBefore.opts,
                LastShownLabelsBefore = pendingBefore.shown,
            });
            body.CurrentDraft = draft;
            body.TurnCount++;
            ApplyPending(body, question, options, mergeShown: false);
        }

        /// <summary>
        /// Re-rolls or initializes <c>PendingOptions</c> without recording a history turn or
        /// touching the draft. Used by intro-section transitions and "More...".
        /// </summary>
        private async Task PopulatePendingFromLLMAsync(
            WizardBody body,
            string modelName,
            string source,
            WizardIntroSection? introSection = null,
            IEnumerable<string>? excludeLabels = null)
        {
            string question;
            List<WizardOption> options;

            if (introSection != null)
            {
                (question, options) = await CallIntroSectionAsync(body, modelName, introSection, excludeLabels)
                    .ConfigureAwait(false);
            }
            else
            {
                var (_, q, opts) = await CallIterationAsync(body, modelName, actionLabel: null, excludeLabels)
                    .ConfigureAwait(false);
                question = q;
                options = opts;
            }

            ApplyPending(body, question, options, mergeShown: source == "more");
        }

        private static void ApplyPending(WizardBody body, string question, List<WizardOption> options, bool mergeShown)
        {
            body.PendingQuestion = question;
            body.PendingOptions = options;
            var labels = options.Select(o => o.Label).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (mergeShown)
            {
                foreach (var l in labels)
                    if (!body.LastShownLabels.Contains(l, StringComparer.OrdinalIgnoreCase))
                        body.LastShownLabels.Add(l);
            }
            else
            {
                body.LastShownLabels = labels;
            }
        }

        // ---------------- Prompt builders (inline; Step 6 will move them to seeded templates) ----------------

        private async Task<(string question, List<WizardOption> options)> CallIntroSectionAsync(
            WizardBody body,
            string modelName,
            WizardIntroSection section,
            IEnumerable<string>? excludeLabels)
        {
            var system = BuildIntroSystemPrompt(section);
            var user = BuildIntroUserPrompt(body, section, excludeLabels);
            var parsed = await SendJsonWithRetryAsync(modelName, system, user, $"intro:{section.Id}").ConfigureAwait(false);
            if (parsed == null)
            {
                _logger.LogWarning("Wizard intro JSON parse failed (section={Section}); returning empty options", section.Id);
                return ($"Pick a {section.Title.ToLowerInvariant()}", new List<WizardOption>());
            }
            return (parsed.Question, parsed.Options ?? new List<WizardOption>());
        }

        private async Task<(string draft, string question, List<WizardOption> options)> CallIterationAsync(
            WizardBody body,
            string modelName,
            string? actionLabel,
            IEnumerable<string>? excludeLabels)
        {
            var system = BuildIterationSystemPrompt(body.LastActionVerb);
            var user = BuildIterationUserPrompt(body, actionLabel, excludeLabels);
            var parsed = await SendJsonWithRetryAsync(modelName, system, user, "iterate").ConfigureAwait(false);
            if (parsed == null)
            {
                _logger.LogWarning("Wizard iteration JSON parse failed; keeping previous draft");
                return (body.CurrentDraft ?? string.Empty, body.PendingQuestion ?? "What should we focus on next?", body.PendingOptions ?? new List<WizardOption>());
            }
            var draft = string.IsNullOrWhiteSpace(parsed.Draft) ? (body.CurrentDraft ?? string.Empty) : parsed.Draft!;
            return (draft, parsed.Question, parsed.Options ?? new List<WizardOption>());
        }

        /// <summary>
        /// Sends a JSON-mode chat request and parses the response. On parse failure, retries once
        /// with a stricter system suffix demanding raw JSON only. Returns <c>null</c> if both
        /// attempts fail to parse.
        /// </summary>
        private async Task<WizardLLMResponse?> SendJsonWithRetryAsync(string modelName, string system, string user, string source)
        {
            var raw = await SendJsonAsync(modelName, system, user).ConfigureAwait(false);
            if (WizardResponseValidator.TryParse(raw, out var parsed)) return parsed;

            _logger.LogWarning("Wizard JSON parse failed on first attempt (source={Source}); retrying with stricter prompt", source);
            const string strictSuffix = "\nIMPORTANT: Respond with raw JSON only. Do not include code fences, comments, or any prose before or after the JSON object.";
            var raw2 = await SendJsonAsync(modelName, system + strictSuffix, user).ConfigureAwait(false);
            return WizardResponseValidator.TryParse(raw2, out var parsed2) ? parsed2 : null;
        }

        private async Task<string> SendJsonAsync(string modelName, string systemPrompt, string userPrompt)
        {
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt },
            };
            var resp = await _ollama.SendChatMessage(modelName, messages, options: null, keepAlive: "15m", stream: false, format: "json")
                .ConfigureAwait(false);
            var content = resp?.Message?.Content ?? string.Empty;
#if DEBUG
            Console.WriteLine($"[Wizard] raw LLM JSON: {content}");
#endif
            return content;
        }

        private static string BuildIntroSystemPrompt(WizardIntroSection section)
        {
            return string.Join('\n', new[]
            {
                "You are a creative prompt-engineering assistant for a stable-diffusion image tool.",
                $"The user is filling in the '{section.Title}' field of a prompt.",
                $"Section guidance: {section.Guidance}",
                "Return ONLY valid JSON with this exact shape (no prose, no code fences):",
                "{ \"question\": \"...\", \"options\": [ { \"label\": \"...\", \"hint\": \"...\" } ] }",
                "Rules:",
                "- 4 to 6 options.",
                "- Each label is a concrete, evocative phrase (max 60 chars, no trailing punctuation).",
                "- Each hint is optional, max 80 chars; explain the visual flavor briefly.",
                "- Vary the options stylistically; do not list synonyms of the same idea.",
            });
        }

        private static string BuildIntroUserPrompt(WizardBody body, WizardIntroSection section, IEnumerable<string>? excludeLabels)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Section: {section.Title}");
            if (section.Examples.Count > 0)
            {
                sb.AppendLine("Stylistic examples (do not copy verbatim):");
                foreach (var ex in section.Examples) sb.AppendLine($"- {ex}");
            }
            if (!string.IsNullOrWhiteSpace(body.CurrentDraft))
                sb.AppendLine($"Current draft so far: {body.CurrentDraft}");
            var excluded = excludeLabels?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (excluded is { Count: > 0 })
            {
                sb.AppendLine("Do NOT propose these labels (already shown):");
                foreach (var e in excluded) sb.AppendLine($"- {e}");
            }
            sb.AppendLine($"Generate options for the {section.Title.ToLowerInvariant()} step now.");
            return sb.ToString();
        }

        private static string BuildIterationSystemPrompt(string? lastVerb)
        {
            var verbHint = string.IsNullOrWhiteSpace(lastVerb)
                ? "Refine the prompt by suggesting the next strongest improvement."
                : lastVerb switch
                {
                    "improve" => "Make the prompt more vivid, specific, and visually compelling. Keep the subject intact.",
                    "change" => "Substitute one major element of the prompt with a different but coherent alternative.",
                    "add" => "Introduce one new sensory or compositional detail to the prompt.",
                    "remove" => "Remove the weakest or most generic element of the prompt.",
                    "surprise" => "Take a creative leap: introduce an unexpected but coherent twist.",
                    _ => "Refine the prompt by suggesting the next strongest improvement.",
                };

            return string.Join('\n', new[]
            {
                "You are a creative prompt-engineering assistant for a stable-diffusion image tool.",
                "Each turn you must produce: (1) a revised draft prompt, (2) a single guiding question, (3) 4-6 button options for the next turn.",
                $"Action focus this turn: {verbHint}",
                "Return ONLY valid JSON with this exact shape (no prose, no code fences):",
                "{ \"draft\": \"...\", \"question\": \"...\", \"options\": [ { \"label\": \"...\", \"hint\": \"...\" } ] }",
                "Rules:",
                "- draft: the FULL updated prompt (single line, comma-separated tags or short prose).",
                "- 4 to 6 options. Each label max 60 chars, no trailing punctuation. Hint optional, max 80 chars.",
                "- Options must be visually distinct from each other and from any excluded labels listed in the user message.",
            });
        }

        private static string BuildIterationUserPrompt(WizardBody body, string? actionLabel, IEnumerable<string>? excludeLabels)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Current draft: {body.CurrentDraft ?? string.Empty}");

            var window = BuildContextWindow(body);
            if (window.Count > 0)
            {
                sb.AppendLine("Recent turns:");
                foreach (var t in window)
                {
                    var q = string.IsNullOrWhiteSpace(t.Question) ? "(no question)" : t.Question;
                    sb.AppendLine($"- {t.Source}: chose '{t.Choice}' (asked: {q})");
                }
            }

            if (!string.IsNullOrWhiteSpace(actionLabel))
                sb.AppendLine($"User action this turn: {actionLabel}");

            var excluded = excludeLabels?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (excluded is { Count: > 0 })
            {
                sb.AppendLine("Do NOT propose these labels (already shown):");
                foreach (var e in excluded) sb.AppendLine($"- {e}");
            }

            sb.AppendLine("Produce the next JSON response now.");
            return sb.ToString();
        }
    }
}
