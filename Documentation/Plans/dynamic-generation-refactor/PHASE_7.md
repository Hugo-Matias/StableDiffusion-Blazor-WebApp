# Phase 7 - Workflow Template Updates

## Status
**Phase:** 7  
**Build Status:** &check; Passing | **Tests:** &check; Complete

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** &rarr; 2. **Test and Debug Features** &rarr; 3. **Discuss Improvements** &rarr; 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules
- **Each step = commit checkpoint** - test thoroughly before proceeding
- **Minimal changes only** - focused on phase objectives
- **Document all issues and resolutions** in this file
- **This document must have enough context** to resume in a new session
- **User permission required** before next step

---

## Objective

Update all workflow templates with:
1. **Unique Pipeline IDs** - Each pipeline step gets a unique `id` field for linking to `GenerationParameters.Fragments`
2. **Sources definitions** - Workflows that need input images/videos get explicit `Sources` arrays
3. **Consistent fragment references** - Ensure all fragments are correctly referenced

---

## Context

### Dependencies
- Phase 1-6 complete
- `GenerationParameters` model with `Fragments` dictionary
- `GenerationParameterService.InitializeFromWorkflowAsync()` reads Pipeline IDs
- `WorkflowService.ParseSources()` reads Sources array

### Current Template Structure
```json
{
  "Title": "Txt2Img",
  "Base": "Flux",
  "Mode": "txt2img",
  "Assets": [ ... ],
  "Pipeline": [
    {
      "fragment": "sampler.sbn",       // No id field!
      "parameters": { ... }
    }
  ]
}
```

### Target Template Structure
```json
{
  "Title": "Txt2Img",
  "Base": "Flux",
  "Mode": "txt2img",
  "Assets": [ ... ],
  "Sources": [                         // NEW: For img2img/img2vid
    { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
  ],
  "Pipeline": [
    {
      "id": "main_sampler",            // NEW: Unique ID
      "fragment": "sampler.sbn",
      "parameters": { ... }
    }
  ]
}
```

### Key Files
- `Workflows/Templates/flux/txt2img.sbn`
- `Workflows/Templates/sd/txt2img.sbn`
- `Workflows/Templates/chroma/txt2img.sbn`
- `Workflows/Templates/qwen/txt2img.sbn`
- `Workflows/Templates/qwen/img2img-edit.sbn`
- `Workflows/Templates/wan/img2vid.sbn`
- `Workflows/Templates/wan/pose2vid-steadydancer.sbn`
- `Workflows/Templates/z-image/txt2img.sbn`

### ID Naming Convention

| Fragment Type | ID Pattern | Example |
|---------------|------------|---------|
| Prompts | `prompts` | `"id": "prompts"` |
| Main Sampler | `main_sampler` | `"id": "main_sampler"` |
| Upscale | `upscale` | `"id": "upscale"` |
| Detailer | `detailer` | `"id": "detailer"` |
| Loader | `loader` or `loader_{base}` | `"id": "loader_flux"` |
| Video Settings | `video_settings` | `"id": "video_settings"` |
| Frame Interpolation | `frame_interpolation` | `"id": "frame_interpolation"` |
| Utility (no UI) | `{descriptive_name}` | `"id": "vae_decode"` |

### Sources Definition

| Mode | Required Sources |
|------|------------------|
| txt2img | None |
| img2img | `source_image` (required), `mask` (optional) |
| img2vid | `source_image` (required) |
| pose2vid | `source_video` (required), `reference_image` (optional) |

---

## Execution Checklist

### Step 7.1: Update Flux Txt2Img Template
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Added `id` to each pipeline step:
  - `loader_flux`, `main_sampler`, `upscale`, `vae_decode`, `loader_detailer`, `detailer`, `save`
- No Sources needed (txt2img)

#### File
`Workflows/Templates/flux/txt2img.sbn`

---

### Step 7.2: Update SD Txt2Img Template
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Added `id` to each pipeline step:
  - `loader_sd`, `main_sampler`, `upscale`, `vae_decode`, `seed_vr2`, `loader_detailer`, `detailer`, `save`
- No Sources needed (txt2img)

#### File
`Workflows/Templates/sd/txt2img.sbn`

---

### Step 7.3: Update Chroma Txt2Img Template
**Complexity:** 2
**Status:** [!] Blocked - Legacy Format

#### Notes
- Chroma template uses legacy `"Prompt"` section with raw ComfyUI nodes instead of Pipeline with fragments
- Requires significant refactoring to convert to fragment-based Pipeline
- Deferring to separate cleanup task

#### File
`Workflows/Templates/chroma/txt2img.sbn`

---

### Step 7.4: Update Qwen Templates
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made

**qwen/txt2img.sbn:**
- Added `id` to each pipeline step:
  - `loader_qwen`, `model_sampling`, `main_sampler`, `vae_decode`, `seed_vr2`, `loader_detailer`, `detailer`, `save`

**qwen/img2img-edit.sbn:**
- Added `id` to each pipeline step:
  - `load_image`, `loader_qwen_edit`, `vae_encode`, `encode_edit`, `main_sampler`, `vae_decode`, `save`
- Added Sources:
  ```json
  "Sources": [
    { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
  ]
  ```

#### Files
- `Workflows/Templates/qwen/txt2img.sbn`
- `Workflows/Templates/qwen/img2img-edit.sbn`

---

### Step 7.5: Update Wan Img2Vid Template
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Added `id` to each pipeline step (including conditional LoRA loading):
  - `loader_high`, `loader_low`, `loader_clip_vae`, `lora_high_{n}`, `lora_low_{n}`
  - `model_sampling_high`, `model_sampling_low`, `load_image`, `clip_vision`
  - `prompts`, `video_settings`, `sampler_high`, `sampler_low`
  - `clean_vram`, `vae_decode`, `frame_interpolation`, `video_save`
- Added Sources:
  ```json
  "Sources": [
    { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
  ]
  ```

#### File
`Workflows/Templates/wan/img2vid.sbn`

---

### Step 7.6: Update Wan Pose2Vid (SteadyDancer) Template
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Added `id` to each pipeline step:
  - `load_video`, `get_image_size`, `load_image`, `resize_subject`
  - `loader_wan`, `loader_vae`, `clip_vision`, `prompts`
  - `pose_detection`, `i2v_encode`, `steadydancer_embeds`, `context_options`
  - `main_sampler`, `vae_decode`, `concat_preview`, `video_save`
- Added Sources:
  ```json
  "Sources": [
    { "id": "source_video", "label": "Source Video", "type": "video", "required": true },
    { "id": "reference_image", "label": "Reference Image", "type": "image", "required": false }
  ]
  ```

#### File
`Workflows/Templates/wan/pose2vid-steadydancer.sbn`

---

### Step 7.7: Update Z-Image Txt2Img Template
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Added `id` to each pipeline step:
  - `loader_zimage`, `lora_{n}` (conditional), `prompts`
  - `seed_variance_enhancer`, `conditioning_variation`, `main_sampler`
  - `vae_decode`, `seed_vr2`, `loader_detailer`, `detailer`, `save`
- No Sources needed (txt2img)

#### File
`Workflows/Templates/z-image/txt2img.sbn`

---

### Step 7.8: Validate All Workflows
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Run application and navigate to Generate page
- [x] Select each workflow and verify:
  - No console errors on load
  - Fragments appear in UI correctly
  - Sources panel appears for img2img/img2vid workflows (Sources array parsed correctly)
  - ~~Generate button enables when required sources provided~~ (UI components in Phase 8)
- [x] Confirmed Sources are parsed and initialized in GenerationParameterService

#### Notes
- Sources are correctly parsed from templates and visible in state
- Actual source input UI components will be created in Phase 8
- All fragment-based templates validated

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 7.1 | &check; | 2 | Flux txt2img |
| 7.2 | &check; | 2 | SD txt2img |
| 7.3 | [!] | 2 | Chroma - legacy format, deferred |
| 7.4 | &check; | 3 | Qwen txt2img + img2img |
| 7.5 | &check; | 3 | Wan img2vid |
| 7.6 | &check; | 3 | Wan pose2vid |
| 7.7 | &check; | 2 | Z-Image txt2img |
| 7.8 | &check; | 3 | Validation complete |

---

## Files Modified This Phase

| File | Changes |
|------|---------|
| `Workflows/Templates/flux/txt2img.sbn` | Added pipeline IDs |
| `Workflows/Templates/sd/txt2img.sbn` | Added pipeline IDs |
| `Workflows/Templates/qwen/txt2img.sbn` | Added pipeline IDs |
| `Workflows/Templates/qwen/img2img-edit.sbn` | Added pipeline IDs, Sources |
| `Workflows/Templates/wan/img2vid.sbn` | Added pipeline IDs, Sources |
| `Workflows/Templates/wan/pose2vid-steadydancer.sbn` | Added pipeline IDs, Sources |
| `Workflows/Templates/z-image/txt2img.sbn` | Added pipeline IDs |

---

## Design Decisions

### ID Assignment Strategy

1. **UI fragments** get semantic IDs matching their purpose (`main_sampler`, `upscale`, `detailer`)
2. **Utility fragments** get descriptive IDs (`vae_decode`, `save`, `loader_flux`)
3. **Multiple instances** of same fragment get unique IDs (`sampler_high`, `sampler_low`)
4. **Scoped fragments** (like detailer loader) get scoped IDs (`loader_detailer`)

### Sources vs Assets

| Concept | Purpose | Example |
|---------|---------|---------|
| **Assets** | Model files selected by user | Checkpoint, VAE, CLIP |
| **Sources** | Input media for generation | Source image, mask, reference video |

Sources are runtime inputs that change per generation, while Assets are typically selected once per workflow session.

### Backward Compatibility

- Templates without `id` fields will still work (existing behavior)
- `GenerationParameterService` generates fallback IDs if not present
- New IDs enable proper fragment-to-UI linking

### Chroma Template Deferred

The Chroma template uses a legacy format with raw ComfyUI nodes in a `"Prompt"` section instead of the fragment-based Pipeline. Converting this requires:
1. Breaking down the monolithic workflow into fragments
2. Creating appropriate fragment files
3. Restructuring to match the Pipeline pattern

This is a larger task that should be handled separately.

---

## Issues &amp; Resolutions

| Issue | Resolution |
|-------|------------|
| Chroma template uses legacy format | Deferred to separate cleanup task |

---

## Commit Checkpoints

- [x] After Step 7.4 complete (Txt2Img workflows done)
- [x] After Step 7.7 complete (All fragment-based templates updated)
- [ ] After Step 7.8 complete (Full validation)

---

## Phase Summary

**Steps 7.1-7.7 complete** (except Chroma which uses legacy format).

All fragment-based workflow templates now have:
- Unique `id` fields on each pipeline step
- `Sources` arrays for workflows requiring input media (img2img, img2vid, pose2vid)

**Remaining:** Step 7.8 requires manual validation of all workflows in the application.

---

**Phase Status:** Complete [x]

**Next:** Phase 8 - Source Input UI Components
