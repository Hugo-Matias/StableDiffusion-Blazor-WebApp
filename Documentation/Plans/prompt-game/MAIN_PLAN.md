# Odditarium - Main Plan

> **Working name:** Odditarium (revisitable; see `PERSONA.md`).
> **Persona on duty:** The Curator (`.github/agents/odditarium-curator.agent.md`).
> **Plan status:** [~] Execution — Phases 1-5 complete, Phase 6 next. User approved plan on 2026-05-01.
> **Companion docs:**
>
> - [`PERSONA.md`](./PERSONA.md) - designer's notebook (in-flight ideas, open provocations, dead ends).
> - [`RULESET.md`](./RULESET.md) - formal ruleset governing LLM behavior during game sessions.
> - `SYSTEM_PROMPTS/v{n}.md` - versioned literal system prompts (created from Phase 4 onward).
> - `PHASE_{n}.md` - per-phase execution docs created as work progresses.

---

## Problem Statement

Phase 13 of the LLM Tools expansion shipped a button-driven prompt builder ("Workshop Wizard") mounted above the Workshop composer. Live testing surfaced two distinct categories of issues:

1. **Surface mismatch.** The wizard's audience (new users wanting playful, low-commitment prompt discovery) is the opposite of Workshop's audience (power users managing branched node graphs). Hosting both on the same page burdens both.
2. **Funnel-too-fast.** The hardcoded intro sections (Subject -> Scenery -> Lighting -> Mood -> Style) and the LLM's tendency to return pre-built phrases ("Ethereal crystal forest with trees made of glowing crystalline structures") collapse the design space on turn 1 instead of opening it.

The pivot turns the wizard into **Odditarium** - a dedicated game page where an LLM-driven persona leads the user through a guided drift across descriptive layers. The user collects ingredients; the LLM assembles the meal at commit time. Coherence is preserved by a formal ruleset; replayability is driven by distinct personas that fundamentally reshape every question and option they generate.

---

## Locked Decisions

| Decision             | Choice                                                                                                                                                 |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Working name         | **Odditarium** (revisit after first playable build)                                                                                                    |
| Persona file         | `.github/agents/odditarium-curator.agent.md` (activation) + `PERSONA.md` (notebook)                                                                    |
| Pivot scope          | **Full pivot:** new page + new nav entry; deprecate Workshop mount in this plan                                                                        |
| Core mechanic        | **Layer accumulation.** Each round adds an independent descriptive element. Final prompt assembled at Commit time, not built incrementally in the UI.  |
| Pacing model         | **Freestyle.** No minimum layer count, no turn cap. User commits whenever they want. Sessions persist and are resumable.                               |
| Phase 13 fate        | Archive as "shipped, superseded by Odditarium." Reuse entity, service skeleton, events, validator, and tests under the new page                        |
| Entry flow           | Intro scene (goal + encouragement) -> Start button -> Persona card selection -> Game begins                                                            |
| Persona system       | **Hardcoded roster of 5 handcrafted personas.** Each persona has identity, thematic boundaries, tone bias, and style preferences. Static image assets. |
| Options per round    | **Fixed at 6 options** per round + action buttons                                                                                                      |
| Action buttons       | **Undo** (revert last pick), **More...** (regenerate options for current question), **Skip this axis** (jump past uninteresting questions)             |
| Surprise me behavior | LLM picks freely - can invent unexpected anchor directions for maximum surprise                                                                        |
| User override        | No free-text input. Pure click-through with Undo/More/Skip as escape valves.                                                                           |
| Send-to surfaces     | Workshop composer, txt2img prompt, clipboard (v1)                                                                                                      |
| Persona editor       | Out of scope for v1. Stretch goal for future expansion.                                                                                                |

### Initial Persona Roster (v1)

| Persona       | Identity                                     | Thematic Tags                                  | Tone                                   |
| ------------- | -------------------------------------------- | ---------------------------------------------- | -------------------------------------- |
| The Muse      | Elegant art critic from a timeless gallery   | Beauty, composition, classical aesthetics      | Poetic, refined, slightly distant      |
| Iron Mother   | Battle-hardened survivor from a ruined world | Gritty realism, survival, weathered textures   | Direct, no-nonsense, protective        |
| Void Walker   | Cosmic entity from between dimensions        | Surrealism, cosmic horror, impossible geometry | Ominous but curious, speaks in riddles |
| Pixel Pixie   | Chaotic sprite from a digital fairy realm    | Whimsy, kawaii culture, bright pastels         | Bubbly, excited, playful               |
| The Architect | Cold AI designing perfect structures         | Cyberpunk minimalism, clean geometry           | Clinical, precise, analytical          |

---

## Proposed Solution

### Page Flow

```
Intro Scene -> Persona Selection -> Vibe Suggestion (persona-driven) -> Game Rounds (freestyle layer collection) -> Commit -> Send-to Menu
```

1. **Intro Scene** - Encouraging explanation of game goals with a "Start" button.
2. **Persona Cards** - 5 persona cards, each with an image, flavor text, and thematic stats showing style biases. User picks one.
3. **Vibe Suggestion** - The chosen persona suggests 3-4 vibes within their personality range. User picks one to lock the semantic field for this session.
4. **Game Rounds** - One question, 6 options per round. All driven by LLM under ruleset, filtered through persona identity. Each pick adds a descriptive layer to the collection. Action buttons: Undo, More..., Skip this axis.
5. **Commit** - User clicks whenever ready. The LLM assembles all collected layers into a coherent image prompt.
6. **Send-to Menu** - The committed draft goes to Workshop composer, txt2img prompt, or clipboard.

### How It Feels (Simulation)

**Iron Mother, "Gritty Survival" vibe:**

| Round | Question (persona-voiced)           | Options                                                                                                                | User Picks      |
| ----- | ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | --------------- |
| 1     | _"Who are we looking at out here?"_ | `Human` / `Animal` / `Machine` / `Creature` / `Vehicle` / `Structure`                                                  | `Human`         |
| 2     | _"What's their role in this mess?"_ | `Veteran` / `Child` / `Medic` / `Runner` / `Mechanic` / `Watchman`                                                     | `Veteran`       |
| 3     | _"What marks their experience?"_    | `Scarred` / `Weathered Skin` / `Missing Fingers` / `Bent Posture` / `Tattooed Arms` / `Voice Like Gravel`              | `Scarred`       |
| 4     | _"What's lighting this scene?"_     | `Firelight` / `Flare Gun` / `Broken Streetlamp` / `Headlamp Beam` / `Emergency Red Lights` / `Starlight Through Smoke` | `Firelight`     |
| 5     | _"What are they doing right now?"_  | `Checking Gear` / `Holding Radio` / `Kneeling` / `Looking Back` / `Carrying Wounded` / `Smoking`                       | `Checking Gear` |

**Collected layers:** `[Human] [Veteran] [Scarred] [Firelight] [Checking Gear]`

**Commit output (LLM-assembled):** "A scarred veteran checking their gear by firelight, weathered face illuminated by warm orange flames, gritty realism, dramatic shadows"

Same game mechanics with **The Muse** would produce completely different questions and options for the same anchor type - poetic framing, beauty-adjacent suggestions, refined vocabulary.

### Reused Phase 13 Infrastructure

- `WorkshopWizardSession` entity renamed to `OdditariumSession`. JSON body schema extends with persona fields (personaId, vibe, collectedLayers array).
- `WorkshopWizardService` renamed `OdditariumService`. The intro-catalog dependency is dropped entirely; the new flow is persona + vibe + LLM rounds.
- Events (`WizardTurnAdvancedEventArgs`, etc.) renamed to Odditarium equivalents; pub/sub plumbing reused as-is.
- `WizardResponseValidator` stays - the JSON response shape (question + options array) doesn't change.

The Workshop mount is removed at the end of the plan; Workshop instead gets a small "Open in Odditarium" affordance on its composer for users who want to switch surfaces.

---

## Key Conventions

These bind every phase. Conflicts with these are blockers, not friction.

- **Persona on duty.** All design decisions are made or reviewed by The Curator. Visual/component decisions delegate to the UI Design Language doc.
- **Document trail.** Every accepted decision lands here in the Locked Decisions table or in [`RULESET.md`](./RULESET.md). Versioned system prompts are new files (`SYSTEM_PROMPTS/v2.md`), never in-place rewrites.
- **Pub/sub for events.** Continue routing through `IEventService` ([`AGENTS.md`](../../../AGENTS.md) rule).
- **Persistence.** EF Core 6 / SQLite, JSON-backed body via `ValueConverter` + `IsModified = true` on in-place mutations. Manual migrations carry both `[DbContext]` + `[Migration]` attributes and update `AppDbContextModelSnapshot.cs`.
- **UI.** Page uses one of the documented layout shells per [`04-UI-DESIGN-LANGUAGE.md`](../../Architecture/04-UI-DESIGN-LANGUAGE.md). Spacing tokens from `wwwroot/site.css`. Buttons that move data between surfaces use `.send-to-btn`. No magic numbers.
- **Twin-trace gate.** Before any system-prompt version bump is approved, run a twin-trace check (two short playthroughs from the same first pick) and confirm meaningful divergence by turn 5.

---

## Stress Points (anticipated)

| Stress point                                    | Mitigation                                                                                                                                                                                                                                            |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Funnel-too-fast on Round 1**                  | **Critical.** Rule 3 (Specificity by Depth) enforces broad categories early. Options at round 1 should be single concepts or short phrases, never full scene descriptions. The LLM is free to choose dimensions but constrained on specificity level. |
| LLM returns synonym-stacked options             | Rule 4 (Contrast Spread); cheap heuristic post-validator (reject if 3+ options share a head noun) with one-shot retry                                                                                                                                 |
| Player feels "led down a narrow path"           | Rule 2 (Dimension Freedom) - no hardcoded dimension order. The LLM adapts to context, not a script.                                                                                                                                                   |
| Vibe coherence drifts after many rounds         | Rule 8 (Vibe Coherence guideline). Vibe stays in the system prompt's context for every call.                                                                                                                                                          |
| Persona voice is too subtle or too overpowering | Twin-trace across different personas; review whether same anchor type produces meaningfully different options                                                                                                                                         |
| Migration of existing Phase 13 rows             | Body schema is forward-compatible; nullable fields added rather than renamed                                                                                                                                                                          |
| Designer voice drift across sessions            | Persona agent file (`.github/agents/odditarium-curator.agent.md`) loaded on every relevant trigger                                                                                                                                                    |

---

## The Ruleset (summary)

Full ruleset documented in [`RULESET.md`](./RULESET.md). Summary of the 9 core rules:

| #   | Rule                     | Statement                                                                                            |
| --- | ------------------------ | ---------------------------------------------------------------------------------------------------- |
| 1   | **Layer Accumulation**   | Each round adds one independent descriptive layer. Add, don't replace.                               |
| 2   | **Dimension Freedom**    | LLM freely chooses what dimension to explore. No hardcoded order. Revisiting for depth is valid.     |
| 3   | **Specificity by Depth** | Broad categories early; drill deeper as layers accumulate. Adding detail is valid; replacing is not. |
| 4   | **Contrast Spread**      | 6 options must span different conceptual territories. No synonym stacking.                           |
| 5   | **Persona Filter**       | Every question and option passes through the active persona's identity and tone.                     |
| 6   | **Anti-Noun-Stacking**   | Option labels are 1-4 words max. Concepts, not sentences. Prompt assembled at Commit time.           |
| 7   | **No Loaded Options**    | All options equally compelling. No "obviously best" choice surrounded by weak distractors.           |
| 8   | **Vibe Coherence**       | Guideline: stay within vibe's semantic field. Persona identity can override.                         |
| 9   | **Commit Assembly**      | LLM assembles all collected layers into a coherent image prompt at commit time.                      |

---

## Implementation Phases (Fibonacci complexity)

| #   | Title                                                                                                              | Complexity | Status          |
| --- | ------------------------------------------------------------------------------------------------------------------ | ---------- | --------------- |
| 1   | Page shell, nav entry, route, intro scene                                                                          | 3          | [x] Complete    |
| 2   | Persona data model + hardcoded roster + CCG card selector                                                          | 5          | [x] Complete    |
| 3   | Entity/service rename (WorkshopWizard -> Odditarium), drop intro catalog, add persona/layer fields to session body | 8          | [x] Complete    |
| 4   | System prompt v1 (persona injection, ruleset encoding) with twin-trace review                                      | 8          | [x] Complete    |
| 5   | Round panel UI (6 options, Undo/More/Skip buttons, layer collection display)                                       | 8          | [x] Complete    |
| 6   | Validator hardening (synonym-stacking heuristic, specificity ramp enforcement)                                     | 5          | [ ] Not started |
| 7   | System prompt v2 - tuned from real playthroughs, twin-trace regression baseline                                    | 5          | [ ] Not started |
| 8   | Workshop integration: remove old mount, add "Open in Odditarium" affordance on composer                            | 3          | [ ] Not started |
| 9   | Manual playthrough pass + build/test green + Phase 13 archived as superseded                                       | 2          | [ ] Not started |

Total estimated complexity: **47 points**. Completed: **32 pts** (Phases 1-5). Remaining: **15 pts**.

---

### Phase 1: Page Shell, Nav Entry, Intro Scene

**Objective:** Establish Odditarium as an LLM Tools view inside the Prompts page with a working intro scene. Migrate Prompt Wizard out of WorkshopView.
**Status:** [x] Complete — See [`PHASE_1.md`](./PHASE_1.md)

#### Steps

- [x] Create OdditariumView + CSS — Main view component with game-like hero surface (floating orbs, gradient title, pill badges, start button)
- [x] Add nav entry to LLMNavMenu — Nav item `"odditarium"` with `Icons.Material.Filled.AutoFixHigh`
- [x] Wire view into LLMToolsTab switch — Added `"odditarium"` case rendering `<OdditariumView SelectedModel="@_selectedModel" />`
- [x] Remove WorkshopWizardPanel from WorkshopView — Removed both instances and `OnWizardCommitAsync` method

#### Success Criteria

- Odditarium accessible via LLM Tools nav menu ✓
- Intro scene renders with game-like hero surface ✓
- Start button transitions to next screen (placeholder) ✓
- Workshop no longer hosts the wizard panel ✓

---

### Phase 2: Persona Data Model + Hardcoded Roster + CCG Card Selector

**Objective:** Create the persona data model, hardcoded v1 roster of 5 personas, and a CCG-style card selector UI that transitions from the intro scene.
**Status:** [x] Complete — See [`PHASE_2.md`](./PHASE_2.md)

#### Steps

- [x] Create OdditariumPersona.cs data model — Properties: Id, Name, Tagline, Description, ThematicTags, ToneBias, StylePreferences, ImageAsset, AccentColor
- [x] Create OdditariumPersonaRoster.cs with 5 hardcoded personas — Muse (#c9a84c), Iron Mother (#b45a3c), Void Walker (#7b2d8e), Pixel Pixie (#e87cb9), The Architect (#4a90d9)
- [x] Create OdditariumPersonaView.razor — CCG card grid with native HTML + scoped CSS (no MudBlazor grid)
- [x] Wire Start Game -> Persona selection transition — `_showPersonaSelection` state toggles between intro and persona selector

#### Success Criteria

- 5 persona cards render in a centered horizontal row ✓
- Cards show accent-colored corner ornaments, border glow on hover/select, lift animations ✓
- Staggered entrance animations on card load ✓
- Selected persona highlighted with check mark pop animation ✓
- Back button returns to intro scene ✓
- Persona images directory created at `/odditarium/personas/` ✓

---

### Phase 3: Entity/Service Rename (WorkshopWizard -> Odditarium)

**Objective:** Complete the pivot by renaming all WorkshopWizard infrastructure to Odditarium equivalents, dropping the intro-catalog dependency, and extending the session body with persona/layer fields.
**Status:** [x] Complete — See [`PHASE_3.md`](./PHASE_3.md)

#### Steps

- [x] Rename `WorkshopWizardSession` entity to `OdditariumSession` — Extend JSON body schema with: `PersonaId`, `Vibe`, `CollectedLayers[]`, `RoundHistory[]`
- [x] Rename `WorkshopWizardService` to `OdditariumService` — Remove intro-catalog dependency entirely; new flow is persona + vibe + LLM rounds
- [x] Rename Wizard events (`WizardTurnAdvancedEventArgs`, etc.) to Odditarium equivalents — Pub/sub plumbing reused as-is
- [x] Keep `WizardResponseValidator` — Renamed to `OdditariumResponseValidator`; JSON response shape (question + options array) unchanged
- [x] Create EF Core migration for entity rename and body schema extension
- [x] Update all references across the codebase — No traces of "Wizard" naming remain after this phase

#### Success Criteria

- All Wizard-named entities/services/events renamed to Odditarium equivalents ✓
- Session body supports persona fields (PersonaId, Vibe, CollectedLayers) ✓
- EF Core migration applies cleanly with no data loss ✓
- Build passes with 0 errors ✓
- No remaining references to "Wizard" in Odditarium-related code ✓

---

### Phase 4: System Prompt v1 (Persona Injection, Ruleset Encoding)

**Objective:** Create the first version of the system prompt that injects persona identity and encodes all 9 ruleset rules. Validate with twin-trace review.
**Status:** [x] Complete (twin-trace deferred to Phase 7)

#### Steps

- [x] Design system prompt template — Accepts: persona identity block, vibe lock, collected layers array, round number
- [x] Encode Ruleset v1 — All 9 core rules from [`RULESET.md`](./RULESET.md) translated into LLM instructions
- [x] Persona injection mechanism — Each persona's full identity (Name, Tagline, Description, ThematicTags, ToneBias, StylePreferences) injected as a structured block
- [x] Implement `BuildRoundSystemPrompt` / `BuildCommitSystemPrompt` in OdditariumService — Composes system prompt from template + persona + session state
- [!] Twin-trace review — **Deferred to Phase 7** (requires Round Panel UI + Ollama backend)
- [x] Create `SYSTEM_PROMPTS/v1.md` — Versioned literal system prompt file

#### Success Criteria

- System prompt correctly injects persona identity ✓
- All 9 ruleset rules encoded in LLM instructions ✓
- Twin-trace shows meaningful divergence between two playthroughs with same starting pick ✓
- Round 1 options are broad categories (single concepts), never concrete scene descriptions ✓
- `SYSTEM_PROMPTS/v1.md` created and versioned ✓

---

### Phase 5: Round Panel UI (6 Options, Undo/More/Skip, Layer Collection)

**Objective:** Build the main game loop UI — one question with 6 clickable options per round, action buttons (Undo, More..., Skip), and a visible layer collection display.
**Status:** [x] Complete — See [`PHASE_5.md`](./PHASE_5.md)

#### Steps

- [x] Create round panel component — Displays LLM-generated question in persona voice + 6 option buttons
- [x] Implement option selection flow — Clicking an option adds it to `CollectedLayers[]`, triggers next LLM call for next round
- [x] Action buttons: Undo (revert last pick), More... (regenerate options for current question), Skip this axis (jump past uninteresting questions)
- [x] Layer collection display — Visual list of collected layers shown alongside the round panel, growing as rounds progress
- [x] Loading states - Skeleton/spinner while waiting for LLM response between rounds
- [x] Commit button — Always available, no minimum layer count enforced

#### Success Criteria

- [x] Round panel renders question + 6 options per round
- [x] Clicking option adds layer and triggers next round
- [x] Undo reverts last pick and regenerates previous round
- [x] More... regenerates options for current question
- [x] Skip advances past uninteresting questions
- [x] Layer collection grows visibly as rounds progress
- [x] Commit available at any time

#### Additional Notes

- Loading screen (`OdditariumLoading.razor`) is reusable across all async operations (option selection, More, Skip, Undo, Commit) and displays the persona's thinking-state image with breathing animation.
- Persona asset paths migrated to subfolder structure: `personas/{persona-id}/{state}.png` (idle, hover, active, thinking).

---

### Phase 6: Validator Hardening (Synonym Stacking, Specificity Ramp)

**Objective:** Add post-validation heuristics to LLM responses — reject synonym-stacked options and enforce specificity ramp across rounds.
**Status:** [ ] Not started

#### Steps

- [ ] Synonym-stacking heuristic — Reject if 3+ options share the same head noun; trigger one-shot retry
- [ ] Specificity ramp enforcement — Verify Round N options are more specific than Round N-1 (word count, adjective density)
- [ ] Retry logic with circuit breaker — Max 2 retries before falling back to original response
- [ ] Add validator tests — Unit tests for synonym detection and specificity scoring

#### Success Criteria

- No round contains 3+ synonym-stacked options ✓
- Options become progressively more specific across rounds ✓
- Validator rejects bad responses and triggers retry automatically ✓
- Circuit breaker prevents infinite retry loops ✓
- All validator tests pass ✓

---

### Phase 7: System Prompt v2 (Tuned from Real Playthroughs)

**Objective:** Refine system prompt based on real playthrough data. Establish twin-trace regression baseline.
**Status:** [ ] Not started

#### Steps

- [ ] Conduct manual playthroughs with each persona — At least 3 full sessions per persona (15 total)
- [ ] Collect failure cases — Document rounds where options felt stale, too specific, or off-vibe
- [ ] Tune system prompt v2 — Adjust ruleset encoding based on observed failures
- [ ] Twin-trace regression baseline — Re-run twin-trace with v2; confirm improvement over v1
- [ ] Create `SYSTEM_PROMPTS/v2.md` — Versioned literal system prompt file

#### Success Criteria

- At least 15 manual playthroughs completed (3 per persona) ✓
- Documented failure cases addressed in v2 ✓
- Twin-trace shows improved divergence and quality over v1 ✓
- `SYSTEM_PROMPTS/v2.md` created and versioned ✓

---

### Phase 8: Workshop Integration

**Objective:** Finalize the pivot — remove old Workshop mount completely, add "Open in Odditarium" affordance on Workshop composer.
**Status:** [ ] Not started

#### Steps

- [ ] Remove any remaining Wizard references from WorkshopView and related components
- [ ] Add "Open in Odditarium" button to Workshop composer — Navigates user to Odditarium view within LLM Tools
- [ ] Clean up orphaned files — Delete `WorkshopWizardPanel.razor`, `WorkshopWizardService.cs`, old Wizard events
- [ ] Verify Workshop first-send flow still works without regression

#### Success Criteria

- No remaining Wizard references in Workshop ✓
- "Open in Odditarium" button visible on Workshop composer ✓
- Clicking navigates to Odditarium view ✓
- Workshop first-send flow unchanged ✓
- Build passes with 0 errors ✓

---

### Phase 9: Manual Playthrough Pass + Archive

**Objective:** Final validation pass, build/test green, archive Phase 13 as superseded.
**Status:** [ ] Not started

#### Steps

- [ ] Full manual playthrough — Complete the entire flow: Intro → Persona Selection → Vibe Suggestion → Game Rounds → Commit → Send-to Menu
- [ ] Test all send-to surfaces — Workshop composer, txt2img prompt, clipboard
- [ ] Run full test suite — All Odditarium tests + existing tests pass
- [ ] Archive Phase 13 documentation — Mark as "shipped, superseded by Odditarium"
- [ ] Final build verification — Clean build with 0 errors

#### Success Criteria

- Complete flow works end-to-end ✓
- All send-to surfaces functional ✓
- Full test suite passes ✓
- Phase 13 archived ✓
- Build clean ✓

---

## Success Criteria

- A first-time user can open Odditarium, pick a persona, click through 5+ rounds of layer collection, and arrive at a draft prompt they did not expect - coherent with the persona's style, and different from a second playthrough with the same persona.
- Round 1 options are broad categories (single concepts), never concrete scene descriptions. The funnel-too-fast anti-pattern is eliminated.
- No round contains 3+ synonym-stacked options (validator-enforced).
- Two different personas given the same starting conditions produce meaningfully different questions and options.
- The user can commit at any point - no minimum layer count enforced.
- Sessions persist across navigation; users can resume and add more layers to previous playthroughs.
- Phase 13 surface is removed from Workshop with no regression to Workshop's first-send flow.
- All Odditarium tests + existing tests pass; build is clean.
- [`MAIN_PLAN.md`](./MAIN_PLAN.md), [`RULESET.md`](./RULESET.md), and the active `SYSTEM_PROMPTS/v{n}.md` are in sync with what shipped.

---

## Out of Scope (explicit)

- No image generation, branching, or node creation from Odditarium - it strictly produces a draft string. Send-to is how it integrates.
- No multiplayer / shared playthroughs.
- No "expert mode" with raw prompt editing inside Odditarium - if the player wants to edit, they commit and edit in Workshop.
- No analytics dashboards in v1 (the Drift counter and twin-trace tooling are designer instruments only).
- Persona-driven audio / animation flourishes - revisit after the first playable build proves the core loop.
- **Persona editor** - users cannot create custom personas in v1. Stretch goal for future expansion.

---

## Open Clarifications

| #   | Item                                                  | Status   | Resolution                                                                                                           |
| --- | ----------------------------------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------- |
| 1   | Vibe preset count                                     | ~~Open~~ | **RESOLVED:** Vibes are persona-driven, not hardcoded. Each persona suggests 3-4 vibes dynamically.                  |
| 2   | Anchor type list (Subject/Place/Era/Concept/Surprise) | Open     | Lean: closed for v1, LLM-extensible as stretch goal. Revisit if rounds feel claustrophobic.                          |
| 3   | Send-to surfaces                                      | ~~Open~~ | **RESOLVED:** Workshop composer, txt2img prompt, clipboard only for v1.                                              |
| 4   | Page name in nav                                      | Open     | "Odditarium" is working title. Decision deferred to Phase 1 review when the empty-state mock makes the name visible. |
| 5   | Session persistence on navigation away                | Open     | Lean: persist; player resumes where they left off; "New playthrough" button always available.                        |
| 6   | Persona card images                                   | ~~Open~~ | **RESOLVED:** Static hand-authored assets per persona card.                                                          |

---

## Approval

User must explicitly approve this plan before any `PHASE_{n}.md` is generated or any code is written under the new page. The Curator does not assume approval from silence.
