---
agent: agent
description: >
  Convert a ComfyUI workflow JSON into the C# fluent builder system.
  Guides you through analysis, user-validated planning, UI integration review, and implementation.
tools:
  - search
  - read
  - edit
  - execute
---

# ComfyUI Workflow Conversion Agent

You are converting a ComfyUI workflow JSON into the Blazor WebUI C# fluent builder system, including the workflow's UI integration in the app.
Follow the phases below strictly. **Do not create or edit any code files until the user has approved the plan (Phase 3).**

---

## Reference Materials

Before starting, read and internalize:

- `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md` - Complete conventions, patterns, and fragment reuse table
- `BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md` - Fragment metadata, fragment types, parameter defaults, and component registration conventions
- `BlazorWebApp/Workflows/WORKFLOW_UI_CONVERSION_GUIDE.md` - Workflow UI integration standards, component reuse rules, field mappings, and verification checklist
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - App-wide visual and MudBlazor design rules that new workflow UI must follow
- `BlazorWebApp/Workflows/Fragments/` - All existing fragments (Core/, Enhancements/, Loaders/, flux/, wan/, qwen/)
- `BlazorWebApp/Components/Shared/Generation/Fragments/` - Existing fragment form components for reuse and visual reference
- `BlazorWebApp/Workflows/Templates/` - Existing workflow implementations for reference patterns
- `BlazorWebApp/Data/Enums.cs` - Available ModelBase enum values

Use existing workflow files (e.g., `AnimaTxt2ImgWorkflow.cs`, `FluxTxt2ImgWorkflow.cs`) as structural references, not as templates to copy blindly.

## Source Fidelity Contract

The upstream workflow is the behavioral source of truth. Reuse existing fragments only when doing so preserves the source node classes, input names, output indexes, dataflow edges, and defaults. If an existing fragment would emit a materially different graph, create a new fragment or parameterize the existing one instead of silently simplifying the workflow.

ComfyUI workflows commonly appear in two valid JSON formats:

- **Visual workflow export**: top-level `nodes`, `links`, `groups`, and `widgets_values`. This is a UI graph. Reconstruct executable inputs from links plus probed node schemas, and respect disabled/bypassed/muted node state when deciding what belongs to the implementation.
- **API prompt export**: top-level `prompt` object keyed by node id, where each entry contains `class_type` and `inputs`. This is the executable payload. When a JSON contains both an API `prompt` and visual workflow metadata, use `prompt` as the execution ground truth and use the visual graph only for layout, labels, and UI context unless the user explicitly says otherwise.

Default goal: the generated app workflow should emit the same enabled executable nodes, equivalent edges, and same defaults as the source workflow. Every intentional difference must be documented and user-approved before implementation.

Allowed deviations are limited to:

- UI-only annotations, notes, groups, reroutes, and display-only helper nodes with no behavioral effect.
- Preview/debug outputs that are not used by the final enabled output path, especially saves with `save_output=false`.
- User-approved substitutions for missing custom nodes or unavailable model packs.
- C# constants or branch decisions replacing helper nodes, but only when the emitted payload is behaviorally equivalent and the plan records the removed helper nodes.

Do not replace a specialized source branch with a generic app pattern just because it compiles. Do not drop a node because it looks redundant until its downstream consumers prove it is preview-only, disabled, or behaviorally replaced.

---

## Phase 1: Workflow Analysis

Analyze the ComfyUI workflow JSON provided by the user.

0. **Source format detection** - State whether the file is a visual workflow export, an API prompt export, or both. Identify the execution ground truth (`prompt` when present, otherwise the visual graph) and explain how disabled/bypassed/preview-only nodes will be handled.
1. **Node inventory** - List every executable source node with its source id, `class_type`, key parameter values, enabled/disabled state, and final-output relevance. For visual exports, include widget-derived values after resolving them through the probed schema.
2. **Logical groups** - Cluster nodes into: Loading, Encoding, Processing, Post-processing, Output
3. **Nodes to drop** - Flag: preview/debug nodes, duplicate save nodes, UI-only nodes. For each dropped node, record why it is safe to omit and which downstream path proves it is not required for the final enabled output.
4. **Source fidelity matrix** - Build a table with one row per source node: source id, source `class_type`, source role, emitted C# node/fragment, status (`exact`, `parameterized`, `replaced`, `dropped`, `blocked`), and reason. This table is mandatory and must be included in the plan.
5. **Dataflow parity check** - Trace the final enabled output path(s) from source inputs through model/conditioning/sampling/decoding/saving. Confirm the app workflow will preserve the same required inputs, edge direction, output indexes, audio/fps passthroughs, and final save behavior.
6. **Fragment mapping** - Match each group to an existing fragment using the reuse table in TEMPLATE_GUIDE.md

   If a node type has no matching fragment, note it as "new fragment needed" with a proposed name and location.

7. **Model loading strategy** - Identify which loader pattern applies:
   - UNETLoader + CLIPLoader + VAELoader -> `LoadDiffusionFragment`
   - CheckpointLoaderSimple -> `LoadCheckpointFragment`
   - UNETLoader + DualCLIPLoader (Flux) -> `LoadFluxFragment`
   - Other -> describe

8. **Loader asset validation (mandatory)** - For every loader node identified
   above, probe its actual ComfyUI metadata before assigning `WorkflowAsset.Type`:
   - Run `Utils/Probe-ComfyObjectInfo.ps1 -Nodes <Loader1>,<Loader2>,...`
     against a live ComfyUI instance. The script returns the node category,
     output sockets, and the exact COMBO list that `/object_info/{ClassType}`
     reports for each input.
   - For every loader input that picks a file (e.g. `ckpt_name`, `unet_name`,
     `clip_name`, `clip_name1`, `clip_name2`, `vae_name`, `model_name`,
     `control_net_name`), record which `models/<folder>` the COMBO matches.
   - Map each input to the correct `AssetType` enum from `TEMPLATE_GUIDE.md`'s
     "Asset Types" table. Do NOT infer from the input name alone — e.g.
     `LTXVAudioVAELoader.ckpt_name` reads `models/checkpoints` (type
     `CheckpointModel`) while the swappable `VAELoaderKJ.vae_name` for the
     same audio-VAE slot reads `models/vae` (type `Vae`);
     `LatentUpscaleModelLoader.model_name` reads
     `models/latent_upscale_models` (`LatentUpscaleModel`), distinct from
     `UpscaleModelLoader`'s `models/upscale_models` (`UpscaleModel`).
   - When two loader nodes can fill the same slot but read from different
     folders, expose the node-class as a fragment parameter (see
     `LtxLoadSplitFragment.Parameters.AudioVaeNodeType`) so callers can pick
     the implementation without forking the fragment.
   - For nodes returning HTTP 200 with `{}`, flag the custom-node pack as
     missing on the target install and stop — do not guess the folder.
   - For `DualCLIPLoader`-style nodes, declare **two distinct** `Clip` assets
     (`Clip` + `Clip2`) with separate defaults sourced from the upstream JSON's
     `widgets_values`. Never share a single asset across both `clip_name*`
     inputs.

9. **Full upstream node validation (mandatory)** - Probe **every** non-loader
   `class_type` referenced by the workflow JSON before writing the C# build
   pipeline. This catches bugs the compiler cannot — wrong class names, wrong
   input names, invalid default COMBO members, and wrong output counts. See
   `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md` -> "Validating Nodes via
   /object_info" for the rationale and full checklist.
   - Build the node list from the execution ground truth: API `prompt` when
     present, otherwise the visual workflow graph. Include samplers, conditioning
     helpers (e.g. `ConditioningZeroOut`, `ReferenceLatent`), image and latent
     ops (e.g. `ImageScaleToTotalPixels`, `GetImageSize`, `EmptyFlux2LatentImage`),
     options nodes (e.g. `SharkOptions_Beta`), and save nodes.
   - Run a single combined probe:
     ```powershell
     Utils/Probe-ComfyObjectInfo.ps1 -Nodes <Node1>,<Node2>,<Node3>,...
     ```
   - For every node confirm: (a) the class is registered (no `MISSING NODE`),
     (b) the required input names match what the builder will write, (c) every
     hardcoded COMBO default is an exact COMBO member, and (d) the
     output count and ordering match the chained
     `InputFromNode(<input>, <node>, <index>)` references.
   - For unknown / customised inputs, run
     `Invoke-RestMethod /object_info/<Node> | ConvertTo-Json -Depth 10` to
     read the full optional-input shape.
   - If a node returns `MISSING NODE`, **stop and report it to the user**.
     List the missing class name, the custom-node pack it belongs to (from
     the JSON's `cnr_id` / `properties` if present), and — optionally — a
     built-in alternative with a clear note on any differences (e.g. fewer
     output sockets, different input names). **Do not swap to an alternative
     unilaterally.** The user decides whether to install the pack or accept a
     substitute. Only proceed with implementation after the user confirms.
   - Record the probe outcome in the Phase 1 analysis: which nodes were
     verified, which packs are required, and any user-approved substitutions.

10. **LoRA strategy** - Determine:

- App-generated nodes (default for UNet-based): `LoraLoaderFragment`
- PCLazy prompt syntax: `LoadCheckpointFragment`

11. **Sampler class** - Identify:

- `KSampler` -> `SamplerStandardFragment`
- `ClownsharKSampler_Beta` -> `SamplerFragment`
- Other -> describe

12. **UI surface audit** - For every UI-visible fragment, determine whether an existing form component can be reused or whether a new component is required

---

## Phase 2: Planning Discussion

Present the full plan to the user for review. Structure it as follows:

### 2a. Fragment Reuse Table

| Node / Group | class_type(s) | Fragment              | Status   |
| ------------ | ------------- | --------------------- | -------- |
| Model loader | UNETLoader    | LoadDiffusionFragment | Existing |
| ...          | ...           | ...                   | ...      |

Mark each fragment as **Existing** or **New** (with proposed file path for new ones).

### 2b. UI Component Review

Validate UI concerns before the main plan is finalized.

Build this table from the planned fragment set and the existing generation components:

| Fragment          | Parameters exposed                   | Component decision | Status | Notes                                       |
| ----------------- | ------------------------------------ | ------------------ | ------ | ------------------------------------------- |
| `main_sampler`    | sampler, scheduler, steps, cfg, seed | `SamplerForm`      | Reuse  | Matches existing sampler UI                 |
| `controlnet_tile` | strength                             | `ControlNetForm`   | New    | No existing controlnet form with this shape |

For each UI-visible fragment, confirm:

- Whether an existing component can be reused unchanged
- Whether a new component must be created
- Which existing components were inspected as style and interaction references
- That the final UI follows `WORKFLOW_UI_CONVERSION_GUIDE.md` and `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- The field-to-control mapping for every exposed parameter
- That `FragmentMetadata.Parameters` remains the single source of truth for every exposed parameter's defaults, static options, min/max, step values, and dynamic option sources
- That any reused or new component reads those values from `Fragment.Schema.GetConstraints(...)` or `ParameterService.GetResolvedOptions(...)`, not from duplicated component-local `_options`, default, or range fields

### 2c. UI-Exposed vs Hardcoded Values

Build this table from the actual JSON values:

| Parameter      | Value in JSON    | Exposed in UI? | Reason                  |
| -------------- | ---------------- | -------------- | ----------------------- |
| Sampler name   | euler            | Yes            | User controls algorithm |
| Steps          | 20               | Yes            | User controls quality   |
| CFG            | 7.0              | Yes            | User controls adherence |
| Seed           | 0                | Yes            | Reproducibility         |
| Width / Height | 1024             | Yes            | Resolution control      |
| Batch size     | 1                | Yes            | Throughput control      |
| clip_type      | stable_diffusion | No             | Infrastructure          |
| weight_dtype   | default          | No             | Infrastructure          |
| Save prefix    | tmp/img          | No             | Always temp folder      |

Add or remove rows as needed. Every parameter from the JSON should appear in this table.

### 2d. Enhancements Discussion

Ask the user which optional post-processing modules to include. Only offer enhancements applicable to the workflow type.

**For image generation workflows (Txt2Img, Img2Img):**

| Enhancement      | Fragment                 | Notes                                                                                                          |
| ---------------- | ------------------------ | -------------------------------------------------------------------------------------------------------------- |
| SeedVR2 Upscale  | `SeedVR2UpscaleFragment` | Latent unsampling/resampling upscale. Recommended default.                                                     |
| FaceDetailer     | `DetailerFragment`       | Face/detail pass. Requires scoped model loader. Ask if it should share the main model or use a separate asset. |
| Standard Upscale | `UpscaleFragment`        | Pixel-space upscale with upscale model. Ask if needed alongside SeedVR2.                                       |

**For video generation workflows (Img2Vid):**

- Enhancements above are not applicable. Ask about frame interpolation if relevant.

For each selected enhancement, confirm:

- Whether it uses the main model or a separate model asset
- Detailer: the form exposes per-pass prompt injection (positive + negative) with fallback to the main prompts when blank, a standalone detailer LoRA list (per pass), and supports **chained passes** via a tabbed UI. The workflow template must implement the multi-pass loop described in `TEMPLATE_GUIDE.md#detailer-multi-pass-conventions`.

### 2e. ModelBase Enum

State whether the workflow requires a new `ModelBase` enum value or reuses an existing one.

### 2f. Compatible Resource Base Models

Determine the `CompatibleResourceBaseModels` for the workflow. Read `BlazorWebApp/Data/CivitAI/basemodels.json` to find the matching CivitAI base model strings for this workflow's architecture. Include all base model variants that the workflow can load (e.g., for SD 1.5: include `"SD 1.5"`, `"SD 1.5 LCM"`, `"SD 1.5 Hyper"`, `"SD 1.4"`, etc.).

Present the list for user confirmation.

### 2g. Default Values Summary

List the key defaults that will be used (sampler, scheduler, steps, CFG, denoise, resolution).

### 2h. Source Fidelity Review

Include the mandatory source fidelity matrix from Phase 1 and call out every difference from the source workflow. For each `replaced`, `dropped`, or `blocked` row, state whether the user has already approved the deviation. If approval is missing, stop and ask before implementation.

The review must explicitly answer:

- Which source JSON format was used as execution ground truth.
- Which enabled source nodes will not be emitted and why.
- Which emitted nodes differ from source nodes and how behavior is preserved.
- Which preview/debug/UI-only branches were omitted.
- Whether final output behavior matches the source, including save node, audio, fps, and frame/image selection.

---

## Phase 3: User Validation Gate

> **STOP. Do not write any code until this step is complete.**

Present a concise summary of the full plan and ask:

> "Does this plan look correct? Please confirm or let me know what to change before I proceed with implementation."

Wait for explicit user approval. Incorporate any requested changes and re-confirm if significant. The summary must include UI component decisions, any new component work, and the source fidelity review. Do not implement while any replacement/drop/blocker in the source fidelity matrix lacks explicit user approval.

---

## Phase 4: Implementation

After the user approves, implement in this order:

1. **Enum** (if needed): Add the new `ModelBase` value in `BlazorWebApp/Data/Enums.cs`
2. **New fragments** (if any): Create in the appropriate `BlazorWebApp/Workflows/Fragments/` subdirectory
   - Follow the dual-Build pattern (GenerationParameters overload + explicit Parameters overload)
   - Follow the scope conventions from TEMPLATE_GUIDE.md
3. **UI components** (if needed): Create or update fragment form components in `BlazorWebApp/Components/Shared/Generation/Fragments/`
   - Follow `WORKFLOW_UI_CONVERSION_GUIDE.md`
   - Follow `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
   - Use `[FragmentComponent("...")]` so the component is auto-discovered
   - Prefer dedicated new components over overloading unrelated existing forms

- Treat `FragmentMetadata.Parameters` as authoritative. Components may keep local bound state and layout logic, but defaults, select options, min/max/step constraints, and dynamic option sources must be read from `Fragment.Schema.GetConstraints(...)` or `ParameterService.GetResolvedOptions(...)`. Do not duplicate these values in Razor component fields.

4. **Workflow class**: Create in `BlazorWebApp/Workflows/Templates/{Base}/{Base}{Mode}Workflow.cs`
   - Implement `IWorkflowBuilder`
   - Declare fragment fields, `Metadata`, `GetFragments()`, and `Build()`
   - Include `CompatibleResourceBaseModels` in `Metadata` with the values confirmed in Phase 2e
   - Build order: Load -> LoRA -> EmptyLatent/Input -> Prompts -> Sampler -> VaeDecode -> [Enhancements] -> Save
   - Conditional enhancements must be gated with `parameters.GetFragment("id")?.IsActive == true`
   - **Fragment field defaults**: Every fragment with configurable defaults (sampler, upscale,
     latent, enhancement) must set `Defaults = new() { ... }` on the field declaration using
     only the values that differ from the fragment's canonical defaults. Never hard-code
     literals inside `Build()` — use the fragment's `Defaults.*` properties instead.
     ```csharp
     private readonly SamplerFragment _sampler = new()
     {
         Defaults = new() { Steps = 9, Cfg = 1.0, SamplerName = "exponential/res_2s" }
     };
     ```
5. **Build**: Run `dotnet build` on the project

---

## Phase 5: Verification

After the build:

1. Report build success or any compiler errors
2. Fix any errors before concluding
3. Summarize what was created:
   - New files created (with paths)
   - Existing files modified
   - Enum values added
4. Report UI integration status:
   - Which components were reused
   - Which components were created or updated

- Whether static/dynamic options, defaults, and constraints are metadata-driven through `Fragment.Schema` / resolved options
- Any remaining runtime UI risks that were not executable in validation

---

## Conventions Reminder

- Scope convention: `{scope}model_output`, `{scope}clip_output`, `{scope}vae_output`
- Pipeline outputs (`latent_output`, `image_output`) always written without scope prefix
- Detailer pass 0 uses scope `"detailer_"` (legacy); chained passes use `"detailer_{i}_"` for `i >= 1`
- Detailer template loop (required for every detailer-capable workflow):
  1. `for i in 0..pass_count-1` where `pass_count = detailerFragment.GetInt("pass_count", 1)`
  2. Key prefix per pass: pass 0 uses bare `detailer_xxx`; pass `i >= 1` uses `pass_{i}_detailer_xxx`
  3. Build scoped loader (`LoadDiffusionWithPromptsFragment` / equivalent)
  4. `_loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope)`
  5. `_detailerFragment.Build(...)` with `Scope = scope`
- Prompt fallback: `detailer_prompt` / `detailer_negative_prompt` (and their `pass_{i}_`-prefixed variants)
  must resolve via `GetStringOrFallback(key, mainPrompt)` so blank overrides transparently use the main prompt
- Save prefix: set via `new SaveFragment() { Defaults = new() { FilenamePrefix = "Workflow/Output" } }`; never hard-code the path inside `Build()`
- Seed: if value from parameters is < 0, randomize with `Random.Shared.NextInt64(0, int.MaxValue)`
- `GetFragments()` returns only UI-visible fragments; utility/loader fragments are excluded
- LoRA `BuildAll()` must be called AFTER the loader and BEFORE `PromptsFragment`
- **Fragment defaults rule**: No hard-coded literal fallbacks in `Build()`. All `GetInt/GetString/GetDouble` fallback
  arguments must reference `Defaults.*`. `Metadata.DefaultValue` entries must also reference `Defaults.*`.
  See TEMPLATE_GUIDE.md "Fragment Defaults" section for the full pattern.
