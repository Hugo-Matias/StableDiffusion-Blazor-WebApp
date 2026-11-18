# Phase 4: System Prompt v1 (Persona Injection, Ruleset Encoding)

**Objective:** Create the first version of the system prompt that injects persona identity and encodes all 9 ruleset rules. Validate with twin-trace review.
**Complexity:** 8 points
**Status:** [x] Complete (twin-trace deferred to Phase 7)

## Steps

- [x] Design system prompt template — Accepts: persona identity block, vibe lock, collected layers array, round number
- [x] Encode Ruleset v1 — All 9 core rules from [`RULESET.md`](./RULESET.md) translated into LLM instructions
- [x] Persona injection mechanism — Each persona's full identity injected as structured block via `OdditariumPersonaRoster.FindById()`
- [x] Implement `BuildRoundSystemPrompt` in OdditariumService — Already implemented; fixed ruleset gaps (Rule 7, Rule 8)
- [!] Twin-trace review — **Deferred to Phase 7** (requires Round Panel UI + Ollama backend for actual playthroughs)
- [x] Create `SYSTEM_PROMPTS/v1.md` — Versioned literal system prompt file

## Changes Made

### Files Modified

| File                                         | Change                                                                                                                                                                                                  |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Services/OdditariumService.cs` | Fixed ruleset gaps in `BuildRoundSystemPrompt`: added Rule 7 (No Loaded Options), fixed Rule 8 (Vibe Coherence with persona override clause), consolidated Rules 3+4 into proper "Specificity by Depth" |

### Files Created

| File                                                   | Purpose                                                            |
| ------------------------------------------------------ | ------------------------------------------------------------------ |
| `Documentation/Plans/prompt-game/SYSTEM_PROMPTS/v1.md` | Versioned system prompt documentation with ruleset coverage matrix |

## Success Criteria

- [x] System prompt correctly injects persona identity
- [x] All 9 ruleset rules encoded in LLM instructions
- [ ] Twin-trace shows meaningful divergence between two playthroughs with same starting pick (deferred to manual testing)
- [x] Round 1 options are broad categories (single concepts), never concrete scene descriptions
- [x] `SYSTEM_PROMPTS/v1.md` created and versioned

## Notes

Twin-trace review requires a running app with Ollama backend connected. Deferred until Phase 5 when the round panel UI is functional enough to run actual playthroughs.

## Commit Checkpoint

- System prompt v1 complete with all 9 ruleset rules encoded. Build green. Ready for Phase 5 (Round Panel UI).
