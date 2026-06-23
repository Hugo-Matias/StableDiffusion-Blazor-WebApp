---
agent: agent
description: >
  Implement an approved Odditarium persona profile document into the codebase.
  Reads the profile doc, creates the JSON persona file and asset folder.
  This is Step 2 of 2. Run persona-wizard first to create the profile doc.
tools:
  - search
  - read
  - edit
  - execute
---

# Odditarium Persona Create -- Implement

You are implementing a persona from an approved profile document.
Read the profile doc, present the implementation plan, get approval, then implement.

---

## Reference Materials

Read before starting:

- The profile doc provided by the user (path in `Documentation/Odditarium/Personas/`)
- `Documentation/Odditarium/persona-spec.md` -- field reference, system prompt injection order, integration checklist
- `BlazorWebApp/Models/OdditariumPersona.cs` -- field types and property declaration order
- `BlazorWebApp/Data/Odditarium/Personas/muse.json` -- example JSON persona file for format reference

---

## Phase 1: Load Profile

Ask the user for the profile doc path if not provided.

Read the profile doc fully. Extract all field values into a structured list.

Flag any required field that is empty before proceeding. Required fields:

- `Id`, `Name`, `Tagline`, `Description`
- `ThematicTags` (3 values), `ToneBias`, `StylePreferences`
- `CreativePhilosophy`, `QuestionFraming`, `OptionVoice`, `ForbiddenZones`
- `AnchorAffinity` (6 or more entries), `MinDeepensPerAnchor`, `MaxDeepensPerAnchor`
- `AccentColor`
- `ThinkingMessages` (15 or more), `ProdMessages` (15 or more)

If any required field is missing, list the gaps and ask the user to fill them in before continuing.

---

## Phase 2: Implementation Plan

Display this plan before writing any code:

```
=== IMPLEMENTATION PLAN: {Name} ({Id}) ===

Files to change:
[ ] CREATE  BlazorWebApp/Data/Odditarium/Personas/{id}.json
[ ] CREATE  BlazorWebApp/wwwroot/odditarium/personas/{id}/README.md

Asset folder: BlazorWebApp/wwwroot/odditarium/personas/{id}/
  Required image files (place manually after generation):
    idle.png, hover.png, active.png, thinking-1.png
  Optional:
    thinking-2.png through thinking-5.png

No other files need changes. Personas are loaded from JSON at startup.
```

Ask: **"Approve to implement."**

Do not proceed until the user approves.

---

## Phase 3: Implementation

### Step 1 -- Create JSON Persona File

Create `BlazorWebApp/Data/Odditarium/Personas/{id}.json` with the persona data.

The JSON uses camelCase property names matching the C# model. Image asset properties are **omitted** — they are auto-resolved from the persona `Id` at load time using this convention:

- `ImageAssetIdle`: `/odditarium/personas/{id}/idle.png`
- `ImageAssetHover`: `/odditarium/personas/{id}/hover.png`
- `ImageAssetActive`: `/odditarium/personas/{id}/active.png`
- `ImageAssetThinking`: `/odditarium/personas/{id}/thinking-1.png`

JSON structure:

```json
{
  "id": "{id}",
  "name": "{Name}",
  "tagline": "{Tagline}",
  "description": "...",
  "thematicTags": ["...", "...", "..."],
  "toneBias": "...",
  "stylePreferences": "...",
  "accentColor": "#{hex}",
  "thinkingMessages": ["..."],
  "prodMessages": ["..."],
  "anchorAffinity": ["...", "..."],
  "minDeepensPerAnchor": N,
  "maxDeepensPerAnchor": N,
  "questionFraming": "...",
  "optionVoice": "...",
  "forbiddenZones": "...",
  "creativePhilosophy": "..."
}
```

### Step 2 -- Asset folder README

Create `BlazorWebApp/wwwroot/odditarium/personas/{id}/README.md`:

```markdown
# {Name} Assets

Place the following transparent PNG files here (832x1216 portrait recommended):

Required:

- idle.png
- hover.png
- active.png
- thinking-1.png

Optional (thinking rotation):

- thinking-2.png through thinking-5.png

Image generation prompts are in:
Documentation/Odditarium/Personas/{profile-filename}.md
```

### Step 3 -- Verify

Ensure the JSON file is valid. Check for compile errors.
Fix any issues before reporting completion.

---

## Phase 4: Handoff

```
=== PERSONA IMPLEMENTED: {Name} ({Id}) ===

JSON:   BlazorWebApp/Data/Odditarium/Personas/{id}.json -- created
Assets: BlazorWebApp/wwwroot/odditarium/personas/{id}/ -- folder created with README

Next steps:
  1. Drop image assets into BlazorWebApp/wwwroot/odditarium/personas/{id}/
       Required: idle.png, hover.png, active.png, thinking-1.png
       Image gen prompts: Documentation/Odditarium/Personas/{profile}.md

  2. Mark the Integration Checklist in the profile doc as each item is completed.

  3. Update Documentation/Odditarium/persona-spec.md:
       - Section 6: add row to roster table
       - Section 9: add changelog entry
```
