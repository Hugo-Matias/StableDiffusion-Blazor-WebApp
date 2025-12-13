# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 14 - Orchestration Property Relocation ? **COMPLETE**
**Last Updated:** 2025-01-15

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **~240-line orchestrator** that coordinates between specialized services with **zero orchestration properties** remaining.

### Current Architecture

```
????????????????????????????????????????????????????????????????????????
?                    ManagerService (Orchestrator)                     ?
?  - Coordinates between specialized services                         ?
?  - Workflow management and asset coordination                       ?
?  - NO state properties (moved to specialized services)              ?
????????????????????????????????????????????????????????????????????????
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
?  - Validation   ?   ?  - OutputPaths  ?   ?  - Selection    ?
?                 ?   ?  - WSClientId ??   ?                 ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
?  EventService   ?   ?  ImageService   ?   ? SessionService  ?
?  - Pub/sub      ?   ?  - Generation   ?   ?  - Canvas state ?
?  - Typed events ?   ?  - Saving       ?   ?  - Editor state ?
?  - Mediator     ?   ?  - PathPatterns??   ?  - Videos       ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?
        ?                           ?
???????????????????   ???????????????????
? CivitaiService  ?   ? ProgressService ?
?  - API client   ?   ?  - IsConverging??
?  - Downloads    ?   ?  - Progress ?  ?
?  - No M deps    ?   ?  - Notify       ?
???????????????????   ???????????????????
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

### ? Phase 9.5: Facade Removal & Cleanup (Complete)
Removed all backward compatibility facades from ManagerService.

### ? Phase 10: Orchestration Property Relocation (Complete)
Moved generation state to IImageService, removed WebUI-era code.

### ? Phase 11: Civitai Service Decoupling (Complete)
Removed CivitaiModels/Images/Creators from ManagerService, decoupled CivitaiService.

### ? Phase 12: Options & ResourceTypeDirectories Cleanup (Complete)
Removed duplicate `Options` facade and `ResourceTypeDirectories` property from ManagerService.

### ? Phase 12.5: Options to Configuration Migration (Complete)
Removed the legacy `Options` class and migrated to `IConfiguration` for output paths.

### ? Phase 13: Complete Interface Migration (Complete)
Migrated all components from concrete service injection to interface injection.

### ? Phase 14: Orchestration Property Relocation (Complete)
**Completed:** 2025-01-15

#### Objective
Move remaining orchestration properties out of ManagerService to their appropriate specialized services.

#### Analysis

| Property | Original Location | New Location | Reason |
|----------|-------------------|--------------|--------|
| `ComfyWSClientId` | ManagerService | IBackendService | WebSocket is backend-specific |
| `CurrentProgress` | ManagerService | IProgressService | Progress state belongs in progress service |
| `IsConverging` | ManagerService | IProgressService | Generation state is progress-related |
| `InvokeProgressChanged()` | ManagerService | IProgressService.NotifyProgressChanged() | Event publishing for progress |

#### Changes Made

**1. Updated `IBackendService`** ?
- Added `ComfyWSClientId` property

**2. Updated `BackendService`** ?
- Implemented `ComfyWSClientId` property

**3. Updated `IProgressService`** ?
- Added `CurrentProgress` property
- Added `IsConverging` property
- Added `NotifyProgressChanged()` method

**4. Updated `ProgressService`** ?
- Implemented `CurrentProgress` with event publishing
- Implemented `IsConverging` with event publishing
- Implemented `NotifyProgressChanged()` method
- Added `IEventService` dependency

**5. Updated `ComfyUIWebsocketService`** ?
- Replaced `ManagerService` with `IBackendService` and `IProgressService`
- Uses `_backend.ComfyWSClientId` instead of `_m.ComfyWSClientId`
- Uses `_progressService.NotifyProgressChanged()` instead of `_m.InvokeProgressChanged()`

**6. Updated `ImageService`** ?
- Removed `ManagerService` dependency
- Uses `IBackendService` for backend operations
- Uses `IProgressService.IsConverging` for generation state
- Moved `GetCurrentSaveFolder()` and `ConvertPathPattern()` methods into ImageService
- Uses `IModelService` for `GetCurrentModel()`

**7. Updated `RouterService`** ?
- Replaced `ManagerService` with `IBackendService` and `IModelService`
- Uses `_backend.ComfyWSClientId` for WebSocket client ID
- Uses `_models.GetCurrentModel()` and `_models.GetCurrentVae()` directly

**8. Updated Components** ?
- `GeneratedImageTabs.razor`: Uses `IProgressService.IsConverging`
- `GeneratedVideoTabs.razor`: Uses `IProgressService.IsConverging`

**9. Updated `ManagerService`** ?
- Removed `ComfyWSClientId` property
- Removed `CurrentProgress` property
- Removed `IsConverging` property
- Removed `InvokeProgressChanged()` method
- Reduced to ~240 lines

#### Phase 14 Metrics

| Metric | Before Phase 14 | After Phase 14 | Change |
|--------|-----------------|----------------|--------|
| ManagerService lines | ~280 | ~240 | -14% |
| ManagerService properties | 3 | 0 | -100% ? |
| Services depending on ManagerService | 4 | 2 | -50% |
| Build | ? | ? | - |
| Tests | 150 | 150 | - |

#### Breaking Changes
- `ComfyWSClientId` moved from `ManagerService` to `IBackendService`
- `CurrentProgress` moved from `ManagerService` to `IProgressService`
- `IsConverging` moved from `ManagerService` to `IProgressService`
- `InvokeProgressChanged()` replaced with `IProgressService.NotifyProgressChanged()`

---

## Future Phases

### Phase 15: ManagerService Final Evaluation (Proposed)

**Objective:** Determine if ManagerService is still needed or can be fully eliminated.

#### Current ManagerService Responsibilities
1. **Workflow Management** - `GetCurrentWorkflow()`, `SetCurrentWorkflow()`, etc.
2. **Workflow Assets** - `GetWorkflowAsset()`, `SetWorkflowAsset()`, etc.
3. **Model Management** - Delegating to IModelService with state save
4. **Gallery Management** - Delegating to IGalleryService with state coordination
5. **Session Management** - Delegating to ISessionService
6. **State/Settings** - `LoadState()`, `SaveState()`, `LoadSettings()`
7. **Parameter Loading** - `LoadImageInfoParameters()`, `SetGenerationParameter()`
8. **Prompt Parsing** - `ParseAndCleanCopiedPrompt()`, `SetLoras()`

#### Options
1. **Keep as Thin Orchestrator** - Legitimate pattern for coordinating multi-service operations
2. **Extract Workflow Coordination** - Move to new `IWorkflowCoordinatorService`
3. **Move Methods to Existing Services** - Distribute to IStateService, IWorkflowService, etc.

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 11 End | Phase 12 End | Phase 13 End | Phase 14 End |
|--------|---------------|---------------|--------------|--------------|--------------|--------------|
| ManagerService lines | ~1600 | ~450 | ~350 | ~320 | ~280 | ~240 |
| Facade properties | 27 | 0 | 0 | 0 | 0 | 0 ? |
| Orchestration properties | - | 14 | 5 | 3 | 3 | 0 ? |
| Services extracted | 0 | 11 | 11 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 | 11 | 11 |
| Interface-only DI | - | 9 | 9 | 9 | 11 | 11 |
| Unit tests | 0 | 150 | 150 | 150 | 150 | 150 |

---

## Configuration Reference

### Output Paths (appsettings.json)
```json
{
  "OutputDir": "N:\\Images\\StableDiffusion\\",
  "OutputPaths": {
    "Txt2ImgSamples": "Text-2-Image\\_samples",
    "Img2ImgSamples": "Image-2-Image\\_samples",
    "Img2VidSamples": "Image-2-Video\\_samples",
    "Extras": "Extras",
    "DirectoryPattern": "[model_name]/[sampler]",
    "FilenamePattern": "[seed]_[steps]_[cfg]",
    "SamplesFormat": "png",
    "SaveSamples": true
  }
}
```

### DI Registration Reference

#### Interface-Only (Clean Pattern) ?
```csharp
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IIOService, IOService>();
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IBackendService, BackendService>();
builder.Services.AddSingleton<IModelService, ModelService>();
builder.Services.AddSingleton<IGalleryService, GalleryService>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IRouterService, RouterService>();
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IProgressService, ProgressService>();
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();
```

#### Dual Registration (Still Required)
```csharp
// ComfyUIWebsocketService sets Progress directly on ImageService
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<ImageService>());

// ComfyUIService registered via AddHttpClient, interface resolves to same instance
builder.Services.AddHttpClient<ComfyUIService>();
builder.Services.AddSingleton<IComfyUIService>(sp => sp.GetRequiredService<ComfyUIService>());
```

---

## Commit History Reference

| Checkpoint | Description | Tests |
|------------|-------------|-------|
| Phase 8.5 | Legacy events removed | 138 |
| Phase 9 | Service interfaces created | 150 |
| Phase 9.5 | Facades removed, DI cleanup | 150 |
| Phase 10 | Generation state to IImageService | 150 |
| Phase 11 | Civitai decoupling complete | 150 |
| Phase 12 | Options/ResourceTypeDirectories removed | 150 |
| Phase 12.5 | Options migrated to IConfiguration | 150 |
| Phase 13 | Complete interface migration | 150 |
| Phase 14 | Orchestration properties relocated | 150 |

---

**End of Document**
