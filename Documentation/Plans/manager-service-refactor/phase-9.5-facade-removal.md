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

### Batch 1-5: Complete ?
See previous sections for details.

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
- [x] `Options.SDModelCheckpoint` kept for path pattern conversion (legacy compatibility)

### Task C: DI Cleanup in Program.cs ?
- [x] `RouterService` ? Interface-only registration (`IRouterService`)
- [x] `WorkflowService` ? Interface-only registration (`IWorkflowService`)  
- [x] `DatabaseService` ? Dual registration (components still use concrete type)
- [x] `ProgressService` ? Dual registration (ProgressContainer uses OnUpdate event)
- [x] `ImageService` ? Dual registration (CivitaiService uses concrete type)
- [x] Added comments explaining dual registration requirements
- [x] Verify all services accessible via interfaces ?
- [x] Build and tests pass ?

### Task D: Relocate Orchestration Properties (Future)
Evaluate moving from ManagerService in future phases:
- [ ] `Images`, `ImagesInfo`, `GridImage` ? IImageService
- [ ] `Progress` ? IProgressService  
- [ ] `Styles` ? IStateService
- [ ] `ButtonTags` ? ISettingsService
- [ ] `CivitaiModels/Images/Creators` ? ICivitaiService

### Task E: Options Property Evaluation (Future)
- [ ] Verify output paths come from appsettings.json
- [ ] Check if `Options` class can be simplified or removed
- [ ] Update `GetCurrentSaveFolder()` if needed

---

## DI Registration Summary

### Interface-Only Registrations (Clean)
| Interface | Implementation |
|-----------|----------------|
| `IEventService` | `EventService` |
| `ISettingsService` | `SettingsService` |
| `IIOService` | `IOService` |
| `IStateService` | `StateService` |
| `IBackendService` | `BackendService` |
| `IModelService` | `ModelService` |
| `IGalleryService` | `GalleryService` |
| `ISessionService` | `SessionService` |
| `IRouterService` | `RouterService` |
| `IWorkflowService` | `WorkflowService` |
| `IAssetResolverService` | `AssetResolverService` (Scoped) |

### Dual Registrations (Concrete + Interface)
| Service | Reason for Dual Registration |
|---------|------------------------------|
| `ComfyUIService` | HttpClient typed client + `IComfyUIService` |
| `DatabaseService` | Components use concrete, services use interface |
| `ImageService` | `CivitaiService` uses concrete type |
| `ProgressService` | `ProgressContainer` subscribes to `OnUpdate` event |

### Concrete-Only Registrations
| Service | Reason |
|---------|--------|
| `ManagerService` | Orchestration service, no interface needed |
| `CsvService` | Utility service |
| `DynamicPromptsService` | Utility service |
| `CacheService` | Utility service |
| `ThemeService` | Utility service |
| `MagickService` | Transient image processing |
| `JavascriptService` | Scoped JS interop |
| `OllamaService` | Scoped LLM service |

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
| Batch 1 | 12 simple | ? Complete |
| Batch 2 | 10 medium | ? Complete |
| Batch 3 | 4 complex | ? Complete |
| Batch 4 | 5 pages | ? Complete |
| Batch 5 | 4 services | ? Complete |
| Task A | Facade removal | ? Complete |
| Task B | WebUI cleanup | ? Complete |
| Task C | DI cleanup | ? Complete |
| Task D | Prop relocation | ?? Deferred (future phase) |
| Task E | Options eval | ?? Deferred (future phase) |

---

## Commit Checkpoints

- [x] After Batch 1 complete
- [x] After Batch 2 complete
- [x] After Batch 3 complete
- [x] After Batch 4 complete
- [x] After Batch 5 complete
- [x] After Task A (facades removed)
- [x] After Task C (DI cleanup complete)
- [ ] Final verification

---

## Phase 9.5 Summary

### Accomplishments
1. **Removed 27 facade properties** from ManagerService
2. **Eliminated 3 WebUI remnants** (ControlNetEnabled, CmdFlags, IsGalleryFiltered)
3. **Cleaned up DI registrations** - simplified RouterService and WorkflowService to interface-only
4. **Added IsGalleryFiltered** to IGalleryService
5. **Updated 19 components and 3 services** to use proper interface injections
6. **Reduced facade usage from ~305 to 0**

### Metrics
- Components still using ManagerService: 19 (for orchestration methods only)
- Facade properties removed: 27
- WebUI remnants removed: 3
- Interface-only registrations: 11
- Build: ? Passing
- Tests: 150/150 ?

### Deferred Items
- Task D (property relocation) and Task E (Options evaluation) deferred to future phases
- These are lower priority optimizations that can be addressed incrementally

---

**End of Document**
