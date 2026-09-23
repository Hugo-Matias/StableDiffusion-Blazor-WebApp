---
agent: agent
description: >
  Implement an approved prompt preset pack profile into BlazorWebApp/Data/Presets.
  Creates a new importable prompt preset JSON file or appends missing entries to an existing one.
  This is Step 2 of 2. Run prompt-preset-wizard first to create the profile doc.
tools:
  - search
  - read
  - edit
  - execute
---

# Prompt Preset Create -- Implement

You are implementing an approved prompt preset pack profile. Read the profile doc, write full prompt drafts to batch files (for large packs), merge them into the final JSON, and validate the output. For small packs (≤25 entries), you can write the JSON directly inline.

---

## Reference Materials

Read before starting:

- The profile doc provided by the user, usually in `Documentation/Prompts/PresetPacks/`
- `.github/instructions/prompt-presets.instructions.md` -- JSON shape and wording rules
- `BlazorWebApp/Data/Presets/instruct_styles.json` -- I2I style transform examples
- `BlazorWebApp/Data/Presets/camera_shot_prompts.json` -- camera/composition examples
- `BlazorWebApp/Data/Presets/edit_target_prompts.json` -- target selector examples
- `BlazorWebApp/Data/Presets/edit_op_prompts.json` -- operation examples
- `BlazorWebApp/Models/PromptStyle.cs` -- serialized model fields
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor` -- import DTO and import behavior

---

## Phase 1: Load Profile

Ask the user for the profile path if not provided. You can also accept a backlog file path (like `EROTIC_GAP_BACKLOG.md`) with a gap identifier (like "Set K") and locate the corresponding PROFILE.md.

Read the profile fully. Extract:

- `Pack goal`
- `PackType`
- `Mode`
- `Action`
- `Target JSON`
- `Category`
- `Naming convention`
- Target size
- Batch breakdown (for large packs) or JSON entries from the `JSON Entries` block (for small packs)

Flag any required field that is missing before proceeding.

For expansions, read the existing target JSON and compare names case-insensitively. List duplicates and ask whether to skip, replace, or rename before editing.

---

## Phase 2: Implementation Plan

Display this plan before writing files:

```text
=== IMPLEMENTATION PLAN: {Pack Name} ===

Files to change:
[ ] {CREATE or UPDATE} BlazorWebApp/Data/Presets/{file}

Action: {new-file or expand-existing}
Category: {category}
Entries to add: {count}
Duplicates: {none or list}
Pack size: {small (≤25) / large (>25)}

For small packs: Write JSON directly.
For large packs: Write batch files following profile breakdown, then merge using Utils/Merge-PresetBatches.ps1.

No code changes are required. The Prompts page imports preset JSON arrays through its import action.
```

Ask: **"Approve to implement."**

Do not proceed until the user approves.

---

## Phase 3: Implementation

### Small Packs (≤25 entries) — Direct JSON Write

For small packs with inline JSON in the profile, create `BlazorWebApp/Data/Presets/{file}` as a JSON array of approved entries.

For an expansion:

- Preserve existing entries and formatting where practical.
- Append approved new entries unless the user requested a different placement.
- Do not rewrite unrelated entries.
- Do not duplicate existing names.

### Large Packs (>25 entries) — Batch Writing And Merge

**Step 1 -- Write Batch Files**

Follow the batch breakdown documented in the PROFILE.md's "Batching Note" section. For each planned batch:

1. Use `edit` to create `Documentation/Prompts/PresetPacks/{slug}/batch_N_<group>.md`
2. Each batch file contains a markdown header and individual JSON entries wrapped in \`\`\`json fenced blocks
3. Batch file format:

   \`\`\`markdown

   # Batch N: <Group Name> (Entries X–Y)

   ## Style: Entry Name

   \`\`\`json
   {
   "name": "...",
   "prompt": "...",
   "negative_prompt": null,
   "category": "...",
   "tags": ["..."]
   }
   \`\`\`
   \`\`\`

4. Draft full prompts using the appropriate I2I template from [`Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md`](Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md):
   - **Template 1** — Full Preservation (style transforms, art styles, lighting)
   - **Template 2** — Body-Preserving Modification (clothing changes, surface treatments)
   - **Template 3** — Partial Body Modification (body region edits, feature adjustments)
   - **Template 4** — Pose / Composition Change (pose transforms, camera shifts)

5. Write batches iteratively — create a few batch files, then continue with the next set if needed. Ask the user which batches to write if they want to spread across sessions.

**Step 2 -- Merge Batches**

**DO NOT merge batches automatically after writing each batch file.** The merge script deletes batch files after merging, which destroys your work if remaining batches are not yet written. Only merge when ONE of these conditions is met:

1. **All planned batches are complete** — count the `batch_*.md` files in the directory and compare against the PROFILE.md's "Planned batch files" list. If counts match, proceed to merge.
2. **User explicitly approves partial merge** — ask the user whether to merge the current subset of batches. Only run the merge script after receiving explicit approval.

Before merging, verify completeness:

```text
Batches planned in PROFILE.md: {N}
Batch files present: {M}
Missing batches: {list any batch_N_*.md files not yet created}
```

If `{M}` < `{N}`, report which batches remain and ask whether to continue writing or merge what exists. Do not run the merge script until confirmed.

When ready to merge, run the shared merge script:

```powershell
pwsh -File "./Utils/Merge-PresetBatches.ps1" -BatchDirectory "./Documentation/Prompts/PresetPacks/{slug}/" -OutputFileName "{json_file_name}"
```

**Do NOT create per-task merge scripts.** Use the shared utility at `Utils/Merge-PresetBatches.ps1`.

### Validation (All Packs)

Validate with a JSON parser. Then check:

- Root is an array.
- All entries have required fields.
- `negative_prompt` is present, even when null.
- Category is consistent unless the profile intentionally uses more than one.
- Tags are arrays of lowercase strings.
- T2I entries do not include `same as the reference`, `Keep pose`, or similar reference locks.
- I2I entries include preservation locks unless they are raw generation or composition-changing operations.

Fix any issues before reporting completion.

---

## Phase 4: Handoff

Update the PROFILE.md status to "Complete" and add merge date. Report:

**For small packs:**

```text
=== PROMPT PRESET PACK IMPLEMENTED: {Pack Name} ===

JSON: BlazorWebApp/Data/Presets/{file} -- {created or updated}
Entries added: {count}
Category: {category}

Validation:
- JSON parsed successfully
- Required fields present
- Duplicate names checked

Next step:
- Import the JSON from the Prompts page if you want these presets in the app database.
```

**For large packs:**

```text
=== PROMPT PRESET PACK IMPLEMENTED: {Pack Name} ===

JSON: BlazorWebApp/Data/Presets/{file} -- {created or updated}
Entries added: {count} (from {N} batch files)
Category: {category}
Merge script: Utils/Merge-PresetBatches.ps1 (shared utility)

Validation:
- JSON parsed successfully
- Required fields present
- Duplicate names checked

Batch files remain in: Documentation/Prompts/PresetPacks/{slug}/
(Keep for future reference or re-merging if entries need edits.)

Next step:
- Import the JSON from the Prompts page if you want these presets in the app database.
- To add more batches later, run prompt-preset-create again with the same profile path.
```
