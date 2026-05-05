using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        /// <summary>Returns up to <paramref name="maxCount"/> active (non-reset, non-committed) sessions, newest first.</summary>
        Task<IReadOnlyList<OdditariumSession>> ListActiveSessionsAsync(int maxCount = 5);
    }

    public class OdditariumService : IOdditariumService
    {
        /// <summary>Number of recent turns included in the LLM context window.</summary>
        public const int ContextWindowTurns = 3;

        // --- Phase 7.5: Resonance map (Mechanism A) ---
        // Key = anchor that was just picked, Value = anchor to bump to front of remaining queue.
        private static readonly Dictionary<string, string> _resonanceMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "subject",  "action"     },
            { "action",   "framing"    },
            { "lighting", "atmosphere" },
            { "mood",     "style"      },
            { "setting",  "scale"      },
        };

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly OllamaService _ollama;
        private readonly IStateService _state;
        private readonly IEventService _events;
        private readonly ILogger<OdditariumService> _logger;

        public OdditariumService(
            IDbContextFactory<AppDbContext> factory,
            OllamaService ollama,
            IStateService state,
            IEventService events,
            ILogger<OdditariumService> logger)
        {
            _factory = factory;
            _ollama = ollama;
            _state = state;
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

        public async Task<IReadOnlyList<OdditariumSession>> ListActiveSessionsAsync(int maxCount = 5)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            // Body is a JSON ValueConverter column — filter in memory after loading.
            var rows = await ctx.OdditariumSessions
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            return rows
                .Where(r => r.Body.IsActive && !r.Body.IsCommitted)
                .Take(maxCount)
                .ToList();
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
        /// Advance one round: record the user choice as a typed layer, update facet tracking, then load next question.
        /// </summary>
        public async Task<OdditariumSession> NextRoundAsync(int? sessionId, OdditariumOption choice, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            var body = row.Body;
            var snapshot = SnapshotPending(body);

            // Build the typed layer from choice + pending anchor/facet/mode
            var layer = new OdditariumLayer
            {
                Anchor = body.PendingAnchor ?? "detail",
                Facet = body.PendingFacet,
                Mode = body.PendingMode ?? "expand",
                Label = choice.Label,
                Hint = choice.Hint,
                Question = body.PendingQuestion,
            };

            body.History.Add(new OdditariumTurn
            {
                Source = "round",
                Question = body.PendingQuestion ?? string.Empty,
                Options = body.PendingOptions.Select(o => new OdditariumOption { Label = o.Label, Hint = o.Hint, Payload = o.Payload, Anchor = o.Anchor, Facet = o.Facet }).ToList(),
                Choice = choice.Label,
                Layer = layer,
                PendingQuestionBefore = snapshot.Question,
                PendingOptionsBefore = snapshot.Options,
                PendingAnchorBefore = snapshot.Anchor,
                PendingFacetBefore = snapshot.Facet,
                PendingModeBefore = snapshot.Mode,
                ShownLabelsBefore = snapshot.ShownLabels,
                LayersCountBefore = body.CollectedLayers.Count,
            });

            body.CollectedLayers.Add(layer);

            // Track visited facet for this anchor
            if (!string.IsNullOrWhiteSpace(layer.Anchor))
            {
                if (!body.VisitedFacets.TryGetValue(layer.Anchor, out var facetList))
                {
                    facetList = new List<string>();
                    body.VisitedFacets[layer.Anchor] = facetList;
                }
                if (!string.IsNullOrWhiteSpace(layer.Facet) && !facetList.Contains(layer.Facet, StringComparer.OrdinalIgnoreCase))
                    facetList.Add(layer.Facet);
            }

            // Mechanism A: mutate queue based on what anchor was just picked.
            MutateQueueOnPick(body, layer.Anchor);

            body.RoundCount++;
            body.ShownLabelsForCurrentQuestion.Clear();

            await PopulatePendingFromLLMAsync(body, modelName).ConfigureAwait(false);
            await PersistAsync(ctx, row);
            PublishTurn(row, layer);
            return row;
        }

        /// <summary>
        /// Re-roll the 6 options for the current question without advancing the round.
        /// Pins question/anchor/facet/mode; feeds a growing exclusion list to the LLM.
        /// </summary>
        public async Task<OdditariumSession> RequestMoreAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            var body = row.Body;

            // Accumulate current options into the exclusion list before refreshing
            if (body.PendingOptions.Count > 0)
            {
                var existing = new HashSet<string>(body.ShownLabelsForCurrentQuestion, StringComparer.OrdinalIgnoreCase);
                foreach (var opt in body.PendingOptions)
                    if (!string.IsNullOrWhiteSpace(opt.Label) && existing.Add(opt.Label))
                        body.ShownLabelsForCurrentQuestion.Add(opt.Label);

                // Cap at 24 labels (4 More rolls × 6 options)
                if (body.ShownLabelsForCurrentQuestion.Count > 24)
                    body.ShownLabelsForCurrentQuestion = body.ShownLabelsForCurrentQuestion
                        .Skip(body.ShownLabelsForCurrentQuestion.Count - 24)
                        .ToList();
            }

            await PopulateMoreFromLLMAsync(body, modelName).ConfigureAwait(false);
            await PersistAsync(ctx, row);
            return row;
        }

        /// <summary>
        /// Skip the current question and load a new one without collecting a layer.
        /// </summary>
        public async Task<OdditariumSession> SkipAxisAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            EnsureActive(row);

            var body = row.Body;
            var snapshot = SnapshotPending(body);

            body.History.Add(new OdditariumTurn
            {
                Source = "skip",
                Question = body.PendingQuestion ?? string.Empty,
                Choice = "(skipped)",
                Layer = null,
                PendingQuestionBefore = snapshot.Question,
                PendingOptionsBefore = snapshot.Options,
                PendingAnchorBefore = snapshot.Anchor,
                PendingFacetBefore = snapshot.Facet,
                PendingModeBefore = snapshot.Mode,
                ShownLabelsBefore = snapshot.ShownLabels,
                LayersCountBefore = body.CollectedLayers.Count,
            });

            body.ShownLabelsForCurrentQuestion.Clear();

            await PopulatePendingFromLLMAsync(body, modelName).ConfigureAwait(false);
            await PersistAsync(ctx, row);
            PublishTurn(row, null);
            return row;
        }

        /// <summary>
        /// Undo the last turn: restore previous question/options/anchor/facet/mode and remove last layer.
        /// </summary>
        public async Task<OdditariumSession> UndoAsync(int? sessionId)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");
            if (row.Body.History.Count == 0) return row;

            var body = row.Body;
            var last = body.History[^1];
            body.History.RemoveAt(body.History.Count - 1);

            // Restore pending state
            body.PendingQuestion = last.PendingQuestionBefore;
            body.PendingOptions = last.PendingOptionsBefore?.ToList() ?? new List<OdditariumOption>();
            body.PendingAnchor = last.PendingAnchorBefore;
            body.PendingFacet = last.PendingFacetBefore;
            body.PendingMode = last.PendingModeBefore;
            body.ShownLabelsForCurrentQuestion = last.ShownLabelsBefore?.ToList() ?? new List<string>();

            if (last.Source == "round" && last.Layer != null)
            {
                body.CollectedLayers.RemoveAt(body.CollectedLayers.Count - 1);

                // Undo the facet registration
                var anchor = last.Layer.Anchor;
                var facet = last.Layer.Facet;
                if (!string.IsNullOrWhiteSpace(anchor) && !string.IsNullOrWhiteSpace(facet)
                    && body.VisitedFacets.TryGetValue(anchor, out var facets))
                {
                    facets.Remove(facet);
                    if (facets.Count == 0) body.VisitedFacets.Remove(anchor);
                }
            }

            body.RoundCount = Math.Max(0, body.RoundCount - 1);

            await PersistAsync(ctx, row);
            PublishTurn(row, null);
            return row;
        }

        /// <summary>
        /// Two-pass commit: pass 1 expands per-anchor layers into descriptive phrases,
        /// pass 2 assembles those phrases into a single image-generation prompt.
        /// </summary>
        public async Task<OdditariumSession> CommitAsync(int? sessionId, string modelName)
        {
            using var ctx = await _factory.CreateDbContextAsync();
            var row = await FindRowAsync(ctx, sessionId) ?? throw new InvalidOperationException("Odditarium session not found");

            string draft;
            if (row.Body.CollectedLayers.Count == 0)
            {
                draft = await SinglePassCommitAsync(row.Body, modelName).ConfigureAwait(false)
                        ?? FallbackCommitFromLayers(row.Body);
            }
            else
            {
                var expansions = await ExpandLayersAsync(row.Body, modelName).ConfigureAwait(false);
                if (expansions != null && expansions.Count > 0)
                {
                    draft = await AssemblePromptAsync(row.Body, expansions, modelName).ConfigureAwait(false)
                            ?? FallbackCommitFromExpansions(expansions);
                }
                else
                {
                    _logger.LogWarning("Odditarium commit pass 1 (expansion) failed; falling back to single-pass commit");
                    draft = await SinglePassCommitAsync(row.Body, modelName).ConfigureAwait(false)
                            ?? FallbackCommitFromLayers(row.Body);
                }
            }

            row.Body.CommittedDraft = draft;
            row.Body.IsCommitted = true;
            row.Body.IsActive = false;

            await PersistAsync(ctx, row);
            _events.Publish(new OdditariumCommittedEventArgs { SessionId = sessionId, Draft = draft });
            return row;
        }

        // ---------------- LLM I/O ----------------

        /// <summary>
        /// Call the LLM to generate the next round question + 6 options.
        /// AdvanceAnchorTarget decides anchor+mode first; the LLM only invents facet+question+options.
        /// Sets PendingQuestion, PendingOptions, PendingAnchor, PendingFacet, PendingMode, and seeds ShownLabels.
        /// </summary>
        private async Task PopulatePendingFromLLMAsync(OdditariumBody body, string modelName)
        {
            try
            {
                var persona = string.IsNullOrWhiteSpace(body.PersonaId)
                    ? null
                    : OdditariumPersonaRoster.FindById(body.PersonaId);

                // Service decides anchor + mode; LLM only invents facet + question + options.
                AdvanceAnchorTarget(body, persona);

                var system = BuildRoundSystemPrompt(body);
                var user = BuildRoundUserPrompt(body);

                var parsed = await SendJsonWithRetryAsync(modelName, system, user).ConfigureAwait(false);
                if (parsed == null)
                {
                    _logger.LogWarning("Odditarium round JSON parse failed; returning fallback options");
                    SetFallbackPending(body);
                    return;
                }

                body.PendingQuestion = parsed.Question ?? "What should we explore next?";
                body.PendingOptions = parsed.Options ?? new List<OdditariumOption>();
                // anchor and mode were set by AdvanceAnchorTarget — do NOT read from LLM response.
                body.PendingFacet = parsed.Facet;

                // Seed the shown-labels list for this new question.
                body.ShownLabelsForCurrentQuestion.Clear();
                body.ShownLabelsForCurrentQuestion.AddRange(
                    body.PendingOptions
                        .Select(o => o.Label)
                        .Where(l => !string.IsNullOrWhiteSpace(l)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odditarium LLM call failed; returning fallback options");
                SetFallbackPending(body);
            }
        }

        /// <summary>
        /// Call the LLM for the More... flow. Pins question/anchor/facet/mode; only updates PendingOptions.
        /// </summary>
        private async Task PopulateMoreFromLLMAsync(OdditariumBody body, string modelName)
        {
            try
            {
                var system = BuildMoreSystemPrompt(body);
                var user = $"Generate 6 new options for: \"{body.PendingQuestion}\"";

                var parsed = await SendJsonWithRetryAsync(modelName, system, user).ConfigureAwait(false);
                if (parsed == null)
                {
                    _logger.LogWarning("Odditarium More JSON parse failed; options unchanged");
                    return;
                }

                // Only update options — question/anchor/facet/mode are pinned
                body.PendingOptions = parsed.Options ?? body.PendingOptions;

                // Accumulate new options into shown-labels
                var seen = new HashSet<string>(body.ShownLabelsForCurrentQuestion, StringComparer.OrdinalIgnoreCase);
                foreach (var opt in body.PendingOptions)
                    if (!string.IsNullOrWhiteSpace(opt.Label) && seen.Add(opt.Label))
                        body.ShownLabelsForCurrentQuestion.Add(opt.Label);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odditarium More LLM call failed; options unchanged");
            }
        }

        /// <summary>
        /// Commit pass 1: ask the LLM to expand each anchor's layers into a descriptive phrase.
        /// </summary>
        private async Task<Dictionary<string, string>?> ExpandLayersAsync(OdditariumBody body, string modelName)
        {
            var system = BuildCommitExpansionSystemPrompt(body);
            var user = BuildCommitExpansionUserPrompt(body);

            var raw = await SendJsonAsync(modelName, system, user).ConfigureAwait(false);
            if (OdditariumResponseValidator.TryParseExpansions(raw, out var expansions)) return expansions;

            _logger.LogWarning("Odditarium commit expansion parse failed on first attempt; retrying");
            const string strictSuffix = "\nIMPORTANT: Respond with raw JSON only. No code fences. No prose.";
            var raw2 = await SendJsonAsync(modelName, system + strictSuffix, user).ConfigureAwait(false);
            return OdditariumResponseValidator.TryParseExpansions(raw2, out var exp2) ? exp2 : null;
        }

        /// <summary>
        /// Commit pass 2: weave the per-anchor expansions into a final image-generation prompt.
        /// </summary>
        private async Task<string?> AssemblePromptAsync(OdditariumBody body, Dictionary<string, string> expansions, string modelName)
        {
            var system = BuildCommitAssemblySystemPrompt(body);
            var user = BuildCommitAssemblyUserPrompt(body, expansions);

            var raw = await SendJsonAsync(modelName, system, user).ConfigureAwait(false);
            if (OdditariumResponseValidator.TryParseDraft(raw, out var draft)) return draft;

            _logger.LogWarning("Odditarium commit assembly parse failed on first attempt; retrying");
            const string strictSuffix = "\nIMPORTANT: Respond with raw JSON only. No code fences. No prose.";
            var raw2 = await SendJsonAsync(modelName, system + strictSuffix, user).ConfigureAwait(false);
            return OdditariumResponseValidator.TryParseDraft(raw2, out var d2) ? d2 : null;
        }

        /// <summary>
        /// Single-pass commit fallback (used when pass 1 fails or no layers).
        /// </summary>
        private async Task<string?> SinglePassCommitAsync(OdditariumBody body, string modelName)
        {
            var system = BuildCommitSinglePassSystemPrompt(body);
            var user = BuildCommitSinglePassUserPrompt(body);

            var raw = await SendJsonAsync(modelName, system, user).ConfigureAwait(false);
            if (OdditariumResponseValidator.TryParseDraft(raw, out var draft)) return draft;

            _logger.LogWarning("Odditarium single-pass commit parse failed; retrying");
            const string strictSuffix = "\nIMPORTANT: Respond with raw JSON only. No code fences.";
            var raw2 = await SendJsonAsync(modelName, system + strictSuffix, user).ConfigureAwait(false);
            return OdditariumResponseValidator.TryParseDraft(raw2, out var d2) ? d2 : null;
        }

        private static string FallbackCommitFromExpansions(Dictionary<string, string> expansions)
        {
            var order = OdditariumAnchors.All;
            return string.Join(", ", order
                .Where(a => expansions.ContainsKey(a))
                .Select(a => expansions[a])
                .Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        private static string FallbackCommitFromLayers(OdditariumBody body) =>
            string.Join(", ", body.CollectedLayers.Select(l => l.Label).Where(l => !string.IsNullOrWhiteSpace(l)));

        private static void SetFallbackPending(OdditariumBody body)
        {
            body.PendingQuestion = "What should we explore next?";
            body.PendingAnchor = "subject";
            body.PendingFacet = "core-archetype";
            body.PendingMode = "expand";
            body.PendingOptions = new List<OdditariumOption>
            {
                new() { Label = "Figure",     Hint = "A humanoid presence.",        Anchor = "subject", Facet = "core-archetype" },
                new() { Label = "Creature",   Hint = "Something non-human.",        Anchor = "subject", Facet = "core-archetype" },
                new() { Label = "Structure",  Hint = "A built or natural form.",    Anchor = "subject", Facet = "core-archetype" },
                new() { Label = "Vehicle",    Hint = "Something that moves.",       Anchor = "subject", Facet = "core-archetype" },
                new() { Label = "Relic",      Hint = "An object with history.",     Anchor = "subject", Facet = "core-archetype" },
                new() { Label = "Phenomenon", Hint = "A natural event or force.",   Anchor = "subject", Facet = "core-archetype" },
            };
            body.ShownLabelsForCurrentQuestion.Clear();
            body.ShownLabelsForCurrentQuestion.AddRange(body.PendingOptions.Select(o => o.Label));
        }

        // Priming conversation injected before the real user turn for round/more calls.
        // Shows the model the exact JSON schema expected so it doesn't invent its own format.
        // v3 schema: { facet, question, options } — mode and anchor are service-owned.
        private static readonly IReadOnlyList<OllamaChatMessage> RoundSchemaPriming = new List<OllamaChatMessage>
        {
            new() { Role = "user",      Content = "Confirm you understand the required JSON format by showing one valid example response." },
            new() { Role = "assistant", Content = "{\"facet\":\"core-archetype\",\"question\":\"What form does the central presence take?\",\"options\":[{\"label\":\"Wandering Knight\",\"hint\":\"Duty-worn, carries obligations unspoken.\"},{\"label\":\"Street Vendor\",\"hint\":\"Commerce at the margins; every corner known.\"},{\"label\":\"Feral Child\",\"hint\":\"Raised outside order, instincts honed to glass.\"},{\"label\":\"Broken Oracle\",\"hint\":\"Speaks in fragments; once saw too clearly.\"},{\"label\":\"Sleeping Giant\",\"hint\":\"Dormant scale \u2014 overwhelming when stirred.\"},{\"label\":\"Hollow Automaton\",\"hint\":\"Built to serve; the soul question lingers.\"}]}" },
        };

        /// <summary>
        /// Sends a JSON-mode chat request and parses the response. On parse failure, retries once
        /// with a stricter system suffix demanding raw JSON only. Returns <c>null</c> if both fail.
        /// </summary>
        private async Task<OdditariumLLMResponse?> SendJsonWithRetryAsync(string modelName, string system, string user)
        {
            var raw = await SendJsonAsync(modelName, system, user, RoundSchemaPriming).ConfigureAwait(false);
            if (OdditariumResponseValidator.TryParse(raw, out var parsed)) return parsed;

            _logger.LogWarning("Odditarium JSON parse failed on first attempt; retrying with stricter prompt");
            const string strictSuffix = "\nIMPORTANT: Respond with raw JSON only. Do not include code fences, comments, or any prose before or after the JSON object.";
            var raw2 = await SendJsonAsync(modelName, system + strictSuffix, user, RoundSchemaPriming).ConfigureAwait(false);
            return OdditariumResponseValidator.TryParse(raw2, out var parsed2) ? parsed2 : null;
        }

        private async Task<string> SendJsonAsync(
            string modelName,
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<OllamaChatMessage>? priming = null)
        {
            var thinkOpts = _state.State.Generation.LLM.Options;
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
            };
            if (priming != null)
                messages.AddRange(priming);
            messages.Add(new() { Role = "user", Content = userPrompt });
            // think is a top-level Ollama API field. Pass false to skip the reasoning phase (saves tokens).
            // When thinking is enabled, pass true explicitly so the model always reasons.
            // Omitting it (null) leaves the model to decide, which is unreliable.
            bool? think = thinkOpts.EnableThinking ? true : false;
            var resp = await _ollama.SendChatMessage(modelName, messages, options: new OllamaOptions(), keepAlive: "15m", stream: false, format: "json", think: think)
                .ConfigureAwait(false);
            var raw = resp?.Message?.Content ?? string.Empty;

            // Strip custom think tags when thinking is enabled and the user has configured non-default tags.
            if (thinkOpts.EnableThinking
                && !string.IsNullOrWhiteSpace(thinkOpts.ThinkOpenTag)
                && !string.IsNullOrWhiteSpace(thinkOpts.ThinkCloseTag)
                && (thinkOpts.ThinkOpenTag != "<think>" || thinkOpts.ThinkCloseTag != "</think>"))
            {
                var pattern = Regex.Escape(thinkOpts.ThinkOpenTag) + @"[\s\S]*?" + Regex.Escape(thinkOpts.ThinkCloseTag);
                raw = Regex.Replace(raw, pattern, string.Empty, RegexOptions.IgnoreCase);
            }

            return raw;
        }

        // ---------------- Prompt Builders ----------------

        // --- Phase 7.5: Anchor queue helpers ---

        /// <summary>
        /// Builds the anchor visit queue for a session using persona affinity weighting and tier order.
        /// Core → Structural → Enrichment, sorted within each tier by persona preference rank + small jitter.
        /// Already-visited anchors are excluded (supports resume).
        /// </summary>
        internal static List<string> BuildAnchorQueue(OdditariumBody body, OdditariumPersona? persona)
        {
            var affinity = persona?.AnchorAffinity ?? Array.Empty<string>();
            var affinityIndex = affinity
                .Select((name, i) => (name: name.ToLowerInvariant(), idx: i))
                .ToDictionary(x => x.name, x => x.idx);

            var tierOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in OdditariumAnchors.Core) tierOf[a] = 0;
            foreach (var a in OdditariumAnchors.Structural) tierOf[a] = 1;
            foreach (var a in OdditariumAnchors.Enrichment) tierOf[a] = 2;

            var rng = new Random();
            var groups = new[] { new List<(string anchor, double weight)>(), new List<(string anchor, double weight)>(), new List<(string anchor, double weight)>() };

            foreach (var anchor in OdditariumAnchors.All)
            {
                var tier = tierOf[anchor];
                double weight = affinityIndex.TryGetValue(anchor.ToLowerInvariant(), out var idx)
                    ? idx
                    : affinity.Length + tier * 13;
                weight += (rng.NextDouble() - 0.5) * 0.6; // ±0.3 jitter
                groups[tier].Add((anchor, weight));
            }

            var queue = groups
                .SelectMany(g => g.OrderBy(x => x.weight).Select(x => x.anchor))
                .ToList();

            // Exclude already-visited anchors (session resume support)
            var visited = body.VisitedFacets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            queue.RemoveAll(a => visited.Contains(a));
            return queue;
        }

        /// <summary>
        /// Decides the next anchor and mode, writing them into <see cref="OdditariumBody.PendingAnchor"/>
        /// and <see cref="OdditariumBody.PendingMode"/>. Manages the deepen countdown and queue pop logic.
        /// </summary>
        internal static void AdvanceAnchorTarget(OdditariumBody body, OdditariumPersona? persona)
        {
            // Lazy init: build queue on first round or when resuming a session without a queue.
            if (body.AnchorQueue.Count == 0 && body.CurrentAnchorTarget == null)
                body.AnchorQueue = BuildAnchorQueue(body, persona);

            if (body.RemainingDeepensForAnchor > 0)
            {
                // Continue deepening the current anchor.
                body.PendingAnchor = body.CurrentAnchorTarget;
                body.PendingMode = "deepen";
                body.RemainingDeepensForAnchor--;
                return;
            }

            // Pop next anchor from queue.
            string anchor;
            if (body.AnchorQueue.Count > 0)
            {
                anchor = body.AnchorQueue[0];
                body.AnchorQueue.RemoveAt(0);
            }
            else
            {
                // All anchors visited — bonus deepen on thinnest anchor.
                anchor = body.CollectedLayers
                    .GroupBy(l => l.Anchor)
                    .OrderBy(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? OdditariumAnchors.Core[0];
            }

            body.CurrentAnchorTarget = anchor;
            body.PendingAnchor = anchor;
            body.PendingMode = "expand";

            // Set deepen countdown for subsequent rounds on this anchor.
            var rng = new Random();
            int minD = persona?.MinDeepensPerAnchor ?? 1;
            int maxD = persona?.MaxDeepensPerAnchor ?? 2;
            body.RemainingDeepensForAnchor = rng.Next(minD, maxD + 1);
        }

        /// <summary>
        /// Applies Mechanism A (resonance queue dynamics): after a pick from <paramref name="pickedAnchor"/>,
        /// bumps the resonance target anchor to the front of the remaining queue (if applicable).
        /// </summary>
        internal static void MutateQueueOnPick(OdditariumBody body, string pickedAnchor)
        {
            if (!_resonanceMap.TryGetValue(pickedAnchor, out var target)) return;

            // No-op: resonance target is already the current anchor.
            if (string.Equals(target, body.CurrentAnchorTarget, StringComparison.OrdinalIgnoreCase)) return;

            // No-op: resonance target already visited.
            if (body.VisitedFacets.ContainsKey(target)) return;

            // No-op: already at front of queue.
            if (body.AnchorQueue.Count > 0 && string.Equals(body.AnchorQueue[0], target, StringComparison.OrdinalIgnoreCase)) return;

            var idx = body.AnchorQueue.FindIndex(a => string.Equals(a, target, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return; // not in remaining queue

            body.AnchorQueue.RemoveAt(idx);
            body.AnchorQueue.Insert(0, target);
        }

        /// <summary>
        /// Builds the facet seed context text injected into the round system prompt (Mechanisms B and C).
        /// B: last 1-2 picked labels seed expand rounds.
        /// C: a cross-anchor layer seeds deepen rounds.
        /// </summary>
        internal static string BuildFacetSeedBlock(OdditariumBody body, string anchor, string mode)
        {
            if (string.Equals(mode, "deepen", StringComparison.OrdinalIgnoreCase))
            {
                // Mechanism C: inherit from a different anchor's most recent layer.
                var cross = body.CollectedLayers.LastOrDefault(l =>
                    !string.Equals(l.Anchor, anchor, StringComparison.OrdinalIgnoreCase));
                return cross != null
                    ? $"You are deepening [{anchor}]. The most recent layer from another anchor is [{cross.Label}] ({cross.Hint ?? cross.Label}). Invent a {anchor} facet that speaks to how this specific element relates to {anchor}."
                    : $"You are deepening [{anchor}]. No companion anchor layer exists yet — invent a {anchor} facet that fits the persona voice and vibe '{body.Vibe ?? "the session"}'.";
            }

            // Mechanism B: recent picks seed expand rounds.
            var recent = body.CollectedLayers.TakeLast(2).ToList();
            if (recent.Count > 0)
            {
                var seeds = string.Join(" / ", recent.Select(l => $"[{l.Label}] ({l.Hint ?? l.Label})"));
                return $"The session has placed: {seeds}. Invent a {anchor} facet that emerges naturally from this presence.";
            }

            return $"This is the opening round. Invent a {anchor} facet that fits the persona voice and the vibe '{body.Vibe ?? "the session"}'.";
        }

        private static string BuildRoundSystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            var persona = string.IsNullOrWhiteSpace(body.PersonaId) ? null : OdditariumPersonaRoster.FindById(body.PersonaId);

            if (persona != null)
            {
                sb.AppendLine($"You are '{persona.Name}' - {persona.Tagline}.");
                if (!string.IsNullOrWhiteSpace(persona.Description)) sb.AppendLine(persona.Description);
                if (persona.ThematicTags.Length > 0) sb.AppendLine($"Tags: {string.Join(", ", persona.ThematicTags)}");
                if (!string.IsNullOrWhiteSpace(persona.ToneBias)) sb.AppendLine($"Tone bias: {persona.ToneBias}");
                if (!string.IsNullOrWhiteSpace(persona.StylePreferences)) sb.AppendLine($"Style: {persona.StylePreferences}");
                if (!string.IsNullOrWhiteSpace(persona.CreativePhilosophy)) sb.AppendLine($"Creative philosophy: {persona.CreativePhilosophy}");
                if (!string.IsNullOrWhiteSpace(persona.QuestionFraming)) sb.AppendLine($"Question framing: {persona.QuestionFraming}");
                if (!string.IsNullOrWhiteSpace(persona.OptionVoice)) sb.AppendLine($"Option voice: {persona.OptionVoice}");
                if (!string.IsNullOrWhiteSpace(persona.ForbiddenZones)) sb.AppendLine($"FORBIDDEN: {persona.ForbiddenZones}");
                sb.AppendLine();
            }

            sb.AppendLine("VOICE = this persona. JOB = help the player build a Stable Diffusion / Flux image prompt.");
            sb.AppendLine("The player picks one option per round. All picks are assembled at COMMIT into the final prompt.");
            sb.AppendLine();

            // 13 anchors (condensed — LLM no longer selects anchor)
            sb.AppendLine("THE 13 ANCHORS:");
            sb.AppendLine("  Core: subject, setting");
            sb.AppendLine("  Structural: action, lighting, framing, atmosphere");
            sb.AppendLine("  Enrichment: mood, style, detail, time, scale, color, movement");
            sb.AppendLine();

            // Facets
            sb.AppendLine("FACETS: Sub-aspects of an anchor you invent at round time. Lowercase-kebab, 1-4 tokens.");
            sb.AppendLine("Examples (non-exhaustive — invent your own from context):");
            sb.AppendLine("  subject: core-archetype, gender-and-age, clothing-and-armor, face-and-expression");
            sb.AppendLine("  setting: geological-character, weather-and-air, human-traces, scale-and-scope");
            sb.AppendLine("  lighting: direction, color-temperature, hardness-vs-softness, volumetrics");
            sb.AppendLine("  framing: camera-distance, camera-angle, lens-character, depth-of-field");
            sb.AppendLine();

            // Session state
            sb.AppendLine("CURRENT SESSION:");
            sb.AppendLine("Anchor coverage:");
            sb.AppendLine(BuildAnchorCoverageBlock(body));
            sb.AppendLine("Visited facets (do NOT repeat):");
            sb.AppendLine(BuildVisitedFacetsBlock(body));
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(body.Vibe))
            {
                sb.AppendLine($"VIBE LOCK: \"{body.Vibe}\" — Stay in this semantic field. Persona may push boundaries creatively.");
                sb.AppendLine();
            }

            // Directive block — anchor + mode are service-decided, not LLM-decided
            var anchor = body.PendingAnchor ?? "subject";
            var mode = body.PendingMode ?? "expand";
            var seed = BuildFacetSeedBlock(body, anchor, mode);
            sb.AppendLine($"THIS ROUND — anchor: {anchor} | mode: {mode}");
            sb.AppendLine(seed);
            sb.AppendLine();

            sb.AppendLine("Your task:");
            sb.AppendLine("1. Invent ONE facet identifier (lowercase-kebab, 1–4 tokens) for the declared anchor. Do not reuse a visited facet.");
            sb.AppendLine("2. Ask one question in persona voice.");
            sb.AppendLine("3. Offer exactly 6 options.");
            sb.AppendLine();

            sb.AppendLine("RULES (priority order):");
            sb.AppendLine("1. LAYER ACCUMULATION: options ADD to the declared anchor+facet. Never reference or replace other anchors.");
            sb.AppendLine("2. CONTRAST SPREAD: 6 options must span different regions of the concept space for that anchor+facet.");
            sb.AppendLine("   Test: if you removed the anchor name, could each option still be identified as a different category? If 3+ feel like siblings -> regenerate.");
            sb.AppendLine("3. LABELS = bare noun concepts, 1-3 words, no articles, no prepositional tails. Physical adjectives allowed (marble, golden, wet).");
            sb.AppendLine("   BANNED WORDS in labels: ancient, ornate, classical, solitary, serene, ethereal, primal, mystical, majestic, timeless, legendary.");
            sb.AppendLine("4. HINTS carry persona voice. Label = plain concept. Hint = flavor. Hint must not restate the label.");
            sb.AppendLine("5. NO LOADED OPTIONS. All 6 equally compelling regardless of genre preference.");
            sb.AppendLine("6. PERSONA = voice, not funnel. Do not collapse all options into one aesthetic pocket.");
            sb.AppendLine("7. SURPRISE MANDATE: If any option would appear on the first page of a Google image search for this anchor, replace it.");
            sb.AppendLine("8. NEVER RECYCLE: Do not use options, facets, or concepts already present in collected layers or recent rounds.");
            sb.AppendLine();

            sb.AppendLine("Return ONLY valid JSON, no prose, no code fences:");
            sb.AppendLine("{");
            sb.AppendLine("  \"facet\": \"<lowercase-kebab facet you invented>\",");
            sb.AppendLine("  \"question\": \"<persona-voiced question, one sentence>\",");
            sb.AppendLine("  \"options\": [");
            sb.AppendLine("    {\"label\": \"<1-3 words>\", \"hint\": \"<persona flavor, max 80 chars>\"},");
            sb.AppendLine("    ... exactly 6 options");
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string BuildRoundUserPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();

            if (body.CollectedLayers.Count > 0)
            {
                sb.AppendLine("Collected layers (anchor / facet -> label: hint):");
                foreach (var grp in body.CollectedLayers.GroupBy(l => l.Anchor))
                {
                    sb.AppendLine($"  {grp.Key}:");
                    foreach (var layer in grp)
                    {
                        var fp = string.IsNullOrWhiteSpace(layer.Facet) ? "(no facet)" : layer.Facet;
                        var hp = string.IsNullOrWhiteSpace(layer.Hint) ? "" : $" (\"{layer.Hint}\")";
                        sb.AppendLine($"    {fp} -> {layer.Label}{hp}");
                    }
                }
                sb.AppendLine();
            }

            // Per-call entropy seed so the LLM does not produce identical option sets across runs
            var seed = (body.RoundCount * 1009 + (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 9973));
            sb.AppendLine($"[seed:{seed}]");

            var anchor = body.PendingAnchor ?? "subject";
            sb.AppendLine($"Round {body.RoundCount + 1}. Invent the facet for anchor [{anchor}].");

            if (body.CollectedLayers.Count > 0)
            {
                var layerRefs = body.CollectedLayers
                    .Where(l => !string.IsNullOrWhiteSpace(l.Label))
                    .Select(l =>
                    {
                        var ctx = string.IsNullOrWhiteSpace(l.Facet) ? l.Anchor : $"{l.Anchor} \u00b7 {l.Facet}";
                        return $"{l.Label} ({ctx})";
                    });
                sb.AppendLine($"Already committed to this image: {string.Join(", ", layerRefs)}.");
                sb.AppendLine($"Invent [{anchor}] options that feel native to this exact combination. Options that could belong to a different image with none of these committed elements are too generic.");
            }
            else if (!string.IsNullOrWhiteSpace(body.Vibe))
            {
                sb.AppendLine($"This is the opening anchor. Invent options that could only exist in a scene built around: \"{body.Vibe}\".");
                sb.AppendLine($"Every option must feel native to that world — not generic archetypes that could fit any scene.");
            }

            return sb.ToString();
        }

        private static string BuildMoreSystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Keep EXACTLY:");
            sb.AppendLine($"  mode     = \"{body.PendingMode}\"");
            sb.AppendLine($"  anchor   = \"{body.PendingAnchor}\"");
            sb.AppendLine($"  facet    = \"{body.PendingFacet}\"");
            sb.AppendLine($"  question = \"{body.PendingQuestion}\"");
            sb.AppendLine();
            sb.AppendLine("Generate 6 NEW options for that question. These labels are banned (already shown):");
            sb.AppendLine(body.ShownLabelsForCurrentQuestion.Count > 0
                ? string.Join(", ", body.ShownLabelsForCurrentQuestion)
                : "(none)");
            sb.AppendLine();
            sb.AppendLine("CONTRAST SPREAD and LABEL CLARITY rules still apply.");
            sb.AppendLine("SURPRISE MANDATE still applies: none of the new options should overlap conceptually with the already-shown batch.");
            sb.AppendLine("Return JSON in the same shape. mode/anchor/facet/question MUST match the values above exactly.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY valid JSON:");
            sb.AppendLine("{");
            sb.AppendLine($"  \"mode\": \"{body.PendingMode}\",");
            sb.AppendLine($"  \"anchor\": \"{body.PendingAnchor}\",");
            sb.AppendLine($"  \"facet\": \"{body.PendingFacet}\",");
            sb.AppendLine($"  \"question\": \"{body.PendingQuestion}\",");
            sb.AppendLine("  \"options\": [ {\"label\": \"...\", \"hint\": \"...\"}, ... exactly 6 ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildCommitExpansionSystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            var persona = string.IsNullOrWhiteSpace(body.PersonaId) ? null : OdditariumPersonaRoster.FindById(body.PersonaId);

            if (persona != null)
            {
                sb.AppendLine($"You are '{persona.Name}' - {persona.Tagline}.");
                if (!string.IsNullOrWhiteSpace(persona.ToneBias)) sb.AppendLine($"Tone: {persona.ToneBias}");
                if (!string.IsNullOrWhiteSpace(persona.StylePreferences)) sb.AppendLine($"Style: {persona.StylePreferences}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(body.Vibe)) sb.AppendLine($"Vibe: \"{body.Vibe}\"");
            sb.AppendLine();

            sb.AppendLine("The player has finished their session. Their collected layers are below, grouped by anchor.");
            sb.AppendLine();
            sb.AppendLine("YOUR JOB: For each non-empty anchor, write ONE descriptive phrase (10-30 words) that expands the bare labels into vivid image-prompt language. Weave facet detail into the phrase. Use hints as voice cues. Do NOT assemble the final prompt yet.");
            sb.AppendLine();
            sb.AppendLine("RULES:");
            sb.AppendLine("- Skip anchors with no layers.");
            sb.AppendLine("- Merge multiple labels in the same anchor (from different facets) into one coherent phrase.");
            sb.AppendLine("- Stay within vibe and persona voice.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY valid JSON:");
            sb.AppendLine("{ \"expansions\": { \"<anchor>\": \"<10-30 word phrase>\", ... } }");
            return sb.ToString();
        }

        private static string BuildCommitExpansionUserPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Collected layers (anchor / facet -> label [hint]):");
            foreach (var grp in body.CollectedLayers.GroupBy(l => l.Anchor))
            {
                sb.AppendLine($"  {grp.Key}:");
                foreach (var layer in grp)
                {
                    var fp = string.IsNullOrWhiteSpace(layer.Facet) ? "(no facet)" : layer.Facet;
                    var hp = string.IsNullOrWhiteSpace(layer.Hint) ? "" : $" [{layer.Hint}]";
                    sb.AppendLine($"    {fp} -> {layer.Label}{hp}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("Expand each non-empty anchor now.");
            return sb.ToString();
        }

        private static string BuildCommitAssemblySystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            var persona = string.IsNullOrWhiteSpace(body.PersonaId) ? null : OdditariumPersonaRoster.FindById(body.PersonaId);

            if (persona != null)
            {
                sb.AppendLine($"You are '{persona.Name}' - {persona.Tagline}.");
                if (!string.IsNullOrWhiteSpace(persona.ToneBias)) sb.AppendLine($"Tone: {persona.ToneBias}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(body.Vibe)) sb.AppendLine($"Vibe: \"{body.Vibe}\"");
            sb.AppendLine();

            sb.AppendLine("YOUR JOB: Weave the per-anchor phrases below into a single coherent image-generation prompt for Stable Diffusion / Flux.");
            sb.AppendLine();
            sb.AppendLine("Anchor order: subject, action, setting, lighting, framing, atmosphere, mood, style, detail, time, scale, color, movement.");
            sb.AppendLine();
            sb.AppendLine("RULES:");
            sb.AppendLine("- Include something from EVERY non-empty anchor phrase.");
            sb.AppendLine("- Resolve contradictions in the player's favor.");
            sb.AppendLine("- Minimal connecting language. This is a prompt, not prose.");
            sb.AppendLine("- No anchor names in output. Single line. No newlines.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY valid JSON: { \"draft\": \"<final image prompt>\" }");
            return sb.ToString();
        }

        private static string BuildCommitAssemblyUserPrompt(OdditariumBody body, Dictionary<string, string> expansions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Per-anchor phrases:");
            foreach (var anchor in OdditariumAnchors.All.Where(a => expansions.ContainsKey(a)))
                sb.AppendLine($"  {anchor}: {expansions[anchor]}");
            sb.AppendLine();
            sb.AppendLine("Original layers (for fidelity; facet -> label):");
            foreach (var grp in body.CollectedLayers.GroupBy(l => l.Anchor))
            {
                var labels = string.Join(", ", grp.Select(l =>
                    string.IsNullOrWhiteSpace(l.Facet) ? l.Label : $"{l.Facet}:{l.Label}"));
                sb.AppendLine($"  {grp.Key}: [{labels}]");
            }
            sb.AppendLine();
            sb.AppendLine("Assemble the final prompt now.");
            return sb.ToString();
        }

        private static string BuildCommitSinglePassSystemPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            var persona = string.IsNullOrWhiteSpace(body.PersonaId) ? null : OdditariumPersonaRoster.FindById(body.PersonaId);

            if (persona != null)
            {
                sb.AppendLine($"You are '{persona.Name}' - {persona.Tagline}.");
                if (!string.IsNullOrWhiteSpace(persona.ToneBias)) sb.AppendLine($"Tone: {persona.ToneBias}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(body.Vibe)) sb.AppendLine($"Vibe: \"{body.Vibe}\"");
            sb.AppendLine();
            sb.AppendLine("Assemble the collected layers into a single, vivid image-generation prompt for Stable Diffusion / Flux.");
            sb.AppendLine("- Combine naturally. Do NOT just comma-join.");
            sb.AppendLine("- Single line. No newlines.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY valid JSON: { \"draft\": \"<final prompt>\" }");
            return sb.ToString();
        }

        private static string BuildCommitSinglePassUserPrompt(OdditariumBody body)
        {
            var sb = new StringBuilder();
            if (body.CollectedLayers.Count > 0)
            {
                sb.AppendLine("Collected layers (anchor / facet -> label [hint]):");
                foreach (var grp in body.CollectedLayers.GroupBy(l => l.Anchor))
                {
                    sb.AppendLine($"  {grp.Key}:");
                    foreach (var layer in grp)
                    {
                        var fp = string.IsNullOrWhiteSpace(layer.Facet) ? "" : $"{layer.Facet} -> ";
                        var hp = string.IsNullOrWhiteSpace(layer.Hint) ? "" : $" [{layer.Hint}]";
                        sb.AppendLine($"    {fp}{layer.Label}{hp}");
                    }
                }
            }
            else
            {
                sb.AppendLine("No layers collected. Generate a creative surprise based on the persona and vibe.");
            }
            sb.AppendLine();
            sb.AppendLine("Assemble into a coherent image prompt now.");
            return sb.ToString();
        }

        // --- Prompt helpers ---

        private static string BuildAnchorCoverageBlock(OdditariumBody body)
        {
            var byAnchor = body.CollectedLayers.GroupBy(l => l.Anchor)
                               .ToDictionary(g => g.Key, g => g.ToList());
            var sb = new StringBuilder();
            foreach (var anchor in OdditariumAnchors.All)
            {
                if (byAnchor.TryGetValue(anchor, out var layers) && layers.Count > 0)
                {
                    var parts = string.Join(", ", layers.Select(l =>
                        string.IsNullOrWhiteSpace(l.Facet) ? l.Label : $"{l.Facet}:{l.Label}"));
                    sb.AppendLine($"  {anchor}: {parts}");
                }
                else
                {
                    sb.AppendLine($"  {anchor}: (empty)");
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildVisitedFacetsBlock(OdditariumBody body)
        {
            if (body.VisitedFacets.Count == 0) return "  (none yet)";
            var sb = new StringBuilder();
            foreach (var anchor in OdditariumAnchors.All)
                if (body.VisitedFacets.TryGetValue(anchor, out var facets) && facets.Count > 0)
                    sb.AppendLine($"  {anchor}: {string.Join(", ", facets)}");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "  (none yet)";
        }

        private static string BuildStructuralDebtCue(OdditariumBody body)
        {
            var round = body.RoundCount;
            var covered = new HashSet<string>(body.CollectedLayers.Select(l => l.Anchor), StringComparer.OrdinalIgnoreCase);

            if (!covered.Contains("subject"))
            {
                if (round >= 4) return "HIGH - subject anchor is overdue. This round MUST be mode=expand, anchor=subject.";
                if (round == 3) return "MEDIUM - subject has been empty for 3 rounds. Strongly prefer mode=expand on subject.";
            }

            if (!covered.Contains("setting") && round >= 5)
                return "MEDIUM - setting has been empty for 5 rounds. Prefer mode=expand on setting.";

            var debtAnchors = OdditariumAnchors.DebtTracked.Where(a => !covered.Contains(a)).ToList();
            if (debtAnchors.Count >= 3 && round >= 6)
                return $"HIGH - 3+ structural anchors empty after round 6. This round MUST expand one of: {string.Join(", ", debtAnchors.Take(3))}.";

            if (OdditariumAnchors.All.All(a => covered.Contains(a)))
                return "FREE - all 13 anchors have signal. Free to deepen any anchor with a fresh facet.";

            return "LOW - continue exploring naturally.";
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

        private static (string? Question, List<OdditariumOption>? Options, string? Anchor, string? Facet, string? Mode, List<string> ShownLabels)
            SnapshotPending(OdditariumBody body)
        {
            return (
                body.PendingQuestion,
                body.PendingOptions?.Select(o => new OdditariumOption { Label = o.Label, Hint = o.Hint, Payload = o.Payload, Anchor = o.Anchor, Facet = o.Facet }).ToList(),
                body.PendingAnchor,
                body.PendingFacet,
                body.PendingMode,
                body.ShownLabelsForCurrentQuestion.ToList()
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

        private void PublishTurn(OdditariumSession row, OdditariumLayer? layer)
        {
            _events.Publish(new OdditariumTurnAdvancedEventArgs
            {
                SessionId = row.SessionId,
                RoundCount = row.Body.RoundCount,
                LayerAdded = layer?.Label ?? string.Empty,
                LayersCollected = row.Body.CollectedLayers.Count,
            });
        }
    }
}
