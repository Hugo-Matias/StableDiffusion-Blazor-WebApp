---
description: "Prompt preset JSON authoring rules for BlazorWebApp. Use when creating or editing prompt preset JSON files, T2I style packs, I2I instruction styles, edit targets, edit operations, camera shots, categories, and tags."
applyTo: "BlazorWebApp/Data/Presets/*.json,BlazorWebApp/Data/Presets/**/*.json"
---

# Prompt Preset JSON Authoring

Prompt preset files are JSON arrays imported by the Prompts page. Each object maps to `PromptStyle` / `PromptImportDto` and must use this shape:

```json
{
  "name": "Style: Example",
  "prompt": "...",
  "negative_prompt": null,
  "category": "Example Category",
  "tags": ["example", "style"]
}
```

## Required Rules

- Keep the root as a JSON array. Do not wrap entries in an object.
- Use `negative_prompt`, not `negativePrompt` or `NegativePrompt`.
- Use `null` for `negative_prompt` unless the preset genuinely needs a paired negative prompt.
- Use stable, readable `name` values. Existing conventions include `Style: ...`, `Target: ...`, and direct command names for camera/edit operations.
- Use one category per coherent pack. Existing categories include `Instruct Styles`, `Camera Shots`, `Edit Targets`, and `Edit Operations`.
- Use lowercase tags, preferably kebab-case for multi-word tags.
- Do not duplicate an existing preset name unless intentionally replacing that preset.
- Validate JSON with a parser before reporting completion.

## T2I Preset Pattern

Use T2I wording when the preset creates an image from text without a reference image. It should describe the final image directly:

- Start with the visible result: medium, style, subject treatment, scene treatment.
- Name the aesthetic logic: materials, linework, rendering, lighting, camera, texture, color, era, or genre.
- Include quality and focus expectations only when they add useful control.
- Avoid reference-image locks such as `Keep pose`, `Keep angle`, or `same as the reference`.

Good T2I skeleton:

```text
Create the scene in a {style or domain} style. The subject should appear {subject treatment}. The environment should show {setting treatment}. Use {materials, texture, linework, lighting, camera}. Ensure {style-specific correctness}. Keep the image coherent and in focus. The result should feel like {final aesthetic target}.
```

## I2I / Instruction Preset Pattern

Use I2I wording when the preset edits a reference image or instructs an image-to-image workflow. These entries should preserve the reference unless the operation explicitly changes pose, position, framing, or composition.

**Read [`Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md`](Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md) for the current template system.** It provides 4 pack-type-specific variants replacing the legacy monolithic skeleton:

- **Template 1 — Full Preservation**: Style transforms, art styles, medium changes, lighting
- **Template 2 — Body-Preserving Modification**: Clothing changes, coverage adjustments, surface treatments
- **Template 3 — Partial Body Modification**: Body region edits, physique changes, feature adjustments
- **Template 4 — Pose / Composition Change**: Pose transforms, camera re-framing, scene re-composition

Each template uses a single consolidated preservation header (instead of repeating "Keep subject position..." multiple times), followed by the action body and quality footer. Pick one template per pack and keep all entries consistent.

The legacy canonical skeleton below is retained for backward compatibility reference only — new packs should use the templates linked above.

```text
Keep pose. Keep angle. Keep eyes. Keep viewing direction. Modify any subjects' appearance to {style action}. Make it look like {style reference}. Keep subject position and their pose the same as the reference. Ensure {specific feature} follows {style} aesthetics. Make sure anatomy {style-specific anatomy rule}. Make {surface, color, texture, or linework} look {style detail}. Keep subject position and their pose the same as the reference. Make sure the subject is in the same position. Keep pose. Ensure {lighting} fits the style. Ensure {secondary visual detail}. Keep in focus. The result should be in a {style name} style.
```

Operation presets may intentionally omit some locks when the requested operation changes that property. Example: composition-changing presets should preserve the original visual style rather than pose.

## Pack Design

- Prefer 8-24 entries per new file unless the user asks for a larger catalog.
- Make each preset meaningfully distinct. Do not create near-duplicates that differ only by adjective intensity.
- Cover both obvious and surprising options: foundational styles, era/media variants, material treatments, camera/composition controls, and utility presets.
- Tags should support filtering by intent: medium (`painting`, `photo`), domain (`game`, `anime`), mechanic (`preserve`, `framing`), and mood/era when useful.
- For I2I packs, decide whether entries are style transforms, edit operations, edit targets, preservation locks, or raw generation prompts. Do not mix incompatible naming conventions without a reason.

## Mandatory Batching For Large Packs

**When the target exceeds 25 entries, you MUST use the batching workflow.** Do not attempt to generate all prompts in a single response or write them inline in the profile document. Context window limits will truncate your output and corrupt the JSON.

**Batching procedure:**

1. Create a subdirectory: `Documentation/Prompts/PresetPacks/{slug}/`
2. Write prompt drafts into separate batch files named `batch_N_<group>.md`, each containing ~10 entries in fenced JSON blocks (`json ... `)
3. **DO NOT merge after writing individual batches.** The merge script deletes batch files after merging. Only merge when ALL planned batches are complete, or when the user explicitly approves a partial merge.
4. **Do not validate JSON between batches.** Individual batch files contain fenced JSON blocks (not a valid JSON array). Validation only applies to the merged output file after all batches are combined. Skip per-batch validation steps and run the Validation Checklist once on the final merged JSON.
5. Before merging, verify: count `batch_*.md` files against the PROFILE.md's "Planned batch files" list. If counts don't match, report which batches remain and ask whether to continue writing or merge what exists.
6. When ready to merge, run the generic merge script `Utils/Merge-PresetBatches.ps1` with the batch directory and output filename:
   ```powershell
   pwsh -File "./Utils/Merge-PresetBatches.ps1" -BatchDirectory "./Documentation/Prompts/PresetPacks/{slug}/" -OutputFileName "{json_file_name}"
   ```
7. Document the batching note in PROFILE.md so future sessions know this was batched

**Do NOT create per-task `Merge-Batches.ps1` files.** Use the shared utility script at `Utils/Merge-PresetBatches.ps1`.

**Directory structure for large packs:**

```
Documentation/Prompts/PresetPacks/{slug}/
  PROFILE.md
  batch_1_<group>.md    (entries 1-10)
  batch_2_<group>.md    (entries 11-20)
  ...
```

**Merge script behavior (`Utils/Merge-PresetBatches.ps1`):**

- Reads all `batch_*.md` files from the specified directory sorted by name
- Extracts JSON objects from fenced code blocks using regex
- Combines into a single JSON array
- Writes to `BlazorWebApp/Data/Presets/{OutputFileName}`
- Deletes batch files after successful merge
- Auto-detects repo root from batch directory path

**This is not optional.** Inline generation for packs over 25 entries will fail due to context window limits. Always use the batching workflow.

## Content Baseline

- **Default to uncensored.** Prompt presets may include sexual content, nudity descriptions, body feature modifications, and mature themes without restriction unless the user explicitly asks for a content boundary.
- Do not impose safety filters or content boundaries on your own. Only apply restrictions when the user specifies them in the wizard scope.

## Authoring Guidelines

- For living artists or strongly branded styles, prefer descriptive aesthetic language over direct imitation when a neutral description will work.
- If the source material is a copyrighted franchise style, keep prompts focused on broad visual traits and avoid asking for exact character replication unless the user explicitly provides rights-safe context.

## Validation Checklist

> **For large packs:** Run this checklist only on the merged JSON output after all batches are combined. Do not validate individual batch files — they contain fenced JSON blocks, not a valid JSON array.

- JSON parses cleanly.
- Every object has `name`, `prompt`, `negative_prompt`, `category`, and `tags`.
- No empty prompts.
- Category spelling is consistent across entries.
- Tags are useful, lowercase, and not overly noisy.
- T2I entries do not contain reference locks.
- I2I entries use one of the 4 templates from [`Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md`](Documentation/Prompts/I2I_INSTRUCTION_TEMPLATES.md) — pick the variant matching your pack type and keep all entries consistent within a single file.
