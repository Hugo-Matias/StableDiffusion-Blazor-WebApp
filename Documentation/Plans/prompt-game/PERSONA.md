# Odditarium - Designer's Notebook

> **Voice:** The Curator (see `.github/agents/odditarium-curator.agent.md`).
> **Status:** Living. Sketchpad for in-flight design ideas, dead ends, and provocations.
> Anything that hardens lands in `MAIN_PLAN.md` or `RULESET.md`.

---

## Working Title

**Odditarium** - "a cabinet of curiosities you wander through, not a wizard you operate." The name has to earn itself; revisit after the first playable build. Backup names parked here:

- Mood Engine - clean, sells the entrypoint pivot.
- Drift - what the game does to you.
- Strangeloop - what the game is.

If any of these out-test "Odditarium" with a real player, we swap.

---

## Core Pitch

The user does not write prompts. The user makes a series of small, irreversible-feeling choices among options that span axes the LLM picks. Each playthrough is a guided drift through latent space. Coherence is non-negotiable; randomness is the seasoning, never the sauce.

Three things must be true for this to work:

1. The first three turns must hit. They establish the gravity well of the playthrough.
2. The LLM must obey rules, not vibes. Vibes are PERSONA, rules are PROMPT.
3. The player must never feel like they are "completing a wizard." Wizards have endings. Odditarium has commits.

---

## The Anti-Patterns (things we are pivoting _away from_)

| Workshop Wizard sin                                                                                    | Why it failed                                                                          | Odditarium's answer                                                            |
| ------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Hardcoded section titles ("Subject", "Scenery", "Lighting", "Mood", "Style")                           | Lockstep order kills replayability and front-loads commitment                          | Mood-as-entrypoint, then anchor-type pick, then LLM-driven axis discovery      |
| Options like "Ethereal crystal forest - Trees made of glowing, sharp crystalline structures" on turn 1 | Anti-noun-stacking violation; player is locked into a cabinet of pre-built phrases     | Options are _concepts_ until enough anchors exist to compose                   |
| Turn cap at 50                                                                                         | Implies the game is a marathon with a finish line                                      | Milestone-based commit affordance; the player decides                          |
| Five fixed verbs in a permanent row (Improve / Change / Add / Remove / Surprise)                       | Verbs are noise once the LLM is leading; player optimizes the verbs instead of playing | Verbs become contextual prompts that the LLM offers when a turn calls for them |
| Buried inside Workshop                                                                                 | Workshop is a power-user node graph; the wizard's audience is opposite                 | Dedicated page with its own nav entry                                          |

---

## Open Provocations (the Curator's wishlist)

These are pitches the user has not signed off on. They live here so they don't get lost; promote to MAIN_PLAN only after a yes.

1. **Nameless wildcards.** Each round secretly contains one wildcard option. Never marked. We instrument the analytics: do players pick wildcards more or less than chance? Either signal is interesting.
2. **The Drift counter.** A small unobtrusive counter that tracks "how far you've drifted from your starting vibe." Not a score. A barometer. Lights up green when a playthrough is well-curated, amber when it's losing coherence.
3. **The Echo affordance.** A button that shows the player a hypothetical playthrough that _would have happened_ if they'd picked a different early option. Replayability made visible.
4. **Twin-trace test.** Two playthroughs from the same first pick must diverge meaningfully by turn 5. We can run this as an automated regression check on the system prompt - generate 2 trajectories with seed 1 and seed 2, check the embedding cosine of the final drafts, fail if too close.
5. **Vibe gravity, literal.** The mood pick literally changes the _temperature_ the LLM uses for subsequent rounds. "Surreal" runs hotter; "minimal" runs colder. The player never sees this number.
6. **Anchor count instead of turn count.** Show the player "3 anchors defined" not "turn 7 of 50." Anchors are the unit of progress.

---

## Dead Ends (don't revisit unless we learn something new)

- **Chip-style selectors with multi-pick.** Tested the mental model; multi-pick destroys the pacing. One pick per turn is sacred.
- **Showing the running prompt as it grows.** Spoils the surprise; the player starts editing instead of playing. Draft preview is acceptable _only_ once a milestone is hit.
- **"Hard mode" / "easy mode" toggles.** Two modes is one mode too many. Difficulty is implicit in vibe choice.

---

## Open Clarifications

- How many vibe presets do we ship with v1? (Lean: ~9, hand-tuned, never expanded by the LLM.)
- Anchor types - is the list closed (Subject / Place / Era / Concept / Surprise) or LLM-extensible? (Lean: closed for v1, open as a stretch goal.)
- Is "Surprise me" an entrypoint, a verb, or both? (Lean: entrypoint only. As a verb it competes with the Wildcard Quota.)
- Where does the committed draft go? Direct to clipboard + a "Send to..." menu (Workshop / txt2img / img2img)? Or does Odditarium own a tiny local history of past drafts the user can revisit?

---

## Notes from prior sessions

- 2026-04-30: Phase 13 (Workshop Wizard) shipped a working but flawed surface; user feedback identified anti-patterns above. Pivot agreed. Phase 13 artifacts (entity, service, events, intro catalog, validator, tests) are reusable; intro flow and mount point are not.
