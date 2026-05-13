# Qwen Character Reference Sheet - Implementation Plan

## Status

**Current Phase:** Phase 4 implemented and build-validated; ready for runtime UI review before Phase 5  
**Total Complexity:** ~55 points across 5 implementation phases  
**Primary Planning Reference:** `Documentation/Plans/IMPLEMENTATION_GUIDE.md`  
**Workflow Conversion Reference:** `.github/prompts/workflow-conversion.prompt.md`  
**Source Workflow:** `Documentation/Plans/qwen-character-reference-sheet/KiraNugget's Multiview Character Sheet.json`  
**Future Character Domain Reference:** `Documentation/Plans/character-creator/MAIN_PLAN.md`

---

## Implementation Guidelines

Follow `Documentation/Plans/IMPLEMENTATION_GUIDE.md` for execution gates and phase tracking. This plan also follows the workflow-conversion source fidelity contract from `.github/prompts/workflow-conversion.prompt.md`.

### Execution Workflow Per Step

1. Initial Code Writing -> 2. Test and Debug -> 3. Discuss Improvements -> 4. Update Phase Document
   - Do not implement until the user approves this plan.
   - Do not proceed to the next phase step until targeted testing is complete.
   - User approval is required before marking phase documents complete or moving to the next phase.
   - Build runs only after user request or after completing the full edit set for a step.

### Progress Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked / needs discussion

### Repository Conventions

- UI work follows `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` and `.github/instructions/design-language.instructions.md`.
- New tabbed surfaces use `TabbedPageShell` and one of the documented layout variants.
- Form controls default to `Variant.Text`, compact controls use `Dense="true"`, and layout spacing uses app tokens.
- Cross-component notifications use `EventService` pub/sub if page state changes need to notify other services/components.
- ComfyUI node work must probe live `/object_info/{NodeType}` before implementation. Existing probes verified the current source/proposed core nodes except the proposed standard split diffusion loader, which still needs a final live probe before code.
- No new character DB entity is part of this plan. Current persistence target is app state plus existing generated image persistence. A future Character entity remains part of the broader Character Creator direction.

---

## Problem Statement

The source workflow is a Qwen image-edit workflow that creates a multiview character reference sheet from a single input image: body angles, expression closeups, and pose variations. The app currently has Qwen fluent workflows and Generate-page integration, but this workflow is better treated as a dedicated Character page experience because users need labeled output containers, slot management, hidden per-shot prompts, and repeatable character-reference state rather than a generic Generate run.

The source workflow is a visual ComfyUI workflow export, not an API prompt payload. Its top-level graph contains loader/input/helper nodes plus 18 subgraph wrapper nodes. The executable render logic lives inside `definitions.subgraphs`, where each subgraph renders one labeled shot.

---

## Proposed Solution

Build a dedicated Qwen Character Reference Sheet page that composes and runs a fluent C# graph from app-owned shot definitions. The page will let the user select a source image, choose Qwen assets, configure loader mode, manage shot slots, and run all or selected slots. Outputs will return into labeled containers and be persisted through the existing image save path for the currently selected project.

This is not the full Character Creator entity system. It is a workflow-backed reference-generation page and a stepping stone toward the later Character concept described in `Documentation/Plans/character-creator/MAIN_PLAN.md`.

### Key Decisions

| Decision                                                    | Rationale                                                                                                                                                                                                                                |
| ----------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Build as a dedicated Character page                         | The primary UX is labeled reference slots, not the Generate page's prompt-first workflow surface.                                                                                                                                        |
| Reuse the existing navbar project selector only             | Project selection is already global; the page should not add project-selection logic.                                                                                                                                                    |
| Treat this as a Qwen base-model workflow                    | `ModelBase.Qwen` already exists and should be reused.                                                                                                                                                                                    |
| Support mutually exclusive loader modes                     | Users can choose either AIO checkpoint mode or split diffusion/CLIP/VAE mode. The UI must show one asset set at a time.                                                                                                                  |
| Keep downstream shot rendering stable across loader modes   | Loader mode changes only which nodes supply model/clip/vae outputs. Conditioning, sampling, decode, upscale, and output collection stay the same.                                                                                        |
| Make shot definitions app-owned                             | The repeated subgraph becomes reusable C# shot definitions, not 18 copy-pasted builders.                                                                                                                                                 |
| Rename expression labels by removing `Close`                | UI labels become `Neutral`, `Happy`, `Scared`, `Angry`, `Sad`, `Crying`, and `Smug`.                                                                                                                                                     |
| Convert the three custom source slots into addable presets  | `Custom Expression`, `Custom Pose`, and `Custom Landscape Format` are not default slots; they become presets behind the `+ Add` card, plus a blank preset.                                                                               |
| Move Add Slot into the toolbar                              | Addable presets are now opened from a toolbar popover before Enable All/Disable All, keeping the grid dedicated to output slots.                                                                                                         |
| Treat slot prompt edits as extensions                       | Hidden prompt templates preserve the source defaults. The visible prompt field appends user text after the default template with one separating space.                                                                                   |
| Standardize Characters as an image source target            | Local images can be sent to `Characters / Source Image` through the same media Send To dialog used for workflow source inputs.                                                                                                           |
| Treat ComfyUI source-image filenames as ephemeral           | The page keeps the browser/app source data and uploads a fresh Comfy input filename per run when available, avoiding stale `LoadImage` references after Comfy cleanup or validation failures.                                            |
| Treat RTX upscale as opportunistic                          | The `RTXVideoSuperResolution` schema is valid, but NVIDIA VFX runtime initialization can fail in a ComfyUI container even on supported GPUs. Character runs retry once without RTX and disable the toggle after an `NvVFX_Load` failure. |
| Persist page state in app state for now                     | Slot definitions, custom prompts, selected loader mode, and visibility choices live in `AppState.Character`. No character DB work in this plan.                                                                                          |
| Persist generated images through existing image persistence | Outputs should still become app images in the selected project. Future character linkage can attach those images to Character entities later.                                                                                            |
| Leave detailer enhancement out                              | The user explicitly scoped out detailer enhancement for now.                                                                                                                                                                             |

---

## Workflow Analysis

### Source Format Detection

| Item                   | Result                                                                                                                                 |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Source format          | Visual workflow export                                                                                                                 |
| API `prompt` present   | No                                                                                                                                     |
| Execution ground truth | Visual `nodes`, `links`, and `definitions.subgraphs`                                                                                   |
| Top-level node count   | 64                                                                                                                                     |
| Top-level link count   | 85                                                                                                                                     |
| Embedded subgraphs     | 18                                                                                                                                     |
| Preview behavior       | Source uses `PreviewImage`; app should replace this with output nodes/history mapping that can persist and map each output to a label. |

### Top-Level Node Inventory Summary

| Source ids / group |                       class_type | Count | Role                               | Planned status                                           |
| ------------------ | -------------------------------: | ----: | ---------------------------------- | -------------------------------------------------------- |
| 701                |                      `LoadImage` |     1 | Source character image             | Exact or existing app source image loader equivalent     |
| 1109               |     `NunchakuQwenImageDiTLoader` |     1 | Source diffusion/model loader      | Replaced by loader-mode abstraction                      |
| 1110               |                     `CLIPLoader` |     1 | Qwen CLIP loader in split stack    | Exact in split mode                                      |
| 1111               |                      `VAELoader` |     1 | Qwen VAE loader in split stack     | Exact in split mode                                      |
| 1117               |   `NunchakuQwenImageLoraStackV3` |     1 | Source LoRA stack                  | Replaced by app `LoraLoader` chain                       |
| 1078               |                 `Seed Generator` |     1 | Shared seed helper                 | Replaced by C# seed policy                               |
| 1112               |            `Anything Everywhere` |     1 | Visual graph fanout helper         | Replaced by explicit C# wiring                           |
| 1132               |                     `TextInput_` |     1 | Global negative text               | Replaced by app state field                              |
| 1059, 1039, 1040   |                 `CR Prompt Text` |     3 | Source custom slot prompt presets  | Replaced by add-slot presets                             |
| Multiple ids       |                 `CR Prompt Text` |    15 | Per-shot instruction nodes         | Replaced by shot definitions and hidden prompt overrides |
| 18 wrapper nodes   |              UUID subgraph types |    18 | One render branch per shot         | Replaced by reusable shot-render fragment                |
| Multiple ids       |                   `PreviewImage` |    18 | Visual preview output per shot     | Replaced by mapped output collection/persistence         |
| 1074               | `Fast Groups Bypasser (rgthree)` |     1 | Enable/disable visual groups       | Replaced by app slot selection/state                     |
| 1114               |                   `MarkdownNote` |     1 | Human documentation inside ComfyUI | Dropped                                                  |

### Embedded Subgraph Node Inventory

Each render subgraph generally contains the same executable sequence:

| class_type                                | Total count across subgraphs | Role                                         | Planned status                                                                     |
| ----------------------------------------- | ---------------------------: | -------------------------------------------- | ---------------------------------------------------------------------------------- |
| `PathchSageAttentionKJ`                   |                           18 | Model patch/performance option               | Preserve if available, parameterized or hidden default                             |
| `ModelSamplingAuraFlow`                   |                           18 | Qwen/AuraFlow model sampling patch           | Preserve                                                                           |
| `CFGNorm`                                 |                           18 | CFG normalization patch                      | Preserve                                                                           |
| `ImageScaleToTotalPixels`                 |                           18 | Source reference image resize                | Preserve                                                                           |
| `EmptyLatentImage`                        |                           18 | Per-shot latent dimensions                   | Preserve via shot definition                                                       |
| `TextEncodeQwenImageEditPlusPro_lrzjason` |                           36 | Positive and negative Qwen edit conditioning | Preserve with new fragment                                                         |
| `KSampler`                                |                           18 | Per-shot sampling                            | Preserve via shared shot parameters                                                |
| `VAEDecode`                               |                           18 | Decode sampled latent                        | Preserve                                                                           |
| `RTXVideoSuperResolution`                 |                           18 | Pixel upscaling to final output              | Preserve as toggle; retry without it if NVIDIA VFX initialization fails at runtime |
| `easy cleanGpuUsed`                       |                           18 | Cleanup/pass-through helper                  | Keep as an advanced/OOM mitigation toggle if output typing works                   |
| `CM_PromptCombine_JK`                     |                           16 | Prompt composition helper                    | Replace with C# string composition                                                 |
| `TextPreview`                             |                           14 | Debug prompt preview                         | Drop                                                                               |

### Logical Groups

| Group           | Source nodes                                                                                         | Planned app equivalent                                                                        |
| --------------- | ---------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Loading         | `LoadImage`, `NunchakuQwenImageDiTLoader`, `CLIPLoader`, `VAELoader`, `NunchakuQwenImageLoraStackV3` | Source image input, mutually exclusive loader mode, app LoRA chain                            |
| Encoding        | `TextEncodeQwenImageEditPlusPro_lrzjason`, `ImageScaleToTotalPixels`, prompt helpers                 | New Qwen edit-plus-pro conditioning fragment and C# prompt composition                        |
| Processing      | `PathchSageAttentionKJ`, `ModelSamplingAuraFlow`, `CFGNorm`, `EmptyLatentImage`, `KSampler`          | New shot render fragment with per-slot defaults                                               |
| Post-processing | `VAEDecode`, `RTXVideoSuperResolution`, `easy cleanGpuUsed`                                          | Preserve decode/upscale; keep cleanup helper as an advanced toggle after implementation probe |
| Output          | `PreviewImage`                                                                                       | Save/history collector keyed by expected shot output nodes                                    |

---

## Shot Model

### Default Slots

The page starts with 15 default slots. Expression labels omit the source `Close` prefix.

| Slot | Source title        | UI label            | Kind       | Size      | CFG | Default dependency      |
| ---- | ------------------- | ------------------- | ---------- | --------- | --: | ----------------------- |
| 1    | Front view          | Front view          | Body angle | 1088x1920 | 1.6 | Source image            |
| 2    | Left Profile        | Left Profile        | Body angle | 1088x1920 | 1.4 | Front view if available |
| 3    | Right Profile       | Right Profile       | Body angle | 1088x1920 | 1.8 | Front view if available |
| 4    | Back View           | Back View           | Body angle | 1088x1920 | 1.6 | Front view if available |
| 5    | Front Three-Quarter | Front Three-Quarter | Body angle | 1088x1920 | 1.4 | Front view if available |
| 6    | Back Three-Quarter  | Back Three-Quarter  | Body angle | 1088x1920 | 1.4 | Front view if available |
| 7    | Close Neutral       | Neutral             | Expression | 1088x1088 | 1.6 | Source image            |
| 8    | Close Happy         | Happy               | Expression | 1088x1088 | 1.6 | Neutral if available    |
| 9    | Close Scared        | Scared              | Expression | 1088x1088 | 1.6 | Neutral if available    |
| 10   | Close Angry         | Angry               | Expression | 1088x1088 | 1.6 | Neutral if available    |
| 11   | Close Sad           | Sad                 | Expression | 1088x1088 | 1.6 | Neutral if available    |
| 12   | Close Crying        | Crying              | Expression | 1088x1088 | 1.6 | Neutral if available    |
| 13   | Close Smug          | Smug                | Expression | 1088x1088 | 1.6 | Neutral if available    |
| 14   | Model pose          | Model Pose          | Pose       | 1088x1920 | 1.4 | Front view if available |
| 15   | Action Pose         | Action Pose         | Pose       | 1088x1920 | 1.4 | Front view if available |

### Addable Slot Presets

The toolbar contains an `Add Slot` button before Enable All/Disable All. Clicking it opens a compact preset popover and appends the selected slot.

| Preset     | Source title              |                          Default size | Default CFG | Prompt behavior                                                                                                                     |
| ---------- | ------------------------- | ------------------------------------: | ----------: | ----------------------------------------------------------------------------------------------------------------------------------- |
| Expression | `Custom Expression`       |                             1088x1088 |         1.6 | Starts with expression-preserving prompt template; label is user-editable.                                                          |
| Pose       | `Custom Pose`             |                             1088x1920 |         1.6 | Starts with full-body pose prompt template; label is user-editable.                                                                 |
| Camera     | App-defined               |                             1088x1920 |         1.6 | Starts with the same neutral character-preservation prompt style as other addable defaults; the user supplies any camera direction. |
| Body       | App-defined               |                             1088x1920 |         1.6 | Starts with a body-preserving prompt template; the user supplies body/reference details.                                            |
| Outfit     | App-defined               |                             1088x1920 |         1.6 | Starts with an identity-preserving prompt template; the user supplies outfit changes.                                               |
| Landscape  | `Custom Landscape Format` |                             1920x1088 |         1.6 | Starts with wide-format prompt template; label is user-editable.                                                                    |
| Blank      | App-defined               | User chooses or defaults to 1088x1088 |         1.6 | Empty hidden prompt template; user fills details. Suggest is intentionally hidden.                                                  |

### Slot State

Each slot state should include at least:

| Field                    | Purpose                                                                  |
| ------------------------ | ------------------------------------------------------------------------ |
| `Id`                     | Stable client-side id for app-state persistence and output mapping.      |
| `Label`                  | User-facing card label.                                                  |
| `Kind`                   | Body angle, Expression, Pose, Body, Outfit, Landscape, Custom.           |
| `PresetKey`              | Built-in preset identity, if any.                                        |
| `Width`, `Height`        | Latent/output dimensions.                                                |
| `PromptTemplate`         | Hidden default/source prompt text.                                       |
| `PromptExtension`        | Visible expandable text appended after `PromptTemplate`.                 |
| `PromptOverride`         | Legacy fallback for saved state created before prompt extensions.        |
| `NegativePromptOverride` | Optional; defaults to global negative.                                   |
| `IsEnabled`              | Whether this slot participates in the next run.                          |
| `DependencyPolicy`       | Source image, front-view output, neutral output, or explicit prior slot. |
| `LastOutputImageId`      | Existing image DB id after persistence, if available.                    |
| `LastOutputPath`         | Local output path or URL for display.                                    |
| `Status`                 | Idle, queued, running, succeeded, failed.                                |

---

## Loader Mode Plan

The page exposes a loader mode toggle. The asset viewer/asset assignment UI must show only the fields for the selected mode.

| Mode           | Assets shown              | Loader nodes                                                     | Notes                                                                                                                |
| -------------- | ------------------------- | ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| AIO checkpoint | Checkpoint                | `CheckpointLoaderSimple`                                         | Produces model, clip, and vae from one checkpoint. Default checkpoint is `Base/Qwen-Rapid-AIO-NSFW-v19.safetensors`. |
| Split stack    | Diffusion/UNet, CLIP, VAE | Standard diffusion loader candidate + `CLIPLoader` + `VAELoader` | Matches existing Qwen split pattern more closely. Must probe final diffusion loader node before implementation.      |

Downstream outputs should be normalized into the same registry keys: `model_output`, `clip_output`, and `vae_output`. LoRAs run after either loader mode, using app `LoraLoader` chaining.

Known verified split defaults from probe:

| Node         | Input       | Verified default                        |
| ------------ | ----------- | --------------------------------------- |
| `CLIPLoader` | `clip_name` | `qwen_2.5_vl_7b_fp8_scaled.safetensors` |
| `CLIPLoader` | `type`      | `qwen_image`                            |
| `CLIPLoader` | `device`    | `default`                               |
| `VAELoader`  | `vae_name`  | `qwen_image_vae.safetensors`            |

---

## UI Plan

### Page Placement

Add a dedicated top-level page for Character reference generation. Suggested route: `/characters` or `/character`. The nav item should be a simple entry; it should not duplicate project selector behavior because project selection already lives in the navbar.

### Layout

Use `TabbedPageShell`. Initial tab can be `Reference Sheet`. Future tabs can host Character Creator, saved characters, or identity sheets when the broader plan matures.

Recommended layout inside the first tab: `TwoColumnLayout`.

| Region  | Content                                                                                                                        |
| ------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Sidebar | Source image, loader mode, active asset selectors, LoRA selector, global sampler controls, run controls.                       |
| Content | Type-grouped shot grids with labeled image containers, per-slot status, hidden prompt expansion, and toolbar Add Slot popover. |

### Component Review

| Component / Surface        | Parameters exposed                            | Component decision                                      | Status      | Notes                                                                      |
| -------------------------- | --------------------------------------------- | ------------------------------------------------------- | ----------- | -------------------------------------------------------------------------- |
| Character page shell       | active tab index                              | `TabbedPageShell`                                       | Reuse       | Standard tabbed-page structure.                                            |
| Character reference layout | settings/sidebar + shot grid                  | `TwoColumnLayout`                                       | Reuse       | No custom shell variant.                                                   |
| Source image               | source image path/upload                      | Existing image input/dropzone patterns                  | Reuse/adapt | Should behave like current image source selectors.                         |
| Loader mode                | AIO vs Split                                  | New small segmented/toggle control                      | New         | Drives mutually exclusive asset UI.                                        |
| Asset assignment           | checkpoint or split model/clip/vae            | Existing workflow asset selector patterns               | Reuse/adapt | Must hide inactive mode assets.                                            |
| LoRA assignment            | LoRA list and strengths                       | Existing Generate LoRA selector patterns                | Reuse/adapt | Qwen-compatible filtering.                                                 |
| Sampler settings           | sampler, scheduler, steps, CFG override, seed | New compact settings surface, referencing `SamplerForm` | New/adapt   | Per-shot defaults exist, global override is optional.                      |
| Shot grid                  | slot label, image, status, actions            | New `CharacterShotGrid`                                 | New         | This is page-specific, not a Generate fragment form.                       |
| Shot card                  | prompt collapsed by default, output preview   | New `CharacterShotCard`                                 | New         | Stable dimensions to avoid layout shift.                                   |
| Add slot popover           | preset buttons                                | `CharacterShotGrid` toolbar popover                     | New         | Appends Expression, Pose, Camera, Body, Outfit, Landscape, or Blank slots. |
| Asset inspection           | generated image full view                     | `AssetViewer`                                           | Reuse       | Canonical image viewer.                                                    |

### UI-Exposed vs Hardcoded Values

| Parameter           | Source value                                                                              | Exposed?                             | Reason                                                                                          |
| ------------------- | ----------------------------------------------------------------------------------------- | ------------------------------------ | ----------------------------------------------------------------------------------------------- |
| Loader mode         | Source is Nunchaku split-like stack                                                       | Yes                                  | User requested AIO or split loader modes.                                                       |
| Checkpoint          | Not in source                                                                             | Yes in AIO mode                      | Required for AIO mode.                                                                          |
| Diffusion/UNet      | `svdq-int4_r128-qwen-image-edit-2509-lightningv2.0-4steps.safetensors` in Nunchaku loader | Yes in split mode                    | User can assign Qwen diffusion asset.                                                           |
| CLIP                | `qwen_2.5_vl_7b_fp8_scaled.safetensors`                                                   | Yes in split mode                    | Required for split Qwen stack.                                                                  |
| VAE                 | `qwen_image_vae.safetensors`                                                              | Yes in split mode                    | Required for split Qwen stack.                                                                  |
| LoRA                | `Qwen\Qwen-Edit-2509-Multiple-angles.safetensors`, strength `1`                           | Yes                                  | Requested Generate-style LoRA support.                                                          |
| Source image        | Source `LoadImage`                                                                        | Yes                                  | Required user input.                                                                            |
| Default slots       | 15 default slots                                                                          | Yes, as enabled/disabled cards       | Users should choose what to run.                                                                |
| Custom slots        | 3 source custom subgraphs                                                                 | Yes, as addable presets              | Adds value without cluttering default sheet.                                                    |
| Slot prompt         | Source per-shot prompt strings                                                            | Hidden by default                    | Needed for power users without overloading page.                                                |
| Global negative     | `(bra straps, shirt straps:1.5), (((straight black lines,)))`                             | Advanced                             | Useful but not core.                                                                            |
| Steps               | `4`                                                                                       | Advanced/global override             | Source is lightning-style; changing it is advanced.                                             |
| CFG                 | `1.4`, `1.6`, `1.8` by shot                                                               | Advanced/global or per-shot override | Preserve defaults while allowing tuning.                                                        |
| Sampler             | `euler`                                                                                   | Advanced                             | Backend option, useful but secondary.                                                           |
| Scheduler           | `simple`                                                                                  | Advanced                             | Backend option, useful but secondary.                                                           |
| Denoise             | `1`                                                                                       | Hidden/advanced                      | Infrastructure-level for this workflow.                                                         |
| Dimensions          | 1088x1920, 1088x1088, 1920x1088                                                           | Per-slot advanced                    | Defaults preserve source; custom slots may need edits.                                          |
| Qwen target size    | `1024`                                                                                    | Hidden initially                     | Node infrastructure.                                                                            |
| Qwen target VL size | `384`                                                                                     | Hidden initially                     | Node infrastructure.                                                                            |
| Qwen crop method    | `pad`                                                                                     | Hidden initially                     | Node infrastructure.                                                                            |
| Image scale method  | `lanczos`                                                                                 | Hidden initially                     | Source default.                                                                                 |
| RTX upscale         | multiplier `2`, quality `ULTRA`                                                           | Toggle/advanced, default on          | Source behavior, user confirmed it should be enabled by default.                                |
| Clean GPU helper    | `easy cleanGpuUsed`                                                                       | Toggle/advanced                      | Keep available as an OOM mitigation toggle and run only at sensible points if it proves useful. |

---

## Output Collection And Persistence

The source uses one `PreviewImage` per shot. The app needs reliable mapping from ComfyUI history outputs back to slot ids.

### Proposed Collector Contract

| Item             | Plan                                                                                                  |
| ---------------- | ----------------------------------------------------------------------------------------------------- |
| Expected outputs | Workflow builder records output node ids with slot ids and labels.                                    |
| Collection       | New collector reads all configured output nodes from ComfyUI history, not just the first output node. |
| Persistence      | Persist collected images through existing image save service behavior into the selected project.      |
| Slot update      | Update app state with output image id/path/status for each slot.                                      |
| Partial failure  | A slot can fail while other completed outputs remain visible and saved.                               |
| Custom slots     | Output mapping is based on generated slot ids, so dynamic slots are no harder than default slots.     |

Current concern: existing `ComfyUIService.GetFilenameFromHistory` only extracts images from the first output node. This plan requires either a Character-specific collector or a generalized collector that accepts expected output node ids.

---

## App State Persistence

No character DB entity or migration belongs to this plan. Persist the page's working state through `AppState`, similar to other feature state.

Suggested state shape:

```text
AppState.Character
  ActiveTabIndex
  LoaderMode
  AssetsByMode
  Loras
  SourceImage
  GlobalNegativePrompt
  SamplerOverrides
  Slots
  AdvancedPanelState
```

Future Character entity integration should be able to adopt this state by moving `Slots`, source image, and outputs into a JSON-backed Character aggregate. This plan should avoid choices that make that migration awkward, but it should not implement the entity yet.

---

## Source Fidelity Matrix

| Source node/group                          | Source role                  | Emitted C# node/fragment                                                             | Status              | Approval state / reason                                                                                           |
| ------------------------------------------ | ---------------------------- | ------------------------------------------------------------------------------------ | ------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Visual workflow root                       | Execution metadata           | Fluent workflow/service-generated prompt                                             | Replaced            | Approved direction: source is visual export, app must reconstruct executable prompt.                              |
| `LoadImage` id 701                         | Source image                 | Existing/new source image loader                                                     | Exact/parameterized | Preserves source input behavior.                                                                                  |
| `NunchakuQwenImageDiTLoader` id 1109       | Source model loader          | Loader mode abstraction: `CheckpointLoaderSimple` or split standard diffusion loader | Replaced            | User requested AIO or split support; Nunchaku node missing locally.                                               |
| `CLIPLoader` id 1110                       | Split CLIP loader            | `CLIPLoader` in split mode                                                           | Exact               | Probed and verified. Hidden when AIO mode selected.                                                               |
| `VAELoader` id 1111                        | Split VAE loader             | `VAELoader` in split mode                                                            | Exact               | Probed and verified. Hidden when AIO mode selected.                                                               |
| `NunchakuQwenImageLoraStackV3` id 1117     | Source LoRA stack            | App `LoraLoader` chain                                                               | Replaced            | User requested Generate-style LoRA loading; source node missing locally.                                          |
| `Anything Everywhere` id 1112              | Visual fanout helper         | Explicit C# node references                                                          | Replaced            | Behavior preserved by direct graph wiring.                                                                        |
| `Seed Generator` id 1078                   | Shared seed helper           | App seed policy                                                                      | Replaced            | Behavior preserved by storing/applying seeds in state and request model.                                          |
| `Fast Groups Bypasser (rgthree)` id 1074   | Visual group enable/disable  | Slot enabled state and run selection                                                 | Replaced            | Page state replaces visual group toggling.                                                                        |
| `TextInput_` id 1132                       | Global negative prompt       | App state field                                                                      | Replaced            | Behavior preserved; value becomes advanced field.                                                                 |
| `CR Prompt Text` instruction nodes         | Per-shot text inputs         | Built-in shot prompt templates and hidden overrides                                  | Replaced            | User accepted C# replacing helper text nodes.                                                                     |
| Custom `CR Prompt Text` ids 1039/1040/1059 | Custom preset prompts        | Add-slot preset templates                                                            | Replaced            | User requested these become addable presets.                                                                      |
| UUID subgraph wrapper nodes                | One branch per output shot   | Reusable shot render builder over slot list                                          | Replaced            | Behavior preserved by expanding the subgraph pattern from app definitions.                                        |
| `PathchSageAttentionKJ`                    | Model patch                  | Shot render fragment internal node                                                   | Exact/parameterized | Probed and verified; keep hidden default.                                                                         |
| `ModelSamplingAuraFlow`                    | Model sampling patch         | Shot render fragment internal node                                                   | Exact               | Probed and verified.                                                                                              |
| `CFGNorm`                                  | CFG model patch              | Shot render fragment internal node                                                   | Exact               | Probed and verified.                                                                                              |
| `ImageScaleToTotalPixels`                  | Reference resize             | Shot render fragment internal node                                                   | Exact               | Probed and verified.                                                                                              |
| `EmptyLatentImage`                         | Shot dimensions              | Shot render fragment internal node                                                   | Parameterized       | Values come from slot definitions.                                                                                |
| `TextEncodeQwenImageEditPlusPro_lrzjason`  | Qwen image-edit conditioning | New Qwen edit-plus-pro conditioning fragment                                         | Exact/parameterized | Probed and verified; must preserve output indexes.                                                                |
| `KSampler`                                 | Sampling                     | Shot render fragment internal node or shared sampler fragment logic                  | Exact/parameterized | Probed and verified; defaults preserved per slot.                                                                 |
| `VAEDecode`                                | Decode latent to image       | Shot render fragment internal node                                                   | Exact               | Probed and verified.                                                                                              |
| `RTXVideoSuperResolution`                  | Pixel upscale                | Shot render fragment internal node                                                   | Parameterized       | Default preserves source and is enabled by default per user decision.                                             |
| `easy cleanGpuUsed`                        | Cleanup/pass-through helper  | Optional advanced toggle                                                             | Parameterized       | Registered locally; keep as an OOM mitigation option if testing shows it helps and does not break output mapping. |
| `CM_PromptCombine_JK`                      | Prompt composition           | C# string composition                                                                | Replaced            | Source helper missing locally; behavior can be preserved deterministically.                                       |
| `TextPreview`                              | Debug text preview           | None                                                                                 | Dropped             | Preview-only, not part of final image output.                                                                     |
| `PreviewImage`                             | Visual output display        | Save/history output node collector                                                   | Replaced            | App needs persistence and labeled mapping, not unmanaged previews.                                                |
| `MarkdownNote`                             | In-Comfy documentation       | None                                                                                 | Dropped             | UI-only note.                                                                                                     |

---

## Dataflow Parity Check

Planned per-slot dataflow:

```text
Source image
  -> ImageScaleToTotalPixels
  -> TextEncodeQwenImageEditPlusPro_lrzjason positive/negative conditioning
  -> EmptyLatentImage
  -> KSampler
  -> VAEDecode
  -> RTXVideoSuperResolution when enabled
  -> optional cleanGpuUsed
  -> output node keyed by slot id
```

Shared dataflow:

```text
Loader mode
  -> model_output, clip_output, vae_output
  -> LoraLoader chain when LoRAs are enabled
  -> shot render branches
```

Dependency handling:

| Dependency                     | Plan                                                                                        |
| ------------------------------ | ------------------------------------------------------------------------------------------- |
| Front view                     | Can be generated first and reused by body-angle/pose slots when needed.                     |
| Neutral                        | Can be generated first and reused by expression slots when needed.                          |
| Selected single dependent slot | If prerequisite output exists, reuse it; otherwise auto-run the missing prerequisite first. |
| Run all                        | Use a dependency-aware ordering rather than raw UI order.                                   |
| Dynamic custom slots           | Use dependency policy from preset or user setting.                                          |

---

## ModelBase And Resource Compatibility

| Item                           | Decision                               |
| ------------------------------ | -------------------------------------- |
| `ModelBase` enum               | Reuse existing `Qwen`; no enum change. |
| Compatible CivitAI base models | `Qwen`, `Qwen 2`                       |
| Detailer                       | Not included.                          |
| Base model classification      | This is a Qwen base-model workflow.    |

---

## Implementation Phases

### Phase 1 - State And Domain Model (8 points)

- [x] Add app-state model for Character page state at `AppState.Character`.
- [x] Add slot definition/state models for default and custom slots.
- [x] Add loader mode model and asset-state model.
- [x] Add default slot catalog with renamed labels and addable presets.
- [x] Add basic unit tests for default slot construction, label normalization, and add-slot behavior.

Success criteria:

- App state can round-trip default slots and custom slots without DB migrations.
- Default slots are 15 items; custom presets are not created until the user adds them.
- Loader mode state never exposes both asset sets as active at once.

### Phase 2 - Fluent Workflow Fragments And Loader Modes (13 points)

- [x] Probe the final split diffusion loader node schema before implementation.
- [x] Add/parameterize a Qwen loader abstraction for AIO checkpoint and split stack modes.
- [x] Reuse app LoRA chaining after either loader mode.
- [x] Add Qwen edit-plus-pro conditioning fragment for `TextEncodeQwenImageEditPlusPro_lrzjason`.
- [x] Add shot render fragment/builder that emits the repeated subgraph from a slot definition.
- [x] Preserve Qwen target/crop/upscale defaults from probe.
- [x] Add workflow/fragments tests for emitted node classes, inputs, output indexes, and loader-mode exclusivity.

Success criteria:

- AIO and split mode both produce the same downstream registry outputs.
- Slot render output count matches enabled slots.
- Source defaults are preserved unless explicitly parameterized.

Validation completed:

- `QwenCharacterReferenceWorkflowComposerTests`: 7 passed.
- `CharacterStateTests`: 7 passed after adding the Qwen edit instruction default.
- New Character composer/fragments stay outside normal `WorkflowService` discovery.

### Phase 3 - Character Run Service And Output Collector (13 points)

- [x] Add service contract for running all slots, selected slots, and prerequisite slots.
- [x] Add dependency-aware shot ordering that auto-runs missing prerequisites before dependent slots.
- [x] Extend or add ComfyUI history collection by expected output node ids.
- [x] Persist collected images through existing image persistence into the selected project.
- [x] Update app state with per-slot status, image ids, and output paths.
- [x] Add tests for output mapping, partial output handling, and custom-slot output collection.

Success criteria:

- Each completed output maps back to the correct slot label/id.
- Custom slots collect outputs with the same mechanism as defaults.
- Existing first-output history behavior remains compatible for other workflows.

Validation completed:

- `CharacterReferenceSlotPlannerTests`, `ComfyUIHistoryImageOutputCollectorTests`, and `CharacterReferenceRunServiceTests`: 7 passed.
- `QwenCharacterReferenceWorkflowComposerTests` and `CharacterStateTests`: 14 passed.
- Existing `GetFilenameFromHistory` behavior remains unchanged; the new collector is exposed separately as expected-node image output collection.

### Phase 4 - Character Page UI (13 points)

- [x] Add top-level Character page and nav entry.
- [x] Build `TabbedPageShell` + `TwoColumnLayout` page structure.
- [x] Build settings sidebar with source image, loader mode, active asset selectors, LoRAs, and advanced run settings.
- [x] Build shot grid/card components with stable dimensions and collapsed prompt fields.
- [x] Build toolbar Add Slot popover with Expression, Pose, Camera, Body, Outfit, Landscape, and Blank preset buttons.
- [x] Group cards into separate grids by slot type.
- [x] Add Ollama Suggest for non-Blank slots using shared LLM settings.
- [x] Add Characters as a standardized image-source Send To target.
- [x] Wire run actions, per-slot enable/disable, output display, and `AssetViewer` inspection.
- [x] Add UI state persistence through `AppState`.

Success criteria:

- Page follows the documented design language and avoids duplicate project selection.
- Switching loader mode hides the inactive asset set.
- Adding/removing custom slots updates state and output mapping predictably.
- Text fits within card controls on desktop and mobile widths.

Validation completed:

- Razor diagnostics for the new Character page/components and nav import were clean.
- Project `build` task succeeded with existing unrelated warning noise.
- Running app route smoke test returned HTTP 200 for `/characters`.
- Follow-up refinements: Character state/workflow/media-send-to tests passed 30/30, project build succeeded previously, and `/characters` route smoke test returned HTTP 200 previously.

### Phase 5 - Targeted Validation And Documentation (8 points)

- [x] Run focused workflow/fragment tests.
- [x] Run focused service tests for collector, slot ordering, and media send-to target behavior.
- [x] Run targeted build or file diagnostics after edits.
- [ ] Manually verify page state and add-slot UX where automated tests are insufficient.
- [ ] Update phase documentation and any workflow docs needed for future maintenance.

Success criteria:

- Targeted tests pass or unrelated blockers are documented.
- Plan and phase documents accurately reflect implemented behavior.
- Remaining runtime risks are clearly documented.

---

## Stress Points And Concerns

| Concern                           | Risk                                                                                 | Mitigation / Decision Needed                                                                                                                   |
| --------------------------------- | ------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| AIO checkpoint compatibility      | A checkpoint may not actually contain Qwen model/CLIP/VAE in the expected format.    | Default to `Base/Qwen-Rapid-AIO-NSFW-v19.safetensors`, keep split mode available, and validate ComfyUI failures clearly.                       |
| Split diffusion loader exact node | Source used missing Nunchaku loader, not standard `UNETLoader`.                      | Probe and confirm the standard split loader before implementation. If incompatible, split mode becomes blocked until a valid loader is chosen. |
| `easy cleanGpuUsed` passthrough   | It may not type cleanly into save/output mapping or may not be needed.               | Add an advanced toggle and only place it at sensible graph points after testing.                                                               |
| Multi-output history mapping      | Existing service reads first output only.                                            | Add expected-output collector keyed by node id/slot id. Keep existing behavior for Generate workflows.                                         |
| Dynamic custom slots              | Slot count changes between runs, complicating node ids and output mapping.           | Derive stable node ids from slot ids and run id; output collector consumes explicit expected outputs.                                          |
| Dependency DAG                    | Running one expression may require Neutral; running one pose may require Front view. | Encode prerequisites and auto-queue missing prerequisites unless the required output is already available.                                     |
| AppState growth                   | Page state can become large if storing output details.                               | Store references/ids/paths, not binary image payloads. Future Character entity can own richer history.                                         |
| UI density                        | 15+ cards plus addable slots can become crowded.                                     | Use stable card dimensions, responsive grid tracks, and collapsed prompts by default.                                                          |
| Source prompt fidelity            | Source prompt helper nodes are missing locally.                                      | Preserve prompt text as C# templates and hidden overrides; tests can verify template defaults.                                                 |

---

## Resolved Clarifications

| Decision               | Resolution                                                                                     |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| App-state root         | Use `AppState.Character` to match the page route and leave room for future character features. |
| Default AIO checkpoint | Use `Base/Qwen-Rapid-AIO-NSFW-v19.safetensors`.                                                |
| RTX upscale default    | Enabled by default.                                                                            |
| Dependent slot runs    | Auto-run missing prerequisites unless the output is already available.                         |
| GPU cleanup helper     | Keep `easy cleanGpuUsed` as an advanced/OOM mitigation toggle if testing shows it helps.       |

## Open Clarifications

1. For split mode, should the diffusion asset default to the source Nunchaku filename if present in the app's asset index, or should it default empty until the user assigns a compatible Qwen diffusion model?
2. Should generated reference outputs be tagged in image metadata with `CharacterReferenceSlot=<label>` immediately, or only keep that association in app state until the future Character entity exists?
3. Should the `+ Add` card allow duplicate presets with different labels, or enforce one active custom slot per preset type?

---

## Approval Status

The user approved this plan and resolved the main implementation decisions. Implementation may begin with Phase 1. Approved decisions include:

- Replace missing Nunchaku loader nodes with an AIO/split loader mode abstraction.
- Replace missing prompt/text helper nodes with C# prompt templates and app state.
- Replace `PreviewImage` output behavior with mapped save/history collection.
- Keep character-specific persistence in `AppState.Character` for now, with no new Character DB entity.
- Use 15 default slots plus an addable custom-slot model instead of the source's 18 always-present slots.
