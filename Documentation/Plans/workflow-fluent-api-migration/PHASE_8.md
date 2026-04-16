
## Status
**Phase:** 8
**Build Status:** Passing | **Tests:** 371/371 workflow tests passing
**Phase Status:** [~] Paused - Steps 1-3 complete, Step 4 (integration/cleanup) deferred

---

## Objective

Convert the **Z-Image Img2Img** workflow and all remaining unconverted **shared fragment `.sbn` files** to C#. This phase creates the Img2Img variant of Z-Image and ensures all shared fragments have C# equivalents, enabling subsequent phases (SD, Chroma, Qwen) to compose workflows purely from existing C# fragments.

**Deliverables:**
- 1 workflow class (`ZImageImg2ImgWorkflow.cs`)
- 4 new fragment classes (shared fragments not yet converted to C#)
- Unit tests for all new fragments and the workflow
- Deletion of all corresponding `.sbn` files

---

## Analysis

### Workflow: Z-Image Img2Img

Z-Image does **not** have an existing Img2Img `.sbn` template file. However, the Img2Img pattern is well-established across other model bases (Qwen `img2img-edit.sbn`, and the general architecture). The Z-Image Img2Img workflow follows the standard pattern:

**Pipeline Flow:**
```
Load Source Image + Scale (LoadImage -> ImageScaleToTotalPixels)
Load Models (UNETLoader + CLIPLoader + VAELoader via LoadDiffusion)
Load LoRAs (optional)
VAE Encode (source image -> latent)
Encode Prompts (CLIPTextEncode x2)
Sample (ClownsharKSampler_Beta with denoise < 1)
VAE Decode
SeedVR2 Upscale (optional)
Detailer (optional, with scoped model loader)
Save
```

**Assets:**
| Parameter | Label | Type | Default |
|-----------|-------|------|---------|
| Model | Model | DiffusionModel | z_image_turbo_bf16.safetensors |
| Clip | CLIP | Clip | qwen_3_4b.safetensors |
| Vae | VAE | Vae | ae.safetensors |

**Sources:**
| Id | Label | Type | Required |
|----|-------|------|----------|
| source_image | Source Image | image | true |

**Key Differences from Txt2Img:**
1. **Source image input** instead of empty latent
2. **VAE Encode** to convert image to latent space
3. **Image scaling** (LoadImage -> ImageScaleToTotalPixels) for resolution normalization
4. **Denoise < 1** in the sampler (preserves source image structure)
5. **No SeedVarianceEnhancer / ConditioningVariation** (these are Txt2Img creativity tools)

---

## Fragment Inventory

### New Fragments Required

| # | Fragment | Source `.sbn` | Used By | Complexity | Notes |
|---|---------|---------------|---------|------------|-------|
| 1 | `LoadImageScaledFragment` | `load-image-scaled.sbn` | ZImage Img2Img, future Img2Img workflows | 2 | LoadImage + ImageScaleToTotalPixels |
| 2 | `VaeEncodeFragment` | `vae-encode.sbn` | ZImage Img2Img, future Img2Img workflows | 1 | VAEEncode with registry refs |
| 3 | `SamplerStandardFragment` | `sampler-standard.sbn` | Future SD/Qwen workflows | 2 | Standard KSampler (not ClownsharK) |
| 4 | `LoadCheckpointFragment` | `load-checkpoint.sbn` | Future SD workflows | 3 | CheckpointLoaderSimple + PCLazyLoraLoader + PCLazyTextEncode, scoped |

### Already Converted (Reusable)

| Fragment | Used By | Notes |
|----------|---------|-------|
| `LoadDiffusionFragment` | ZImage Img2Img (main loader) | UNETLoader + CLIPLoader + VAELoader |
| `LoadDiffusionWithPromptsFragment` | ZImage Img2Img (detailer loader) | Scoped loader with prompt encoding |
| `LoraLoaderFragment` | ZImage Img2Img (optional) | Standard LoRA loader chain |
| `PromptsFragment` | ZImage Img2Img | CLIPTextEncode x2 |
| `SamplerFragment` | ZImage Img2Img | ClownsharKSampler_Beta |
| `VaeDecodeFragment` | ZImage Img2Img | Standard VAE decode |
| `SaveFragment` | ZImage Img2Img | SaveImage |
| `SeedVR2UpscaleFragment` | ZImage Img2Img (optional) | GGUF upscaler |
| `DetailerFragment` | ZImage Img2Img (optional) | Face detailer |

**Total new fragments: 4**
**Total `.sbn` files to delete: 4** (fragments) + dependent on cleanup scope

---

## Key Technical Considerations

### 1. Image Scaling (LoadImageScaledFragment)
The `load-image-scaled.sbn` uses `ImageScaleToTotalPixels` which scales based on megapixels rather than explicit width/height. Parameters:
- `image`: source image path (from `Sources["source_image"]`)
- `upscale_method`: scaling algorithm (default: `"lanczos"`)
- `megapixels`: target megapixel count (default: `1`)

Registers output: `image_input` (scaled image for VAE encode and other consumers)

### 2. VAE Encode (VaeEncodeFragment)
Simple fragment that encodes an image to latent space:
- Input: `image_input` from registry (via `LoadImageScaledFragment`)
- Input: `vae_output` from registry (via loader)
- Registers output: `latent_output` (replaces what `EmptyLatentFragment` would provide in Txt2Img)

### 3. Source Image Resolution
The workflow gets the source image path from `parameters.Sources["source_image"].Path`. This is already established in the Wan workflows.

### 4. Denoise for Img2Img
The sampler's `denoise` parameter should default to a value < 1 (e.g., 0.75) for Img2Img to preserve structure from the source image. The exact value comes from the user's fragment parameter `main_sampler.denoise`.

### 5. LoadCheckpointFragment (for SD)
This is needed by future phases but makes sense to convert now since it's a shared fragment. It uses `CheckpointLoaderSimple` (loads model+clip+vae from a single checkpoint file) + `PCLazyLoraLoader` for LoRA scheduling + `PCLazyTextEncode` for prompt encoding. Supports scoping for detailer.

### 6. SamplerStandardFragment (for SD/Qwen)
A standard `KSampler` node (not ClownsharK). Needed by Qwen Img2Img-Edit and SD workflows. Simple wrapper with configurable denoise.

---

## Execution Checklist

### Step 1: Img2Img Shared Fragments
**Complexity:** 5
**Status:** [x] Complete

Create the fragments required for Img2Img pipelines:
- [x] `LoadImageScaledFragment` (`load-image-scaled.sbn`) - LoadImage + ImageScaleToTotalPixels, registers `image_input`
- [x] `VaeEncodeFragment` (`vae-encode.sbn`) - VAEEncode with registry refs for image and VAE
- [x] Unit tests for both fragments (14 tests)

**Files to create:**
- `BlazorWebApp/Workflows/Fragments/Core/LoadImageScaledFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/VaeEncodeFragment.cs`
- `BlazorWebApp.Tests/Workflows/Fragments/Img2ImgFragmentTests.cs`

**Commit checkpoint:** Fragments compile and unit tests pass

---

### Step 2: Future-Proofing Shared Fragments
**Complexity:** 5
**Status:** [x] Complete

Convert remaining shared fragments that future workflows need:
- [x] `LoadCheckpointFragment` (`load-checkpoint.sbn`) - CheckpointLoaderSimple + PCLazyLoraLoader + PCLazyTextEncode, scoped
- [x] `SamplerStandardFragment` (`sampler-standard.sbn`) - Standard KSampler wrapper with denoise
- [x] Unit tests for both fragments (16 tests)

**Files to create:**
- `BlazorWebApp/Workflows/Fragments/Core/LoadCheckpointFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/SamplerStandardFragment.cs`
- `BlazorWebApp.Tests/Workflows/Fragments/SharedFragmentTests.cs`

**Commit checkpoint:** Fragments compile and unit tests pass

---

### Step 3: ZImageImg2ImgWorkflow
**Complexity:** 5
**Status:** [x] Complete (untested in ComfyUI - Z-Image may not support Img2Img)

Compose fragments into the complete Z-Image Img2Img workflow:
- [x] Create `ZImageImg2ImgWorkflow.cs` implementing `IWorkflowBuilder`
- [x] Define metadata (assets, sources with `source_image`, deterministic ID)
- [x] Implement `GetFragments()` returning UI-visible fragments
- [x] Implement `Build()` composing the full pipeline
- [x] Handle denoise parameter (< 1 for Img2Img)
- [x] Handle conditional SeedVR2 Upscale
- [x] Handle conditional Detailer with scoped loader
- [x] Add unit tests for workflow (33 tests)

**Build order in `Build()`:**
```csharp
1. LoadImageScaled (source_image, megapixels)
2. LoadDiffusion (UNet, CLIP, VAE)
3. [For each LoRA: LoraLoader]
4. VaeEncode (image_input -> latent_output)
5. Prompts (positive + negative)
6. Sampler (denoise < 1, latent from VaeEncode)
7. VaeDecode
8. [SeedVR2Upscale if active]
9. [LoadDiffusionWithPrompts (detailer_ scope) + Detailer if active]
10. Save
```

**GetFragments() UI order:**
```
- PromptsFragment (prompt text input)
- SamplerFragment (sampling settings with denoise)
- SeedVR2UpscaleFragment (optional upscale)
- DetailerFragment (optional face detailer)
```

**Files to create:**
- `BlazorWebApp/Workflows/Templates/ZImage/ZImageImg2ImgWorkflow.cs`
- `BlazorWebApp.Tests/Workflows/ZImageImg2ImgWorkflowTests.cs`

**Commit checkpoint:** Workflow generates valid JSON, unit tests pass

---

### Step 4: Integration Testing & Cleanup
**Complexity:** 2
**Status:** [ ] Deferred

- [ ] Test ZImage Img2Img workflow execution in ComfyUI generates images
- [ ] Verify source image is properly scaled and encoded
- [ ] Verify denoise preserves source image structure
- [ ] Verify Detailer and SeedVR2 Upscale work correctly
- [ ] Delete converted `.sbn` files:

**Fragment `.sbn` files to delete:**
- `load-image-scaled.sbn`
- `vae-encode.sbn`
- `sampler-standard.sbn`
- `load-checkpoint.sbn`

**Commit checkpoint:** Phase 8 complete, all `.sbn` files deleted

---

## Progress Tracking

| Step | Description | Status | Complexity | Notes |
|------|-------------|--------|------------|-------|
| 1 | Img2Img Shared Fragments | [x] | 5 | LoadImageScaled + VaeEncode (14 tests) |
| 2 | Future-Proofing Shared Fragments | [x] | 5 | LoadCheckpoint + SamplerStandard (16 tests) |
| 3 | ZImageImg2ImgWorkflow | [x] | 5 | Workflow + tests (33 tests) |
| 4 | Integration Testing & Cleanup | [ ] | 2 | Deferred - Z-Image Img2Img unverified |
| **Total** | | **88%** | **17** | **4 fragments + 1 workflow, 63 tests** |

### Additional Work Completed
- **Deterministic Workflow IDs:** Replaced hardcoded `Guid.Parse()` in all workflows with UUID v5 derived from `Base + Mode + Title`. Fixed GUID collision between ZImageTxt2Img and WanImg2Vid. Added duplicate detection in `WorkflowService.DiscoverWorkflowBuilders()`.

---

## Remaining `.sbn` Inventory (Post Phase 8)

After this phase completes, the following `.sbn` files will remain for future phases:

### Shared Fragment `.sbn` (already have C# equivalents)
These have C# classes but the `.sbn` files were not deleted in earlier phases:
- `conditioning-variation.sbn` -> `ConditioningVariationFragment.cs`
- `detailer-core.sbn` -> `DetailerFragment.cs`
- `empty-latent.sbn` -> `EmptyLatentFragment.cs`
- `load-diffusion.sbn` -> `LoadDiffusionFragment.cs`
- `load-diffusion-w-prompts.sbn` -> `LoadDiffusionWithPromptsFragment.cs`
- `lora-loader.sbn` -> `LoraLoaderFragment.cs`
- `model-sampling-auraflow.sbn` -> `ModelSamplingAuraFlowFragment.cs`
- `prompts.sbn` -> `PromptsFragment.cs`
- `sampler.sbn` -> `SamplerFragment.cs`
- `save.sbn` -> `SaveFragment.cs`
- `seed-variance-enhancer.sbn` -> `SeedVarianceEnhancerFragment.cs`
- `upscale-seedvr2.sbn` -> `SeedVR2UpscaleFragment.cs`
- `vae-decode.sbn` -> `VaeDecodeFragment.cs`

### Fragments Without C# Equivalents (Needed by Future Phases)
- `llm.sbn` - Searge LLM Node (used by Chroma monolithic template - needs analysis)
- `qwen/encode-edit.sbn` - TextEncodeQwenImageEditPlus (Phase 7: Qwen)
- `qwen/load-qwen-edit.sbn` - Qwen model chain with LoRA + ModelSamplingAuraFlow + CFGNorm (Phase 7: Qwen)
- `utils/condition-helpers.sbn` - Scriban helper functions (dead code, delete when no `.sbn` consumers remain)

### Template `.sbn` Files (Workflows Not Yet Converted)
- `sd/txt2img.sbn` - StableDiffusion Txt2Img
- `chroma/txt2img.sbn` - Chroma Txt2Img (monolithic, 828 lines)
- `qwen/txt2img.sbn` - Qwen Txt2Img
- `qwen/img2img-edit.sbn` - Qwen Img2Img Edit

### Recommended Phase Order (After Phase 8)
1. **Phase 7 (Qwen):** `qwen/encode-edit.sbn`, `qwen/load-qwen-edit.sbn` -> `QwenTxt2ImgWorkflow`, `QwenImg2ImgEditWorkflow`
2. **Phase 9 (SD):** Uses `LoadCheckpointFragment` (from Phase 8) -> `SDTxt2ImgWorkflow`
3. **Phase 10 (Chroma):** Monolithic template decomposition -> `ChromaTxt2ImgWorkflow`
4. **Phase 11 (Final Cleanup):** Delete all remaining `.sbn` files, `utils/condition-helpers.sbn`, update docs
