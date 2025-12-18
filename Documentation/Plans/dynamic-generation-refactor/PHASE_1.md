# Phase 1 - Schema Definition & Documentation

## Status
**Phase:** 1  
**Build Status:** N/A (documentation phase) | **Tests:** N/A

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update This Document**
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

Define the complete UI schema format for fragments, establish conventions, and document the system before implementation begins. This ensures we have a solid contract between fragments and UI components.

---

## Context

- This is a documentation-first phase
- Output: `FRAGMENT_SCHEMA_GUIDE.md` in `BlazorWebApp/Workflows/`
- The schema will be used by `WorkflowService` to parse fragment `#meta` blocks
- Components will read schema to get constraints (min/max/step) and behavior flags

---

## Execution Checklist

### Step 1.1: Design Complete UI Schema JSON Structure
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Define root `ui` object structure
- [x] Define `parameters` object for designed components
- [x] Define `fields` array for dynamic field rendering
- [x] Define behavior flags (collapsible, chainable, etc.)
- [x] Define source references for select fields

#### Deliverable
Complete JSON schema specification documented in FRAGMENT_SCHEMA_GUIDE.md.

---

### Step 1.2: Create FRAGMENT_SCHEMA_GUIDE.md
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Create the guide file
- [x] Document all schema properties
- [x] Include examples for each scenario
- [x] Document field types and their properties
- [x] Document component registration conventions

#### Changes Made
- Created `BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`
- Comprehensive documentation covering all schema aspects

---

### Step 1.3: Define Field Types and Properties
**Complexity:** 2
**Status:** [x] Complete

#### Tasks
- [x] Define all supported field types
- [x] Document required vs optional properties per type
- [x] Define validation rules
- [x] Define source reference format

#### Field Types Defined
- `slider` - Numeric slider with min/max/step
- `numeric` - Numeric input field
- `seed` - Specialized seed input with randomize/restore
- `select` - Dropdown with source or static options
- `text` - Single-line text input
- `textarea` - Multi-line text input
- `checkbox` - Boolean toggle
- `switch` - Boolean toggle (alternative style)
- `color` - Color picker
- `file` - File selection
- `resolution` - Paired width/height with aspect ratio
- `group` - Visual grouping container

---

### Step 1.4: Define Component Registry Conventions
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Establish naming conventions (PascalCase + Form suffix)
- [x] Document registration process
- [x] List initial components to register

#### Initial Components
| Component | Fragment | Description |
|-----------|----------|-------------|
| PromptsForm | prompts.sbn | Prompt fields with autocomplete |
| SamplerForm | sampler.sbn | Sampler, scheduler, steps, CFG, seed |
| ResolutionForm | (embedded) | Width/height with presets |
| LoraForm | (embedded) | LoRA selection |
| UpscaleForm | upscale.sbn | Upscale settings |
| DetailerForm | detailer-core.sbn | Detailer settings |
| ConditioningVariationForm | conditioning-variation.sbn | CV settings |
| SeedVarianceEnhancerForm | seed-variance-enhancer.sbn | SVE settings |

---

### Step 1.5: Review with Example Fragments
**Complexity:** 2
**Status:** [~] In Progress

#### Tasks
- [x] Draft schema for `sampler.sbn`
- [x] Draft schema for `prompts.sbn`
- [ ] Draft schema for `upscale.sbn`
- [ ] Draft schema for `detailer-core.sbn`
- [ ] Draft schema for `seed-variance-enhancer.sbn`
- [ ] User review and approval

#### Drafted Schemas

**sampler.sbn:**
```json
#meta
{
  "outputs": {
    "latent_output": {"node": "{{ sampler_id }}", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.{{ sampler_id }}.IsActive"]
  },
  "ui": {
    "component": "SamplerForm",
    "title": "Sampler",
    "icon": "fa-solid fa-dice",
    "collapsible": true,
    "chainable": true,
    "order": 50,
    "parameters": {
      "sampler_name": { "source": "Backend.Samplers" },
      "scheduler": { "source": "Backend.Schedulers" },
      "steps": { "min": 1, "max": 150, "step": 1 },
      "cfg": { "min": 1, "max": 30, "step": 0.5 },
      "seed": { "min": -1 }
    }
  }
}
#end
```

**prompts.sbn:**
```json
#meta
{
  "outputs": {
    "positive_output": {"node": "text_positive", "index": 0},
    "negative_output": {"node": "text_negative", "index": 0}
  },
  "ui": {
    "component": "PromptsForm",
    "title": "Prompts",
    "order": 10,
    "collapsible": false,
    "parameters": {
      "positive": { },
      "negative": { }
    }
  }
}
#end
```

**upscale-seedvr2.sbn (draft):**
```json
#meta
{
  "outputs": {
    "image_output": {"node": "seedvr2_upscale", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.seedvr2.IsActive"]
  },
  "ui": {
    "component": "SeedVR2Form",
    "title": "SeedVR2 Upscale",
    "icon": "fa-solid fa-expand",
    "collapsible": true,
    "defaultCollapsed": true,
    "order": 100,
    "parameters": {
      "seedvr2_model": { "source": "Backend.Upscalers" },
      "seedvr2_vae_model": { "source": "Backend.Vaes" },
      "seedvr2_resolution": { "min": 512, "max": 4096, "step": 64 },
      "seedvr2_batch_size": { "min": 1, "max": 4, "step": 1 },
      "seedvr2_input_noise_scale": { "min": 0, "max": 1, "step": 0.01 },
      "seedvr2_latent_noise_scale": { "min": 0, "max": 1, "step": 0.01 },
      "blocks_to_swap": { "min": 0, "max": 100, "step": 1 },
      "vae_tile_size": { "min": 256, "max": 2048, "step": 64 },
      "vae_tile_overlap": { "min": 0, "max": 512, "step": 8 }
    }
  }
}
#end
```

**detailer-core.sbn (draft):**
```json
#meta
{
  "outputs": {
    "image_output": {"node": "{{ scope }}detailer", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.{{ scope }}detailer.IsActive"]
  },
  "ui": {
    "component": "DetailerForm",
    "title": "Detailer",
    "icon": "fa-solid fa-face-smile",
    "collapsible": true,
    "defaultCollapsed": true,
    "chainable": true,
    "order": 120,
    "parameters": {
      "detailer_detection_model": { "source": "Backend.DetectionModels" },
      "detailer_sampler": { "source": "Backend.Samplers" },
      "detailer_scheduler": { "source": "Backend.Schedulers" },
      "detailer_steps": { "min": 1, "max": 100, "step": 1 },
      "detailer_cfg": { "min": 1, "max": 30, "step": 0.5 },
      "detailer_denoise": { "min": 0, "max": 1, "step": 0.01 },
      "detailer_feather": { "min": 0, "max": 100, "step": 1 },
      "detailer_bbox_threshold": { "min": 0, "max": 1, "step": 0.01 },
      "detailer_bbox_dilation": { "min": 0, "max": 100, "step": 1 },
      "detailer_bbox_crop_factor": { "min": 1, "max": 10, "step": 0.1 },
      "detailer_drop_size": { "min": 1, "max": 200, "step": 1 },
      "detailer_guide_size": { "min": 64, "max": 2048, "step": 64 },
      "detailer_max_size": { "min": 256, "max": 4096, "step": 64 },
      "detailer_cycle": { "min": 1, "max": 10, "step": 1 },
      "detailer_seed": { "min": -1 }
    }
  }
}
#end
```

**seed-variance-enhancer.sbn (draft):**
```json
#meta
{
  "outputs": {
    "noise_output": {"node": "seed_variance_enhancer", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.seed_variance_enhancer.IsActive"]
  },
  "ui": {
    "component": "SeedVarianceEnhancerForm",
    "title": "Seed Variance Enhancer",
    "icon": "fa-solid fa-shuffle",
    "collapsible": true,
    "defaultCollapsed": true,
    "order": 80,
    "parameters": {
      "randomize_percent": { "min": 0, "max": 100, "step": 1 },
      "strength": { "min": 0, "max": 100, "step": 1 },
      "noise_insert": { "options": ["noise on beginning steps", "noise on ending steps"] },
      "steps_switchover_percent": { "min": 0, "max": 100, "step": 1 },
      "seed": { "min": -1 },
      "mask_starts_at": { "options": ["beginning", "end"] },
      "mask_percent": { "min": 0, "max": 100, "step": 1 },
      "log_to_console": { }
    }
  }
}
#end
```

**conditioning-variation.sbn (draft):**
```json
#meta
{
  "outputs": {
    "positive_output": {"node": "conditioning_variation", "index": 0},
    "negative_output": {"node": "conditioning_variation", "index": 1}
  },
  "conditions": {
    "required": ["Fragments.conditioning_variation.IsActive"]
  },
  "ui": {
    "component": "ConditioningVariationForm",
    "title": "Conditioning Variation",
    "icon": "fa-solid fa-code-branch",
    "collapsible": true,
    "defaultCollapsed": true,
    "order": 75,
    "parameters": {
      "switch_point": { "min": 0, "max": 1, "step": 0.01 }
    }
  }
}
#end
```

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1.1 | ? | 3 | Schema structure complete |
| 1.2 | ? | 3 | Guide created at BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md |
| 1.3 | ? | 2 | 12 field types defined |
| 1.4 | ? | 1 | 8 initial components identified |
| 1.5 | [~] | 2 | Drafted 6 schemas, awaiting review |

---

## Issues & Resolutions

*None yet*

---

## Commit Checkpoints

- [x] After Step 1.2 complete (guide created)
- [ ] After Step 1.5 complete (phase complete, user approved)

---

## Awaiting User Review

Please review:
1. **FRAGMENT_SCHEMA_GUIDE.md** - Is the schema format complete and clear?
2. **Drafted schemas** - Do the example schemas look correct for each fragment?
3. **Field types** - Any missing field types we need?
4. **Component list** - Any components missing from the initial list?

Once approved, Phase 1 is complete and we can proceed to Phase 2 (Core Infrastructure).

---

**Phase Status:** In Progress [~] - Awaiting User Review
