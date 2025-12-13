# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 9.5 - Facade Removal & Cleanup (Detour)  
**Last Updated:** 2025-01-15

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **658-line orchestrator** that coordinates between specialized services.

### Current Architecture

```
???????????????????????????????????????????????????????????????????????
?                    ManagerService (Orchestrator)                     ?
?  - Coordinates between specialized services                         ?
?  - Manages generation state (Images, Progress, Options)             ?
?  - Provides facade properties for backward compatibility            ?
???????????????????????????????????????????????????????????????????????
                                    ?
        ?????????????????????????????????????????????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
?   StateService  ?   ?   ModelService  ?   ? WorkflowService ?
?  - AppState     ?   ?  - Checkpoints  ?   ?  - Templates    ?
?  - Parameters   ?   ?  - Diffusion    ?   ?  - Pipeline     ?
?  - Load/Save    ?   ?  - VAEs, CLIPs  ?   ?  - Assets       ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
? SettingsService ?   ? BackendService  ?   ? GalleryService  ?
?  - App settings ?   ?  - ComfyUI API  ?   ?  - Folders      ?
?  - JSON persist ?   ?  - Health check ?   ?  - Projects     ?
?  - Validation   ?   ?  - Samplers     ?   ?  - Selection    ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
?  EventService   ?   ?  ImageService   ?   ? SessionService  ?
?  - Pub/sub      ?   ?  - Generation   ?   ?  - Canvas state ?
?  - Typed events ?   ?  - Saving       ?   ?  - Editor state ?
?  - Mediator     ?   ?  - Video gen    ?   ?  - Videos       ?
???????????????????   ???????????????????   ???????????????????
```

---

## Completed Phases

### ? Phase 1-8: Service Extraction (Complete)
- Extracted 8 specialized services from ManagerService
- Created interfaces for all extracted services
- 138 tests passing

### ? Phase 8.5: ManagerService Cleanup (Complete)
- ManagerService reduced from ~1600 to 658 lines (59% reduction)
- All legacy Action events removed
- All components use EventService

### ? Phase 9: Service Interfaces (Complete)
- Created interfaces: IWorkflowService, IProgressService, IImageService, IRouterService
- 150 tests passing

---

## Current Phase: Phase 9.5 - Facade Removal & Cleanup

### Objective
Remove backward compatibility facades from ManagerService, eliminate WebUI remnants, and ensure consistent interface-based DI across the codebase.

---

### Task 1: Identify Facade Usages (~25 facade properties)

#### Service Facades in ManagerService (to be removed):

| Facade Property | Source Service | Component Usages |
|-----------------|----------------|------------------|
| `M.State` | `IStateService.State` | Multiple pages, components |
| `M.ParametersTxt2Img` | `IStateService.ParametersTxt2Img` | Txt2Img.razor, forms |
| `M.ParametersImg2Img` | `IStateService.ParametersImg2Img` | Img2Img.razor, forms |
| `M.ParametersUpscale` | `IStateService.ParametersUpscale` | Upscale components |
| `M.ParametersImg2Vid` | `IStateService.ParametersImg2Vid` | Img2Vid.razor, forms |
| `M.Settings` | `ISettingsService.Settings` | Settings dialogs |
| `M.CheckpointModels` | `IModelService.CheckpointModels` | Model selectors |
| `M.DiffusionModels` | `IModelService.DiffusionModels` | Model selectors |
| `M.SDVAEs` | `IModelService.VAEModels` | VAE selectors |
| `M.ClipModels` | `IModelService.ClipModels` | CLIP selectors |
| `M.ClipVisionModels` | `IModelService.ClipVisionModels` | Clip vision selectors |
| `M.SDADetailerModels` | `IModelService.ADetailerModels` | Detailer forms |
| `M.Samplers` | `IBackendService.Samplers` | Sampler dropdowns |
| `M.Schedulers` | `IBackendService.Schedulers` | Scheduler dropdowns |
| `M.Upscalers` | `IBackendService.Upscalers` | Upscaler forms |
| `M.IsComfyUIUp` | `IBackendService.IsBackendAvailable` | Status indicators |
| `M.Folders` | `IGalleryService.Folders` | Gallery navigation |
| `M.Projects` | `IGalleryService.Projects` | Project lists |
| `M.SelectedImageIds` | `IGalleryService.SelectedImageIds` | Multi-select |
| `M.CanvasStates` | `ISessionService.CanvasStates` | Img2Img canvas |
| `M.SessionGeneratedVideos` | `ISessionService.SessionGeneratedVideos` | Video display |
| `M.CanvasImageData` | `ISessionService.CanvasImageData` | Img2Img canvas |
| `M.CanvasMaskData` | `ISessionService.CanvasMaskData` | Inpainting |
| `M.UpscaleImageData` | `ISessionService.UpscaleImageData` | Upscale form |
| `M.Img2VidInputImage` | `ISessionService.Img2VidInputImage` | Img2Vid form |
| `M.Img2ImgInputImage` | `ISessionService.Img2ImgInputImage` | Img2Img form |
| `M.ImageEditorState` | `ISessionService.ImageEditorState` | Image editor |

---

### Task 2: WebUI Deprecation Assessment

#### Properties/Methods to Remove or Evaluate:

| Item | Location | Assessment | Action |
|------|----------|------------|--------|
| `Options` property | ManagerService | Used for output paths, formats - **KEEP but relocate** | Move to IBackendService |
| `Options.SDModelCheckpoint` | Options.cs | WebUI-specific model switching - **OBSOLETE** | Remove usage |
| `PostOptions()` | ManagerService | WebUI settings sync - **EVALUATE** | Check ComfyUI usage |
| `GetOptions()` | ManagerService | Loads output config - **KEEP** | Already in BackendService |
| `CmdFlags` | ManagerService | WebUI launch params - **OBSOLETE** | Remove |
| `ControlNetEnabled` | ManagerService | Legacy WebUI ControlNet - **OBSOLETE** | Remove |
| `ParseWebUIInfoParameters()` | Parser.cs | WebUI-only parsing - **KEEP for import** | Mark as legacy |

#### ComfyUI-Specific Patterns Already in Place:
- ? Workflow-based generation (no scripts)
- ? WebSocket progress tracking
- ? Node-based parameter passing
- ? Workflow assets for model selection

---

### Task 3: Interface Consistency in Program.cs

#### Current Registration Issues:
```csharp
// CURRENT (Mixed patterns):
builder.Services.AddSingleton<ManagerService>();  // No interface
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<ImageService>());

// TARGET (Consistent):
builder.Services.AddSingleton<IImageService, ImageService>();
```

#### Services Needing Interface-Only Registration:

| Service | Current | Target |
|---------|---------|--------|
| ComfyUIService | HttpClient + manual | `IComfyUIService` only |
| ImageService | Dual registration | `IImageService` only |
| RouterService | Dual registration | `IRouterService` only |
| WorkflowService | Dual registration | `IWorkflowService` only |
| ProgressService | Dual registration | `IProgressService` only |
| ManagerService | Concrete only | Keep concrete (orchestrator) |
| MagickService | Concrete only | Add `IMagickService` |
| CivitaiService | Concrete only | Add `ICivitaiService` (future) |

---

### Task 4: Execution Strategy

#### Step 4.1: Update Component Injections (HIGH IMPACT)
Replace `@inject ManagerService M` patterns with direct service injections:

**Before:**
```razor
@inject ManagerService M
<MudSelect @bind-Value=Parameters.SamplerName>
    @foreach (var sampler in M.Samplers)
```

**After:**
```razor
@inject IBackendService Backend
<MudSelect @bind-Value=Parameters.SamplerName>
    @foreach (var sampler in Backend.Samplers)
```

#### Step 4.2: Remove ManagerService Facades
Once all components updated, remove facade properties from ManagerService.

#### Step 4.3: Clean Up Orchestration Properties
Evaluate remaining properties on ManagerService:
- `Images`, `ImagesInfo`, `GridImage` ? Move to IImageService
- `Progress` ? Move to IProgressService
- `Styles` ? Move to new IStyleService or IStateService
- `CivitaiModels/Images/Creators` ? Move to ICivitaiService
- `ButtonTags` ? Move to ISettingsService or dedicated service

#### Step 4.4: WebUI Cleanup
- Remove `CmdFlags` property
- Remove `ControlNetEnabled` property
- Evaluate `Options.SDModelCheckpoint` usage
- Clean up WebUI-specific parsing in ImageService

---

### Estimated Impact

| Area | Files Affected | Complexity |
|------|----------------|------------|
| Component injections | ~40 components | Medium |
| ManagerService facades | 1 file | Low |
| Program.cs DI | 1 file | Low |
| ImageService cleanup | 1 file | Medium |
| WebUI removal | ~5 files | Low |

---

### Acceptance Criteria

1. ? No components inject ManagerService just for facade access
2. ? ManagerService < 400 lines (orchestration only)
3. ? All services registered as interfaces in Program.cs
4. ? No WebUI-specific code paths remain active
5. ? 150+ tests passing
6. ? Build successful

---

## Phase 9.5 Execution Order

1. **Step A: Component Audit** - Identify all M.* facade usages
2. **Step B: Bulk Component Update** - Replace with interface injections
3. **Step C: Remove Facades** - Clean ManagerService
4. **Step D: Relocate Orchestration Props** - Move Images, Progress, etc.
5. **Step E: WebUI Cleanup** - Remove obsolete code
6. **Step F: DI Cleanup** - Interface-only registrations
7. **Step G: Final Build & Test** - Verify everything works

---

## Key Metrics Target

| Metric | Current | Target |
|--------|---------|--------|
| ManagerService Lines | 658 | < 400 |
| Facade Properties | 25 | 0 |
| Services with Interfaces | 13 | 15+ |
| WebUI Code Paths | ~5 | 0 |
| Unit Tests | 150 | 160+ |

---

**End of Document**
