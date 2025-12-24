# Phase 4 - Template Conversion (All Remaining Fragments)

## Status
**Phase:** 4  
**Build Status:** Passing | **Tests:** 53/53 Passing

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** - 2. **Test and Debug Features** - 3. **Discuss Improvements** - 4. **Update This Document**
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

Convert ALL remaining fragment files to Fluid/Liquid syntax. By the end of this phase, every fragment should be a `.liquid` file ready for Phase 5 (workflow template integration).

---

## Context

- **Phase 3 Complete:** 4 simple fragments converted, patterns established
- **Current State:** Phase 4 COMPLETE - All 50 fragments converted and renamed
- **Target State:** All fragments use `.liquid` extension with Fluid syntax

---

## Execution Checklist

### Step 4.1: Convert Root Tier 1 Fragments (Simple)
**Complexity:** 5  
**Status:** [x] Complete

#### Files (10)
- [x] `vae-encode.liquid` - Converted
- [x] `load-image.liquid` - Converted
- [x] `load-image-scaled.liquid` - Converted
- [x] `load-video.liquid` - Converted
- [x] `load-clip-vision.liquid` - Converted
- [x] `get-image-size.liquid` - Converted
- [x] `resize-image-kj.liquid` - Converted
- [x] `upscale.liquid` - Converted
- [x] `save-video.liquid` - Converted
- [x] `concat-preview.liquid` - Converted

---

### Step 4.2: Convert Root Tier 2 Fragments (Medium)
**Complexity:** 8  
**Status:** [x] Complete

#### Files (8)
- [x] `prompts.liquid` - Converted
- [x] `lora-loader.liquid` - Converted
- [x] `sampler.liquid` - Converted
- [x] `sampler-standard.liquid` - Converted
- [x] `model-sampling-auraflow.liquid` - Converted
- [x] `load-diffusion-w-prompts.liquid` - Converted
- [x] `clean-vram.liquid` - Converted
- [x] `llm.liquid` - Converted

---

### Step 4.3: Convert Root Tier 3 Fragments (Complex)
**Complexity:** 8  
**Status:** [x] Complete

#### Files (5)
- [x] `load-checkpoint.liquid` - Converted (uses string_contains filter)
- [x] `detailer-core.liquid` - Converted
- [x] `conditioning-variation.liquid` - Converted
- [x] `seed-variance-enhancer.liquid` - Converted
- [x] `upscale-seedvr2.liquid` - Converted

---

### Step 4.4: Convert Subdirectory Fragments (flux, qwen, utils)
**Complexity:** 3  
**Status:** [x] Complete

#### Files (3)
- [x] `flux/load-flux.liquid` - Converted
- [x] `qwen/load-qwen-edit.liquid` - Converted
- [x] `qwen/encode-edit.liquid` - Converted
- [x] `utils/condition-helpers.sbn` - **DELETED** (Scriban-only file, unused)

---

### Step 4.5: Convert WAN Tier 1 & 2 Fragments
**Complexity:** 8  
**Status:** [x] Complete

#### Files (12)
- [x] `wan/load-wan-vae.liquid` - Converted
- [x] `wan/load-wan-model.liquid` - Converted
- [x] `wan/load-clip-vae.liquid` - Converted
- [x] `wan/decode-wan.liquid` - Converted
- [x] `wan/clip-vision.liquid` - Converted
- [x] `wan/context-options.liquid` - Converted
- [x] `wan/prompts.liquid` - Converted
- [x] `wan/text-encode-wan.liquid` - Converted
- [x] `wan/load-image.liquid` - Converted
- [x] `wan/i2v-encode.liquid` - Converted
- [x] `wan/sampler-wan.liquid` - Converted
- [x] `wan/lora-loader-model-only.liquid` - Converted

---

### Step 4.6: Convert WAN Tier 3 & 4 Fragments (Complex)
**Complexity:** 13  
**Status:** [x] Complete

#### Files (8)
- [x] `wan/load-model-sage.liquid` - Converted (Critical fragment fixed)
- [x] `wan/load-dual-models.liquid` - Converted
- [x] `wan/sampler-advanced.liquid` - Converted
- [x] `wan/frame-interpolation.liquid` - Converted
- [x] `wan/painter-i2v.liquid` - Converted
- [x] `wan/pose-detection.liquid` - Converted
- [x] `wan/model-sampling-sd3.liquid` - Converted
- [x] `wan/steadydancer-embeds.liquid` - Converted

---

### Step 4.7: Rename All Fragment Files to .liquid
**Complexity:** 3  
**Status:** [x] Complete

#### Tasks
- [x] Renamed all 50 `.sbn` fragment files to `.liquid`
- [x] Deleted unused `utils/condition-helpers.sbn`

---

### Step 4.8: Update Workflow Templates to Reference .liquid Files
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Updated all workflow templates to reference `.liquid` instead of `.sbn`
- [x] Verified 7 workflow template files updated

---

### Step 4.9: Final Verification
**Complexity:** 3  
**Status:** [x] Complete

#### Tasks
- [x] Run full build - PASSING
- [x] Run all unit tests - 53/53 PASSING
- [x] Verify no `.sbn` fragment files remain - CONFIRMED
- [x] Count: 50 total `.liquid` fragment files

---

## Progress Tracking

| Step | Status | Complexity | Files | Notes |
|------|--------|------------|-------|-------|
| 4.1 | Done   | 5          | 10    | Simple root - complete |
| 4.2 | Done   | 8          | 8     | Medium root - complete |
| 4.3 | Done   | 8          | 5     | Complex root - complete |
| 4.4 | Done   | 3          | 3     | flux, qwen (utils deleted) |
| 4.5 | Done   | 8          | 12    | WAN simple/medium - complete |
| 4.6 | Done   | 13         | 8     | WAN complex - complete |
| 4.7 | Done   | 3          | 50    | All files renamed |
| 4.8 | Done   | 2          | 7     | Workflow templates updated |
| 4.9 | Done   | 3          | -     | Verification complete |

**Total Phase Complexity:** 53 points

---

## Key Conversion Patterns

### Pattern: Default Values
**Scriban:** `{{ var ?? "default" }}`  
**Fluid:** `{{ var | default: "default" }}`

### Pattern: Variable Assignment
**Scriban:** `{{~ clean_id = node_id ?? "clean_vram" ~}}`  
**Fluid:** `{% assign clean_id = node_id | default: "clean_vram" %}`

### Pattern: String Concatenation with Default
**Scriban:** `{{ (scope ?? "") + "model_output" }}`  
**Fluid:** `{{ scope | default: "" | append: "model_output" }}`

### Pattern: Dynamic get_ref with Concatenation
**Scriban:** `{{ get_ref ((scope ?? "") + "model_output") }}`  
**Fluid:** `{% assign ref_key = scope | default: "" | append: "model_output" %}{% get_ref ref_key %}`

### Pattern: string.contains Conditional
**Scriban:** `{{~ if scope && scope | string.contains "detailer" ~}}`  
**Fluid:** `{% assign is_detailer = scope | string_contains: "detailer" %}{% if scope and is_detailer %}`

### Pattern: Conditional Scope in Keys
**Scriban:** `{{ if scope }}{{ scope }}{{ end }}node_id`  
**Fluid:** `{% if scope %}{{ scope }}{% endif %}node_id`

---

## Issues and Resolutions

| Issue | Resolution |
|-------|------------|
| `utils/condition-helpers.sbn` was Scriban-only | Deleted - unused file with Scriban function definitions |
| Workflow templates referenced `.sbn` fragments | Updated all templates to reference `.liquid` |

---

## Files Summary

| Category | Count | Status |
|----------|-------|--------|
| Root Tier 1 (Simple) | 10 | Done |
| Root Tier 2 (Medium) | 8 | Done |
| Root Tier 3 (Complex) | 5 | Done |
| Subdirectories (flux, qwen) | 3 | Done |
| WAN Tier 1 & 2 | 12 | Done |
| WAN Tier 3 & 4 | 8 | Done |
| Phase 3 files (save, vae-decode, etc.) | 4 | Done |
| **TOTAL** | **50** | **Complete** |

---

## Phase 4 Complete

All fragments have been:
1. Converted from Scriban to Fluid syntax
2. Renamed from `.sbn` to `.liquid` extension
3. Workflow templates updated to reference new `.liquid` files

**Ready for Phase 5:** Workflow template integration
