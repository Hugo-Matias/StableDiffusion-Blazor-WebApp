---
agent: agent
description: >
  Define a new Odditarium persona from scratch.
  Guides you through all identity, voice, anchor, visual, and message fields,
  then outputs a complete profile document to Documentation/Odditarium/Personas/.
  This is Step 1 of 2. Run persona-create next to implement the code.
tools:
  - read_file
  - search_files
  - list_files
  - write_to_file
  - replace_in_file
  - execute_command
---

# Odditarium Persona Wizard -- Define

You are defining a new Odditarium persona. Your only output is a profile document
at `Documentation/Odditarium/Personas/{name}.md`. You do **not** write any code.

---

## Reference Materials

Read before starting the interview:

- `Documentation/Odditarium/persona-spec.md` -- full field reference, anchor system, writing guidelines, field injection order
- `BlazorWebApp/Models/OdditariumPersona.cs` -- all fields with doc comments
- `BlazorWebApp/Models/OdditariumPersonaRoster.cs` -- existing 5 personas as concrete examples
- `BlazorWebApp/Models/OdditariumModels.cs` lines 187-210 -- the 13 anchors and tiers
- `.roo/rules/01-workspace-standards.md` -- Roo workspace-level standards

---

## Phase 1: Character Identity

Ask both groups one at a time. Wait for answers before proceeding.

### 1a -- Core identity

> I need to understand who this persona is. Answer as many as you like -- I will suggest anything missing.
>
> 1. **Name** -- Character's name. 1-2 words, proper noun. (e.g., "Aurelie", "Vera Colt")
> 2. **Tagline** -- 2-4 word descriptor under the name on the card. (e.g., "Golden-Tongued Critic", "Cold Logic Engine")
> 3. **Concept** -- One sentence: who are they and where are they from?
> 4. **Vibe** -- What feeling should a player have after a full session with this persona?

Derive and propose:

- `Description` paragraph -- 2-4 sentences, flavor-rich, no mechanical game terms
- `Id` slug -- lowercase, hyphen-separated (e.g., `night-weaver`)

Show derivations and ask: **"Does this capture the character? Anything to adjust?"**

### 1b -- Thematic profile

> Now I need the thematic world this persona inhabits.
>
> 1. **Three tags** -- Short nouns shown as chips on the card. (e.g., "Gritty Realism", "Survival", "Weathered")
> 2. **Tone bias** -- How does this character speak? 1-2 sentences as if briefing an actor.
> 3. **Style preferences** -- What visual aesthetic does a session with this persona produce?

Derive and propose all three. Confirm before moving on.

---

## Phase 2: Voice Depth

Ask each field separately. Show a roster example after each. These are the most impactful fields.

### 2a -- Creative Philosophy

> What does **{Name}** believe makes a great image? This is their artistic manifesto.
>
> Example (Null): *"A great image is a solved equation -- every element justified, no redundancy. Composition is constraint satisfaction. The optimal structure is also the beautiful one."*
>
> Be opinionated and character-specific. Avoid generic truisms.

Draft in the persona's voice. Confirm.

### 2b -- Question Framing

> How does **{Name}** frame a question -- not the vocabulary, the rhetorical stance.
>
> Example (Vera Colt): *"Frame as an operational choice -- what survives, what works under pressure, what costs too much. No decorative phrasing."*
>
> Start with: "Pose as...", "Frame as...", "State as...", or "Phrase as..."

Draft. Confirm.

### 2c -- Option Voice

> How does **{Name}** write option labels and hints?
> - Labels: the short name of each option
> - Hints: the one-sentence explanation beneath each label
>
> Example (Aurelie): *"Labels: evocative noun phrases that carry visual weight. Hints: one sentence of art-critical observation that names what makes the element interesting, not what it looks like."*

Draft. Confirm.

### 2d -- Forbidden Zones

> What must **{Name}** never suggest? Hard exclusions that prevent persona drift across a long session.
>
> Example (Trixel): *"Never suggest dark, grim, horror, or desaturated themes. Avoid weight, grief, silence, and decay. Everything should feel like it wants to be touched."*

Draft. Confirm.

---

## Phase 3: Anchor Personality

Show the user the 13 anchors from `OdditariumModels.cs` before asking.

> There are 13 anchors across 3 tiers:
> - **Core** (always first): `subject`, `setting`
> - **Structural**: `action`, `lighting`, `framing`, `atmosphere`
> - **Enrichment**: `mood`, `style`, `detail`, `time`, `scale`, `color`, `movement`
>
> **3a. Anchor affinity** -- Which anchors feel most natural for {Name}? List in priority order (highest first).
>
> Examples: Aurelie -> framing, style, lighting | Vera Colt -> action, subject, setting | Trixel -> color, style, mood
>
> **3b. Session depth** -- How deeply does {Name} explore each anchor before moving on?
> - Shallow (0/1): chaotic/playful | Moderate (1/2): balanced | Deep (2/3): methodical/moody

Derive the full `AnchorAffinity` array: user-listed anchors first, then remaining in tier order. Confirm.

---

## Phase 4: Assets and Visual Identity

### 4a -- Accent color

> What color represents {Name}? Card glow and selection highlight.
>
> Existing: Aurelie `#c9a84c` gold | Vera Colt `#b45a3c` rust | Nyx `#7b5ea7` violet | Trixel `#e87db0` pink | Null `#3c8c9c` teal
>
> Provide a hex code or describe the color.

### 4b -- Visual Identity Lock

The visual identity lock is the canonical set of descriptors that keep all generated images consistent.

> I need the **visual identity lock** for **{Name}** -- the fixed descriptors that must appear in every image.
>
> Please describe:
> 1. **Character type** -- 1girl, 1boy, androgynous, etc.
> 2. **Hair** -- color, length, style
> 3. **Eyes** -- color, notable shape or expression
> 4. **Skin tone**
> 5. **Signature outfit** -- 2-4 key clothing elements that define them visually
> 6. **Distinguishing features** -- accessories, scars, markings, props they always carry
> 7. **Overall art style** -- anime, semi-realistic, painterly, chibi-adjacent, etc.
> 8. **Resting mood** -- the emotional baseline of their default expression

Derive a compact set of NoobAI/Illustrious-compatible tag phrases from these descriptors. Show the derived tags and confirm.

### 4c -- Image generation prompts

Using the visual identity lock from 4b, generate draft prompts for all 9 states.
Show all 9 prompts together for the user to review.

**Target models**: Illustrious, NoobAI, Anima -- full body portrait. Background is removed by the existing app workflow; no background tags needed.

**Recommended generation resolution**: 832x1216

**Shared base prompt** structure (fill with the actual derived visual identity tags):

    masterpiece, best quality, newest, absurdres, highres, {character_type}, solo, full body,
    {visual identity tags},
    looking at viewer

**Shared negative prompt** -- character-specific issues only (generic quality negatives assumed by the model):
Identify any character-specific problem areas and add targeted negatives.

**State-specific additions** -- adapt language to match the persona's character:

- **idle.png** -- Natural standing, neutral expression. Resting state, no tension.
- **hover.png** -- Slight forward lean, hint of engagement or attention.
- **active.png** -- Peak expressiveness for this character. Most dramatic or confident.
- **thinking-1.png** -- Hand near chin or cheek, gaze downward or inward.
- **thinking-2.png** -- Looking to the side, distant. Lost in thought.
- **thinking-3.png** -- Eyes closed or half-closed. Centered, meditative, processing inward.
- **thinking-4.png** -- Head tilted, curious expression. Considering something.
- **thinking-5.png** -- Eyes upward or upward-angled. Accessing, wondering.

Present all 9 complete prompts. Ask the user to adjust any before proceeding.

### 4d -- Thinking messages

> I need 15-20 **in-character loading messages** for **{Name}** -- shown in rotation while the LLM is generating a round.
>
> These should suggest the persona is _doing something_ related to their core activity. No generic "Loading..." copy.
>
> Example range for Vera Colt:
>
> - "Scouting the terrain..."
> - "Calculating what's worth the cost..."
> - "Reading the smoke on the horizon..."
>
> What is **{Name}** _doing_ while thinking? Give me the activity domain and I will draft the full set for you to approve.

Draft 18 messages based on the user's input. Ask them to add, remove, or adjust any.

### 4e -- Prod messages

> I also need 15-20 **prod messages** -- shown when the user clicks the thinking image as an easter egg. The character is being interrupted and they are not thrilled.
>
> Example range for Aurelie:
>
> - "Patience is itself an aesthetic."
> - "Such urgency. How very provincial."
> - "I am considering seventeen variations simultaneously."
>
> What does **{Name}** say when you poke them? Brief (1-2 sentences), unmistakably in-character.

Draft 18 messages based on the character. Ask the user to approve or adjust.

---

## Phase 5: Generate Profile Document

Once all phases are confirmed, create the file:
`Documentation/Odditarium/Personas/{name}.md`

Fill the template below with all approved values. Every section must contain actual generated content -- do not leave placeholder text.

---

### Profile Document Template

Replace all {placeholder} text with actual approved content from phases 1-4.

---

# {Name} -- Odditarium Persona Profile

> **Id**: `{id}` | **Status**: Draft
> **Profile created**: {today's date}

---

## Identity

- **Id**: `{id}`
- **Name**: {Name}
- **Tagline**: {Tagline}

**Description**

{Description}

---

## Thematic Profile

- **ThematicTags**: {tag1}, {tag2}, {tag3}
- **ToneBias**: {ToneBias}
- **StylePreferences**: {StylePreferences}

---

## Voice Depth

**Creative Philosophy**
> {CreativePhilosophy}

**Question Framing**
> {QuestionFraming}

**Option Voice**
> {OptionVoice}

**Forbidden Zones**
> {ForbiddenZones}

---

## Anchor Personality

**AnchorAffinity** (ordered high to low):

| Priority | Anchor | Rationale |
|---|---|---|
| 1 | {anchor} | {why it fits this character} |

(all 13 anchors listed)

- **MinDeepensPerAnchor**: {n}
- **MaxDeepensPerAnchor**: {n}

---

## Accent Color

- **Hex**: `{hex}`
- **Rationale**: {why this color fits the character}

---

## Messages

### Thinking Messages

1. "{msg}"

(18 total)

### Prod Messages

1. "{msg}"

(18 total)

---

## Visual Identity Lock

> These descriptors are canonical for {Name}. Every generated image must be consistent.

- **Character type**: {1girl / 1boy / etc.}
- **Hair**: {color, length, style}
- **Eyes**: {color, shape}
- **Skin**: {tone}
- **Signature outfit**: {key clothing elements}
- **Distinguishing features**: {accessories, markings, props}
- **Art style**: {anime / semi-realistic / painterly / etc.}
- **Resting mood**: {baseline expression quality}

---

## Image Generation Prompts

> **Compatible models**: Illustrious, NoobAI, Anima
> **Format**: Full body portrait -- background removed by workflow
> **Recommended generation resolution**: 832x1216

### Shared Base Prompt

```
(actual shared base prompt with visual identity tags filled in)
```

### Shared Negative Prompt

```
worst quality, low quality, bad anatomy, bad hands, missing fingers, extra fingers, blurry, watermark, signature, text,
(character-specific negatives)
```

---

### idle.png -- Neutral State

**Pose intent**: {describe idle for this character}

```
(full idle prompt)
```

---

### hover.png -- Engaged State

**Pose intent**: Slight forward lean, hint of interest

```
(full hover prompt)
```

---

### active.png -- Active State

**Pose intent**: {describe peak expressiveness for this character}

```
(full active prompt)
```

---

### thinking-1.png -- Hand Near Face

**Pose intent**: Hand near chin or cheek, gaze slightly downward

```
(full thinking-1 prompt)
```

---

### thinking-2.png -- Distant Gaze

**Pose intent**: Looking to the side, lost in thought

```
(full thinking-2 prompt)
```

---

### thinking-3.png -- Eyes Closed

**Pose intent**: Centered, eyes closed, processing inward

```
(full thinking-3 prompt)
```

---

### thinking-4.png -- Head Tilted

**Pose intent**: Head tilted, curious, considering

```
(full thinking-4 prompt)
```

---

### thinking-5.png -- Upward Gaze

**Pose intent**: Looking up, accessing, wondering

```
(full thinking-5 prompt)
```

---

## Integration Checklist

### Assets

- [ ] `idle.png` placed in `BlazorWebApp/wwwroot/odditarium/personas/{id}/`
- [ ] `hover.png` placed
- [ ] `active.png` placed
- [ ] `thinking-1.png` placed (required)
- [ ] `thinking-2.png` placed (optional)
- [ ] `thinking-3.png` placed (optional)
- [ ] `thinking-4.png` placed (optional)
- [ ] `thinking-5.png` placed (optional)

### Code

- [ ] Roster entry added to `BlazorWebApp/Models/OdditariumPersonaRoster.cs`

### Verification

- [ ] Persona appears on selection screen with correct name, tagline, and accent color
- [ ] Thinking messages cycle during a session
- [ ] Prod messages appear when thinking image is clicked
- [ ] Voice depth fields produce distinct in-character questions and options
- [ ] No ForbiddenZones content appears in generated options

### Documentation

- [ ] `Documentation/Odditarium/persona-spec.md` Section 6 (roster table) updated
- [ ] `Documentation/Odditarium/persona-spec.md` Section 9 (changelog) updated

---

**End of profile document template.**

---

After creating the file, confirm:

> Profile document created: `Documentation/Odditarium/Personas/{name}.md`
>
> **Next step -- implement the code:**
> - VS Code: use `.github/prompts/persona-create.prompt.md`
> - Roo Code: use `.roo/commands/persona-create.md`
>
> Provide the profile path when prompted.