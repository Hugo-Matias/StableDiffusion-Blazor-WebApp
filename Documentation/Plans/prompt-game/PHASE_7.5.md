# Phase 7.5 - Service-Driven Anchor Interleaving + Choice-Driven Facet Entropy

> **Phase complexity:** 13
> **Status:** [ ] Not started
> **Depends on:** Phase 7 (anchor/facet/mode model, typed layers, VisitedFacets, two-pass commit)
> **Companion docs:** [`MAIN_PLAN.md`](./MAIN_PLAN.md), [`PHASE_7.md`](./PHASE_7.md), [`RULESET.md`](./RULESET.md)
> **Persona on duty:** The Curator (`.github/agents/odditarium-curator.agent.md`)
> **Target LLM:** Qwen3 9B / Gemma 4 — same target as Phase 7. Prompts stay terse and example-led.

---

## Problem Statement

Phase 7 shipped the structural backbone (anchors, facets, modes, VisitedFacets) but left the **decision of which anchor to target and what mode to use entirely with the LLM**. Observed in early playthroughs:

1. **Breadth-first drift.** The LLM consistently sweeps across anchors — expand/expand/expand — never deepening any of them. Sessions reach 6 anchors with one layer each rather than 2–3 anchors with genuine depth.

2. **Generic facets.** Because the LLM picks the facet without a concrete seed, it defaults to the same handful per anchor: `core-archetype` for subject, `time-of-day` for setting, `direction` for lighting. The facet vocabulary converges across runs.

3. **Choices do not shape future rounds.** A player who picks "Rusted Automaton" gets the same setting question as one who picks "Feral Child." Earlier decisions have no structural effect on what questions come later — the session feels like a form, not a game.

---

## Solution Summary

Three layered changes address these failures:

**1. Service-owned anchor + mode.** Move anchor and mode decisions from the LLM to the service. The LLM only returns: `facet` + `question` + `options`. This eliminates breadth-first drift and makes deepen pacing deterministic and persona-tuned.

**2. Persona anchor personality.** Each persona carries `AnchorAffinity[]`, `MinDeepensPerAnchor`, and `MaxDeepensPerAnchor`. The anchor queue and deepen countdown are built from these at session start, so Iron Mother and Pixel Pixie produce structurally different round sequences even from the same opening pick.

**3. Choice-driven facet entropy — three mechanisms:**

- **A. Resonance queue dynamics.** Every pick reshapes the anchor queue by bumping the most thematically adjacent unvisited anchor earlier. The queue is not static; it evolves continuously with the session's choices.
- **B. Choice-driven facet seeding.** When the service tells the LLM "anchor=setting, mode=expand", it also passes the last 1–2 picked labels+hints as explicit seed context: "The subject you are building around is [Rusted Automaton]. Invent a setting facet that emerges from this." Same anchor, different prior choices → different facet territories.
- **C. Cross-anchor facet inheritance on deepens.** When entering a deepen round, the service injects a layer from a _different_ anchor as the inheritance seed: "The subject is [Kneeling Veteran]. Invent a lighting facet that addresses how the light falls on this specific posture." Deepens feel like natural follow-ups to prior picks, not isolated checklist items.

The LLM round response schema is simplified: `mode` and `anchor` are removed (service-owned). A round response is now `{ "facet", "question", "options" }`.

---

## Locked Decisions

| Decision                                 | Choice                                                                                                                                                                                                                                                                          |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Anchor + mode ownership                  | **Service-owned.** LLM no longer declares them in round responses.                                                                                                                                                                                                              |
| Facet ownership                          | **LLM-invented.** Service provides a seed context; LLM generates the facet identifier, question, and options.                                                                                                                                                                   |
| Round response schema                    | `{ "facet": string, "question": string, "options": [{label, hint}×6] }`. `mode` and `anchor` removed from LLM output.                                                                                                                                                           |
| Persona anchor fields                    | `AnchorAffinity string[]`, `MinDeepensPerAnchor int`, `MaxDeepensPerAnchor int` added to `OdditariumPersona`.                                                                                                                                                                   |
| Body queue fields                        | `AnchorQueue List<string>`, `CurrentAnchorTarget string?`, `RemainingDeepensForAnchor int` added to `OdditariumBody`.                                                                                                                                                           |
| Queue build timing                       | Once at session start (`StartSessionAsync`). Persisted in the body JSON for resume. Resonance mutations modify the queue in place at each pick.                                                                                                                                 |
| Resonance map                            | Fixed 5-entry static dictionary: `subject→action`, `action→framing`, `lighting→atmosphere`, `mood→style`, `setting→scale`. Evaluated after every pick; bumps the resonance anchor to position 0 in the remaining queue if it is not already visited and not the current target. |
| Facet seed — expand rounds               | Last 1–2 collected layers (label + hint) injected as: "The session has placed: [label] (hint). Invent a {anchor} facet that emerges naturally from this presence."                                                                                                              |
| Facet seed — deepen rounds (mechanism C) | `CollectedLayers.Last(l => l.Anchor != currentAnchor)` injected as the inheritance seed. Omitted gracefully if no cross-anchor layer exists yet.                                                                                                                                |
| Facet seed — first round                 | "This is the opening round. Invent a {anchor} facet that fits the persona voice and the vibe '{body.Vibe}'."                                                                                                                                                                    |
| System prompt version                    | **v3 (round prompt only).** Commit prompts remain on v2. `SYSTEM_PROMPTS/v3.md` created.                                                                                                                                                                                        |
| Validator changes                        | `mode` and `anchor` no longer validated or coerced on round responses. Facet kebab-coercion and non-empty check retained.                                                                                                                                                       |
| `OdditariumBody` backward compat         | All three new fields are additive JSON. Existing sessions without them get the queue built lazily on first call to `AdvanceAnchorTarget`.                                                                                                                                       |

---

## Persona Anchor Personality

### New `OdditariumPersona` fields

```csharp
/// <summary>
/// Anchor names ordered high to low persona affinity. Biases the anchor queue shuffle:
/// high-affinity anchors surface earlier in the session. Anchors not listed are
/// appended in default tier order after all listed ones.
/// </summary>
public string[] AnchorAffinity { get; set; } = Array.Empty<string>();

/// <summary>Minimum deepen follow-up rounds to run per anchor for this persona (0 = possible skip).</summary>
public int MinDeepensPerAnchor { get; set; } = 1;

/// <summary>Maximum deepen follow-up rounds to run per anchor for this persona.</summary>
public int MaxDeepensPerAnchor { get; set; } = 2;
```

### Per-persona values

| Persona       | AnchorAffinity (high → low)                                                           | Min / Max Deepens | Rationale                                                                                 |
| ------------- | ------------------------------------------------------------------------------------- | ----------------- | ----------------------------------------------------------------------------------------- |
| The Muse      | `framing`, `style`, `lighting`, `mood`, `detail`, `setting`, `subject`, `action`      | 2 / 3             | Dwells on aesthetics; each anchor deserves refined treatment before moving on             |
| Iron Mother   | `action`, `subject`, `setting`, `atmosphere`, `lighting`, `framing`, `mood`, `style`  | 1 / 2             | Moves fast and practical; one deep look then next objective                               |
| Void Walker   | `atmosphere`, `mood`, `setting`, `scale`, `color`, `subject`, `style`, `framing`      | 2 / 3             | Builds dread slowly; enrichment anchors surface before subject gets pinned                |
| Pixel Pixie   | `color`, `style`, `mood`, `movement`, `subject`, `setting`, `atmosphere`, `framing`   | 0 / 1             | Chaotic; skips fast across everything, rarely lingers — some anchors get no deepen at all |
| The Architect | `framing`, `scale`, `setting`, `style`, `detail`, `subject`, `lighting`, `atmosphere` | 2 / 3             | Methodical; composes the frame before populating it                                       |

---

## Body Schema Changes

```csharp
// NEW — which anchor the service is currently targeting
[JsonPropertyName("currentAnchorTarget")]
public string? CurrentAnchorTarget { get; set; }

// NEW — how many more deepen rounds remain for CurrentAnchorTarget (0 = advance to next)
[JsonPropertyName("remainingDeepensForAnchor")]
public int RemainingDeepensForAnchor { get; set; }

// NEW — ordered list of unvisited anchors remaining in this session's queue
[JsonPropertyName("anchorQueue")]
public List<string> AnchorQueue { get; set; } = new();
```

No EF Core migration needed. All three are additive JSON properties on the existing `OdditariumBody` value type.

---

## Resonance Map

Static readonly in `OdditariumService`. Applied in `NextRoundAsync` after the layer is committed.

| Pick came from anchor | Resonance target (bump toward front of remaining queue) |
| --------------------- | ------------------------------------------------------- |
| `subject`             | `action`                                                |
| `action`              | `framing`                                               |
| `lighting`            | `atmosphere`                                            |
| `mood`                | `style`                                                 |
| `setting`             | `scale`                                                 |

**No-op conditions:** resonance target is already `CurrentAnchorTarget`, already visited (in `VisitedFacets`), or already at position 0 of the queue. Side effects: the resonance anchor is removed from wherever it sits in the queue and re-inserted at index 0.

---

## Queue Building Algorithm

`BuildAnchorQueue(OdditariumBody body, OdditariumPersona persona)`:

1. All 13 anchors in 3 tiers: **Core** (`subject`, `setting`), **Structural** (`action`, `lighting`, `framing`, `atmosphere`), **Enrichment** (`mood`, `style`, `detail`, `time`, `scale`, `color`, `movement`).
2. Assign each anchor an affinity weight: index in `persona.AnchorAffinity` (0 = highest priority). Unmentioned anchors get weight = `AnchorAffinity.Length + defaultTierIndex`.
3. Sort within each tier by weight, then apply a small random jitter (±0.3 per item) to prevent identical queues on repeated runs with the same persona.
4. Concatenate tiers: Core → Structural → Enrichment.
5. Remove any anchors already present in `body.VisitedFacets.Keys` (session resume support).
6. Store result in `body.AnchorQueue`.

---

## `AdvanceAnchorTarget` Logic

Called at the start of `PopulatePendingFromLLMAsync`. Sets `body.PendingAnchor` and `body.PendingMode`.

```
if body.AnchorQueue.Count == 0 AND body.CurrentAnchorTarget == null:
    BuildAnchorQueue(body, persona)    // first round, or resume with uninitialised queue

if body.RemainingDeepensForAnchor > 0:
    // continue deepening the current anchor
    mode   = "deepen"
    anchor = body.CurrentAnchorTarget
    body.RemainingDeepensForAnchor--
else:
    // pop next anchor from queue
    if body.AnchorQueue.Count > 0:
        anchor = body.AnchorQueue[0]; body.AnchorQueue.RemoveAt(0)
    else:
        // all anchors visited — bonus deepen on the thinnest anchor
        anchor = body.CollectedLayers.GroupBy(l => l.Anchor).OrderBy(g => g.Count()).First().Key
    mode = "expand"
    body.CurrentAnchorTarget = anchor
    body.RemainingDeepensForAnchor = Random(persona.MinDeepensPerAnchor, persona.MaxDeepensPerAnchor)

body.PendingAnchor = anchor
body.PendingMode   = mode
```

---

## Facet Seed Block Construction

`BuildFacetSeedBlock(OdditariumBody body, string anchor, string mode)`:

```
// Mechanism C — cross-anchor inheritance for deepens
if mode == "deepen":
    crossLayer = body.CollectedLayers.LastOrDefault(l => l.Anchor != anchor)
    if crossLayer != null:
        return $"You are deepening [{anchor}]. The most recent layer from another anchor is
                 [{crossLayer.Label}] ({crossLayer.Hint}). Invent a {anchor} facet that
                 speaks to how this specific element relates to {anchor}."
    else:
        return $"You are deepening [{anchor}]. No companion anchor layer exists yet —
                 invent a {anchor} facet that fits the persona voice and vibe '{body.Vibe}'."

// Mechanism B — last pick seeds expand rounds
recentLayers = body.CollectedLayers.TakeLast(2).ToList()
if recentLayers.Count > 0:
    seedText = string.Join(" / ", recentLayers.Select(l => $"[{l.Label}] ({l.Hint})"))
    return $"The session has placed: {seedText}. Invent a {anchor} facet that emerges
             naturally from this presence."
else:
    return $"This is the opening round. Invent a {anchor} facet that fits the persona
             voice and the vibe '{body.Vibe}'."
```

---

## System Prompt v3 Changes (Round Prompt Only)

### What is removed

The `ROUND MODES (declare which one you are using)` section is removed entirely, as is the `mode` and `anchor` fields from the return JSON schema declaration and the anchor-coverage + structural-debt pressure blocks (these were only needed when the LLM was making the anchor decision).

### What is added

**Directive block** (replaces the LLM anchor-selection instructions):

```
THIS ROUND — anchor: {anchor} | mode: {mode}
{facet_seed_block}

Your task:
1. Invent one facet identifier (lowercase-kebab, 1–4 tokens) for anchor [{anchor}].
2. Ask one question in persona voice.
3. Offer exactly 6 options (label 1–3 words, hint optional).

Return ONLY valid JSON, no prose, no code fences:
{
  "facet": "<lowercase-kebab facet you invented>",
  "question": "<persona-voiced question>",
  "options": [
    { "label": "<1–3 word concept>", "hint": "<persona-flavored hint, max 80 chars>" },
    ... exactly 6 items
  ]
}
```

The rest of the system prompt (persona block, vibe lock, label rules, hint rules, no-loaded-options rule) is unchanged.

### Priming example update

The `RoundSchemaPriming` static field in `OdditariumService` must be updated to match the new schema (no `mode`/`anchor` fields):

```json
{
  "facet": "core-archetype",
  "question": "What form does the central presence take?",
  "options": [
    {
      "label": "Wandering Knight",
      "hint": "Duty-worn, carries obligations unspoken."
    },
    {
      "label": "Street Vendor",
      "hint": "Commerce at the margins; every corner known."
    },
    {
      "label": "Feral Child",
      "hint": "Raised outside order, instincts honed to glass."
    },
    {
      "label": "Broken Oracle",
      "hint": "Speaks in fragments; once saw too clearly."
    },
    {
      "label": "Sleeping Giant",
      "hint": "Dormant scale — overwhelming when stirred."
    },
    {
      "label": "Hollow Automaton",
      "hint": "Built to serve; the soul question lingers."
    }
  ]
}
```

---

## Implementation Steps

| #   | Step                                                                                                                                                                                                                                                                                                                                                                     | Complexity |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------- |
| 1   | Add `AnchorAffinity`, `MinDeepensPerAnchor`, `MaxDeepensPerAnchor` to `OdditariumPersona.cs`. Update `OdditariumPersonaRoster.cs` for all 5 personas with the approved values from the Locked Decisions table.                                                                                                                                                           | 1          |
| 2   | Add `CurrentAnchorTarget`, `RemainingDeepensForAnchor`, `AnchorQueue` to `OdditariumBody` in `OdditariumModels.cs`.                                                                                                                                                                                                                                                      | 1          |
| 3   | Implement `BuildAnchorQueue(OdditariumBody body, OdditariumPersona persona)` in `OdditariumService` — weighted shuffle respecting tier order + affinity rank + jitter. Skip already-visited anchors.                                                                                                                                                                     | 2          |
| 4   | Implement `AdvanceAnchorTarget(OdditariumBody body, OdditariumPersona persona)` per spec. Sets `body.PendingAnchor` and `body.PendingMode`. Lazy queue init on first call.                                                                                                                                                                                               | 2          |
| 5   | Implement static resonance map + `MutateQueueOnPick(OdditariumBody body, string pickedAnchor)` — bump resonance target to queue index 0 when applicable. Call from `NextRoundAsync` after layer is committed to `CollectedLayers`.                                                                                                                                       | 1          |
| 6   | Implement `BuildFacetSeedBlock(OdditariumBody body, string anchor, string mode)` per spec — returns the context string for mechanism B/C/first-round.                                                                                                                                                                                                                    | 1          |
| 7   | Rewrite `PopulatePendingFromLLMAsync`: call `AdvanceAnchorTarget` first, then build facet seed block, then call `BuildRoundSystemPrompt` with the decided anchor + mode + seed. No longer reads `parsed.Anchor` / `parsed.Mode` from LLM response.                                                                                                                       | 2          |
| 8   | Rewrite `BuildRoundSystemPrompt` / `BuildRoundUserPrompt` per v3 spec: inject directive block (anchor + mode + seed), remove LLM mode/anchor declaration section and anchor-coverage / structural-debt blocks.                                                                                                                                                           | 3          |
| 9   | Update `RoundSchemaPriming` to match the new `{ facet, question, options }` schema. Update `OdditariumResponseValidator.Sanitize` — stop coercing / validating `mode` + `anchor` on round responses; retain facet kebab-coercion. `OdditariumLLMResponse.Mode` and `.Anchor` remain on the DTO (used by commit expansions) but are ignored when parsing round responses. | 1          |
| 10  | Write `SYSTEM_PROMPTS/v3.md` with the literal v3 round system prompt and user prompt (commit prompts noted as unchanged from v2).                                                                                                                                                                                                                                        | 1          |
| 11  | Unit tests (see Test Plan).                                                                                                                                                                                                                                                                                                                                              | 3          |

**Step-level total: 18. Fibonacci estimate: 13.**

---

## Test Plan

### New tests in `OdditariumServiceTests.cs`

| Test                                                     | What it verifies                                                                     |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `BuildAnchorQueue_MuseHasFramingInFirstThreePositions`   | Muse affinity surfaces framing early                                                 |
| `BuildAnchorQueue_IronMotherHasActionBeforeStyle`        | Iron Mother's action precedes style                                                  |
| `BuildAnchorQueue_PixelPixieHasColorInFirstThree`        | Pixel Pixie color affinity visible in queue                                          |
| `BuildAnchorQueue_SkipsAlreadyVisitedAnchors`            | Resume: visited anchors omitted from queue                                           |
| `BuildAnchorQueue_CoreAnchorsInFirstHalf`                | subject and setting appear before enrichment anchors for all personas                |
| `AdvanceAnchorTarget_FirstCallSetsExpandMode`            | mode=expand and pops from queue                                                      |
| `AdvanceAnchorTarget_DeepensDecrementCountdown`          | after expand, deepen calls decrement RemainingDeepens to 0                           |
| `AdvanceAnchorTarget_AdvancesQueueWhenDepleted`          | countdown reaches 0 → next call pops next anchor, mode=expand                        |
| `AdvanceAnchorTarget_PixelPixieCanHaveZeroDeepens`       | MinDeepens=0 allows some anchors to get no deepen at all                             |
| `MutateQueueOnPick_BumpsResonanceAnchorToFront`          | picking from `subject` bumps `action` to queue index 0                               |
| `MutateQueueOnPick_NoOpWhenAlreadyVisited`               | resonance target already visited → queue unchanged                                   |
| `MutateQueueOnPick_NoOpWhenAlreadyCurrentTarget`         | resonance target == CurrentAnchorTarget → queue unchanged                            |
| `BuildFacetSeedBlock_ExpandUsesLastLayer`                | seed block contains the last collected label+hint                                    |
| `BuildFacetSeedBlock_DeepensUsesCrossAnchorLayer`        | deepen seed uses a layer from a different anchor                                     |
| `BuildFacetSeedBlock_FirstRoundUsesVibeOpener`           | no collected layers → vibe-based opener                                              |
| `PopulatePending_DoesNotReadModeOrAnchorFromLLMResponse` | parsed.Mode / parsed.Anchor are ignored; PendingMode/PendingAnchor come from service |

---

## Success Criteria

1. Iron Mother and Pixel Pixie starting from the same first pick produce different anchor sequences by round 3. Confirmed by unit test queue inspection.
2. Two runs with the same persona and same anchor sequence produce different facet identifiers at least 70% of the time. Confirmed by twin-trace.
3. Deepen round questions reference the cross-anchor inheritance seed anchor's label in the question or hints (they feel connected to prior picks). Confirmed by twin-trace.
4. Resonance queue bump is observable in tests: picking from `subject` causes the next non-deepen round to target `action`.
5. LLM round responses no longer include `mode` or `anchor` (schema simplified). `SYSTEM_PROMPTS/v3.md` matches literal service output.
6. All existing Odditarium tests continue to pass. Build clean.

---

## Stress Points

| Stress point                                                    | Mitigation                                                                                                                                                            |
| --------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| LLM ignores facet seed and defaults to generic facets anyway    | Seed is in the directive line directly before the output schema. If twin-trace shows generic facets persisting, elevate the seed to a GOOD/BAD example in the prompt. |
| Pixel Pixie MinDeepens=0 produces sessions with no depth at all | Acceptable by design — it is Pixel Pixie's trait. If twin-trace shows it hurts session quality, raise to 1.                                                           |
| Queue exhausted (all 13 anchors visited)                        | `AdvanceAnchorTarget` falls back to deepening the thinnest anchor. Effectively supports infinite sessions.                                                            |
| Resonance bump creates two framing rounds back-to-back          | Resonance is no-op if the resonance target == `CurrentAnchorTarget`. The bump only affects the remaining queue, not the current round.                                |
| Session resume restores wrong queue order                       | `AnchorQueue` is persisted in the body JSON. It is restored as-is rather than rebuilt, preserving the exact run order.                                                |
| Cross-anchor seed unavailable on first deepen of a session      | Graceful fallback: "No companion anchor layer exists yet — invent a facet that fits the persona voice."                                                               |
| Commit prompts not updated                                      | Commit prompts remain on v2; they receive layers with anchor/facet/label/hint already fully populated, so v2 commit logic is unaffected.                              |

---

## Open Clarifications

- [ ] (none at phase start)

---

## Resolved Assumptions

- `AnchorQueue` is persisted in session body JSON so a resumed session restores the exact queue state rather than rebuilding from scratch mid-run.
- Resonance map is fixed in code, not persona-configurable in this phase. Persona-specific resonance weights are a stretch goal for the persona improvement phase.
- `OdditariumLLMResponse.Mode` and `.Anchor` remain on the DTO because commit expansion responses use them. Round responses that include these fields are silently ignored rather than rejected.
- The jitter in `BuildAnchorQueue` is applied per-session using a fresh `Random()` instance; no seed is stored. This is intentional — same persona should not produce the same queue every run.
- Phase 7.5 ships before Phase 6 (Validator Hardening). Phase 6's synonym-stacking heuristic is unaffected by this change, but the specificity ramp enforcement operates on the simplified response schema.
