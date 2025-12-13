# Phase 9.5 - Facade Removal & Cleanup

## Status
**Phase:** 9.5 (Detour from main refactor)  
**Started:** 2025-01-15  
**Build Status:** ? Passing | **Tests:** 150/150 ?

---

## Objective

Remove backward compatibility facades from ManagerService, eliminate WebUI remnants, and ensure consistent interface-based DI across the codebase.

---

## Audit Summary

### Component Injection Analysis

| Metric | Count |
|--------|-------|
| Components with `@inject ManagerService M` | 31 ? 19 |
| Components with direct interface injections | 65+ |
| Total facade property usages in components | ~305 ? 0 ? |

### Facade Removal Summary

All facade properties have been removed from ManagerService:
- ? `State` ? Use `IStateService.State`
- ? `ParametersTxt2Img` ? Use `IStateService.ParametersTxt2Img`
- ? `ParametersImg2Img` ? Use `IStateService.ParametersImg2Img`
- ? `ParametersImg2Vid` ? Use `IStateService.ParametersImg2Vid`
- ? `ParametersUpscale` ? Use `IStateService.ParametersUpscale`
- ? `Settings` ? Use `ISettingsService.Settings`
- ? `CheckpointModels` ? Use `IModelService.CheckpointModels`
- ? `DiffusionModels` ? Use `IModelService.DiffusionModels`
- ? `SDVAEs` ? Use `IModelService.VAEModels`
- ? `ClipModels` ? Use `IModelService.ClipModels`
- ? `ClipVisionModels` ? Use `IModelService.ClipVisionModels`
- ? `SDADetailerModels` ? Use `IModelService.ADetailerModels`
- ? `Samplers` ? Use `IBackendService.Samplers`
- ? `Schedulers` ? Use `IBackendService.Schedulers`
- ? `Upscalers` ? Use `IBackendService.Upscalers`
- ? `IsComfyUIUp` ? Use `IBackendService.IsBackendAvailable`
- ? `Folders` ? Use `IGalleryService.Folders`
- ? `Projects` ? Use `IGalleryService.Projects`
- ? `SelectedImageIds` ? Use `IGalleryService.SelectedImageIds`
- ? `CanvasStates` ? Use `ISessionService.CanvasStates`
- ? `CanvasImageData` ? Use `ISessionService.CanvasImageData`
- ? `CanvasMaskData` ? Use `ISessionService.CanvasMaskData`
- ? `UpscaleImageData` ? Use `ISessionService.UpscaleImageData`
- ? `Img2VidInputImage` ? Use `ISessionService.Img2VidInputImage`
- ? `Img2ImgInputImage` ? Use `ISessionService.Img2ImgInputImage`
- ? `ImageEditorState` ? Use `ISessionService.ImageEditorState`
- ? `SessionGeneratedVideos` ? Use `ISessionService.SessionGeneratedVideos`

### Removed WebUI Remnants
- ? `IsGalleryFiltered` ? Moved to `IGalleryService.IsGalleryFiltered`
- ? `ControlNetEnabled` ? Removed (WebUI remnant)
- ? `CmdFlags` ? Removed (WebUI remnant)

---

## Execution Checklist

### Batch 1: Simple Components (1-5 usages) ?
Components with minimal facade usages, straightforward replacements.

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 1 | `Components/Resources/CivitaiFileButton.razor` | ? | M.State ? State.State |
| 2 | `Components/Resources/CivitaiModelVersionInfoPanel.razor` | ? | M.State ? State.State |
| 3 | `Components/Resources/DanbooruSearchesDrawer.razor` | ? | M.Settings ? Settings.Settings |
| 4 | `Components/Resources/CivitaiImageCard.razor` | ? | Removed unused M injection |
| 5 | `Pages/Prompts.razor` | ? | M.State ? State.State |
| 6 | `Components/Prompts/PromptsPanel.razor` | ? | Added IStateService, kept M for GetStyles() |
| 7 | `Components/Prompts/PromptDialog.razor` | ?? | Skipped - only uses M.GetStyles() (orchestration) |
| 8 | `Components/Shared/Generation/WorkflowAssetsPanel.razor` | ? | Added IStateService, kept M for workflow methods |
| 9 | `Components/Shared/Generation/WorkflowAssetSelector.razor` | ?? | Skipped - only uses orchestration methods |
| 10 | `Components/Shared/Generation/ConditioningVariationForm.razor` | ? | M.Settings ? Settings.Settings |
| 11 | `Components/Shared/AssetViewer.razor` | ?? | Skipped - already uses interfaces, M only for orchestration |
| 12 | `Components/Gallery/SelectionsDialog.razor` | ? | M.SelectedImageIds ? Gallery.SelectedImageIds |

**Batch 1 Complete:** 7 updated, 4 skipped (orchestration only), 1 N/A

---

### Batch 2: Medium Components (5-25 usages) ?

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 13 | `Components/Img2Img/Img2ImgCanvas.razor` | ? | M.State/Canvas ? State.State/Session.Canvas |
| 14 | `Components/Resources/CivitaiImageDialog.razor` | ? | Added IStateService for parameters |
| 15 | `Components/Resources/ResourceImageDialog.razor` | ? | Added IStateService for parameters |
| 16 | `Components/Resources/CivitaiModelsPanel.razor` | ?? | Already uses interfaces, M for CivitaiModels |
| 17 | `Pages/Danbooru.razor` | ? | Added IStateService/ISettingsService |
| 18 | `Components/Gallery/InfiniteScrollMasonry.razor` | ? | M.SelectedImageIds ? Gallery.SelectedImageIds |
| 19 | `Components/Shared/Image/ImageInfoDialog.razor` | ?? | Already uses IStateService, M for orchestration |
| 20 | `Components/Shared/Image/ImageViewer.razor` | ?? | Already uses IStateService, M for orchestration |
| 21 | `Components/Shared/Generation/GeneratedImageTabs.razor` | ?? | Already uses interfaces, M for orchestration |
| 22 | `Components/Img2Vid/GeneratedVideoTabs.razor` | ?? | Already uses interfaces, M for orchestration |

**Batch 2 Complete:** 5 updated, 5 skipped (already proper/orchestration only)

---

### Batch 3: Complex Components (25+ usages) ?

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 23 | `Components/Prompts/WildcardsPanel.razor` | ? | Full refactor - IStateService + ISettingsService |
| 24 | `Components/Shared/Generation/LLMPromptEnhancerForm.razor` | ? | Removed M completely - only uses State/Settings |
| 25 | `Components/Img2Vid/GenerateFormImg2Vid.razor` | ? | Full refactor - removed M completely |
| 26 | `Components/Txt2Img/GenerateFormTxt2Img.razor` | ?? | Already uses interfaces, M only for orchestration |

**Batch 3 Complete:** 3 updated, 1 skipped (already proper)

---

### Batch 4: Pages ?

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 27 | `Pages/Index.razor` | ? | M.IsGalleryFiltered ? Gallery.IsGalleryFiltered, removed M |
| 28 | `Pages/Txt2Img.razor` | ?? | Only uses M for orchestration methods |
| 29 | `Pages/Img2Img.razor` | ?? | Only uses M for orchestration methods |
| 30 | `Pages/Img2Vid.razor` | ?? | Only uses M for orchestration methods |
| 31 | `Components/Shared/MainLayout.razor` | ?? | Only uses M for orchestration methods |

**Batch 4 Complete:** 1 updated, 4 skipped (orchestration only)

**New API:** Added `IsGalleryFiltered` property to `IGalleryService`/`GalleryService`

---

### Batch 5: Service Files ?

| # | Service | Status | Notes |
|---|---------|--------|-------|
| 32 | `Services/ResourcesService.cs` | ? | Added IStateService, replaced facade usages |
| 33 | `Services/RouterService.cs` | ? | Added IBackendService + IStateService |
| 34 | `Services/MagickService.cs` | ? | Replaced M with ISettingsService completely |
| 35 | `Services/CivitaiService.cs` | ?? | Only uses M.CurrentProgress (orchestration) |

**Batch 5 Complete:** 3 updated, 1 skipped (orchestration only)

---

## Post-Batch Tasks

### Task A: Remove Facades from ManagerService ?
- [x] Remove `#region Service Facades` block (27 facade properties removed)
- [x] Update internal references to use injected services directly
- [x] Remove WebUI remnants: `ControlNetEnabled`, `CmdFlags`, `IsGalleryFiltered`
- [x] Verify no compilation errors
- [x] Run full test suite (150/150 ?)

### Task B: WebUI Cleanup ?
- [x] Remove `CmdFlags` property
- [x] Remove `ControlNetEnabled` property
- [x] `IsGalleryFiltered` moved to `IGalleryService`
- [ ] Evaluate `Options.SDModelCheckpoint` usage (kept for path pattern conversion)
- [ ] Review `ParseWebUIInfoParameters()` - mark as legacy import helper (N/A - not found)

### Task C: DI Cleanup in Program.cs
- [ ] Convert dual registrations to interface-only
- [ ] Evaluate `IMagickService` creation
- [ ] Verify all services accessible via interfaces

### Task D: Relocate Orchestration Properties
Evaluate moving from ManagerService:
- [ ] `Images`, `ImagesInfo`, `GridImage` ? IImageService
- [ ] `Progress` ? IProgressService  
- [ ] `Styles` ? IStateService
- [ ] `ButtonTags` ? ISettingsService
- [ ] `CivitaiModels/Images/Creators` ? ICivitaiService (future)

### Task E: Options Property Evaluation
- [ ] Verify output paths come from appsettings.json
- [ ] Check if `Options` class can be simplified or removed
- [ ] Update `GetCurrentSaveFolder()` if needed

---

## Remaining Orchestration Properties on ManagerService

These properties remain as true orchestration concerns:

| Property | Purpose |
|----------|---------|
| `Options` | Output path configuration (from ComfyUI backend) |
| `Images`, `ImagesInfo` | Current generation result storage |
| `GeneratedImageEntities` | Generated image DTOs for gallery |
| `GridImage` | Grid generation result |
| `Progress` | Inference progress tracking |
| `Styles` | Available prompt styles |
| `GeneratedUpscaleImage` | Upscale result storage |
| `ButtonTags` | UI button tags configuration |
| `CivitaiModels/Images/Creators` | Civitai browsing state |
| `ComfyWSClientId` | WebSocket session ID |
| `ResourceTypeDirectories` | Resource path configuration |
| `CurrentProgress` | Progress with event publishing |
| `IsConverging` | Convergence state with event publishing |

---

## Progress Tracking

| Phase | Components | Status |
|-------|------------|--------|
| Batch 1 | 12 simple | ? Complete (7 updated, 4 skipped, 1 N/A) |
| Batch 2 | 10 medium | ? Complete (5 updated, 5 skipped) |
| Batch 3 | 4 complex | ? Complete (3 updated, 1 skipped) |
| Batch 4 | 5 pages | ? Complete (1 updated, 4 skipped) |
| Batch 5 | 4 services | ? Complete (3 updated, 1 skipped) |
| Task A | Facade removal | ? Complete (27 facades removed) |
| Task B | WebUI cleanup | ? Complete (3 properties removed) |
| Task C | DI cleanup | ? Not Started |
| Task D | Prop relocation | ? Not Started |
| Task E | Options eval | ? Not Started |

---

## Commit Checkpoints

- [x] After Batch 1 complete
- [x] After Batch 2 complete
- [x] After Batch 3 complete
- [x] After Batch 4 complete
- [x] After Batch 5 complete
- [x] After Task A (facades removed)
- [ ] After Tasks B-E (cleanup complete)
- [ ] Final verification

---

**End of Document**
