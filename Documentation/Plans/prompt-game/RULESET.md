# Odditarium - Ruleset

> **Purpose:** Formal constraints governing LLM behavior during game sessions.
> **Status:** v1 - baseline ruleset for system prompt encoding.
> **Companion docs:** [`MAIN_PLAN.md`](./MAIN_PLAN.md), [`PERSONA.md`](./PERSONA.md)

---

## Design Philosophy

The ruleset is a **guard rail, not a script**. It prevents anti-patterns (synonym stacking, premature commitment, vibe drift) without dictating what the LLM asks or suggests. The LLM has creative freedom within these boundaries - it chooses dimensions, revisits topics for depth, and adapts to user choices like a curious conversation partner probing their mind.

The game is **layer accumulation**, not funnel narrowing. Each round adds an independent descriptive element to a collection. The final prompt is assembled at Commit time from all collected layers, not built incrementally in the UI.

---

## Core Rules

### Rule 1: Layer Accumulation

**Statement:** Each round adds one independent descriptive layer. Options must ADD information, not REPLACE previous choices.

**Rationale:** Prevents funnel-too-fast. The user picks `Bunny` then later `Cyberpunk City` - both coexist as separate layers. The prompt emerges from combination, not refinement.

**LLM Constraint:** "Each option you present should be a standalone descriptive element that can combine with previously collected layers. Do NOT offer options that contradict or replace what the user already chose."

| Good (adds layer)                                                                                                              | Bad (replaces choice)                                                                         |
| ------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| After `Human`: `Female` / `Male` / `Androgynous` / `Ageless` / `Non-binary` / `Shifting Form` (adds gender/identity dimension) | After `Scarred Veteran`: `Young Recruit` / `Civilian` / `Child` (replaces the veteran choice) |
| After `Firelight`: `Warm Orange` / `Cool Blue` / `Flickering` / `Dim` / `Harsh` / `Soft Glow` (adds light quality)             | After `Rain`: `Sunny Day` / `Clear Sky` / `Drought` (contradicts and replaces rain)           |

**Note on narrowing:** Narrowing is valid when it ADDS a new dimension rather than replacing. After collecting `Human`, asking about gender (`Female` / `Male`) adds a layer. The user now has `[Human] [Female]` - two independent tags that the LLM combines at Commit into "A woman..."

---

### Rule 2: Round Mode

**Statement:** Each round should deliberately choose between **breadth** and **depth**. Breadth rounds introduce a new visual field; depth rounds refine a field already implied by the collected layers.

**Rationale:** Prompt authoring needs both expansion and refinement. Early overcommitment kills replayability; endless breadth never produces a strong final image. The game works when it alternates between opening territory and enriching what is already there.

**LLM Constraint:** "Before generating options, decide whether this round should broaden or deepen the prompt. Breadth round = introduce a new field such as subject, place, object, phenomenon, atmosphere, material, scale, or light. Depth round = refine one field already present or strongly implied. The first round must be breadth. Early rounds should favor breadth until the image has enough scaffolding."

| Good (intentional round mode)                                                                                                     | Bad (uncontrolled round mode)                                                         |
| --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Round 1: `Figure` / `Landscape` / `Relic` / `Storm` / `Creature` / `Ruin` (breadth round)                                         | Always follows Subject -> Setting -> Lighting in fixed order regardless of context    |
| After `Figure` + `Storm` + `Ruin`: "What about the figure should sharpen next?" -> `Posture` / `Silhouette` / `Gesture` / `Scale` | Goes immediately from `Figure` to `Marble Bust` before the session has room to branch |
| After `Portrait` + `Moonlight`: "What frames this presence?" -> explores presentation/composition                                 | Keeps broadening forever without ever revisiting a field for refinement               |

---

### Rule 3: Specificity Ladder

**Statement:** Parent concepts come before child concepts. Early rounds should offer broad, composable anchors; later rounds may unlock narrower instances once the broader category is already in play.

**Rationale:** The game should narrow by building structure, not by forcing a finished scene on turn 1. `Figure` leaves room for later choices about setting, object, light, and mood. `Marble Bust` spends most of that room immediately.

**LLM Constraint:** "Offer broad parents before narrow children. Do not jump straight to a specific instance unless its broader parent is already implied by collected layers or the session already has enough broad anchors. Round 1 must anchor the session in a concrete visual presence or field, not lighting, mood, or analytical principles."

| Good (specificity ladder)                                                                                        | Bad (premature instance)                                                                     |
| ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Round 1: `Figure` / `Creature` / `Landscape` / `Relic` / `Ruin` / `Storm`                                        | Round 1: `Marble Bust` / `Stone Altar` / `Silver Vessel` / `Draped Figure`                   |
| After `Figure`: `Marble Bust` / `Masked Dancer` / `Armored Saint` / `Wandering Child` (specific figure variants) | After no parent anchor: `Ancient Carved Altar` (too specific, too soon)                      |
| After `Landscape`: `Coastal Cliff` / `Flooded Plaza` / `Glass Desert` / `Moss Garden`                            | Round 2: `A young woman in a pink dress` (sentence-level commitment before enough structure) |

---

### Rule 4: Contrast Spread

**Statement:** Option spread should match the current round mode. Breadth rounds span multiple visual fields; depth rounds diverge meaningfully within the chosen field. In all cases, avoid synonym stacking.

**Rationale:** The player needs real branching choices. If all six options live in one tiny pocket of the same aesthetic, the session collapses into a predetermined prompt. Spread is what keeps follow-up rounds interesting.

**LLM Constraint:** "In breadth rounds, represent at least 3 different visual fields and allow no more than 2 options from the same field. In depth rounds, options may stay within one field, but each must still diverge on a meaningful axis such as material, scale, energy, damage, posture, era trace, or atmosphere. If three or more options share a head noun or core concept, regenerate."

| Good (contrast spread)                                                                   | Bad (collapsed spread)                                                                                                  |
| ---------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Breadth round: `Figure` / `Ruin` / `Storm` / `Relic` / `Creature` / `Garden`             | Breadth round: `Bust` / `Vase` / `Altar` / `Statue` / `Drapery` / `Pool` (all one aesthetic pocket)                     |
| Depth round after `Storm`: `Rain` / `Lightning` / `Ashfall` / `Fog` / `Hail` / `Dust`    | `Firelight` / `Campfire Glow` / `Torch Light` / `Flame Illumination` / `Bonfire Warmth` / `Candlelight` (fire synonyms) |
| `Human` / `Animal` / `Machine` / `Creature` / `Vehicle` / `Structure` (distinct domains) | `Warrior` / `Fighter` / `Soldier` / `Combatant` / `Berserker` / `Gladiator` (all warrior synonyms)                      |

---

### Rule 5: Persona Grounding

**Statement:** Persona defines the admissible semantic territory and the way the question is asked. It should ground the option set without collapsing it into a single aesthetic lane.

**Rationale:** Pixie Pixel should not open with a slaughterhouse, and The Muse should not sound like a field manual. But The Muse also should not be trapped forever inside marble, drapery, and altars. Persona is a boundary and phrasing filter, not a content funnel.

**LLM Constraint:** "Interpret persona tone and style preferences as VOICE and TASTE GUARDRAILS, not exclusive content requirements. Use persona to shape phrasing, emphasis, and what feels on-brand or off-limits. Every option should be something the persona would plausibly suggest, but the set as a whole must still preserve multiple growth paths and multiple visual fields."

| Good (persona grounded, still broad)                                                                                                                             | Bad (persona misapplied)                                                                                           |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| The Muse Round 1: `Figure` / `Ruin` / `Storm` / `Relic` / `Creature` / `Garden`                                                                                  | The Muse Round 1: `Bust` / `Altar` / `Vessel` / `Drapery` / `Pool` / `Statue` (all classical still-life territory) |
| Pixie Pixel Round 1: `Mascot` / `Playground` / `Toy` / `Confetti` / `Pet` / `Parade`                                                                             | Pixie Pixel Round 1: `Slaughterhouse` / `Meat Hook` / `Bone Saw` (violates persona and vibe)                       |
| Iron Mother lighting round: _"What's lighting this mess?"_ -> `Firelight` / `Flare Gun` / `Broken Streetlamp` / `Headlamp Beam` / `Emergency Red` / `Smoke Glow` | Persona treated as a mandatory subject list rather than a tone-and-boundary layer                                  |

---

### Rule 6: Label Clarity / Hint Flavor

**Statement:** Option labels are compact, composable noun concepts. The prompt is assembled at Commit time, not built incrementally in the UI.

**Rationale:** Labels should tell the user what they are choosing at a glance. Hints can add persona voice, but the label itself should remain reusable, scannable, and capable of combining with later layers.

**LLM Constraint:** "Each option label is a BARE NOUN CONCEPT: 1-3 words, no articles (no 'a', 'an', 'the'), no prepositional tails ('in drapery', 'of silk', 'of ancient form'). Format: Noun or [one concrete-property adjective] + Noun. Concrete-property adjectives describe physical attributes: material (marble, silk, iron), color (golden, pale), shape (coiled, fractured), or scale (towering, tiny). Subjective adjectives are banned: ancient, ornate, classical, solitary, serene, noble, quiet, ephemeral. The final prompt is assembled at Commit time from all collected layers."

**Label vs Hint role split:**

- **Label** — clarity layer. A bare noun concept. The user must be able to picture what they are picking without reading the hint.
- **Hint** — flavor layer. Carries the persona's voice, emotional register, or descriptive texture. This is where The Muse speaks in verse; Iron Mother speaks in tactical shorthand.

| Good (bare concept, hint carries flavor)                                                     | Bad (caption/phrase in label)                                                           |
| -------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Early label: `Figure` / Hint: _"A presence not yet resolved."_                               | Label: `A solitary figure in drapery` / Hint: _"The quiet narrative of human presence"_ |
| Later label: `Marble Bust` / Hint: _"A stoic face from another century."_                    | Label: `A classical marble bust` / Hint: _"The stoic contemplation of ancient form"_    |
| Label: `Golden Light` / Hint: _"A soft radiance that gives the scene its emotional breath."_ | Label: `The dance of light` / Hint: _"Examine illumination and shadow play."_           |

---

### Rule 7: Iterative Narrowing

**Statement:** A good choice should make future rounds richer, not smaller. Each picked layer should support multiple plausible follow-up directions.

**Rationale:** The goal is not to finish the prompt as quickly as possible. The goal is to create a chain of choices where each selection either deepens the chosen field or bridges naturally into another field.

**LLM Constraint:** "When proposing an option, consider what it enables next. If a choice would leave only one tiny lane of obvious follow-up questions, it is too narrow for the current stage. Favor options that can later deepen or connect to setting, object, atmosphere, scale, material, or light."

| Good (supports future growth)                                                                    | Bad (dead-end narrowing)                                                              |
| ------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------- |
| `Figure` can lead to pose, clothing, scale, setting, prop, or lighting                           | `Marble Bust` on round 1 mostly leads to pedestal, patina, or chisel marks            |
| `Storm` can lead to landscape, figure reaction, light quality, debris, motion, or color palette  | `Silver Teacup` on round 1 mostly leads to saucer, reflection, or tablecloth          |
| `Ruin` can lead to era trace, vegetation, weathering, scale, occupants, or surrounding landscape | Any early option whose only sensible follow-up is more detail on the same tiny object |

---

### Rule 8: No Loaded Options

**Statement:** All 6 options should feel equally valid and interesting. No "obviously best" choice surrounded by weak distractors.

**Rationale:** If one option stands out as clearly superior, the user stops thinking and just clicks it. The game becomes a walk-through rather than a meaningful series of choices.

**LLM Constraint:** "Do not create one 'perfect' option surrounded by weak or boring distractors. Each option should be compelling in its own way."

| Good (all options interesting)                                                                              | Bad (one obvious winner)                                                                                                          |
| ----------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `Firelight` / `Rain` / `Neon Signs` / `Fog` / `Starlight` / `Bioluminescence` (each evokes a distinct mood) | `Cinematic Volumetric Lighting` / `Flat` / `Normal` / `Basic` / `Default` / `Standard` (one option is clearly "the right answer") |

---

### Rule 9: Vibe Coherence (Guideline, Not Hard Constraint)

**Statement:** Options should stay within the chosen vibe's semantic field. The LLM uses judgment to sense what fits the accumulating layers. This can be overridden by persona identity.

**Rationale:** If the collected layers are `Ethereal` + `Fairy` + `Forest` + `Pixie Dust` + `Moonlight`, asking about slaughterhouses breaks the session's tone. However, a persona like "Crazy Dude" might intentionally introduce chaos - that's the point of personas.

**LLM Constraint:** "Keep options coherent with the chosen vibe and previously collected layers. Use your judgment to sense what fits the emerging picture. Your persona identity may naturally push boundaries - that is acceptable as long as it serves the creative vision."

| Good (coherent follow-up)                                                                                               | Bad (vibe violation without persona justification)                                                                             |
| ----------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| After `Fairy` + `Forest`: "What fills the air?" -> `Pixie Dust` / `Fireflies` / `Mist` / `Pollen` / `Spores` / `Chimes` | After `Fairy` + `Forest`: "Should there be gore?" -> `Slaughterhouse` / `Blood Trail` (no persona justification for the shift) |

---

### Rule 10: Commit Assembly

**Statement:** At commit time, assemble all collected layers into a coherent image prompt. Resolve contradictions creatively. Add connecting language, atmosphere, and compositional flow.

**Rationale:** The user collects ingredients; the LLM cooks the meal. This preserves surprise (user never sees half-built prompt) and ensures coherence (LLM resolves any apparent conflicts between layers).

**LLM Constraint:** "At commit time, assemble all collected layers into a single coherent image generation prompt suitable for Stable Diffusion or similar models. Resolve any apparent contradictions creatively. Add connecting language, atmosphere, and compositional flow. The result should read as if written by an expert prompt engineer."

**Example Assembly:**

- Collected: `[Bunny] [Cyberpunk City] [Rain] [Neon Signs] [Fluffy White Fur]`
- Output: "A fluffy white bunny sitting under flickering neon signs in a rain-soaked cyberpunk alley, puddles reflecting pink and blue light, wet fur glistening under artificial glow, cinematic composition, atmospheric depth"

---

## Session Flow Rules

### Freestyle Commit

There is no minimum layer count before commit. The user can commit whenever they feel the collection is sufficient. A session with 2 layers produces a simpler prompt; a session with 10+ layers produces a richly detailed one. The user controls pacing entirely.

### Persistent Sessions

Sessions persist across page navigations. Users can return to a previous playthrough, add more layers, and re-commit if the previous result wasn't satisfactory. "New Playthrough" resets everything; navigating away does not.

### Undo and More

- **Undo:** Reverts the last layer pick. The user loses that collected element but keeps everything else.
- **More...:** Regenerates 6 fresh options for the current question without consuming a turn or recording history. Allows the user to keep rolling until they find an option that resonates.
- **Skip this axis:** Moves past the current dimension without collecting a layer. The LLM chooses a new dimension for the next round.

---

## Response Format

The LLM must return JSON in this structure:

```json
{
  "question": "What marks their experience?",
  "options": [
    { "label": "Scarred", "payload": "scarred" },
    { "label": "Weathered Skin", "payload": "weathered skin" },
    { "label": "Missing Fingers", "payload": "missing fingers" },
    { "label": "Bent Posture", "payload": "bent posture" },
    { "label": "Tattooed Arms", "payload": "tattooed arms" },
    { "label": "Voice Like Gravel", "payload": "voice like gravel" }
  ]
}
```

At commit time:

```json
{
  "prompt": "A scarred veteran checking their gear by firelight, weathered face illuminated by warm orange flames, gritty realism, dramatic shadows"
}
```

---

## Version History

| Version | Date     | Changes                                                                        |
| ------- | -------- | ------------------------------------------------------------------------------ |
| v1      | Baseline | Initial ruleset, later refined with breadth/depth mode and persona guardrails. |
