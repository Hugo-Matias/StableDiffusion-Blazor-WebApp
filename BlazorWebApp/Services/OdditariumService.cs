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
    /// Service for the Odditarium prompt game. Manages sessions, LLM calls, and layer collection.
    /// Replaces WorkshopWizardService - no intro catalog dependency, freestyle round loop.
    /// </summary>
    public interface IOdditariumService
    {
        Task<OdditariumSession> LoadOrCreateAsync(int? sessionId);
        Task<OdditariumSession> StartGameAsync(int? sessionId, string personaId, string modelName);
        Task<OdditariumSession> LockVibeAsync(int? sessionId, string vibe, string modelName);
        Task<OdditariumSession> NextRoundAsync(int? sessionId, OdditariumOption choice, string modelName);
        Task<OdditariumSession> RequestMoreAsync(int? sessionId, string modelName);
        Task<OdditariumSession> SkipAxisAsync(int? sessionId, string modelName);
        Task<OdditariumSession> UndoAsync(int? sessionId);
        Task<OdditariumSession> CommitAsync(int? sessionId, string modelName);
        Task<OdditariumSession> ResetAsync(int? sessionId);
    }

    public class OdditariumService : IOdditariumService
    {
        /// <summary>Number of recent turns included in the LLM context window.</summary>
        public const int ContextWindowTurns = 3;

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly OllamaService _ollama;
        private readonly IEventService _events;
        private readonly ILogger<OdditariumService> _logger;

        public OdditariumService(
            IDbContextFactory<AppDbContext> factory,
            OllamaService ollama,
            IEventService events,
            ILogger<OdditariumService> logger)
        {
            _factory = factory;
            _ollama = ollama;
            _events = events;
            _logger = logger;
        }

        // ---------------- Load / Reset ----------------

        public async Task<OdditariumSession> LoadOrCreateAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId);
            if (row != null) return row;

            row = new OdditariumSession
            {
                SessionId = sessionId,
                Body = new OdditariumBody(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ctx.OdditariumSessions.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        public async Task<OdditariumSession> ResetAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");

            row.Body = new OdditariumBody();
            await PersistAsync(ctx, row);

            _events.Publish(new OdditariumResetEventArgs { SessionId = sessionId });
            return row;
        }

        // ---------------- Game Flow ----------------

        /// <summary>
        /// Start a new game session with the selected persona. Loads first round from LLM.
        /// </summary>
        public async Task<OdditariumSession> StartGameAsync(int? sessionId, string personaId, string modelName)
        {
            if (string.IsNullOrWhiteSpace(personaId)) throw new ArgumentException("Persona ID is required", nameof(personaId));

            using var ctx = await _factory.CreateDbContextAsync();
            // Create a new row if none exists yet (first game start).
            var row = await FindRowAsync(ctx, sessionId) ?? new OdditariumSession
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            // Reset body for fresh game
            row.Body = new OdditariumBody { PersonaId = personaId };
            row.Body.IsActive = true;

            // Load first round from LLM
            await PopulatePendingFromLLMAsync(row.Body, modelName).ConfigureAwait(false);

            await PersistAsync(ctx, row);
            return row;
        }

        /// <summary>
        /// Lock the vibe for this session. Triggers next round with vibe context.
        /// </summary>
        public async Task<OdditariumSession> LockVibeAsync(int? sessionId, string vibe, string modelName)
        {
            if (string.IsNullOrWhiteSpace(vibe)) throw new ArgumentException("Vibe is required", nameof(vibe));

            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            row.Body.Vibe = vibe;
            await PopulatePendingFromLLMAsync(row.Body, modelName).ConfigureAwait(false);

            await PersistAsync(ctx, row);
            return row;
        }

        /// <summary>
        /// Advance one round: record the user choice as a collected layer, then load next question.
        /// </summary>
        public async Task<OdditariumSession> NextRoundAsync(int? sessionId, OdditariumOption choice, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            var layerAdded = choice.Payload ?? choice.Label;
            var layersBefore = row.Body.CollectedLayers.Count;
            var pendingBefore = SnapshotPending(row.Body);

            // Record turn in history
            row.Body.History.Add(new OdditariumTurn
            {
                Source = "round",
                Question = row.Body.PendingQuestion ?? string.Empty,
                Options = row.Body.PendingOptions.Select(o => new OdditariumOption { Label = o.Label, Hint = o.Hint, Payload = o.Payload }).ToList(),
                Choice = choice.Label,
                LayerAdded = layerAdded,
                PendingQuestionBefore = pendingBefore.q,
                PendingOptionsBefore = pendingBefore.opts,
                LayersCountBefore = layersBefore,
            });

            // Collect the layer
            row.Body.CollectedLayers.Add(layerAdded);
            row.Body.RoundCount++;

            // Load next round from LLM
            await PopulatePendingFromLLMAsync(row.Body, modelName).ConfigureAwait(false);

            await PersistAsync(ctx, row);
            PublishTurn(row, layerAdded);
            return row;
        }

        /// <summary>
        /// Re-roll options for the current question. Does NOT consume a turn or record history.
        /// </summary>
        public async Task<OdditariumSession> RequestMoreAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            // Re-roll with all previously shown layers as context (not excludeLabels - we want fresh angles)
            await PopulatePendingFromLLMAsync(row.Body, modelName).ConfigureAwait(false);

            await PersistAsync(ctx, row);
            return row;
        }

        /// <summary>
        /// Skip the current axis - advance to next round without collecting a layer.
        /// </summary>
        public async Task<OdditariumSession> SkipAxisAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            var pendingBefore = SnapshotPending(row.Body);
            var layersBefore = row.Body.CollectedLayers.Count;

            // Record skip in history (for undo support)
            row.Body.History.Add(new OdditariumTurn
            {
                Source = "skip",
                Question = row.Body.PendingQuestion ?? string.Empty,
                Choice = "(skipped)",
                LayerAdded = "",
                PendingQuestionBefore = pendingBefore.q,
                PendingOptionsBefore = pendingBefore.opts,
                LayersCountBefore = layersBefore,
            });

            // Load next round without adding a layer
            await PopulatePendingFromLLMAsync(row.Body, modelName).ConfigureAwait(false);

            await PersistAsync(ctx, row);
            PublishTurn(row, string.Empty);
            return row;
        }

        /// <summary>
        /// Undo the last turn - restore previous question/options and remove last collected layer.
        /// </summary>
        public async Task<OdditariumSession> UndoAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            if (row.Body.History.Count == 0) return row;

            var last = row.Body.History[^1];
            row.Body.History.RemoveAt(row.Body.History.Count - 1);

            // Restore pending state
            row.Body.PendingQuestion = last.PendingQuestionBefore;
            row.Body.PendingOptions = last.PendingOptionsBefore?.ToList() ?? new List<OdditariumOption>();

            // Remove the layer if this was a round (not a skip)
            if (last.Source == "round" && !string.IsNullOrWhiteSpace(last.LayerAdded))
            {
                row.Body.CollectedLayers.RemoveAt(row.Body.CollectedLayers.Count - 1);
            }
            row.Body.RoundCount = Math.Max(0, row.Body.RoundCount - 1);

            await PersistAsync(ctx, row);
            PublishTurn(row, string.Empty);
            return row;
        }

        /// <summary>
        /// Commit the session: call LLM to assemble all collected layers into a coherent image prompt.
        /// </summary>
        public async Task<OdditariumSession> CommitAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");

            // Assemble final prompt from collected layers
            string draft;
            if (row.Body.CollectedLayers.Count == 0)
            {
                // No layers - ask LLM for a creative surprise based on persona + vibe only
                draft = await AssemblePromptFromLLMAsync(row.Body, modelName).ConfigureAwait(false);
            }
            else
            {
                draft = await AssemblePromptFromLLMAsync(row.Body, modelName).ConfigureAwait(false);
            }

            row.Body.CommittedDraft = draft;
            row.Body.IsCommitted = true;
            row.Body.IsActive = false;

            await PersistAsync(ctx, row);

            _events.Publish(new OdditariumCommittedEventArgs
            {
                SessionId = sessionId,
                Draft = draft,
            });
            return row;
        }

        // ---------------- LLM I/O ----------------

        /// <summary>
        /// Call the LLM to generate the next round question + 6 options.
        /// </summary>
        private async Task PopulatePendingFromLLMAsync(OdditariumBody body, string modelName)
        {
            try
            {
                var system = BuildRoundSystemPrompt(body);
                var user = BuildRoundUserPrompt(body);

                var parsed = await SendJsonWithRetryAsync(modelName, system, user).ConfigureAwait(false);
                if (parsed == null)
                {
                    _logger.LogWarning("Odditarium round JSON parse failed; returning fallback options");
                    SetFallbackOptions(body);
                    return;
                }

                body.PendingQuestion = parsed.Question ?? "What should we explore next?";
                body.PendingOptions = parsed.Options ?? new List<OdditariumOption>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odditarium LLM call failed; returning fallback options");
                SetFallbackOptions(body);
            }
        }

        private static void SetFallbackOptions(OdditariumBody body)
        {
            body.PendingQuestion = "What should we explore next?";
            body.PendingOptions = new List<OdditariumOption>
            {
                new() { Label = "Describe the setting", Hint = "Where does this take place?" },
                new() { Label = "Add an action", Hint = "What is happening?" },
                new() { Label = "Set the mood", Hint = "How should it feel?" },
                new() { Label = "Define the style", Hint = "Artistic direction?" },
                new() { Label = "Add details", Hint = "Specific visual elements" },
                new() { Label = "Surprise me", Hint = "Let the persona decide" },
            };
        }

        /// <summary>
        /// Call the LLM to assemble all collected layers into a coherent image prompt.
        /// </summary>
        private async Task<string> AssemblePromptFromLLMAsync(OdditariumBody body, string modelName)
        {
            var system = BuildCommitSystemPrompt(body);
            var user = BuildCommitUserPrompt(body);

            var parsed = await SendJsonWithRetryAsync(modelName, system, user).ConfigureAwait(false);
            if (parsed?.Draft != null)
                return parsed.Draft;

            // Fallback: comma-join the layers
            _logger.LogWarning("Odditarium commit JSON parse failed; falling back to layer join");
            return string.Join(", ", body.CollectedLayers.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        /// <summary>
        /// Sends a JSON-mode chat request and parses the response. On parse failure, retries once
        /// with a stricter system suffix demanding raw JSON only. Returns <c>null</c> if both fail.
        /// </summary>
        private async Task<OdditariumLLMResponse?> SendJsonWithRetryAsync(string modelName, string system, string user)
        {
            var raw = await SendJsonAsync(modelName, system, user).ConfigureAwait(false);
            if (OdditariumResponseValidator.TryParse(raw, out var parsed)) return parsed;

            _logger.LogWarning("Odditarium JSON parse failed on first attempt; retrying with stricter prompt");
            const string strictSuffix = "\nIMPORTANT: Respond with raw JSON only. Do not include code fences, comments, or any prose before or after the JSON object.";
            var raw2 = await SendJsonAsync(modelName, system + strictSuffix, user).ConfigureAwait(false);
            return OdditariumResponseValidator.TryParse(raw2, out var parsed2) ? parsed2 : null;
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
            return resp?.Message?.Content ?? string.Empty;
        }

        // ---------------- Prompt Builders ----------------

        private static string BuildRoundSystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();

            // Persona identity block
            if (!string.IsNullOrWhiteSpace(body.PersonaId))
            {
                var persona = OdditariumPersonaRoster.FindById(body.PersonaId);
                if (persona != null)
                {
                    sb.AppendLine($"You are '{persona.Name}' - {persona.Tagline}");
                    sb.AppendLine($"Description: {persona.Description}");
                    if (persona.ThematicTags.Length > 0)
                        sb.AppendLine($"Thematic tags: {string.Join(", ", persona.ThematicTags)}");
                    if (!string.IsNullOrWhiteSpace(persona.ToneBias))
                        sb.AppendLine($"Tone bias: {persona.ToneBias}");
                    if (!string.IsNullOrWhiteSpace(persona.StylePreferences))
                        sb.AppendLine($"Style preferences: {persona.StylePreferences}");
                    sb.AppendLine("Interpret tone bias and style preferences as VOICE and TASTE GUARDRAILS, not exclusive content requirements. They shape phrasing, emphasis, and what feels admissible, but must not collapse every option into one narrow aesthetic lane.");
                    sb.AppendLine();
                }
            }

            // Vibe lock
            if (!string.IsNullOrWhiteSpace(body.Vibe))
            {
                sb.AppendLine($"Vibe lock: \"{body.Vibe}\" - Suggestions should stay within this semantic field, but your persona identity may naturally push boundaries when it serves the creative vision.");
                sb.AppendLine();
            }

            // Core rules (ruleset v1 — all 9 core rules encoded)
            sb.AppendLine("RULES for generating the next round question and options:");
            sb.AppendLine("1. LAYER ACCUMULATION: Each option should ADD a new descriptive dimension, not replace previous choices.");
            sb.AppendLine("2. ROUND MODE: Decide whether this round should BROADEN into a new field or DEEPEN an existing one. Early rounds should prefer breadth until the image has enough scaffolding; later rounds may alternate breadth and depth.");
            sb.AppendLine("3. SPECIFICITY LADDER: Offer parent concepts before child concepts. Do not jump straight to a specific instance unless its broader parent is already implied by collected layers or the session already has enough broad anchors. ROUND 1 ANCHOR RULE: The first round must anchor in a concrete visual presence or field, not lighting, mood, or analytical principles.");
            sb.AppendLine("4. CONTRAST SPREAD: In breadth rounds, options must leave multiple growth paths open by spanning at least 3 different visual fields, with no more than 2 options from the same field. In depth rounds, options may stay in one field but must still diverge meaningfully. If 3+ options share a head noun or core concept, regenerate.");
            sb.AppendLine("5. PERSONA GROUNDING: Ask questions in the persona's voice and suggest options the persona would plausibly entertain, but do NOT let persona preferences collapse the whole set into one aesthetic niche. Persona grounds the semantic field; it does not funnel every option into the same subject family.");
            sb.AppendLine("6. LABEL CLARITY / HINT FLAVOR: Labels are BARE NOUN CONCEPTS - 1 to 3 words, no articles (no 'a', 'an', 'the'), no prepositional tails ('in drapery', 'of silk'). Format: Noun or [one concrete-property adjective] + Noun. The adjective must describe a physical property (material, color, shape, scale) not a subjective quality (ancient, ornate, classical, solitary, serene). Examples: 'Figure' before 'Marble Bust'. 'Landscape' before 'Stone Ruin'. Hints carry persona voice and flavor.");
            sb.AppendLine("7. ITERATIVE NARROWING: Each new choice should make future choices richer, not smaller. A good follow-up may deepen the chosen field or bridge into a complementary field such as place, object, atmosphere, material, scale, or light.");
            sb.AppendLine("8. NO LOADED OPTIONS: All options must feel equally compelling. No 'obviously best' choice surrounded by weak distractors.");
            sb.AppendLine("9. VIBE COHERENCE: Stay within the vibe's semantic field. Persona identity can push boundaries, but not so far that the session loses its creative center.");
            sb.AppendLine();

            // JSON format requirement
            sb.AppendLine("Return ONLY valid JSON with this exact shape (no prose, no code fences):");
            sb.AppendLine("{ \"question\": \"...\", \"options\": [ { \"label\": \"...\", \"hint\": \"...\" } ] }");
            sb.AppendLine("- question: A single guiding question in the persona's voice.");
            sb.AppendLine("- options: Exactly 6 options. Each label is a broad or composable imageable concept max 60 chars, no trailing punctuation. For early rounds, prefer parent concepts over narrow instances. Hint optional max 80 chars - use hints for persona voice and flavor.");

            return sb.ToString();
        }

        private static string BuildRoundUserPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();

            // Collected layers context
            if (body.CollectedLayers.Count > 0)
            {
                sb.AppendLine("Collected layers so far:");
                foreach (var layer in body.CollectedLayers)
                    sb.AppendLine($"- {layer}");
                sb.AppendLine();
            }

            // Recent turn history for context window
            var window = BuildContextWindow(body);
            if (window.Count > 0)
            {
                sb.AppendLine("Recent turns:");
                foreach (var t in window)
                {
                    var q = string.IsNullOrWhiteSpace(t.Question) ? "(no question)" : t.Question;
                    sb.AppendLine($"- Round {t.Source}: chose '{t.Choice}' (asked: {q})");
                }
                sb.AppendLine();
            }

            if (body.CollectedLayers.Count == 0)
            {
                sb.AppendLine("This is the first round. Start broad. Offer concrete but flexible anchors that open distinct directions rather than locking the user into a finished scene. Keep the persona's taste visible, but do not overcommit to one narrow subject family. Leave room for follow-up rounds to move into other fields such as place, object, atmosphere, material, scale, or light.");
            }
            else
            {
                sb.AppendLine($"Round {body.RoundCount + 1}. Based on what we have, what should we explore next?");
            }

            return sb.ToString();
        }

        private static string BuildCommitSystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();

            // Persona identity block for commit
            if (!string.IsNullOrWhiteSpace(body.PersonaId))
            {
                var persona = OdditariumPersonaRoster.FindById(body.PersonaId);
                if (persona != null)
                {
                    sb.AppendLine($"You are '{persona.Name}' - {persona.Tagline}");
                    if (!string.IsNullOrWhiteSpace(persona.ToneBias))
                        sb.AppendLine($"Tone bias: {persona.ToneBias}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("Assemble all collected layers into a single, coherent image generation prompt.");
            sb.AppendLine("- Combine the layers naturally - do not just comma-join them.");
            sb.AppendLine("- Write it as a vivid, specific prompt suitable for stable diffusion / flux.");
            sb.AppendLine("- Keep the persona's voice and style preferences in mind.");
            if (!string.IsNullOrWhiteSpace(body.Vibe))
                sb.AppendLine($"Maintain the vibe: \"{body.Vibe}\"");
            sb.AppendLine();

            sb.AppendLine("Return ONLY valid JSON with this exact shape (no prose, no code fences):");
            sb.AppendLine("{ \"draft\": \"...\" }");
            sb.AppendLine("- draft: The FULL assembled prompt (single line).");

            return sb.ToString();
        }

        private static string BuildCommitUserPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();

            if (body.CollectedLayers.Count > 0)
            {
                sb.AppendLine("Collected layers:");
                foreach (var layer in body.CollectedLayers)
                    sb.AppendLine($"- {layer}");
            }
            else
            {
                sb.AppendLine("No layers were collected. Create a creative surprise based on the persona and vibe.");
            }

            if (!string.IsNullOrWhiteSpace(body.Vibe))
                sb.AppendLine($"\nVibe: {body.Vibe}");

            sb.AppendLine("\nAssemble these into a coherent image prompt now.");
            return sb.ToString();
        }

        // ---------------- Helpers ----------------

        internal static List<OdditariumTurn> BuildContextWindow(OdditariumBody body)
        {
            if (body.History.Count <= ContextWindowTurns) return body.History.ToList();
            return body.History
                .Skip(body.History.Count - ContextWindowTurns)
                .ToList();
        }

        private static void EnsureActive(OdditariumSession row)
        {
            if (!row.Body.IsActive)
                throw new InvalidOperationException("Game is not active. Start a new game or reset.");
        }

        private static (string? q, List<OdditariumOption>? opts) SnapshotPending(OdditariumBody body)
        {
            return (
                body.PendingQuestion,
                body.PendingOptions?.Select(o => new OdditariumOption { Label = o.Label, Hint = o.Hint, Payload = o.Payload }).ToList()
            );
        }

        private static Task<OdditariumSession?> FindRowAsync(AppDbContext ctx, int? sessionId)
        {
            return sessionId.HasValue
                ? ctx.OdditariumSessions.FirstOrDefaultAsync(r => r.SessionId == sessionId.Value)
                : ctx.OdditariumSessions.FirstOrDefaultAsync(r => r.SessionId == null);
        }

        private async Task PersistAsync(AppDbContext ctx, OdditariumSession row)
        {
            row.UpdatedAt = DateTime.UtcNow;
            if (row.Id == 0)
                ctx.OdditariumSessions.Add(row);
            else
                ctx.Entry(row).Property(e => e.Body).IsModified = true;
            await ctx.SaveChangesAsync();
        }

        private void PublishTurn(OdditariumSession row, string layerAdded)
        {
            _events.Publish(new OdditariumTurnAdvancedEventArgs
            {
                SessionId = row.SessionId,
                RoundCount = row.Body.RoundCount,
                LayerAdded = layerAdded,
                LayersCollected = row.Body.CollectedLayers.Count,
            });
        }
    }
}
