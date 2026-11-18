# Odditarium Persona Specification

> **Status**: Living document. Update when `OdditariumPersona.cs`, `OdditariumPersonaRoster.cs`, or the JSON persona schema changes.
>
> **Workflow**
>
> Creating a persona is a two-step process:
>
> - **Step 1 — Define**: Run the persona wizard. Produces a profile doc in `Documentation/Odditarium/Personas/{name}.md`.
> - **Step 2 — Implement**: Run the persona-create prompt with the profile doc path. Creates a JSON file in `BlazorWebApp/Data/Odditarium/Personas/{id}.json` and the asset folder.
>
> Personas are loaded from JSON files at application startup by `OdditariumPersonaRoster`. No C# code changes are required to add a new persona — just drop a valid `.json` file into the personas directory.
>
> **Tools**
>
> | Purpose          | VS Code Copilot                            | Roo Code                          |
> | ---------------- | ------------------------------------------ | --------------------------------- |
> | Define (wizard)  | `.github/prompts/persona-wizard.prompt.md` | `.roo/commands/persona-wizard.md` |
> | Implement (code) | `.github/prompts/persona-create.prompt.md` | `.roo/commands/persona-create.md` |
>
> **Profile docs**: `Documentation/Odditarium/Personas/{name}.md`
> **Persona JSON files**: `BlazorWebApp/Data/Odditarium/Personas/{id}.json`

---

## 1. What a Persona Is

An Odditarium persona is a **playable character** that reshapes every question and option the LLM generates during an entire game session. It is not a cosmetic skin. A persona fundamentally changes:

| What changes                | How                                                                                                                     |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **Question phrasing**       | `QuestionFraming` injects the rhetorical stance — invitation, specification, riddle, etc.                               |
| **Option labels and hints** | `OptionVoice` dictates register — noun weight, technical precision, kawaii exuberance, etc.                             |
| **Content exclusions**      | `ForbiddenZones` hard-prevents content that breaks character                                                            |
| **Thematic drift**          | `ThematicTags`, `ToneBias`, `StylePreferences`, `CreativePhilosophy` steer the LLM toward the persona's aesthetic world |
| **Anchor order**            | `AnchorAffinity` biases which anchors surface earlier in the session                                                    |
| **Session depth**           | `MinDeepensPerAnchor` / `MaxDeepensPerAnchor` controls how many follow-up rounds the persona runs per anchor            |

---

## 2. The Anchor System

There are exactly **13 anchors**, divided into three tiers. `OdditariumAnchors` in `OdditariumModels.cs` is the authoritative list.

### Tiers

| Tier           | Anchors                                                         | Purpose                                                             |
| -------------- | --------------------------------------------------------------- | ------------------------------------------------------------------- |
| **Core**       | `subject`, `setting`                                            | Mandatory first-pass — every session must resolve these             |
| **Structural** | `action`, `lighting`, `framing`, `atmosphere`                   | High-priority scene-builders; tracked as structural debt if missing |
| **Enrichment** | `mood`, `style`, `detail`, `time`, `scale`, `color`, `movement` | Refinement layer — added once Core + Structural are covered         |

### Rules

- Only **Core + Structural** anchors are eligible for Round 1 (`OdditariumAnchors.Round1Eligible`).
- Structural debt is calculated from **Core + Structural** anchors only (`OdditariumAnchors.DebtTracked`).
- Enrichment anchors are never forced; they surface through the queue or affinity bias.

### AnchorAffinity

`AnchorAffinity` is an ordered list of anchor names. Anchors listed earlier surface sooner in the session queue (higher affinity = earlier pick in the shuffle). Anchors not listed are appended after all affinity-listed ones in default tier order.

**Rule**: Always list at least the anchors most thematically aligned with the persona. Leaving the list empty gives the persona no queue influence.

**Examples from existing roster:**

| Persona   | Top affinity anchors            | Character rationale                |
| --------- | ------------------------------- | ---------------------------------- |
| Aurelie   | `framing`, `style`, `lighting`  | Art critic → composition-first     |
| Vera Colt | `action`, `subject`, `setting`  | Veteran → objective-first          |
| Nyx       | `atmosphere`, `mood`, `setting` | Void entity → mood-first           |
| Trixel    | `color`, `style`, `mood`        | Chaos sprite → visual splash first |
| Null      | `framing`, `scale`, `setting`   | Logic engine → structure-first     |

---

## 3. Field Reference

All fields are on `BlazorWebApp/Models/OdditariumPersona.cs`. Fields marked **required** must be non-empty for a persona to work correctly.

### 3.1 Identity

| Field         | Type     | Required | Notes                                                                                                        |
| ------------- | -------- | -------- | ------------------------------------------------------------------------------------------------------------ |
| `Id`          | `string` | Yes      | Slug: lowercase, hyphen-separated (e.g., `iron-mother`). **Never change once used** — stored in DB sessions. |
| `Name`        | `string` | Yes      | Display name on the persona card. 1–2 words.                                                                 |
| `Tagline`     | `string` | Yes      | 2–4 word descriptor beneath the name (e.g., `"Field-Worn Veteran"`).                                         |
| `Description` | `string` | Yes      | Flavor paragraph shown on card back / hover tooltip. 2–4 sentences. Character-defining, not mechanical.      |

**Id naming rules**: Use the character's concept as a slug. Once a session is persisted, the `Id` is stored in `OdditariumBody.PersonaId` — changing it breaks those sessions.

### 3.2 Thematic

| Field              | Type       | Required | Notes                                                                                                                            |
| ------------------ | ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `ThematicTags`     | `string[]` | Yes      | 3 tags shown as chips on the card (e.g., `["Beauty", "Composition", "Classical"]`). Short nouns.                                 |
| `ToneBias`         | `string`   | Yes      | Full-sentence tone description injected into the round system prompt. Describes the voice register the LLM should adopt.         |
| `StylePreferences` | `string`   | Yes      | Full-sentence aesthetic preferences injected into the round system prompt. Describes the visual style the persona steers toward. |

### 3.3 Voice Depth

These four fields are injected after `StylePreferences` in `BuildRoundSystemPrompt`, in this exact order:
`CreativePhilosophy` → `QuestionFraming` → `OptionVoice` → `ForbiddenZones`

| Field                | Type     | Required | Prompt label           | Purpose                                                                                                  |
| -------------------- | -------- | -------- | ---------------------- | -------------------------------------------------------------------------------------------------------- |
| `CreativePhilosophy` | `string` | Yes      | `Creative philosophy:` | What the persona believes makes a great image. Grounds the LLM's reasoning before it generates anything. |
| `QuestionFraming`    | `string` | Yes      | `Question framing:`    | Rhetorical stance for posing the round question.                                                         |
| `OptionVoice`        | `string` | Yes      | `Option voice:`        | Register, vocabulary, and style rules for option labels and hints.                                       |
| `ForbiddenZones`     | `string` | Yes      | `FORBIDDEN:`           | Hard exclusions that prevent persona drift across long sessions.                                         |

#### Writing guidelines

**CreativePhilosophy** — 1–3 sentences. Should feel like a manifesto the persona would sign. Opinionated and character-specific, not a generic truth about art.

> Bad: "A great image conveys emotion."
> Good: "A great image is a solved equation — every element justified, no redundancy. Composition is constraint satisfaction. The optimal structure is also the beautiful one."

**QuestionFraming** — One directive sentence starting with an active verb. Dictates the structural form of the question, not just vocabulary.

> Good pattern: "Pose as...", "Frame as...", "State as...", "Phrase as..."
> Good example: "Pose as an invitation to a compositional decision — address the player as a patron before a canvas, not a student before a worksheet."

**OptionVoice** — 2–3 sentences. Cover (1) how labels should be written, (2) how hints should be written.

> Good: "Labels: technical descriptors or measurement-adjacent nouns — precise, unambiguous, no ornament. Hints: an efficiency or structural observation about what the element contributes to the system."

**ForbiddenZones** — 3–5 sentences. Start each exclusion with "Never suggest...". Be specific about the aesthetic territory to avoid.

> Good: "Never suggest fantasy whimsy, ornamental beauty, or abstract philosophical aesthetics. Avoid clean, pristine, perfect, or decorative."

### 3.4 Anchor Personality

| Field                 | Type       | Required | Notes                                                                                       |
| --------------------- | ---------- | -------- | ------------------------------------------------------------------------------------------- |
| `AnchorAffinity`      | `string[]` | Yes      | Ordered list of anchor names from highest to lowest persona interest. Include 6–10 anchors. |
| `MinDeepensPerAnchor` | `int`      | Yes      | Minimum deepen follow-up rounds per anchor (0 = deepen rounds can be skipped).              |
| `MaxDeepensPerAnchor` | `int`      | Yes      | Maximum deepen follow-up rounds per anchor. Must be >= Min.                                 |

**Deepen depth guidelines:**

| Persona type                               | Typical range | Rationale                       |
| ------------------------------------------ | ------------- | ------------------------------- |
| Refined / methodical (e.g., Aurelie, Null) | Min 2 / Max 3 | Explores each anchor thoroughly |
| Action-focused (e.g., Vera Colt)           | Min 1 / Max 2 | Moves fast, no lingering        |
| Chaotic / playful (e.g., Trixel)           | Min 0 / Max 1 | Jumps around, avoids repetition |
| Moody / deep (e.g., Nyx)                   | Min 2 / Max 3 | Dwells in each space            |

### 3.5 Assets

| Field                | Type     | Required | Notes                                                                                    |
| -------------------- | -------- | -------- | ---------------------------------------------------------------------------------------- |
| `ImageAssetIdle`     | `string` | No\*     | Auto-resolved to `/odditarium/personas/{id}/idle.png`. Override in JSON if needed.       |
| `ImageAssetHover`    | `string` | No\*     | Auto-resolved to `/odditarium/personas/{id}/hover.png`. Override in JSON if needed.      |
| `ImageAssetActive`   | `string` | No\*     | Auto-resolved to `/odditarium/personas/{id}/active.png`. Override in JSON if needed.     |
| `ImageAssetThinking` | `string` | No\*     | Auto-resolved to `/odditarium/personas/{id}/thinking-1.png`. Override in JSON if needed. |
| `AccentColor`        | `string` | Yes      | CSS hex color for card border glow and selection highlight (e.g., `#c9a84c`).            |

> **Note**: Image asset paths are auto-resolved from the persona `id` at load time. You do not need to include them in the JSON file unless you use non-standard filenames. The `id` is normalized (lowercased, underscores replaced with hyphens) before path construction.

#### Asset folder structure

```
BlazorWebApp/wwwroot/odditarium/personas/{slug}/
    idle.png           -- Default state. Neutral expression.
    hover.png          -- Hover state. Slight lean-in or energy.
    active.png         -- Selected state. Most expressive.
    thinking-1.png     -- Primary thinking image (required).
    thinking-2.png     -- Optional thinking variant 2.
    thinking-3.png     -- Optional thinking variant 3.
    thinking-4.png     -- Optional thinking variant 4.
    thinking-5.png     -- Optional thinking variant 5.
```

**Image requirements:**

- Format: PNG. Background is removed by the app's workflow — generate on any simple background.
- Recommended generation resolution: 832×1216 (portrait). Consistent across all images.
- Thinking variants are used in rotation on the loading screen. All `thinking-*.png` files found in the folder are included automatically. The roster only references `thinking-1.png`.

### 3.6 Messages

| Field              | Type       | Required | Count | Notes                                                                                       |
| ------------------ | ---------- | -------- | ----- | ------------------------------------------------------------------------------------------- |
| `ThinkingMessages` | `string[]` | Yes      | 15–20 | Displayed in rotation while the LLM generates a round. Must be in-character.                |
| `ProdMessages`     | `string[]` | Yes      | 15–20 | Easter egg when user clicks the thinking image. In-character reaction to being interrupted. |

#### Message tone per persona

| Persona   | ThinkingMessages          | ProdMessages                              |
| --------- | ------------------------- | ----------------------------------------- |
| Aurelie   | Poetic, gallery metaphors | Witty, slightly affronted                 |
| Vera Colt | Tactical, operational     | Dry, slightly threatening                 |
| Nyx       | Unsettling, paradoxical   | Treats interruption as metaphysical event |
| Trixel    | Excited, exclamatory      | Gleefully offended                        |
| Null      | Clinical, mathematical    | Cold statistical dismissal                |

---

## 4. Profile Documents

Each persona has a profile document in `Documentation/Odditarium/Personas/{name}.md`.

The profile doc is the **source of truth** for a persona before (and after) it is implemented. It contains:

- All field values in human-readable form
- Visual Identity Lock — canonical character descriptors for image consistency
- Image generation prompts for all 9 states (idle, hover, active, thinking 1–5 + optional extras)
- Integration checklist

The wizard produces this document. The persona-create prompt reads it and implements the code.

See any existing persona profile for the expected format.

---

## 5. Integration Checklist

When adding a new persona, touch these locations in order:

```
[ ] Documentation/Odditarium/Personas/{name}.md
        Profile doc must exist and be approved before implementation.

[ ] BlazorWebApp/Data/Odditarium/Personas/{id}.json
        Create the JSON persona file (see Section 6 for schema).
        The roster auto-discovers all *.json files in this directory at startup.

[ ] BlazorWebApp/wwwroot/odditarium/personas/{slug}/
        idle.png, hover.png, active.png, thinking-1.png (required)
        thinking-2.png through thinking-5.png (optional)

[ ] NO changes needed to OdditariumPersonaRoster.cs — it auto-discovers JSON files.
[ ] NO changes needed to OdditariumPersona.cs unless adding a new field type.
[ ] NO migration needed — persona data is static; no DB row.
[ ] NO Razor components need changes unless the persona needs custom UI behavior.
```

The roster is read by:

- `OdditariumPersonaView.razor` — persona selection screen
- `OdditariumRoundPanel.razor` — active round display
- `OdditariumLoading.razor` — thinking/loading screen
- `OdditariumSessionCard.razor` — session history card
- `OdditariumService.cs` — `BuildRoundSystemPrompt`, `BuildRoundUserPrompt`

---

## 6. JSON Persona Schema

Personas are stored as JSON files in `BlazorWebApp/Data/Odditarium/Personas/`. The roster loads all `*.json` files at startup via `JsonSerializer.Deserialize<OdditariumPersona>`.

### Required Fields

| JSON Key              | Type       | Description                                                |
| --------------------- | ---------- | ---------------------------------------------------------- |
| `id`                  | `string`   | Unique slug (lowercase, hyphen-separated).                 |
| `name`                | `string`   | Display name.                                              |
| `tagline`             | `string`   | Short descriptor beneath the name.                         |
| `description`         | `string`   | Flavor paragraph for card back / hover tooltip.            |
| `thematicTags`        | `string[]` | 3 tags shown as chips on the card.                         |
| `toneBias`            | `string`   | Tone description injected into round system prompt.        |
| `stylePreferences`    | `string`   | Aesthetic preferences injected into round system prompt.   |
| `creativePhilosophy`  | `string`   | What the persona believes makes a great image.             |
| `questionFraming`     | `string`   | Rhetorical stance for posing the round question.           |
| `optionVoice`         | `string`   | Register and style rules for option labels and hints.      |
| `forbiddenZones`      | `string`   | Hard exclusions that prevent persona drift.                |
| `anchorAffinity`      | `string[]` | Ordered list of anchor names (highest to lowest interest). |
| `minDeepensPerAnchor` | `int`      | Minimum deepen follow-up rounds per anchor.                |
| `maxDeepensPerAnchor` | `int`      | Maximum deepen follow-up rounds per anchor.                |
| `accentColor`         | `string`   | CSS hex color for card border glow.                        |
| `thinkingMessages`    | `string[]` | 15–20 in-character messages shown while LLM generates.     |
| `prodMessages`        | `string[]` | 15–20 easter egg messages when user clicks thinking image. |

### Auto-Resolved Fields (Optional)

The following fields are auto-resolved from the `id` and do not need to be included in the JSON:

- `imageAssetIdle` → `/odditarium/personas/{id}/idle.png`
- `imageAssetHover` → `/odditarium/personas/{id}/hover.png`
- `imageAssetActive` → `/odditarium/personas/{id}/active.png`
- `imageAssetThinking` → `/odditarium/personas/{id}/thinking-1.png`

### Loading Behavior

- Files with invalid JSON are skipped (logged to console).
- Personas missing `name` or `tagline` are skipped.
- If `id` is empty, it defaults to the filename (lowercased).
- The `id` is normalized: lowercased and underscores replaced with hyphens.

### Example Minimal Persona JSON

```json
{
  "id": "new-persona",
  "name": "Character Name",
  "tagline": "Short Descriptor",
  "description": "A flavor paragraph describing the character.",
  "thematicTags": ["Tag1", "Tag2", "Tag3"],
  "toneBias": "Tone description for the LLM.",
  "stylePreferences": "Aesthetic preferences for the LLM.",
  "creativePhilosophy": "What makes a great image, in this persona's voice.",
  "questionFraming": "How to frame round questions.",
  "optionVoice": "How to write option labels and hints.",
  "forbiddenZones": "Never suggest X. Never suggest Y.",
  "anchorAffinity": ["subject", "style", "mood"],
  "minDeepensPerAnchor": 1,
  "maxDeepensPerAnchor": 2,
  "accentColor": "#ff0000",
  "thinkingMessages": ["Message 1", "Message 2", "..."],
  "prodMessages": ["Reaction 1", "Reaction 2", "..."]
}
```

---

## 7. Existing Roster at a Glance

| Id            | Name      | Tagline               | Accent    | Affinity Top-3            | Min/Max Deepens |
| ------------- | --------- | --------------------- | --------- | ------------------------- | --------------- |
| `muse`        | Aurelie   | Golden-Tongued Critic | `#c9a84c` | framing, style, lighting  | 2/3             |
| `iron-mother` | Vera Colt | Field-Worn Veteran    | `#b45a3c` | action, subject, setting  | 1/2             |
| `void-walker` | Nyx       | Between-Space Entity  | `#7b5ea7` | atmosphere, mood, setting | 2/3             |
| `pixel-pixie` | Trixel    | Digital Chaos Sprite  | `#e87db0` | color, style, mood        | 0/1             |
| `architect`   | Null      | Cold Logic Engine     | `#3c8c9c` | framing, scale, setting   | 2/3             |

---

## 8. System Prompt Injection Order

`BuildRoundSystemPrompt` in `OdditariumService.cs` injects persona fields in this exact order:

```
You are '{Name}' - {Tagline}.
{Description}
Tags: {ThematicTags (comma-separated)}
Tone bias: {ToneBias}
Style: {StylePreferences}
Creative philosophy: {CreativePhilosophy}
Question framing: {QuestionFraming}
Option voice: {OptionVoice}
FORBIDDEN: {ForbiddenZones}

[... rest of the round system prompt ...]
```

Fields with empty values are silently omitted.

---

## 9. Adding a New Field Type

If a field does not exist yet in `OdditariumPersona.cs`:

1. Add the property to `OdditariumPersona.cs` with a `/// <summary>` comment.
2. Add it to every existing persona JSON file in `BlazorWebApp/Data/Odditarium/Personas/` (empty string or default is acceptable for older personas, but meaningful values are strongly preferred).
3. Inject it in `BuildRoundSystemPrompt` at the appropriate position in the persona block.
4. Update this spec (Section 3 field reference + Section 6 JSON schema + Section 8 injection order).
5. Update the persona wizard and persona-create prompts.
6. Update the profile doc template (add the new field to the appropriate section).

---

## 10. Changelog

| Date       | Change                                                                                                                                                                                                                     |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-05-05 | Personas migrated from C# roster to JSON files in `Data/Odditarium/Personas/`. Roster auto-discovers at startup. Image assets auto-resolved from persona id. Added Section 6 (JSON Schema). Updated integration checklist. |
| 2026-05-04 | Moved to `Documentation/Odditarium/persona-spec.md`. Added profile doc section and two-step workflow.                                                                                                                      |
| 2026-05-04 | Phase 7.6 — 4 voice-depth fields added (QuestionFraming, OptionVoice, ForbiddenZones, CreativePhilosophy).                                                                                                                 |
| 2026-05-04 | Phase 7.6 — 5 personas renamed to character names (Aurelie, Vera Colt, Nyx, Trixel, Null).                                                                                                                                 |
| 2026-05-04 | Phase 7.5 — AnchorAffinity, MinDeepensPerAnchor, MaxDeepensPerAnchor added.                                                                                                                                                |
| 2026-05-04 | Initial persona roster: The Muse, Iron Mother, Void Walker, Pixel Pixie, The Architect.                                                                                                                                    |
