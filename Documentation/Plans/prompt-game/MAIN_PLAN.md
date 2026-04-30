# Odditarium - Main Plan

> **Working name:** Odditarium (revisitable; see `PERSONA.md`).
> **Persona on duty:** The Curator (`.github/agents/odditarium-curator.agent.md`).
> **Plan status:** [ ] Draft - awaiting user approval before any phase document is written.
> **Companion docs:**
>
> - `PERSONA.md` - designer's notebook (in-flight ideas, open provocations, dead ends).
> - `RULESET.md` - formal ruleset (created in Phase 2).
> - `SYSTEM_PROMPTS/v{n}.md` - versioned literal system prompts (created from Phase 3 onward).
> - `PHASE_{n}.md` - per-phase execution docs created as work progresses.

---

## Problem Statement

Phase 13 of the LLM Tools expansion shipped a button-driven prompt builder ("Workshop Wizard") mounted above the Workshop composer. Live testing surfaced two distinct categories of issues:

1. **Surface mismatch.** The wizard's audience (new users wanting playful, low-commitment prompt discovery) is the opposite of Workshop's audience (power users managing branched node graphs). Hosting both on the same page burdens both.
2. **Funnel-too-fast.** The hardcoded intro sections (Subject -> Scenery -> Lighting -> Mood -> Style) and the LLM's tendency to return pre-built phrases ("Ethereal crystal forest with trees made of glowing crystalline structures") collapse the design space on turn 1 instead of opening it.

The pivot turns the wizard into **Odditarium** - a dedicated game page where the LLM-as-curator leads the user through a guided drift across mood, anchor, and progressively narrower axes, with coherence preserved by a named ruleset and replayability preserved by a Wildcard Quota.

## Locked Decisions (from planning Q&A)

| Decision      | Choice                                                                                                                                      |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Working name  | **Odditarium** (revisit after first playable build)                                                                                         |
| Persona file  | **Both:** `.github/agents/odditarium-curator.agent.md` (activation) + `Documentation/Plans/prompt-game/PERSONA.md` (notebook)               |
| Pivot scope   | **Full pivot:** new page + new nav entry; deprecate Workshop mount in this plan                                                             |
| Ruleset shape | Plan documents rules-as-axes; literal system prompt text iterated in dedicated PHASE doc with 2-3 review rounds                             |
| Pacing model  | **Milestone-based.** No turn cap. Commit affordance lights up when Vibe + Anchor + 1 specifier are defined; the player decides when to stop |
| Phase 13 fate | Archive as "shipped, superseded by Odditarium." Reuse entity, service skeleton, events, validator, and tests under the new page             |

## Proposed Solution

A new top-level page under LLM Tools - `Pages/LLMTools/OdditariumPage.razor` - reachable from the existing LLM Tools sidebar nav. The page hosts:

- **Entrypoint card** (mood-as-entrypoint, plus an explicit "Anchor type" pick: Subject / Place / Era / Concept / Surprise me).
- **Round panel** - one question, N options, all driven by the LLM under the ruleset.
- **Axis trail** - lightweight visualization of "anchors defined so far" (replaces the linear "Step 4 of 5" wizard counter).
- **Draft preview** - hidden until the first milestone hits. Once visible, `Commit` lights up.
- **Send-to menu** - the committed draft goes wherever the user wants (Workshop, txt2img, clipboard).

Existing Phase 13 infrastructure is reused under the hood:

- `WorkshopWizardSession` entity is renamed to `OdditariumSession` (the rename is purely cosmetic; the JSON body stays compatible). FK to Workshop sessions becomes optional and gradually unused.
- `WorkshopWizardService` is renamed `OdditariumService`. The intro-catalog dependency is dropped; the new flow is mood + anchor + LLM rounds.
- Events (`WizardTurnAdvancedEventArgs`, etc.) are renamed; pub/sub plumbing reused as-is.
- `WizardResponseValidator` stays - the JSON shape doesn't change.

The Workshop mount is removed at the end of the plan; Workshop instead gets a small "Open in Odditarium" affordance on its composer for users who want to switch surfaces.

## Key Conventions

These bind every phase. Conflicts with these are blockers, not friction.

- **Persona on duty.** All design decisions are made or reviewed by The Curator. Visual/component decisions delegate to the UI Design Specialist.
- **Document trail.** Every accepted decision lands here in the Locked Decisions table or in `RULESET.md`. Versioned system prompts are new files (`SYSTEM_PROMPTS/v2.md`), never in-place rewrites.
- **Pub/sub for events.** Continue routing through `IEventService` (`AGENTS.md` rule).
- **Persistence.** EF Core 6 / SQLite, JSON-backed body via `ValueConverter` + `IsModified = true` on in-place mutations. Manual migrations carry both `[DbContext]` + `[Migration]` attributes and update `AppDbContextModelSnapshot.cs`.
- **UI.** Page uses one of the documented layout shells (likely `TopbarLayout` or `ContentOnlyLayout`). Spacing tokens from `wwwroot/site.css`. Buttons that move data between surfaces use `.send-to-btn`. No magic numbers.
- **Twin-trace gate.** Before any system-prompt version bump is approved, run a twin-trace check (two short playthroughs from the same first pick) and confirm meaningful divergence by turn 5.

## Stress Points (anticipated)

| Stress point                                | Mitigation                                                                                                                          |
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| LLM returns synonym-stacked options         | Negative examples in the system prompt; cheap heuristic post-validator (reject if 3+ options share a head noun) with one-shot retry |
| Player still feels "led down a narrow path" | Generality Gate enforced strictly; Contrast Spread rule checks every round                                                          |
| Wildcard becomes obvious                    | Wildcards never marked in UI; verify with twin-trace + occasional manual playthrough                                                |
| Vibe coherence drifts after many rounds     | Vibe stays in the system prompt's context for every call, not just turn 1                                                           |
| Migration of existing Phase 13 rows         | Body schema is forward-compatible; nullable fields added rather than renamed                                                        |
| Designer voice drift across sessions        | Persona agent file (`.github/agents/odditarium-curator.agent.md`) loaded on every relevant trigger                                  |

## The Ruleset (rules-as-axes; literal prompts iterated later)

These are the rules the system prompts must encode. Each must be expressible as a constraint the LLM can be made to obey; rules that can't be constraints are vibes and live in `PERSONA.md`.

| Rule                    | Statement (one line)                                                                                                  |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **Generality Gate**     | Each round must be one level less specific than the previous, until N anchors are defined; then specificity opens up. |
| **Contrast Spread**     | Options in a round must differ from siblings on at least one major attribute (kingdom, scale, era, palette, energy).  |
| **Wildcard Quota**      | Exactly one option per round pulls in an unexpected dimension. Plausible, never marked.                               |
| **Vibe Coherence**      | Every option must be reachable from the chosen vibe's semantic field.                                                 |
| **Anti-noun-stacking**  | Options are concepts, not multi-clause phrases. Hard ceiling on label complexity.                                     |
| **No Synonym Stacking** | Reject any round where 3+ options share a head noun or are paraphrases of each other.                                 |
| **No Loaded Options**   | Options must be axes-of-choice, not "the right answer + four distractors."                                            |

The literal prompt text encoding these rules is owned by `SYSTEM_PROMPTS/v{n}.md` and reviewed in 2-3 rounds during Phase 3.

## Implementation Phases (Fibonacci complexity)

| #   | Title                                                                                   | Complexity | Status          |
| --- | --------------------------------------------------------------------------------------- | ---------- | --------------- |
| 1   | Page shell, nav entry, route, empty-state                                               | 3          | [ ] Not started |
| 2   | Ruleset doc + entity/service rename + cleanup of intro catalog                          | 5          | [ ] Not started |
| 3   | System prompt v1 (entrypoint round + axis-discovery rounds) with twin-trace review      | 8          | [ ] Not started |
| 4   | Round panel UI (entrypoint card, axis trail, options, send-to menu)                     | 8          | [ ] Not started |
| 5   | Validator hardening (synonym-stacking heuristic, Wildcard plausibility check)           | 5          | [ ] Not started |
| 6   | System prompt v2 - tuned from real playthroughs, twin-trace regression baseline         | 5          | [ ] Not started |
| 7   | Workshop integration: remove old mount, add "Open in Odditarium" affordance on composer | 3          | [ ] Not started |
| 8   | Manual playthrough pass + build/test green + Phase 13 archived as superseded            | 2          | [ ] Not started |

Total estimated complexity: **39 points** (genuinely large; the design-iteration phases 3 and 6 absorb most of it).

## Success Criteria

- A first-time user can open Odditarium, pick a vibe, click through 4-6 rounds, and arrive at a draft that they did not expect, was coherent with the vibe, and that a second playthrough from the same vibe would not have produced.
- No round on a fresh playthrough contains 3+ synonym-stacked options (validator-enforced).
- The wildcard is undetectable in usability testing (player cannot reliably pick it out post-hoc more than chance).
- Phase 13 surface is removed from Workshop with no regression to Workshop's first-send flow.
- All Odditarium tests + existing tests pass; build is clean.
- `MAIN_PLAN.md`, `RULESET.md`, and the active `SYSTEM_PROMPTS/v{n}.md` are in sync with what shipped.

## Out of Scope (explicit)

- No image generation, branching, or node creation from Odditarium - it strictly produces a draft string. Send-to is how it integrates.
- No multiplayer / shared playthroughs.
- No "expert mode" with raw prompt editing inside Odditarium - if the player wants to edit, they commit and edit in Workshop.
- No analytics dashboards in v1 (the Drift counter and twin-trace tooling are designer instruments only).
- Persona-driven audio / animation flourishes - revisit after the first playable build proves the core loop.

## Open Clarifications

These are real ambiguities that will need to be resolved before or during the relevant phase. Parking them here so they don't slip.

1. **Vibe preset count for v1** - lean ~9, hand-tuned, never expanded by the LLM. Confirm during Phase 3.
2. **Anchor type list** - closed (Subject / Place / Era / Concept / Surprise) for v1 vs. LLM-extensible. Lean closed; revisit if rounds feel claustrophobic.
3. **Send-to surfaces** - confirm the v1 list (Workshop composer, txt2img prompt, clipboard). img2img? llm-tools chat?
4. **Page name in the nav** - "Odditarium" is the working title. The user explicitly invited an alternative. Decision deferred to Phase 1 review when the empty-state mock makes the name visible.
5. **Persistence semantics on session pivot** - if the user is mid-playthrough and navigates away, do we persist (current Phase 13 behavior) or reset? Current lean: persist; player resumes where they left off; "New playthrough" button always available.

---

## Approval

User must explicitly approve this plan before any `PHASE_{n}.md` is generated or any code is written under the new page. The Curator does not assume approval from silence.
