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
| Components with `@inject ManagerService M` | 31 |
| Components with direct interface injections | 65+ |
| Total facade property usages in components | ~305 |

### Top Facade Usages (Priority Order)

| Facade | Count | Replace With |
|--------|-------|--------------|
| `M.State` | 67 | `State.State` (via IStateService) |
| `M.ParametersImg2Vid` | 66 | `State.ParametersImg2Vid` |
| `M.ParametersTxt2Img` | 48 | `State.ParametersTxt2Img` |
| `M.Settings` | 47 | `Settings.Settings` (via ISettingsService) |
| `M.ParametersImg2Img` | 38 | `State.ParametersImg2Img` |
| `M.Canvas*` | 15 | `Session.Canvas*` (via ISessionService) |
| `M.CivitaiModels` | 11 | Keep on ManagerService (Civitai orchestration) |
| `M.SelectedImageIds` | 9 | `Gallery.SelectedImageIds` (via IGalleryService) |
| `M.Samplers` | 2 | `Backend.Samplers` (via IBackendService) |
| `M.Schedulers` | 2 | `Backend.Schedulers` |

---

## Execution Checklist

### Batch 1: Simple Components (1-5 usages)
Components with minimal facade usages, straightforward replacements.

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 1 | `Components/Resources/CivitaiFileButton.razor` | ? | |
| 2 | `Components/Resources/CivitaiModelVersionInfoPanel.razor` | ? | |
| 3 | `Components/Resources/DanbooruSearchesDrawer.razor` | ? | |
| 4 | `Components/Resources/CivitaiImageCard.razor` | ? | |
| 5 | `Pages/Prompts.razor` | ? | |
| 6 | `Components/Prompts/PromptsPanel.razor` | ? | |
| 7 | `Components/Prompts/PromptDialog.razor` | ? | |
| 8 | `Components/Shared/Generation/WorkflowAssetsPanel.razor` | ? | |
| 9 | `Components/Shared/Generation/WorkflowAssetSelector.razor` | ? | |
| 10 | `Components/Shared/Generation/ConditioningVariationForm.razor` | ? | |
| 11 | `Components/Shared/AssetViewer.razor` | ? | |
| 12 | `Components/Gallery/SelectionsDialog.razor` | ? | |

**Batch 1 Total:** 12 components

---

### Batch 2: Medium Components (5-25 usages)

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 13 | `Components/Img2Img/Img2ImgCanvas.razor` | ? | Canvas + Session |
| 14 | `Components/Resources/CivitaiImageDialog.razor` | ? | State + Settings |
| 15 | `Components/Resources/ResourceImageDialog.razor` | ? | State + Settings |
| 16 | `Components/Resources/CivitaiModelsPanel.razor` | ? | Keep M.CivitaiModels |
| 17 | `Pages/Danbooru.razor` | ? | State + Settings |
| 18 | `Components/Gallery/InfiniteScrollMasonry.razor` | ? | Gallery |
| 19 | `Components/Shared/Image/ImageInfoDialog.razor` | ? | State |
| 20 | `Components/Shared/Image/ImageViewer.razor` | ? | Session |
| 21 | `Components/Shared/Generation/GeneratedImageTabs.razor` | ? | |
| 22 | `Components/Img2Vid/GeneratedVideoTabs.razor` | ? | Session |

**Batch 2 Total:** 10 components

---

### Batch 3: Complex Components (25+ usages)

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 23 | `Components/Prompts/WildcardsPanel.razor` | ? | State + Settings |
| 24 | `Components/Shared/Generation/LLMPromptEnhancerForm.razor` | ? | Settings heavy |
| 25 | `Components/Img2Vid/GenerateFormImg2Vid.razor` | ? | Largest - 80 usages |
| 26 | `Components/Txt2Img/GenerateFormTxt2Img.razor` | ? | Already uses interfaces |

**Batch 3 Total:** 4 components

---

### Batch 4: Pages

| # | Component | Status | Notes |
|---|-----------|--------|-------|
| 27 | `Pages/Index.razor` | ? | Main layout |
| 28 | `Pages/Txt2Img.razor` | ? | Already uses State |
| 29 | `Pages/Img2Img.razor` | ? | |
| 30 | `Pages/Img2Vid.razor` | ? | |
| 31 | `Components/Shared/MainLayout.razor` | ? | App shell |

**Batch 4 Total:** 5 components

---

### Batch 5: Service Files

| # | Service | Status | Notes |
|---|---------|--------|-------|
| 32 | `Services/ResourcesService.cs` | ? | |
| 33 | `Services/RouterService.cs` | ? | |
| 34 | `Services/MagickService.cs` | ? | |
| 35 | `Services/CivitaiService.cs` | ? | Check for M.* usage |

**Batch 5 Total:** 4 service files

---

## Post-Batch Tasks

### Task A: Remove Facades from ManagerService
After all batches complete:
- [ ] Remove `#region Service Facades` block
- [ ] Verify no compilation errors
- [ ] Run full test suite

### Task B: WebUI Cleanup
- [ ] Remove `CmdFlags` property
- [ ] Remove `ControlNetEnabled` property
- [ ] Evaluate `Options.SDModelCheckpoint` usage
- [ ] Review `ParseWebUIInfoParameters()` - mark as legacy import helper

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

## Replacement Patterns

### Pattern 1: State Access
```razor
// Before
@inject ManagerService M
M.State.Gallery.ProjectId
M.ParametersTxt2Img.Prompt

// After
@inject IStateService State
State.State.Gallery.ProjectId
State.ParametersTxt2Img.Prompt
```

### Pattern 2: Settings Access
```razor
// Before
@inject ManagerService M
M.Settings.Generation.Shared.Steps.Max

// After
@inject ISettingsService Settings
Settings.Settings.Generation.Shared.Steps.Max
```

### Pattern 3: Backend Access
```razor
// Before
@inject ManagerService M
M.Samplers
M.Schedulers
M.Upscalers

// After
@inject IBackendService Backend
Backend.Samplers
Backend.Schedulers
Backend.Upscalers
```

### Pattern 4: Session Access
```razor
// Before
@inject ManagerService M
M.CanvasImageData
M.Img2VidInputImage

// After
@inject ISessionService Session
Session.CanvasImageData
Session.Img2VidInputImage
```

### Pattern 5: Gallery Access
```razor
// Before
@inject ManagerService M
M.SelectedImageIds
M.Folders
M.Projects

// After
@inject IGalleryService Gallery
Gallery.SelectedImageIds
Gallery.Folders
Gallery.Projects
```

### Pattern 6: Model Access
```razor
// Before
@inject ManagerService M
M.CheckpointModels
M.DiffusionModels
M.SDVAEs

// After
@inject IModelService Models
Models.CheckpointModels
Models.DiffusionModels
Models.VAEModels
```

---

## Keep on ManagerService (Do NOT Remove)

These properties/methods should remain as they are true orchestration concerns:

| Property/Method | Reason |
|-----------------|--------|
| `Options` | Output path configuration (evaluate later) |
| `Images`, `ImagesInfo` | Generation result storage |
| `GridImage` | Grid generation result |
| `Progress` | Inference progress tracking |
| `IsConverging`, `CurrentProgress` | Generation state |
| `Styles` | Prompt style orchestration |
| `CivitaiModels/Images/Creators` | Civitai browsing state |
| `ComfyWSClientId` | WebSocket session ID |
| `GetCurrentSaveFolder()` | Path resolution logic |
| `ConvertPathPattern()` | Path pattern conversion |
| `ParseAndCleanCopiedPrompt()` | Prompt parsing logic |
| `SetLoras()` | Lora management |
| `LoadImageInfoParameters()` | Parameter loading |
| `SetGenerationParameter()` | Parameter setting |
| Workflow methods | Workflow orchestration |

---

## Progress Tracking

| Phase | Components | Status |
|-------|------------|--------|
| Batch 1 | 12 simple | ? Not Started |
| Batch 2 | 10 medium | ? Not Started |
| Batch 3 | 4 complex | ? Not Started |
| Batch 4 | 5 pages | ? Not Started |
| Batch 5 | 4 services | ? Not Started |
| Task A | Facade removal | ? Not Started |
| Task B | WebUI cleanup | ? Not Started |
| Task C | DI cleanup | ? Not Started |
| Task D | Prop relocation | ? Not Started |
| Task E | Options eval | ? Not Started |

---

## Commit Checkpoints

- [ ] After Batch 1 complete
- [ ] After Batch 2 complete
- [ ] After Batch 3 complete
- [ ] After Batch 4 complete
- [ ] After Batch 5 complete
- [ ] After Task A (facades removed)
- [ ] After Tasks B-E (cleanup complete)
- [ ] Final verification

---

**End of Document**
