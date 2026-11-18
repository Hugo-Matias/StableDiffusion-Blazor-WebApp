# Phase 4 - Convert Flux Txt2Img Workflow

## Status
**Phase:** 4  
**Build Status:** ? Passing | **Tests:** ? 26 Passing
**Phase Status:** [x] Complete

---

## Pre-Phase Cleanup

Before proceeding with Flux conversion, we performed a Scriban legacy code audit (see `SCRIBAN_AUDIT.md`).

### Cleanup Completed
- [x] Removed duplicate `NodeRegistry` from `BlazorWebApp/Models/Workflow.cs`
- [x] Removed `SubgraphContext` from `BlazorWebApp/Models/Workflow.cs`
- [x] Added `Merge()` method to `NodeRegistry` in Builders
- [x] Added `GetReference()` and `GetOutput()` methods to `NodeRegistry`
- [x] Added `ClassType()`, `InputFromNode()`, `InputFromRegistry()` methods to `NodeBuilder`
- [x] Fixed `OrchestratorService.SetDefaultBaseModel()` to use `workflow.Assets` instead of deprecated `Pipeline`
- [x] Updated test files to use correct `NodeRegistry` namespace

---

## Objective

Convert the Flux Txt2Img workflow to C#. This is a high-priority workflow as Flux models are widely used.

**Deliverables:**
- `LoadFluxFragment.cs` - Flux-specific model loader with dual CLIP and optional ReFlux support ? Created
- `UpscaleFragment.cs` - Image upscaling with model and latent sampling ? Created
- `FluxTxt2ImgWorkflow.cs` - Complete Flux Txt2Img workflow ? Created
- `FluxTxt2ImgWorkflowTests.cs` - Unit tests for workflow ? Created (26 tests passing)

---

## Analysis

### Flux Workflow Structure (from flux/txt2img.sbn)

The Flux workflow uses:
1. **flux/load-flux.sbn** - Loads UNet, dual CLIPs, VAE, encodes prompts with ReFlux ? `LoadFluxFragment.cs` created
2. **sampler.sbn** - Standard sampler (already have SamplerFragment) ?
3. **upscale.sbn** - Image upscaling ? `UpscaleFragment.cs` created
4. **vae-decode.sbn** - VAE decode (already have VaeDecodeFragment) ?
5. **flux/load-flux.sbn** (detailer scope) - Same loader for detailer ?
6. **detailer-core.sbn** - Detailer (already have DetailerFragment) ?
7. **save.sbn** - Save output (already have SaveFragment) ?

### Key Differences from Z-Image
- Dual CLIP loaders (T5 + ViT)
- ReFlux guidance system (optional - currently disabled due to node issues)
- Model sampling with exponential scaling
- Upscale fragment with unsample/resample chain

---

## Execution Checklist

### Step 0: Pre-Phase Cleanup
**Complexity:** 3
**Status:** [x] Complete

- [x] Audit codebase for Scriban remnants
- [x] Remove duplicate `NodeRegistry` from Models
- [x] Fix `OrchestratorService.SetDefaultBaseModel()`
- [x] Update `NodeBuilder` with missing methods
- [x] Fix test files

---

### Step 1: Create LoadFluxFragment
**Complexity:** 8
**Status:** [x] Complete

Created `BlazorWebApp/Workflows/Fragments/Flux/LoadFluxFragment.cs`

#### Implemented Features
- Load UNet (UNETLoader)
- Load dual CLIPs (DualCLIPLoader with T5 + ViT)
- Load VAE (VAELoader)
- PCLazyLoraLoader for LoRA handling
- ReFluxPatcher for guidance (conditional via `RefluxEnabled` flag)
- PCLazyTextEncode for prompt encoding
- FluxGuidance for conditioning
- EmptySD3LatentImage for latent creation
- VAEEncodeAdvanced for latent encoding
- ModelSamplingAdvancedResolution for model sampling
- Scoped outputs for detailer support

#### ReFluxPatcher Note
`ReFluxPatcher` is controlled by the `Parameters.RefluxEnabled` flag. When disabled, `ModelSamplingAdvancedResolution` wires directly to `lora_loader`. Currently set to `false` in `FluxTxt2ImgWorkflow` due to a node issue in ComfyUI.

---

### Step 2: Create UpscaleFragment
**Complexity:** 8
**Status:** [x] Complete

Created `BlazorWebApp/Workflows/Fragments/Enhancements/UpscaleFragment.cs`

#### Implemented Features
- Load upscale model (UpscaleModelLoader)
- VAE decode current latent
- Upscale with model (ImageUpscaleWithModel)
- Scale to target resolution (ImageScale)
- VAE encode back to latent
- Unsample (ClownsharKSampler_Beta in unsample mode)
- Resample chain (ClownsharkChainsampler_Beta x2)
- Updates `latent_output` registry for downstream consumption

---

### Step 3: Create FluxTxt2ImgWorkflow
**Complexity:** 5
**Status:** [x] Complete

Created `BlazorWebApp/Workflows/Templates/Flux/FluxTxt2ImgWorkflow.cs`

#### Implemented Features
- Metadata with Flux-specific assets (Model, Clip1, Clip2, VAE)
- GetFragments() returning UI-visible fragments:
  - PromptsFragment (for prompt input UI)
  - EmptyLatentFragment (for resolution settings)
  - SamplerFragment
  - UpscaleFragment (conditional)
  - DetailerFragment (conditional)
- Build() composing the workflow:
  1. LoadFlux (with prompt encoding via PCLazyTextEncode/FluxGuidance)
  2. Sampler (ClownsharKSampler_Beta with CFG=1)
  3. Upscale (conditional - unsample/resample chain)
  4. VaeDecode
  5. LoadFlux (detailer scope, conditional)
  6. Detailer (conditional)
  7. Save

---

### Step 4: Test Flux Workflow
**Complexity:** 3
**Status:** [x] Complete

Created `BlazorWebApp.Tests/Workflows/FluxTxt2ImgWorkflowTests.cs`

**26 Tests Passing:**
- Metadata validation (ID, title, base, mode, assets)
- GetFragments validation (prompts, sampler, upscale, detailer)
- Build validation:
  - Valid JSON generation
  - All required nodes present (unet_loader, dual_clip_loader, vae_loader, flux_guidance, sampler_main, vae_decoder, save)
  - ReFluxPatcher node absent (disabled)
  - Conditional nodes (upscale, detailer)
  - Custom asset support

**Integration Testing:** ? Confirmed working in ComfyUI with actual Flux model

---

### Step 5: Delete Scriban Files
**Complexity:** 1
**Status:** [x] Complete

- [x] Delete `Workflows/Templates/flux/txt2img.sbn`
- [x] Delete `Workflows/Fragments/flux/load-flux.sbn`
- [x] Delete `Workflows/Fragments/upscale.sbn`

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 0 - Pre-Phase Cleanup | [x] | 3 | Audit and cleanup done |
| 1 - LoadFluxFragment | [x] | 8 | Created with full feature set, ReFlux optional |
| 2 - UpscaleFragment | [x] | 8 | Created with unsample/resample |
| 3 - FluxTxt2ImgWorkflow | [x] | 5 | Created and building |
| 4 - Test Workflow | [x] | 3 | Unit tests + integration testing complete |
| 5 - Delete .sbn files | [x] | 1 | Deleted all 3 Scriban files |
| **Total** | **100%** | **28** | **6/6 complete** |

---

## Reference: load-flux.sbn Structure

Key nodes implemented:
```
UNETLoader -> [ReFluxPatcher (optional)] -> ModelSamplingAdvancedResolution
DualCLIPLoader -> PCLazyLoraLoader -> PCLazyTextEncode -> FluxGuidance
VAELoader
EmptySD3LatentImage -> VAEEncodeAdvanced
```

Outputs registered:
- `{scope}model_output` - Final model after optional ReFlux and sampling config
- `{scope}clip_output` - Dual CLIP for encoding
- `{scope}vae_output` - VAE for decode
- `{scope}latent_output` - Encoded empty latent
- `{scope}positive_output` - Encoded positive conditioning with FluxGuidance
- `{scope}negative_output` - Empty negative (Flux doesn't use traditional negatives)

---

## Files Created

1. `BlazorWebApp/Workflows/Templates/Flux/FluxTxt2ImgWorkflow.cs` - Main workflow
2. `BlazorWebApp.Tests/Workflows/FluxTxt2ImgWorkflowTests.cs` - Unit tests

---

## Next Steps

1. ~~Create `FluxTxt2ImgWorkflow.cs` composing all fragments~~ ? Done
2. ~~Create unit tests~~ ? Done (26 tests passing)
3. ~~Test in ComfyUI with actual Flux model~~ ? Done
4. ~~Delete .sbn files~~ ? Done

---

**Phase Status:** [x] Complete
