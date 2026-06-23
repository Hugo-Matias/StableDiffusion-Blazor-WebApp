# Phase 8 — Anchor Sequencer + Continue Session

> **Status:** [ ] Not started
> **Complexity:** 8 points (Fibonacci)
> **Depends on:** Phase 7 complete, `OdditariumBody.BodySchemaVersion = 2` established

---

## Goals

1. **Anchor Sequencer** — replace LLM-driven breadth-first roaming with a service-controlled "slot then facets" loop: fill one anchor (expand), follow with 1–2 deepen rounds on the same anchor, then move to the next empty anchor. LLM is reduced to inventing the facet name + question + options within a pre-determined anchor/mode frame.

2. **Continue Session** — a new session-picker screen in `OdditariumView` that loads recent active sessions and lets the user resume any of them, unlocking undo, more rounds, or commit from any point.

---

## Locked Decisions

| Decision                                     | Choice                                                                                                                                                                            |
| -------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Anchor pick order                            | Core first (subject → setting), then Structural in definition order (action → lighting → framing → atmosphere). After all 6 are filled: let LLM drive freely (pivot + enrichment) |
| Deepen rounds after expand                   | Random 0–2 per anchor (rolled at schedule time, not each round). Service decides, not LLM                                                                                         |
| Skip effect on sequencer                     | Clears `ScheduledAnchor` + `RemainingFacetRounds` immediately → service picks next anchor                                                                                         |
| LLM role when sequenced                      | Invents facet + question + 6 options only. Anchor + mode are sent as constraints in the prompt                                                                                    |
| LLM role when unsequenced (enrichment phase) | Current free-drive behavior unchanged                                                                                                                                             |
| `BodySchemaVersion` bump                     | 2 → 3 (sequencer fields added). Old rows wiped already                                                                                                                            |
| Continue screen trigger                      | Shown when at least 1 active (non-committed, non-reset) row exists in `OdditariumSessions`                                                                                        |
| Continue session data                        | `OdditariumSession.Id`, `Body.PersonaId`, `Body.RoundCount`, `Body.CollectedLayers.Count`, `UpdatedAt`, `Body.IsCommitted`                                                        |
| Discard from continue screen                 | Calls `ResetAsync` on that session — does NOT delete the row, just clears `IsActive = false`                                                                                      |
| Max sessions shown                           | 5 most recently updated                                                                                                                                                           |
| Unbound slot (`SessionId = null`)            | Continue screen can show and resume it                                                                                                                                            |

---

## State Changes to `OdditariumBody`

Add two new fields (backwards-compatible — both default to safe values if absent in old JSON):

```csharp
/// <summary>Anchor currently being worked by the sequencer. Null when in free-drive (enrichment) phase.</summary>
public string? ScheduledAnchor { get; set; }

/// <summary>How many more deepen rounds remain for ScheduledAnchor after the initial expand. 0 = move to next anchor after this round.</summary>
public int RemainingFacetRounds { get; set; }
```

`BodySchemaVersion` bumped to 3 (edit the property default in `OdditariumModels.cs`).

---

## Service Changes (`OdditariumService.cs`)

### New interface method

```csharp
Task<IReadOnlyList<OdditariumSession>> ListActiveSessionsAsync(int maxCount = 5);
```

### Implementation of `ListActiveSessionsAsync`

Query `OdditariumSessions` where `Body` deserialises with `IsActive == true && !IsCommitted`, ordered by `UpdatedAt DESC`, take `maxCount`. Return the list; do not call `PersistAsync`.

> Note: EF will deserialise the full body; no projection needed since the entity is small.

### Changes to `PopulatePendingFromLLMAsync`

Replace the current "let LLM decide mode/anchor" path with the sequencer:

```
AdvanceSequencer(body)   // picks next ScheduledAnchor + RemainingFacetRounds if needed
if body.ScheduledAnchor != null:
    body.PendingAnchor = body.ScheduledAnchor
    body.PendingMode   = (anchor has 0 layers) ? "expand" : "deepen"
    system = BuildSequencedRoundSystemPrompt(body)   // new builder
    user   = BuildSequencedRoundUserPrompt(body)     // new builder
else:
    // enrichment / free-drive phase - existing builders unchanged
    system = BuildRoundSystemPrompt(body)
    user   = BuildRoundUserPrompt(body)
```

### `AdvanceSequencer(OdditariumBody body)` (private static)

```
if body.ScheduledAnchor != null:
    return  // already scheduled, caller will manage decrement

nextAnchor = first anchor in (Core + Structural) with 0 layers in CollectedLayers, skipping any already visited
if nextAnchor == null:
    return  // all Core+Structural filled → enrichment phase; leave ScheduledAnchor null

body.ScheduledAnchor = nextAnchor
body.RemainingFacetRounds = Random.Shared.Next(0, 3)  // 0, 1, or 2 deepen rounds
```

### Changes to `NextRoundAsync` — decrement sequencer after a choice

After the layer is added and before calling `PopulatePendingFromLLMAsync`:

```
if body.ScheduledAnchor != null:
    if body.RemainingFacetRounds > 0:
        body.RemainingFacetRounds--
        // stay on same ScheduledAnchor (next call will be deepen)
    else:
        body.ScheduledAnchor = null  // done with this anchor, AdvanceSequencer picks next
```

### Changes to `SkipAxisAsync`

After recording the skip turn, before calling `PopulatePendingFromLLMAsync`:

```
body.ScheduledAnchor = null
body.RemainingFacetRounds = 0
```

### New prompt builders

**`BuildSequencedRoundSystemPrompt(OdditariumBody body)`**

Persona block → same as current. Then:

```
VOICE = this persona. JOB = generate a question and 6 options for the frame below.
You do NOT choose the anchor or mode — they are determined by the game.
Your only creative decisions: invent an appropriate facet, write the question, produce 6 options.

THE 13 ANCHORS: [same reference block as current]

FACETS: [same facet block as current]

CURRENT FRAME:
  anchor : {body.ScheduledAnchor}
  mode   : {pendingMode}   (expand = first pick for this anchor | deepen = add facet detail)
  {if deepen: "Layers already on this anchor: {existing labels for anchor}"}
  Visited facets for this anchor (do NOT repeat): {list or "(none)"}

COLLECTED LAYERS (other anchors):
{BuildAnchorCoverageBlock without the scheduled anchor}

RULES:
1. Facet must be lowercase-kebab, 1–4 tokens, not in the visited list.
2. [CONTRAST SPREAD, LABEL, HINT, SURPRISE, NEVER-RECYCLE rules — same as current]

Return ONLY valid JSON: { "facet": "...", "question": "...", "options": [...6...] }
Note: Do NOT include "mode" or "anchor" in your response — they are fixed by the frame.
```

> The validator will coerce anchor + mode from the frame since the LLM is not asked to emit them.
> `OdditariumResponseValidator.Sanitize` should inject `body.PendingAnchor` and `body.PendingMode`
> into the sanitized response when those fields are null in the raw JSON.

**`BuildSequencedRoundUserPrompt(OdditariumBody body)`**

```
[seed:{N}]
Round {body.RoundCount + 1} — anchor: {body.ScheduledAnchor}, mode: {pendingMode}.
{if mode == expand: "Give 6 options that span radically different territory within this anchor."}
{if mode == deepen: "The user picked '{last label for this anchor}'. Drill into a new facet that reveals something unexpected about it."}
```

### Changes to `OdditariumResponseValidator.Sanitize`

Add an optional `(string? forceAnchor, string? forceMode)` overload parameter (or call-site inject). When the sanitized anchor is null/unknown and `forceAnchor` is provided, use `forceAnchor` instead of coercing to `"detail"`. Same for mode.

Alternatively (simpler): in `PopulatePendingFromLLMAsync`, after calling `TryParse`, if `response.Anchor` is null, set it to `body.ScheduledAnchor`. Same for mode. This keeps the validator unchanged.

---

## UI Changes (`OdditariumView.razor`)

### New state fields

```csharp
private IReadOnlyList<OdditariumSession>? _activeSessions;
private bool _loadingSessions;
```

### `OnInitializedAsync`

Load active sessions:

```csharp
_loadingSessions = true;
_activeSessions = await OdditariumService.ListActiveSessionsAsync(5);
_loadingSessions = false;
```

### Intro screen additions

Below the existing feature list and above the Start button, add a "Continue a journey" section:

```
@if (_activeSessions is { Count: > 0 })
{
    <div class="odditarium-continue-section">
        <h3>Continue a journey</h3>
        @foreach (var s in _activeSessions)
        {
            <OdditariumSessionCard Session="s"
                                   OnResume="HandleResumeSession"
                                   OnDiscard="HandleDiscardSession" />
        }
    </div>
}
```

### New handlers

```csharp
private async Task HandleResumeSession(int sessionId)
{
    _session = await OdditariumService.LoadOrCreateAsync(sessionId);
    _selectedPersonaId = _session.Body.PersonaId;
    _showPersonaSelection = false;
    _showRoundPanel = true;
}

private async Task HandleDiscardSession(int sessionId)
{
    await OdditariumService.ResetAsync(sessionId);
    _activeSessions = await OdditariumService.ListActiveSessionsAsync(5);
}
```

---

## New Component: `OdditariumSessionCard.razor`

**Location:** `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumSessionCard.razor`

**Parameters:**

- `[Parameter] OdditariumSession Session`
- `[Parameter] EventCallback<int> OnResume`
- `[Parameter] EventCallback<int> OnDiscard`

**Rendered content:**

- Persona thumbnail (idle image, small) + persona name (from `OdditariumPersonaRoster.FindById`)
- Layer count badge: `{Session.Body.CollectedLayers.Count} layers`
- Last updated: relative time (e.g. "2 hours ago") — use `TimeAgo(Session.UpdatedAt)` helper
- Resume button (primary style)
- Discard button (text style, secondary, with confirm-on-click via toggle)

**CSS:** `OdditariumSessionCard.razor.css` — compact horizontal card, persona thumbnail 40x40, align items center, gap using `var(--space-3)`

---

## Implementation Steps

| #   | Description                                                                                                                     | Points |
| --- | ------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 1   | `OdditariumBody`: add `ScheduledAnchor`, `RemainingFacetRounds`, bump `BodySchemaVersion` to 3                                  | 1      |
| 2   | `OdditariumService`: `AdvanceSequencer`, decrement logic in `NextRoundAsync`, clear in `SkipAxisAsync`                          | 2      |
| 3   | New prompt builders: `BuildSequencedRoundSystemPrompt`, `BuildSequencedRoundUserPrompt`; route in `PopulatePendingFromLLMAsync` | 2      |
| 4   | Anchor/mode injection after `TryParse` when sequenced (no validator changes)                                                    | 1      |
| 5   | `ListActiveSessionsAsync` service method + interface declaration                                                                | 1      |
| 6   | `OdditariumSessionCard.razor` + CSS                                                                                             | 2      |
| 7   | `OdditariumView.razor`: load sessions on init, continue section, resume/discard handlers                                        | 2      |
| 8   | Unit tests: `AdvanceSequencer` priority order, decrement/clear logic, `ListActiveSessionsAsync` with in-memory DB               | 2      |

**Total: 13 points**

---

## Success Criteria

1. Starting a new game: first question targets `subject` (expand), followed by 0–2 deepen rounds on subject, then `setting` expand + deepens, then `action`... until all Core + Structural anchors are filled.
2. Skipping during a sequenced anchor immediately advances to the next anchor.
3. After all Core + Structural anchors are filled, the game continues in free-drive (current LLM-driven) mode.
4. Continue screen shows up to 5 active sessions on the intro page; clicking Resume jumps directly to the round panel.
5. Clicking Discard marks the session inactive and removes it from the list.
6. All Phase 7 unit tests still pass. New tests cover sequencer logic.

---

## Open Clarifications

- **Deepen count range**: currently 0–2 (`Random.Shared.Next(0, 3)`). If 0 is picked, the anchor gets only the expand round. This feels right for pacing — some slots stay shallow, some go deep. Adjust if playtesting shows too many shallow rounds.
- **Enrichment anchors in free-drive**: once sequencer is exhausted, the LLM may pivot to mood/style/etc. The existing structural-debt cue can be removed from free-drive prompts since it served the same purpose the sequencer now handles explicitly.
- **`ListActiveSessionsAsync` definition of "active"**: `IsActive == true && !IsCommitted`. If `IsActive` is never set to false after commit, use `!IsCommitted` only.
