# Phase 10 - Legacy Deprecation &amp; RouterService Refactor

## Status
**Phase:** 10  
**Build Status:** &#9745; Pass | **Tests:** &#9745; Updated

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

Remove all legacy parameter classes and DTOs, establishing `GenerationParameters` as the **sole parameter model** throughout the system. This phase eliminates the temporary conversion layer added in Phase 6 and resolves the Phase 8 blocker.

---

## Context

### Current State (Legacy System)
The legacy system has these parameter classes that need removal:

| Class | Location | Purpose |
|-------|----------|---------|
| `SharedParameters` | `Models/SharedParameters.cs` | Base class for all parameter types |
| `Txt2ImgParameters` | `Models/Txt2ImgParameters.cs` | Text-to-image parameters |
| `Img2ImgParameters` | `Models/Img2ImgParameters.cs` | Image-to-image parameters |
| `Img2VidParameters` | `Models/Img2VidParameters.cs` | Image-to-video parameters |
| `UpscaleParameters` | `Models/UpscaleParameters.cs` | Upscale/extras parameters |
| `SeedVR2Parameters` | Nested in DTOs | SeedVR2 upscaler parameters - **REMOVED** |
| `ConditioningVariationParameters` | Nested in DTOs | Conditioning variation - **REMOVED** |
| `SeedVarianceEnhancerParameters` | Nested in DTOs | Seed variance enhancer - **REMOVED** |
| `FrameInterpolationParameters` | Nested in DTOs | Frame interpolation - **REMOVED** (inlined to Img2VidParameters) |

### Legacy DTOs (ComfyUI) - REMOVED
| Class | Location | Status |
|-------|----------|--------|
| `Txt2ImgComfyUI` | `Data/Dtos/ComfyUI/Workflow/Txt2ImgComfyUI.cs` | **REMOVED** |
| `Img2ImgComfyUI` | `Data/Dtos/ComfyUI/Workflow/Img2ImgComfyUI.cs` | **REMOVED** |
| `Img2VidComfyUI` | `Data/Dtos/ComfyUI/Workflow/Img2VidComfyUI.cs` | **REMOVED** |

### Current Services - Updated
| Service | Legacy Dependencies | Status |
|---------|---------------------|--------|
| `StateService` | `ParametersTxt2Img`, `ParametersImg2Img`, `ParametersUpscale`, `ParametersImg2Vid` | Kept for state persistence |
| `ImageService` | Legacy methods stubbed out | `GenerateImagesAsync()`, `GenerateVideoAsync()` are primary |
| `RouterService` | Legacy methods removed | `PostGenerationAsync()`, `PostVideoGenerationAsync()` only |
| `ComfyUIService` | Legacy methods removed | `PostGenerationAsync()`, `PostVideoGenerationAsync()` only |

### Phase 8 Blocker - Deferred
**Problem:** When fragments are first activated via `CollapsibleFeatureSection`, they don't exist in `ParameterService.Current.Fragments`, causing model dropdowns (like in `SeedVR2Form`) to not be initialized.

**Status:** Deferred - will be addressed if issues arise during testing.

---

## Dependencies

### Files Removed
```
Extensions/ParameterMapper.cs
Data/Dtos/ComfyUI/Workflow/Txt2ImgComfyUI.cs
Data/Dtos/ComfyUI/Workflow/Img2ImgComfyUI.cs
Data/Dtos/ComfyUI/Workflow/Img2VidComfyUI.cs
```

### Files to Still Remove (Phase 2)
```
Pages/Txt2Img.razor
Pages/Img2Img.razor
Pages/Img2Vid.razor
Components/Txt2Img/GenerateFormTxt2Img.razor
Components/Img2Img/GenerateFormImg2Img.razor
Components/Img2Vid/GenerateFormImg2Vid.razor
Components/Shared/Generation/PromptFields.razor (keep PromptsForm.razor)
```

### Legacy Parameter Files - Kept for Compatibility
The following files are kept because they are used by StateService for serializing/deserializing saved state:
```
Models/SharedParameters.cs - Base class
Models/Txt2ImgParameters.cs - Txt2Img state (simplified, no SeedVR2/ConditioningVariation)
Models/Img2ImgParameters.cs - Img2Img state (simplified, no ToComfyUI)
Models/Img2VidParameters.cs - Img2Vid state (simplified, inline frame interpolation)
Models/UpscaleParameters.cs - Upscale state
```

---

## Execution Checklist

### Step 10.1: Fix SetFragmentActive to Create Fragments
**Complexity:** 3
**Status:** [x] Complete

#### Objective
Resolve the Phase 8 blocker by ensuring `SetFragmentActive()` creates the fragment with defaults if it doesn't exist.

#### Implementation
- Updated `SetFragmentActive()` to create fragment with defaults when activating a non-existent fragment
- Added `CreateFragmentWithDefaults(fragmentId)` helper method that:
  - Looks up the workflow's pipeline for matching step
  - Applies pipeline default values
  - Applies fragment template defaults
  - Uses `InferFragmentFile()` for common ID to file mappings
- Added `InferFragmentFile(fragmentId)` helper with known mappings for:
  - prompts, main_sampler, refiner_sampler, latent, upscale
  - seed_vr2, conditioning_variation, detailer, frame_interpolation
- Added `GetWorkflowById(Guid)` to IWorkflowService and WorkflowService

#### Files Modified
- `Services/GenerationParameterService.cs` - Added fragment creation on activation
- `Services/IWorkflowService.cs` - Added GetWorkflowById method
- `Services/WorkflowService.cs` - Implemented GetWorkflowById method

---

### Step 10.2: Update RouterService for GenerationParameters
**Complexity:** 5
**Status:** [x] Complete

#### Objective
Add new unified generation methods that accept `GenerationParameters` directly.

#### Tasks
- [x] Add `IRouterService.PostGenerationAsync(GenerationParameters, Workflow)` method
- [x] Add `IRouterService.PostVideoGenerationAsync(GenerationParameters, Workflow)` method
- [x] Build ComfyUI workflow payload directly from `GenerationParameters.Fragments`
- [x] Remove legacy methods (`PostTxt2Img`, `PostImg2Img`, `PostImg2Vid`)

#### Files Modified
- `Services/IRouterService.cs` - Removed legacy methods
- `Services/RouterService.cs` - Removed legacy methods, simplified constructor

---

### Step 10.3: Update ImageService to Use New Router Methods
**Complexity:** 5
**Status:** [x] Complete

#### Objective
Refactor `GenerateImagesAsync()` and `GenerateVideoAsync()` to use new router methods, eliminating the legacy conversion layer.

#### Tasks
- [x] Update `GenerateImagesAsync()` to call `PostGenerationAsync()` directly
- [x] Update `GenerateVideoAsync()` to call `PostVideoGenerationAsync()` directly
- [x] Add `PrepareGenerationParametersAsync()` for wildcard/seed processing
- [x] Add `SaveImagesFromGenerationParams()` for new save flow
- [x] Add `SaveVideosFromGenerationParams()` for new video save flow
- [x] Add helper methods for path/filename generation from GenerationParameters
- [x] Stub out legacy methods `GetImages()` and `GetVideo()` (throw NotSupportedException)

#### Files Modified
- `Services/IImageService.cs` - Removed legacy methods from interface
- `Services/ImageService.cs` - Refactored to use new router methods, legacy methods throw exceptions

---

### Step 10.4: Update Parser for GenerationParameters
**Complexity:** 3
**Status:** [x] Complete (Skipped - Handled in ImageService)

#### Objective
Wildcard/seed processing now handled directly in ImageService.PrepareGenerationParametersAsync()

---

### Step 10.5: Update StateService
**Complexity:** 3
**Status:** [x] Complete

#### Objective
Ensure StateService properly handles GenerationParameters initialization.

#### Tasks
- [x] Updated Img2VidParameters to use inline frame interpolation properties
- [x] Legacy parameter classes simplified (removed DTOs/ToComfyUI methods)
- [x] StateService still uses legacy parameters for state persistence (intentional)

#### Files Modified
- `Models/Txt2ImgParameters.cs` - Removed SeedVR2, ConditioningVariation, SeedVarianceEnhancer properties
- `Models/Img2ImgParameters.cs` - Removed ToComfyUI method
- `Models/Img2VidParameters.cs` - Inlined FrameInterpolation properties
- `Services/StateService.cs` - Updated InitializeImg2VidParameters

---

### Step 10.6: Remove Legacy DTOs
**Complexity:** 8
**Status:** [x] Complete

#### Tasks
- [x] Remove `Extensions/ParameterMapper.cs`
- [x] Remove `Data/Dtos/ComfyUI/Workflow/Txt2ImgComfyUI.cs`
- [x] Remove `Data/Dtos/ComfyUI/Workflow/Img2ImgComfyUI.cs`
- [x] Remove `Data/Dtos/ComfyUI/Workflow/Img2VidComfyUI.cs`
- [x] Remove FrameInterpolationParameters ignore from AppDbContext
- [x] Update IComfyUIService - Remove legacy methods
- [x] Update ComfyUIService - Remove legacy methods
- [x] Remove legacy methods from IRouterService
- [x] Remove legacy methods from RouterService
- [x] Remove DetailerParameters reference from Parser.cs
- [x] Fix syntax errors (Parser.cs ConvertCloudMount, ParseHighresResolution)
- [x] Fix VideoViewer binding in InfiniteScrollMasonry
- [x] Update test files (RouterServiceTests, StateServiceTests)

---

### Step 10.7: Test Complete Generation Flow
**Complexity:** 5
**Status:** [ ] Not Started

#### Objective
Verify that the new unified flow works end-to-end.

#### Tests
- [ ] Flux txt2img generation works
- [ ] SD txt2img generation works
- [ ] Qwen img2img-edit generation works (with source image)
- [ ] Wan img2vid generation works (with source image)
- [ ] SeedVR2Form model dropdowns work when activated
- [ ] Upscale fragment works when activated
- [ ] All fragment values are correctly passed to ComfyUI
- [ ] Seeds are properly randomized when -1
- [ ] Wildcards are expanded correctly
- [ ] Generated images are saved to database correctly

---

### Step 10.8: Remove Legacy UI Pages (Future)
**Complexity:** 3
**Status:** [x] Complete

**Note:** Most legacy UI files had already been removed in earlier phases.

#### Tasks Completed
- [x] Removed `Components/Img2Vid/PromptFieldsSimple.razor` (unused)
- [x] Removed `Components/Img2Vid/PromptFieldsSimple.razor.css` (orphaned)

#### Files Already Removed (prior phases)
- `Pages/Txt2Img.razor`
- `Pages/Img2Img.razor`
- `Pages/Img2Vid.razor`
- `Components/Txt2Img/GenerateFormTxt2Img.razor`
- `Components/Img2Img/GenerateFormImg2Img.razor`
- `Components/Img2Vid/GenerateFormImg2Vid.razor`
- `Components/Shared/Generation/PromptFields.razor`

#### Files Kept (still in use)
- `Components/Img2Img/Img2ImgCanvas.razor` - Used for canvas/inpainting functionality

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 10.1 | [x] | 3 | SetFragmentActive fix - Complete |
| 10.2 | [x] | 5 | RouterService cleanup - Complete |
| 10.3 | [x] | 5 | ImageService refactor - Complete |
| 10.4 | [x] | 3 | Skipped - Handled in ImageService |
| 10.5 | [x] | 3 | StateService/Models cleanup - Complete |
| 10.6 | [x] | 8 | DTO removal &amp; build fixes - Complete |
| 10.7 | [ ] | 5 | E2E testing - Pending |
| 10.8 | [x] | 3 | Legacy UI removal - Complete |

**Total Complexity:** 35 points

---

## Issues &amp; Resolutions

| Issue | Resolution |
|-------|------------|
| Step 10.1 deferred | Completed - SetFragmentActive now creates fragments with defaults |
| Step 10.4 skipped | Wildcard/seed processing handled in ImageService.PrepareGenerationParametersAsync() |
| FrameInterpolationParameters | Inlined into Img2VidParameters as separate properties |
| SeedVR2Parameters, ConditioningVariationParameters | Removed from Txt2ImgParameters - now fragment-based |
| Parser.cs ParseComfyDetailerLoras | Removed - DetailerParameters no longer exists |
| VideoViewer @bind-IsVisible | Changed to explicit IsVisible + IsVisibleChanged binding |
| ImageService GetImagePath | Added missing helper method |
| ImageService GetOrAddSampler | Changed to GetSamplerIdByName |
| RouterService constructor | Simplified to 3 dependencies (removed IStateService, IModelService) |
| Test files | Updated RouterServiceTests and StateServiceTests for new API |
| PromptFieldsSimple.razor unused | Removed along with its CSS file |
| GetWorkflowById missing | Added to IWorkflowService and WorkflowService |

---

## Commit Checkpoints

- [x] After Step 10.1 complete (fragment activation fix)
- [x] After Step 10.3 complete (ImageService refactor)
- [x] After Step 10.5 complete (StateService cleanup)
- [x] After Step 10.6 complete (DTO removal, build passing)
- [ ] After Step 10.7 complete (E2E testing passed)
- [x] After Step 10.8 complete (legacy UI removed)

---

**Phase Status:** In Progress [~] - Build Passing, E2E Testing Pending (Step 10.7)
