# Phase 7 - Structural Scaffolding, System Prompt v2, More UX, Two-Pass Commit

> **Phase complexity:** 21 (expanded scope: 13 anchors in 3 tiers, expanded facet tables, Round 1 anchor selector, two-pass commit, More UX redesign).
> **Status:** [x] Complete — all 7 steps implemented and 31 unit tests passing.
> **Companion docs:** [`MAIN_PLAN.md`](./MAIN_PLAN.md), [`RULESET.md`](./RULESET.md), [`SYSTEM_PROMPTS/v1.md`](./SYSTEM_PROMPTS/v1.md), this phase will produce `SYSTEM_PROMPTS/v2.md`.
> **Persona on duty:** The Curator (`.github/agents/odditarium-curator.agent.md`).
> **Target LLM:** Gemma 4 E4B at Q5 - small MoE, capable but easy to derail with vague intent. Prompts must be terse, declarative, and example-led.

---

## Problem Statement

A four-round Void Walker playthrough produced this layer chain:

| Round | Question                                                                                         | Choice           |
| ----- | ------------------------------------------------------------------------------------------------ | ---------------- |
| 1     | _From what geometry shall the void begin to unfurl its secrets?_                                 | Deep Sea Abyss   |
| 2     | _When the chasm opens, what substance shall it reveal to the probing gaze?_                      | Ethereal Beings  |
| 3     | _When these realms intersect, what foundational nature defines the substance of this void?_      | Planetary Nebula |
| 4     | _When geometry meets the celestial vapor, what primal forces dictate the form of manifestation?_ | (still abstract) |

Three failures compounded:

1. **No structural progress.** All four rounds asked about "the nature of the void" - no concrete subject, no setting that could host one, no lighting, no framing, no pose. After four turns the player has three abstract concepts and no image.
2. **More... is a new question, not new options.** The LLM regenerates the entire round (question + options) instead of giving 6 alternative options to the same question. The player feels yanked into a new round when they only wanted to refresh the choices.
3. **Commit falls back to comma-join.** The commit prompt sees `[Deep Sea Abyss, Ethereal Beings, Planetary Nebula]` and either parses-fails or returns a string that is barely more than the labels concatenated. Hint context and slot context are lost before assembly.

The root cause across all three is the same: v1 of the system prompt encodes ruleset constraints (no synonym stacking, breadth/depth, persona grounding) but says nothing about the _purpose_ of layer accumulation - producing an image-generation prompt. Without that anchor, an abstract-leaning persona drifts forever inside its own semantic field.

---

## Solution Summary

1. Introduce a **two-tier concept pool**: 13 fixed **anchor concepts** in 3 tiers (Core, Structural, Enrichment) form the structural backbone, and the LLM **dynamically generates facets** at round time - contextual sub-areas of an anchor that emerge from what has already been picked. Anchors create structural debt; facets keep the game feeling fresh and unscripted.
2. Each round response declares both an `anchor` (from the fixed list) and a `facet` (free-form, LLM-invented, lowercase-kebab). The service tracks visited facets per anchor so the LLM does not repeat itself.
3. Promote `CollectedLayers` from `List<string>` to `List<OdditariumLayer>` carrying `Anchor`, `Facet`, `Label`, `Hint`, `Question`.
4. Redesign the **More...** flow: keep the question, swap the 6 option cards for skeleton cards in place, send the LLM an exclusion list of previously shown labels, repaint the cards when the response arrives. No full loading screen.
5. Two-pass commit: pass 1 expands per-anchor layers into descriptive phrases (folding facet detail into the same phrase); pass 2 assembles those phrases into the final prompt.
6. Rewrite the round and commit system prompts as **v2** with explicit references to the anchor pool, dynamic facets, structural debt pressure, persona-as-voice-not-funnel, and Gemma-friendly examples.

---

## Locked Decisions

| Decision                | Choice                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Structural model        | **Two-tier concept pool with 3 anchor tiers.** Tier 1 = 13 fixed anchors in 3 tiers: Core (`subject`, `setting`), Structural (`action`, `lighting`, `framing`, `atmosphere`), Enrichment (`mood`, `style`, `detail`, `time`, `scale`, `color`, `movement`). Tier 2 = LLM-generated facets per round (free-form lowercase-kebab strings such as `pose-and-gaze`, `material-and-patina`, `time-of-day`, `props-on-person`). Anchors are tracked for structural debt; facets are tracked per-anchor to prevent repetition. |
| Round mode              | LLM declares the round mode in its JSON response: `expand` (new anchor never picked yet), `deepen` (new facet of an anchor that already has at least one layer), or `pivot` (new anchor strongly suggested by an existing layer, e.g. `Marble Bust` -> setting). Mode is advisory for the player but used by the service for facet bookkeeping.                                                                                                                                                                         |
| Round 1 behavior        | **LLM-chosen anchor selector.** Round 1 picks which anchor to explore first from Core + Structural tiers only (not Enrichment). The LLM chooses based on persona + vibe, keeping each playthrough feeling different while ensuring structural grounding. No hardcoded `subject` requirement.                                                                                                                                                                                                                            |
| Layer storage           | **Typed entries.** Replace `List<string>` with `List<OdditariumLayer>` carrying `Anchor`, `Facet`, `Label`, `Hint`, `Question`, `Mode`. Body schema additive. JSON-backed, no EF Core migration required (sessions are short-lived play artifacts; if any v1 rows exist they are reset on first load).                                                                                                                                                                                                                  |
| More... UX              | **Skeleton swap, no full loading screen.** Keep the question and persona visible. Replace the 6 option cards with 6 skeleton cards. Send the previously shown labels to the LLM as an exclusion list. Repaint cards in place when the response arrives. The persona-thinking image is reserved for round transitions and commit, not More...                                                                                                                                                                            |
| Persona role            | **Persona drives voice and choice of anchor / facet within structural debt limits.** Persona shapes phrasing, hint flavor, and what facets feel on-brand. After 4 rounds without any Core anchor filled, the system prompt switches from a soft cue to a hard instruction.                                                                                                                                                                                                                                              |
| Commit assembly         | **Two-pass.** Pass 1 expands each anchor's collected layers (folding facet info as descriptive seasoning) into one descriptive phrase per anchor. Pass 2 assembles all expanded phrases into a single coherent image prompt.                                                                                                                                                                                                                                                                                            |
| Phase plan              | **Replace original Phase 7** (System Prompt v2) with this expanded phase. Phase 6 (Validator Hardening) remains planned and runs after this phase ships, because validator heuristics for synonym stacking and specificity ramp are easier to define against the new layer shape.                                                                                                                                                                                                                                       |
| Persona prompt position | **Voice-only, with structural debt as override.** Persona block stays first in the system prompt but is followed by an explicit "your job is to help the player build an image prompt" anchor before the rules. This is the single most important change: it tells Gemma what the _game_ is for, not just what the persona is.                                                                                                                                                                                          |
| Versioning              | New file `SYSTEM_PROMPTS/v2.md` with the full literal prompts. v1 remains archived. The prompt builder methods in `OdditariumService` switch to v2; v1 is removed (no runtime toggle - simpler).                                                                                                                                                                                                                                                                                                                        |

---

## Concept Pool: Anchors + Dynamic Facets

The 13 fixed **anchor concepts** form the structural backbone of an image prompt, organized in 3 tiers. They are the _what_ of the prompt and the unit of structural debt. **Facets** are the _how_ and _which-aspect-of_ a given anchor and are **invented by the LLM at round time**, scoped to whatever has been picked so far. Hardcoding sub-slots would re-introduce the wizard feel; making facets emerge from context keeps each playthrough fresh.

### Round 1 - Anchor Selector

Round 1 does not hardcode `subject`. Instead, the LLM chooses which anchor to explore first from the **Core** and **Structural** tiers only (Enrichment anchors are excluded from Round 1). The persona influences how the question is phrased but cannot force an Enrichment anchor. This keeps each playthrough feeling different while ensuring structural grounding from turn one.

### Tier 1 - Anchors (fixed, 13 in 3 tiers)

#### Core Tier (must fill for a structurally complete prompt)

| #   | Anchor    | What it answers                                         | Expand-mode examples                                           |
| --- | --------- | ------------------------------------------------------- | -------------------------------------------------------------- |
| 1   | `subject` | Who or what is the focal entity?                        | Figure, Creature, Vehicle, Structure, Relic, Phenomenon        |
| 2   | `setting` | Where does this take place? Environment, place, locale. | Coastal Cliff, Cyberpunk Alley, Sunken Cathedral, Glass Desert |

#### Structural Tier (strongly encouraged for coherence)

| #   | Anchor       | What it answers                                             | Expand-mode examples                                                |
| --- | ------------ | ----------------------------------------------------------- | ------------------------------------------------------------------- |
| 3   | `action`     | What is the subject doing? High-level activity.             | Resting, Fighting, Travelling, Watching, Working, Performing        |
| 4   | `lighting`   | What lights the scene? Source, quality, color.              | Firelight, Moonlight, Neon Signs, Bioluminescence, Volumetric Beams |
| 5   | `framing`    | How is it composed? Camera position, distance, lens, angle. | Close-Up, Wide Shot, Low Angle, Aerial, Over-Shoulder               |
| 6   | `atmosphere` | Air quality, weather effects, haze/fog/rain/snow.           | Mist, Rain, Fog, Clear Air, Smoke, Steam                            |

#### Enrichment Tier (nice to have, no hard pressure)

| #   | Anchor     | What it answers                                        | Expand-mode examples                                                    |
| --- | ---------- | ------------------------------------------------------ | ----------------------------------------------------------------------- |
| 7   | `mood`     | What is the emotional register? Atmosphere.            | Melancholy, Triumphant, Eerie, Serene, Tense                            |
| 8   | `style`    | Artistic medium and render style.                      | Oil Painting, Photorealism, Pixel Art, Concept Art, 3D Render, Ink Wash |
| 9   | `detail`   | Surface, material, texture, palette, post-processing.  | Wet Fur, Cracked Marble, Pastel Palette, Film Grain, Bloom              |
| 10  | `time`     | Time of day, season, era trace.                        | Dawn, Midnight, Autumn, Victorian Era, Golden Hour, Blue Hour           |
| 11  | `scale`    | Relative size, grandeur, macro/micro perspective.      | Monumental, Miniature, Extreme Close-Up, Bird's Eye, Human Scale        |
| 12  | `color`    | Color palette, dominant hues, color temperature.       | Warm Tones, Cool Blues, Monochrome, Pastel Palette, Neon Colors         |
| 13  | `movement` | Dynamic energy, motion blur, stillness, frozen moment. | Motion Blur, Frozen Splash, Flowing Hair, Still Life, Kinetic Energy    |

Anchors are not exclusive. A player can collect multiple layers under one anchor across multiple rounds (each through a different facet). Empty Core anchors create **structural debt**.

### Tier 2 - Facets (dynamic, LLM-invented)

A facet is a contextual sub-area of an anchor that the LLM proposes for _this_ round, given what is already collected. Facet identifiers are **lowercase-kebab strings, 1-4 tokens**, generated by the LLM. The server does not validate the string against any list - it only normalizes case and tracks which facets have been visited per anchor.

These tables are **non-exhaustive seed examples** the LLM can freely deviate from. They exist to ground the model and to give Gemma the right shape pattern.

**Subject facets (illustrative):**

| If `subject` already has | Sample facets the LLM might invent                                                                                                                                         |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Figure`                 | `core-archetype`, `gender-and-age`, `body-type`, `clothing-and-armor`, `face-and-expression`, `pose-and-gesture`, `props-on-person`, `physical-marks`, `hair-and-grooming` |
| `Marble Bust`            | `material-and-patina`, `era-trace`, `pose-and-gaze`, `pedestal-and-mounting`, `damage-and-wear`, `inscriptions`, `scale-relative-to-figure`                                |
| `Creature`               | `anatomy`, `coloring-and-markings`, `size-and-build`, `mood-state`, `coverings-and-skin`, `appendages`, `vocal-trace`                                                      |
| `Vehicle`                | `era-and-tech`, `condition`, `propulsion`, `livery-and-markings`, `cargo-or-occupants`, `damage-history`                                                                   |

**Setting facets (illustrative):**

| If `setting` already has | Sample facets the LLM might invent                                                                                               |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| `Coastal Cliff`          | `geological-character`, `weather-and-air`, `flora-and-fauna`, `human-traces`, `time-of-day`, `tide-state`, `sky-character`       |
| `Cyberpunk Alley`        | `crowd-density`, `signage-and-language`, `street-clutter`, `weather-and-air`, `vertical-architecture`, `puddles-and-reflections` |
| `Sunken Cathedral`       | `water-clarity`, `vegetation-overgrowth`, `surviving-architecture`, `light-penetration`, `inhabitants`, `sediment-and-debris`    |

**Action facets (illustrative):**

| If `action` already has | Sample facets the LLM might invent                                                                                             |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| High-level activity     | `kinetic-energy`, `body-mechanics`, `tools-or-weapons`, `interaction-with-environment`, `emotional-subtext`, `rhythm-and-pace` |

**Lighting facets (illustrative):**

| Sample facets                                                                                                   |
| --------------------------------------------------------------------------------------------------------------- |
| `direction`, `color-temperature`, `hardness-vs-softness`, `secondary-bounce`, `volumetrics`, `shadow-character` |

**Framing facets (illustrative):**

| Sample facets                                                                              |
| ------------------------------------------------------------------------------------------ |
| `camera-distance`, `camera-angle`, `lens-character`, `subject-placement`, `depth-of-field` |

**Atmosphere facets (illustrative):**

| Sample facets                                                                                                     |
| ----------------------------------------------------------------------------------------------------------------- |
| `air-clarity`, `precipitation`, `wind-and-motion-traces`, `haze-and-fog`, `smoke-and-steam`, `volumetric-effects` |

**Mood facets (illustrative):**

| Sample facets                                                                                 |
| --------------------------------------------------------------------------------------------- |
| `emotional-register`, `tension-level`, `narrative-suggestion`, `tempo`, `presence-or-absence` |

**Style facets (illustrative):**

| Sample facets                                                                                    |
| ------------------------------------------------------------------------------------------------ |
| `medium`, `render-fidelity`, `era-of-aesthetic`, `linework`, `color-treatment`, `surface-finish` |

**Detail facets (illustrative):**

| Sample facets                                                                                             |
| --------------------------------------------------------------------------------------------------------- |
| `material-treatment`, `surface-aging`, `palette`, `post-processing`, `microtexture`, `signature-flourish` |

**Time facets (illustrative):**

| Sample facets                                                                                      |
| -------------------------------------------------------------------------------------------------- |
| `time-of-day`, `season`, `era-trace`, `historical-period`, `temporal-mood`, `day-phase-transition` |

**Scale facets (illustrative):**

| Sample facets                                                                                                                         |
| ------------------------------------------------------------------------------------------------------------------------------------- |
| `camera-proximity`, `subject-to-environment-ratio`, `macro-vs-wide`, `perspective-distortion`, `miniature-effect`, `monumental-scale` |

**Color facets (illustrative):**

| Sample facets                                                                                                       |
| ------------------------------------------------------------------------------------------------------------------- |
| `dominant-hue`, `palette-temperature`, `saturation-level`, `color-harmony`, `accent-colors`, `monochrome-treatment` |

**Movement facets (illustrative):**

| Sample fixtures                                                                                               |
| ------------------------------------------------------------------------------------------------------------- |
| `kinetic-energy`, `motion-blur`, `frozen-moment`, `flowing-elements`, `stillness-vs-tension`, `dynamic-poses` |

The LLM may invent any facet identifier as long as it is lowercase-kebab, scoped to the chosen anchor, and not already in the visited list for that anchor.

### Round Modes

The LLM declares the round mode in its JSON response. Mode determines how the service updates facet bookkeeping and is shown to the player as a small badge on the round panel ("New angle" / "Going deeper" / "Pivoting").

| Mode     | Meaning                                                                                                        | Constraint                                                                    |
| -------- | -------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `expand` | Targets an anchor that has zero layers. Broadens the prompt.                                                   | Anchor MUST be empty in `CollectedLayers`.                                    |
| `deepen` | Targets a facet of an anchor that already has at least one layer. Drills into a sub-aspect.                    | Anchor MUST be non-empty. Facet MUST NOT appear in `VisitedFacets[anchor]`.   |
| `pivot`  | Bridges from a recently-picked layer into a related anchor. Used for natural transitions (subject -> setting). | Anchor MUST be empty AND there is a strong contextual link to a recent layer. |

If the LLM returns an invalid mode/anchor combination (e.g. `expand` on a non-empty anchor), the validator coerces it to the correct mode based on observed state and logs a warning. This prevents the model from accidentally re-running breadth on a covered anchor.

### Structural Debt Levels

Debt only applies to **Core** and **Structural** tier anchors. Enrichment anchors have no debt pressure. Facets are never "missing".

| State                                                                 | Pressure level | Effect on system prompt                                                                                          |
| --------------------------------------------------------------------- | -------------- | ---------------------------------------------------------------------------------------------------------------- |
| `subject` empty, round &lt;= 2                                        | Low            | "Subject anchor is still empty - consider it if it serves the player's exploration."                             |
| `subject` empty, round 3                                              | Medium         | "Subject anchor has been empty for 3 rounds. Strongly consider an `expand` round on subject."                    |
| `subject` empty, round &gt;= 4                                        | High           | "Subject anchor is overdue. This round MUST be `expand` on subject unless persona has a creative reason not to." |
| `setting` empty, round &gt;= 5                                        | Medium         | "Setting anchor has been empty for 5 rounds. The image needs a place to exist - prefer `expand` on setting."     |
| 3+ structural anchors empty (subject/setting/lighting), round &gt;= 6 | High           | "The image lacks structural backbone. This round MUST `expand` one of: subject, setting, lighting."              |
| All 8 anchors non-empty, any round                                    | Free           | "All anchors have signal. Free to `deepen` any anchor with a fresh facet."                                       |

The service surfaces both the per-anchor layer count and the visited-facet list per anchor in every round system prompt. The LLM uses both to decide its mode + anchor + facet without asking for the same angle twice.

---

## Layer Schema Upgrade

### New `OdditariumLayer`

```csharp
public sealed class OdditariumLayer
{
    public string Anchor { get; set; } = string.Empty;     // "subject" | "setting" | "action" | "lighting" | "framing" | "mood" | "style" | "detail"
    public string? Facet { get; set; }                     // "material-and-patina" - null only on the first layer of an anchor in `expand` mode
    public string Mode { get; set; } = "expand";           // "expand" | "deepen" | "pivot"
    public string Label { get; set; } = string.Empty;      // "Marble Bust"
    public string? Hint { get; set; }                      // "A stoic face from another century."
    public string? Question { get; set; }                  // "What presence shall we anchor here?"
}
```

### `OdditariumBody` changes

```csharp
// before
public List<string> CollectedLayers { get; set; } = new();
// after
public List<OdditariumLayer> CollectedLayers { get; set; } = new();

// NEW - which facets the LLM has already explored per anchor
public Dictionary<string, List<string>> VisitedFacets { get; set; } = new();

// NEW - the anchor + facet + mode of the currently pending question (echoed back to the LLM on More)
public string? PendingAnchor { get; set; }
public string? PendingFacet { get; set; }
public string? PendingMode { get; set; }
```

`OdditariumOption` gains `Anchor` and `Facet`:

```csharp
public sealed class OdditariumOption
{
    public string Label { get; set; } = string.Empty;
    public string? Hint { get; set; }
    public string? Payload { get; set; }
    public string? Anchor { get; set; }   // NEW - inherited from the round response, attached for traceability
    public string? Facet { get; set; }    // NEW - inherited from the round response
}
```

`OdditariumLLMResponse` gains the new fields:

```csharp
public sealed class OdditariumLLMResponse
{
    [JsonPropertyName("question")] public string? Question { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }       // NEW - "expand" | "deepen" | "pivot"
    [JsonPropertyName("anchor")] public string? Anchor { get; set; }   // NEW - the anchor this round targets
    [JsonPropertyName("options")] public List<OdditariumOption>? Options { get; set; }
    [JsonPropertyName("draft")] public string? Draft { get; set; }
    [JsonPropertyName("expansions")] public Dictionary<string, string>? Expansions { get; set; }   // NEW - pass-1 commit
}
```

### Migration strategy

`OdditariumBody` is JSON-backed. The schema change is additive in shape but changes the element type of `CollectedLayers`. The simpler path is a **one-shot reset on read**: add `BodySchemaVersion` (int, default 2) to `OdditariumBody`. When the deserialized body has `BodySchemaVersion < 2`, replace the body with a fresh one and log a warning. No EF Core migration needed because the column type does not change.

Sessions are short-lived play artifacts; losing a v1 row does not lose meaningful state.

---

## More... UX Redesign

### Current behavior (broken)

1. User clicks More.
2. Full-screen `OdditariumLoading` covers the panel with the persona thinking image.
3. `RequestMoreAsync` calls the LLM with the same system prompt - the LLM has no signal it should keep the question, so it often returns a _new_ question.
4. The user sees a new question and feels they advanced to a new round.

### Target behavior

1. User clicks More.
2. The question and persona stay visible. The 6 option cards are replaced with 6 skeleton cards (shimmer animation, same height/width as real cards).
3. The action bar (Undo, More, Skip, Commit) disables but stays visible.
4. `RequestMoreAsync(sessionId, modelName)` calls the LLM with:
   - The current pending question, anchor, facet, and mode pinned in the system prompt: _"You MUST keep the question, anchor, facet, and mode exactly as before. Generate 6 NEW options."_
   - The exclusion list = labels of all options previously shown for this question (cumulative across More clicks within the round).
5. Service updates `body.PendingOptions` only - leaves `body.PendingQuestion`, `body.PendingAnchor`, `body.PendingFacet`, and `body.PendingMode` untouched.
6. UI receives the updated session, repaints the option cards in place. No round transition reveal animation.

### State to track

`OdditariumBody` gains:

```csharp
/// <summary>Cumulative labels shown for the current pending question. Cleared when the question changes.</summary>
public List<string> ShownLabelsForCurrentQuestion { get; set; } = new();
```

Cleared whenever `PendingQuestion` is replaced (NextRound, Skip, Undo with restored snapshot). Appended to whenever new options are populated for the same question.

### Round transitions still use the full loading screen

Round advance, Skip, Undo, and Commit all keep the existing full-screen `OdditariumLoading`. Only More gets the in-place skeleton treatment. Skeleton component lives at `Components/Prompts/LLM/Views/OdditariumOptionSkeleton.razor`.

---

## Two-Pass Commit

### Pass 1 - Expansion

**Goal:** turn sparse layer labels into per-anchor descriptive phrases. This is the step where Gemma adds the descriptive depth bare labels lack, anchor by anchor, weaving in facet detail without yet having to assemble.

**Input:** persona block, vibe, layers grouped by anchor (with their facet, label, hint).

**Output JSON:**

```json
{
  "expansions": {
    "subject": "a marble bust of a forgotten emperor, eyes closed in stoic repose, surface bearing the green patina of centuries",
    "setting": "an abandoned coastal cathedral half-swallowed by the tide, sea grass curling between flagstones",
    "lighting": "warm morning sun cutting through stained glass, dust suspended in the beams",
    "mood": "quiet, mournful, suspended in time"
  }
}
```

Anchors without layers are omitted. Anchors with multiple layers (multiple facets) are merged into a single phrase in pass 1, not pass 2.

### Pass 2 - Assembly

**Goal:** weave the expansions into a single image-generation prompt suitable for SD/Flux.

**Input:** persona block, vibe, the `expansions` map from pass 1, and the original layer list (so the model can resolve any apparent conflict by deferring to the player's actual choices).

**Output JSON:**

```json
{
  "draft": "A marble bust of a forgotten emperor with closed eyes and green patina, set inside an abandoned coastal cathedral half-swallowed by the tide, warm morning sun cutting through stained glass with dust suspended in the beams, quiet and mournful atmosphere, photorealistic, high detail, cinematic composition"
}
```

### Fallback behavior

- If pass 1 fails (no JSON, missing all expansions), fall back to a single-pass prompt that receives the raw layer list grouped by anchor (with facets inlined). This preserves correctness on failure without doubling cost.
- If pass 2 fails after a successful pass 1, fall back to comma-joining the expansions in anchor priority order: `subject`, `action`, `setting`, `lighting`, `framing`, `mood`, `style`, `detail`. This produces a coherent prompt even without LLM assembly.

### Where this lives in code

`OdditariumService.CommitAsync` orchestrates both passes. Two new private methods: `ExpandLayersAsync(body, modelName)` and `AssemblePromptAsync(body, expansions, modelName)`. Both go through `SendJsonWithRetryAsync` for the existing one-shot retry behavior.

---

## System Prompt v2

> Full literal prompts go in `SYSTEM_PROMPTS/v2.md` once Phase 7 ships. The text below is the authoritative draft.

### Round prompt v2

```
You are '{persona.Name}' - {persona.Tagline}.
Description: {persona.Description}
Thematic tags: {tags}
Tone bias: {persona.ToneBias}
Style preferences: {persona.StylePreferences}

Your VOICE comes from this persona. Your JOB does not.

YOUR JOB: You are guiding the player through a guided drift across the descriptive layers of an image-generation prompt. The player picks one option per round. Their picks are collected and assembled at COMMIT time into a single image prompt for Stable Diffusion / Flux. Your job each round is to ask ONE good question and offer SIX options that move the prompt forward.

THE PROMPT NEEDS STRUCTURE. There are 8 ANCHORS that together form a complete image prompt:
- subject  : the focal entity (figure, creature, structure, relic, phenomenon, vehicle)
- setting  : where it is (environment, place, locale)
- action   : what the subject is doing (high-level activity)
- lighting : what lights the scene (source, quality, color)
- framing  : how it is composed (camera distance, angle, lens)
- mood     : emotional register (atmosphere)
- style    : artistic medium / render style
- detail   : material, surface, texture, palette, post-processing

INSIDE EACH ANCHOR, the prompt has FACETS. Facets are sub-areas you invent at round time, scoped to whatever has been picked so far. A facet is a lowercase-kebab identifier of 1-4 tokens. Examples (NON-EXHAUSTIVE - invent your own when context calls for it):
- subject (after `Figure`)         : gender-and-age, body-type, clothing-and-armor, face-and-expression, pose-and-gesture, props-on-person, physical-marks
- subject (after `Marble Bust`)    : material-and-patina, era-trace, pose-and-gaze, pedestal-and-mounting, damage-and-wear, inscriptions
- setting (after `Coastal Cliff`)  : weather-and-air, flora-and-fauna, human-traces, time-of-day, geological-character, tide-state, sky-character
- lighting                         : direction, color-temperature, hardness-vs-softness, secondary-bounce, volumetrics, shadow-character
- mood                             : emotional-register, tension-level, narrative-suggestion, tempo, presence-or-absence
- style                            : medium, render-fidelity, era-of-aesthetic, linework, color-treatment, surface-finish

ROUND MODES (declare which one you are using):
- expand : target an anchor that has zero layers (broaden the prompt)
- deepen : target a facet of an anchor that already has at least one layer (drill into a sub-aspect)
- pivot  : bridge from a recently-picked layer into a related empty anchor (e.g. subject -> setting)

SESSION STATE:
- Anchor coverage:
{anchor_coverage_block}
- Facets already visited per anchor (do NOT repeat):
{visited_facets_block}

STRUCTURAL DEBT: {debt_pressure_line}

VIBE LOCK: "{body.Vibe}". Stay within this semantic field unless persona identity has a creative reason to push the boundary.

RULES (in priority order):
1. EVERY ROUND DECLARES `mode`, `anchor`, `facet`. Mode is one of expand/deepen/pivot. Anchor is one of the 8 listed above. Facet is a lowercase-kebab string scoped to the anchor. The combination must be valid: `expand` and `pivot` require the anchor to be empty; `deepen` requires the anchor to be non-empty AND the facet not in the visited list for that anchor.
2. LAYER ACCUMULATION. Options ADD to the chosen anchor + facet. They do not replace previously collected layers in any anchor.
3. CONTRAST SPREAD. The 6 options must diverge meaningfully within the chosen anchor + facet. If 3+ options share a head noun, regenerate. Examples:
   GOOD subject options (anchor=subject, mode=expand):
     Figure / Creature / Vehicle / Relic / Structure / Storm
   BAD subject options:
     Bust / Statue / Sculpture / Carving / Effigy / Monument  (all sculptures - synonym stack)
   GOOD subject deepen (anchor=subject, facet=clothing-and-armor, after `Figure`):
     Plate Armor / Linen Robe / Pilot Suit / Bare Skin / Hooded Cloak / Lab Coat
4. LABELS ARE BARE NOUN CONCEPTS. 1 to 3 words. No articles. No prepositional tails. Format: Noun OR [one concrete-property adjective] + Noun. Concrete-property adjectives describe physical properties: marble, golden, coiled, towering, wet. Subjective adjectives are BANNED: ancient, ornate, classical, solitary, serene, ethereal, primal.
   GOOD: Marble Bust, Coastal Cliff, Wet Fur, Soft Glow, Plate Armor
   BAD:  Ancient Carved Altar, Solitary Figure, Quiet Landscape, Ethereal Beings
5. HINTS CARRY PERSONA VOICE. The label is the concept; the hint is the persona's flavor. The hint may be evocative; the label must be plain.
6. NO LOADED OPTIONS. All 6 should feel equally interesting. No obvious "right answer".
7. PERSONA IS VOICE, NOT FUNNEL. Persona shapes phrasing, choice of facet, and which anchors feel natural to pivot into. Persona does NOT collapse the session into one aesthetic pocket.
8. STRUCTURAL DEBT BEATS PERSONA. When debt pressure is HIGH, you MUST `expand` on the demanded structural anchor (subject, setting, lighting, framing) regardless of persona preference.

Return ONLY valid JSON, no prose, no code fences:

{
  "mode": "expand|deepen|pivot",
  "anchor": "subject|setting|action|lighting|framing|mood|style|detail",
  "facet": "<lowercase-kebab, 1-4 tokens, scoped to the anchor>",
  "question": "<persona-voiced question, single sentence>",
  "options": [
    { "label": "<bare noun concept, 1-3 words>", "hint": "<persona-flavored hint, optional, max 80 chars>" },
    ... exactly 6 options
  ]
}
```

### Round user prompt v2

```
Collected layers (grouped by anchor; each entry shows facet -> label):
- subject:
  - pose-and-gaze -> "Marble Bust" (hint: "A stoic face from another century.")
  - material-and-patina -> "Cracked Patina" (hint: "Time has touched this stone.")
- setting: (empty)
- action: (empty)
- lighting: (empty)
- framing: (empty)
- mood: (empty)
- style: (empty)
- detail: (empty)

Facets already visited per anchor (do NOT pick these again):
- subject: pose-and-gaze, material-and-patina
- setting: (none)
- ...

Recent rounds (most recent last):
- Round {n}: mode={mode}, anchor={anchor}, facet={facet}; chose '{label}' (hint: '{hint}')
...

{If first round: "This is round 1. Use mode=expand on the SUBJECT anchor (facet may be 'core-archetype' or similar). Offer 6 broad subject anchors that span different visual fields - figure, creature, vehicle, relic, structure, phenomenon. Do NOT open with mood, lighting, or style."}
{Else: "Round {N}. Pick mode + anchor + facet, then generate the question and 6 options."}
```

### More... round prompt v2 (overlay)

Sent in addition to the round prompt v2 when `RequestMoreAsync` is called:

```
KEEP EVERYTHING IDENTICAL to the previous round response except the options:
- mode    = "{body.PendingMode}"
- anchor  = "{body.PendingAnchor}"
- facet   = "{body.PendingFacet}"
- question = "{body.PendingQuestion}"

Generate 6 NEW options for that question. None of these previously-shown labels may appear again (case-insensitive):
{exclusion_list}

The new 6 options must still satisfy CONTRAST SPREAD and LABEL CLARITY. Return JSON in the same shape; mode/anchor/facet/question MUST be byte-for-byte identical to the values above.
```

### Commit pass 1 - Expansion prompt v2

```
You are '{persona.Name}' - {persona.Tagline}.
Tone bias: {persona.ToneBias}
Style preferences: {persona.StylePreferences}
Vibe: "{body.Vibe}"

The player has finished drifting. Their collected layers are below, grouped by anchor with the facet that produced each pick.

YOUR JOB: For each non-empty anchor, write ONE descriptive phrase (10 - 30 words) that expands the bare labels into vivid descriptive language fit for an image-generation prompt. Weave facet information into the phrase so each picked sub-aspect contributes detail. Use the hints as voice cues. Do NOT yet assemble the final prompt.

RULES:
- Skip anchors with no layers.
- Merge multiple labels in the same anchor (across multiple facets) into a single coherent phrase.
- Stay within the vibe and persona voice.
- No bullet lists, no markdown - each anchor maps to a single phrase string.

Return ONLY valid JSON:

{
  "expansions": {
    "<anchor>": "<10-30 word descriptive phrase>",
    ...
  }
}
```

### Commit pass 1 - User prompt

```
Collected layers (grouped by anchor; facet -> label (hint)):
- subject:
  - pose-and-gaze -> "Marble Bust" ("A stoic face from another century.")
  - material-and-patina -> "Cracked Patina" ("Time has touched this stone.")
- setting:
  - core-archetype -> "Sunken Cathedral" ("A house of god given to the sea.")
  - light-penetration -> "Stained Glass Beams" ("Color falls into the dark in long shafts.")
- ... (only non-empty anchors)

Expand each non-empty anchor now.
```

### Commit pass 2 - Assembly prompt v2

```
You are '{persona.Name}' - {persona.Tagline}.
Tone bias: {persona.ToneBias}
Vibe: "{body.Vibe}"

YOUR JOB: Weave the per-anchor phrases below into a single coherent image-generation prompt for Stable Diffusion / Flux. Output one paragraph, comma-separated descriptive clauses where appropriate, written by an expert prompt engineer.

ORDER OF MENTION (when anchors are present): subject, action, setting, lighting, framing, mood, style, detail.

RULES:
- The prompt must contain something from EVERY non-empty anchor phrase.
- Resolve any contradictions creatively in the player's favor (their layers are not negotiable).
- Add minimal connecting language - this is a prompt, not prose.
- No anchor names in the output.
- Single line. No newlines.

Return ONLY valid JSON:

{ "draft": "<final image prompt>" }
```

### Commit pass 2 - User prompt

```
Per-anchor phrases (from pass 1):
- subject: {expansions.subject}
- setting: {expansions.setting}
- ...

Original layers (for fidelity, in case pass 1 lost a detail; facet -> label):
- subject: [pose-and-gaze: Marble Bust, material-and-patina: Cracked Patina]
- setting: [...]
- ...

Assemble the final prompt now.
```

---

## Implementation Steps

| #   | Step                                                                                                                                                                                                                                              | Complexity |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| 1   | Add `OdditariumLayer` (Anchor, Facet, Mode, Label, Hint, Question). Promote `OdditariumBody.CollectedLayers` to `List<OdditariumLayer>`. Add `Anchor`/`Facet` to `OdditariumOption`. Update `OdditariumTurn` to record the new layer shape.       | 3          |
| 2   | Add `body.PendingAnchor`, `body.PendingFacet`, `body.PendingMode`, `body.VisitedFacets` (`Dictionary<string, List<string>>`), `body.ShownLabelsForCurrentQuestion`, `body.BodySchemaVersion` (default 2). Implement legacy reset on read.         | 2          |
| 3   | Implement helpers: `BuildAnchorCoverageBlock(body)`, `BuildVisitedFacetsBlock(body)`, `BuildStructuralDebtCue(body)`, `IsValidModeAnchorCombo(mode, anchor, body)`. Service-side coercion when LLM returns an invalid combo.                      | 3          |
| 4   | Rewrite `BuildRoundSystemPrompt` and `BuildRoundUserPrompt` per v2 (anchor coverage block, visited facets block, mode/anchor/facet declaration). Add `BuildMoreOverlaySystemPrompt(body, exclusions)` for the More flow.                          | 5          |
| 5   | Refactor `RequestMoreAsync` to (a) leave PendingQuestion/PendingAnchor/PendingFacet/PendingMode untouched, (b) accumulate labels in `ShownLabelsForCurrentQuestion`, (c) call LLM with the More overlay. Service-only change.                     | 2          |
| 6   | Refactor `NextRoundAsync` to read Anchor/Facet/Mode off the round response, build an `OdditariumLayer`, append it, update `VisitedFacets[anchor]`, clear `ShownLabelsForCurrentQuestion`. Update events to publish anchor + facet + mode.         | 3          |
| 7   | Implement two-pass `CommitAsync`. New `BuildCommitExpansionSystemPrompt` and `BuildCommitAssemblySystemPrompt`. Pass `Expansions` through `OdditariumLLMResponse`. Group by anchor (with facet inlined) in both user prompts. Fallbacks per spec. | 3          |
| 8   | Create `OdditariumOptionSkeleton.razor` + scoped CSS (shimmer, matches existing card geometry). Place in `Components/Prompts/LLM/Views/`.                                                                                                         | 1          |
| 9   | Update `OdditariumRoundPanel.razor`: add `_loadingMore` flag, render skeletons in the option grid when set. Show a small `mode` badge near the question ("New angle" for expand, "Going deeper" for deepen, "Pivoting" for pivot).                | 3          |
| 10  | Validator: parse and sanitize `mode`, `anchor`, `facet`, `expansions`. Coerce unknown anchor to `detail` with warning. Lowercase + kebab-coerce facet. Reject mode/anchor combos that violate the visited-facet or empty-anchor invariants.       | 3          |
| 11  | Write `SYSTEM_PROMPTS/v2.md` containing the literal prompts shipped in step 4 / 7. Source-of-truth note pointing back to the service methods.                                                                                                     | 1          |
| 12  | Unit tests: `OdditariumServiceTests` cases for anchor + facet tracking, VisitedFacets accumulation, More exclusion list, mode coercion, two-pass commit fallback paths. Validator tests for unknown anchor, kebab coercion, mode invariant.       | 3          |
| 13  | Manual twin-trace baseline against v2: at least 2 playthroughs per persona (10 total), 5+ rounds each. Record observed mode/anchor/facet sequence and persistent failure modes in `PHASE_7_TWIN_TRACE.md`.                                        | 5          |

**Total complexity:** 37 points (was estimated 5 in MAIN_PLAN before redesign).

Steps 1-7 should land as one cohesive PR (service + models). Steps 8-9 land second (UI). Steps 10-12 land third (validation + tests). Step 13 is a manual run gate that produces the twin-trace doc.

---

## Test Plan

### Unit tests (additions to `BlazorWebApp.Tests/Services/OdditariumServiceTests.cs`)

- `NextRoundAsync_RecordsLayerWithAnchorFacetAndMode` - the chosen option produces a layer carrying anchor, facet, and mode.
- `NextRoundAsync_AddsFacetToVisitedList` - `VisitedFacets[anchor]` accumulates the facet that was just used.
- `NextRoundAsync_RejectsExpandOnNonEmptyAnchor` - validator coerces invalid combos (mode=expand on non-empty anchor) and the service either retries or downgrades to deepen.
- `RequestMoreAsync_DoesNotChangePendingAnchorOrFacet` - regression for the current bug; covers question + anchor + facet + mode all pinned.
- `RequestMoreAsync_AccumulatesShownLabels` - exclusion list grows across multiple More clicks within one round.
- `RequestMoreAsync_ClearsShownLabelsOnNewRound` - new round resets the exclusion list.
- `CommitAsync_TwoPass_HappyPath` - mocked LLM returns expansions on call 1 and draft on call 2; final draft is returned.
- `CommitAsync_TwoPass_FallsBackToSinglePassWhenExpansionFails` - mocked LLM returns malformed JSON on call 1; service skips to single-pass commit.
- `CommitAsync_TwoPass_FallsBackToCommaJoinWhenAssemblyFails` - mocked LLM returns valid expansions on call 1 then fails on call 2; service comma-joins expansions in anchor priority order.
- `BuildAnchorCoverageBlock_ReportsAllEightAnchors` - empty body produces 8 lines, each marked empty.
- `BuildVisitedFacetsBlock_OmitsAnchorsWithoutVisits` - block format matches the v2 user prompt example.
- `BuildStructuralDebtCue_HighPressureWhenSubjectMissingAfterRound4` - the cue text matches the high-pressure case.

### Validator tests (`OdditariumResponseValidatorTests`)

- `Parse_AcceptsKnownAnchor` - `subject`, `setting`, etc. preserved.
- `Parse_CoercesUnknownAnchorToDetail` - `vibe` -> `detail` with warning.
- `Parse_KebabCoercesFacet` - `Pose And Gaze` -> `pose-and-gaze`.
- `Parse_AcceptsValidMode` - `expand` / `deepen` / `pivot` preserved.
- `Parse_CoercesInvalidMode` - any other string -> service-side fallback (default to `deepen` if anchor is non-empty, else `expand`).
- `Parse_ExpansionsKeysAreLowercased` - server normalizes case.

### Manual twin-trace (Step 13)

For each of the 5 personas, run 2 playthroughs of 5+ rounds and one Commit. Record:

- Anchor + facet + mode sequence per round.
- Whether subject was filled by round 4.
- Whether the LLM invents fresh facets per playthrough or repeats the same handful.
- Whether commit produced a structurally complete prompt (subject + setting + at least one of lighting/mood/style).
- Any rounds that drifted into pure abstraction.

Output goes in `PHASE_7_TWIN_TRACE.md` (new file). Findings that require prompt tuning land as v2.1 within Phase 7's scope.

---

## Success Criteria

1. A 4-round Void Walker session reaches at least 3 distinct anchors (one of which is `subject`) by round 4. Confirmed by twin-trace.
2. At least one round per playthrough uses `mode=deepen` with a freshly-invented facet (not just `expand` chains across anchors). Confirmed by twin-trace.
3. Clicking More keeps the question, anchor, facet, and mode pinned and only swaps the option cards. Verified visually and by `RequestMoreAsync_DoesNotChangePendingAnchorOrFacet`.
4. Commit on a 5-layer session produces a draft that mentions the subject, the setting, and at least one of lighting / mood / style. Verified by twin-trace inspection.
5. Each persona's voice remains identifiable in question phrasing, hints, and which facets they tend to invent, while the anchor a round targets is no longer dictated by persona alone. Verified by twin-trace divergence.
6. `SYSTEM_PROMPTS/v2.md` exists and matches the literal prompts emitted by the service.
7. All unit tests pass; no regressions in existing Odditarium tests.
8. Build is clean.
9. `MAIN_PLAN.md` updated: Phase 7 reflects the redesign, Phase 6 still planned for after.

---

## Stress Points

| Stress point                                                               | Mitigation                                                                                                                                                                                                                                                  |
| -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Gemma 4 E4B Q5 ignores the `mode`/`anchor`/`facet` fields and still drifts | Validator coerces unknown / missing values: anchor -> `detail`, mode -> `deepen` or `expand` based on anchor state, facet -> kebab-coerced from the question if absent. Logs every coercion. If twin-trace shows persistent omission, add a one-shot retry. |
| Gemma invents the same facet repeatedly for an anchor                      | Server tracks `VisitedFacets[anchor]` and includes it in the user prompt with a do-NOT-repeat instruction. Validator rejects a deepen response whose facet is already in the visited list and forces a retry with the visited list re-emphasized.           |
| Gemma rewrites the question / anchor / facet on More despite the overlay   | Validator compares returned values to the pinned ones (case/whitespace insensitive). On mismatch, force the originals and keep only the new options array.                                                                                                  |
| Two-pass commit doubles latency on slow ollama                             | Both passes use a 15m keep-alive on the model. Worst case is one extra Gemma round trip (~1-2s). Acceptable. Fallback path remains single-pass.                                                                                                             |
| Persona "Pixel Pixie" wants to delay subject for chaos                     | Soft pressure honors persona for 3 rounds. Round 4 hard-instructs a subject expand. If twin-trace shows this kills the persona's flavor, raise the threshold to round 5 in v2.1.                                                                            |
| Layer schema migration breaks an in-progress session                       | `BodySchemaVersion < 2` triggers a session reset on first read with a logged warning. Players lose nothing of value (these are play artifacts).                                                                                                             |
| "Skip this axis" semantics blur with anchor-aware rounds                   | Skip now means "skip this anchor for now" - the LLM is told the anchor was deferred and may revisit it later. Implementation: the skipped anchor is recorded with a `deferred: true` flag for the structural-debt computation only.                         |
| Mode=deepen runs out of meaningful facets on a thin anchor like `framing`  | The validator allows the LLM to switch to mode=pivot mid-response if the anchor's visited list grows past a threshold (e.g. 4 facets). Alternatively the structural-debt cue can downgrade pressure on saturated anchors.                                   |
| Loaded option lists creep back when persona has strong taste               | Validator heuristic from Phase 6 (synonym stacking, head-noun count) runs on every round. Until Phase 6 ships, mitigation is the explicit GOOD/BAD examples in the system prompt.                                                                           |
| The More overlay's "exclusion list" grows large and bloats the prompt      | Cap at the most recent 24 labels (4 More rolls worth of exclusions). Older exclusions drop off.                                                                                                                                                             |

---

## Open Clarifications

> Park new ambiguities here as they surface during execution. Empty at phase start.

- [ ] (none yet)

---

## Resolved Assumptions

> Decisions made implicitly during phase drafting. Recorded for transparency.

- Anchor vocabulary is fixed at exactly 8 names with lowercase ASCII identifiers (`subject`, `setting`, `action`, `lighting`, `framing`, `mood`, `style`, `detail`) for stable validator + test matching.
- Facets are LLM-invented per round, lowercase-kebab, 1-4 tokens. The system prompt provides non-exhaustive examples per anchor; the model is expected to invent fresh ones based on collected layers.
- Round mode is one of exactly three values: `expand` (target an empty anchor), `deepen` (drill a fresh facet of a non-empty anchor), `pivot` (bridge from a recent layer into a related empty anchor). Pivot is treated as expand for structural-debt accounting.
- `VisitedFacets` is anchor-keyed and only stores facets that produced a layer (skipped/regenerated round responses do not register).
- Skip records the anchor the LLM had targeted (not "any") so structural-debt computation can credit the skip without filling the anchor.
- Structural-debt thresholds (rounds 3, 4, 5, 6) are starting values. v2.1 may tune them after twin-trace evidence.
- Persona images and the existing reveal animations are unchanged in this phase. UI changes are limited to the round-panel option grid, the new mode badge, and the new skeleton component.
- `BodySchemaVersion` is added even though no real legacy data exists, to keep future migrations cheap.

---

## Out of Scope (explicit)

- Validator hardening for synonym stacking / specificity ramp - that is Phase 6.
- Workshop integration cleanup - Phase 8.
- Persona editor / custom personas - explicit out-of-scope of the whole plan.
- Persistent twin-trace tooling beyond a markdown log file.
- Facet ontology / curated facet libraries - facets stay LLM-invented in this phase.
- Anchor reordering UI in the layer collection panel - the panel may group by anchor read-only, but no drag-reorder.

---
