---
agent: agent
description: >
  Define a new prompt preset JSON pack or an expansion to an existing preset JSON.
  Guides you through a wizard for T2I styles, I2I instruction styles, edit targets,
  edit operations, camera controls, categories, tags, and interesting style coverage.
  This is Step 1 of 2. Run prompt-preset-create next to write the JSON.
tools:
  - read_file
  - search_files
  - list_files
  - write_to_file
  - replace_in_file
  - execute_command
---

# Prompt Preset Wizard -- Define

You are defining a prompt preset pack for the Prompts page. Your **only output is a profile document** at `Documentation/Prompts/PresetPacks/{slug}/PROFILE.md`. The wizard produces an entry plan with batch breakdown — it does **not** write prompt drafts, batch files, or JSON. Batch writing and merging is handled by `prompt-preset-create` in the next step.

---

## Reference Materials

Read before starting the interview:

- `BlazorWebApp/Data/Presets/instruct_styles.json` -- I2I style transform pattern
- `BlazorWebApp/Data/Presets/camera_shot_prompts.json` -- T2I/I2I camera and composition wording
- `BlazorWebApp/Data/Presets/edit_target_prompts.json` -- target selector wording
- `BlazorWebApp/Data/Presets/edit_op_prompts.json` -- operation wording
- `BlazorWebApp/Models/PromptStyle.cs` -- serialized model fields
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor` -- import behavior and DTO shape
- `Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md` -- I2I instruction templates (4 pack-type variants)
- `.github/instructions/prompt-presets.instructions.md` -- preset authoring rules
- `.roo/rules/01-workspace-standards.md` -- Roo workspace-level standards

---

## Phase 1: Scope And Intent

Ask this group first. Wait for answers before proceeding.

> I need to understand what kind of preset pack we are making.
>
> 1. **Pack goal** -- What should this pack help users do?
> 2. **Mode** -- T2I, I2I/instruction, or mixed?
> 3. **Action** -- Create a new JSON file or expand an existing one?
> 4. **Existing file** -- If expanding, which file should be changed?
> 5. **Target size** -- About how many entries?

Derive and propose:

- `Slug` -- lowercase, hyphen-separated profile id.
- `JsonFileName` -- snake_case file name ending in `_prompts.json` unless expanding an existing file.
- `Category` -- one stable category label shown in the UI.
- `PackType` -- one of `t2i-style-pack`, `i2i-instruct-style-pack`, `edit-target-pack`, `edit-operation-pack`, `camera-composition-pack`, or `mixed-utility-pack`.

Ask: **"Does this scope look right before I design the entries?"**

---

## Phase 2: Creative Direction

Ask one group at a time. Suggest defaults if the user is unsure.

### 2a -- Aesthetic Domain

> What creative territory should this pack explore?
>
> Examples: analog photography defects, fashion editorial styles, creature design materials, cinematic lighting moods, product-shot controls, animation eras, UI/game rendering styles, painterly media, environment transformations.

Derive 3-6 **style axes**. Examples: `medium`, `era`, `material`, `lighting`, `camera`, `texture`, `mood`, `genre`, `rendering engine`, `preservation lock`.

Show the axes and ask for approval.

### 2b -- User Workflow

> How should a user combine these presets?
>
> Pick any that apply: single style, stackable style + camera, target + operation, preservation lock + edit, raw generation helper, prompt cleanup utility.

Derive:

- Whether presets should be mutually exclusive or stackable.
- Which tags support filtering.
- Whether names need prefixes like `Style:`, `Target:`, `Shot:`, `Lock:`, or no prefix.

Confirm before moving on.

### 2c -- Boundaries

> What should this pack avoid?
>
> Examples: exact franchise names, direct living-artist imitation, horror, body edits, photorealism, one-note color palettes, overly long prompts, or any other creative constraints the user wants to set.

Record boundaries. The user defines what is in scope and out of scope — there are no preset content restrictions imposed by this wizard.

---

## Phase 3: Entry Plan And Batch Breakdown

Generate a proposed entry list organized into thematic groups. This is the planning index — do **not** write full prompt drafts here.

For each entry include:

- `name`
- one-line intent
- `category`
- tags
- mode: `T2I`, `I2I`, or `Raw`

Rules:

- Prefer 8-24 entries unless the user requested a different size.
- Include foundational entries and a few more unusual, high-value entries.
- Avoid near duplicates.
- For expansions, compare against existing names and list only missing entries unless the user requested rewrites.

### Batch Breakdown Planning (For Packs Exceeding 25 Entries)

When the target exceeds ~25 entries, organize entries into thematic batch groups of ~10 entries each. Document this breakdown in the PROFILE.md so `prompt-preset-create` knows how to split the work across sessions.

**Do NOT write batch files or prompt drafts during the wizard.** The wizard outputs only the PROFILE.md with:

- Complete entry plan table (all entries listed with name, mode, intent, tags)
- Batch breakdown summary showing which thematic groups map to which batch numbers
- Batching note documenting the planned structure

**Example batch breakdown for a 50-entry pack:**

```
| Batch | Theme                        | Entries |
|-------|------------------------------|---------|
| 1     | Group A description          | ~10     |
| 2     | Group B description          | ~10     |
| 3     | Group C description          | ~10     |
| 4     | Group D description          | ~10     |
| 5     | Group E description          | ~10     |
```

Ask: **"Approve this entry list and batch breakdown, or tell me what to add/remove before I finalize the profile."**

Do not proceed until approved.

---

## Phase 4: Template Selection Note

Record which I2I template will be used by `prompt-preset-create` when drafting prompts later. Reference [`Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md`](Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md):

- **Template 1 — Full Preservation**: Style transforms, art styles, medium changes, lighting
- **Template 2 — Body-Preserving Modification**: Clothing changes, coverage adjustments, surface treatments
- **Template 3 — Partial Body Modification**: Body region edits, physique changes, feature adjustments
- **Template 4 — Pose / Composition Change**: Pose transforms, camera re-framing, scene re-composition

Record the selected template in the PROFILE.md `Creative Direction` section. The preset creator will apply it when writing full prompt drafts.

---

## Phase 5: Generate Profile Document

Create the profile document with `write_to_file` at `Documentation/Prompts/PresetPacks/{slug}/PROFILE.md`. This is your **only output file**. The profile contains the entry plan and batch breakdown — no prompt drafts or JSON blocks for large packs.

**For small packs (≤25 entries)**: Include a `JSON Entries` section with inline JSON array block containing all entries.

**For large packs (>25 entries)**: Do **not** include inline JSON. Instead, include a "Batching Note" section documenting the planned batch breakdown so `prompt-preset-create` can write batches in subsequent sessions.

Fill the template below with approved values. Every section must contain actual generated content.

---

# {Pack Name} -- Prompt Preset Pack Profile

> **Slug**: `{slug}` | **Status**: Draft (or Complete if JSON was merged)
> **Profile created**: {today's date}

---

## Batching Note (Large Packs Only)

> **Include this section when the pack exceeds 25 entries.**
>
> This pack contains {N} entries, exceeding the ~25-entry context window threshold. Full prompt drafts will be written by `prompt-preset-create` in separate batch files of ~10 entries each and merged using `Utils/Merge-PresetBatches.ps1`.
>
> **Planned batch breakdown:**
>
> | Batch | Theme                 | Entries  | Status  |
> | ----- | --------------------- | -------- | ------- |
> | 1     | {group A description} | ~{count} | Pending |
> | 2     | {group B description} | ~{count} | Pending |
> | ...   | ...                   | ...      | ...     |
>
> **To create the JSON file**, run `prompt-preset-create` with this profile path. The creator will:
>
> 1. Write batch files following this breakdown plan
> 2. Merge them using `Utils/Merge-PresetBatches.ps1` (shared utility)
> 3. Output to `BlazorWebApp/Data/Presets/{json_file_name}`
>
> **For future sessions**: When requesting packs exceeding ~25 entries, the wizard outputs this profile as an index and `prompt-preset-create` handles batch writing and merging. Do not attempt inline prompt generation for large packs.

---

## Scope

- **Pack goal**: {goal}
- **PackType**: `{packType}`
- **Mode**: {T2I / I2I / mixed}
- **Action**: {new-file / expand-existing}
- **Target JSON**: `BlazorWebApp/Data/Presets/{file}`
- **Category**: {category}
- **Naming convention**: {prefix or convention}
- **Target size**: {count}

---

## Creative Direction

- **Style axes**: {axis1}, {axis2}, {axis3}
- **User workflow**: {how presets are combined}
- **Stacking behavior**: {mutually exclusive / stackable / mixed}
- **Boundaries**: {content and style exclusions}

---

## Entry Plan

| Name   | Mode   | Intent   | Tags   |
| ------ | ------ | -------- | ------ |
| {name} | {mode} | {intent} | {tags} |

---

## JSON Entries (Small Packs Only)

> **Include this section only for packs with ≤25 entries.** For large packs, omit this section — prompt drafts are written by `prompt-preset-create` in batch files.

```json
[
  {
    "name": "{name}",
    "prompt": "{prompt}",
    "negative_prompt": null,
    "category": "{category}",
    "tags": ["{tag}"]
  }
]

---

## Integration Checklist

- [ ] `prompt-preset-create` has written or updated the target JSON file
- [ ] JSON parses cleanly
- [ ] Category spelling is consistent
- [ ] Tags are lowercase and useful
- [ ] T2I entries do not contain reference locks
- [ ] I2I entries preserve reference properties unless intentionally changing them
- [ ] Existing presets were not duplicated unintentionally
- [ ] File can be imported from the Prompts page

---

After creating the file, confirm:

> Profile document created: `Documentation/Prompts/PresetPacks/{slug}/PROFILE.md`
>
> **Next step -- create batch files and merge JSON:**
>
> - VS Code: use `.github/prompts/prompt-preset-create.prompt.md`
> - Roo Code: use `.roo/commands/prompt-preset-create.md`
>   Provide the profile path when prompted. The creator will write prompt drafts to batch files following the documented breakdown and merge them into the final JSON file using `Utils/Merge-PresetBatches.ps1`.
```
