# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 13 - Complete Interface Migration ? **COMPLETE**
**Last Updated:** 2025-01-15

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **~280-line orchestrator** that coordinates between specialized services with **only 3 properties** remaining.

### Current Architecture

```
????????????????????????????????????????????????????????????????????????
?                    ManagerService (Orchestrator)                     ?
?  - Coordinates between specialized services                         ?
?  - Workflow management and asset coordination                       ?
?  - Only 3 properties: ComfyWSClientId, CurrentProgress, IsConverging?
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
?  - Validation   ?   ?  - Samplers     ?   ?  - Selection    ?
?                 ?   ?  - OutputPaths ??   ?                 ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
?  EventService   ?   ?  ImageService   ?   ? SessionService  ?
?  - Pub/sub      ?   ?  - Generation   ?   ?  - Canvas state ?
?  - Typed events ?   ?  - Saving       ?   ?  - Editor state ?
?  - Mediator     ?   ?  - Progress     ?   ?  - Videos       ?
???????????????????   ???????????????????   ???????????????????
        ?
        ?
???????????????????
? CivitaiService  ?
?  - API client   ?
?  - Downloads    ?
?  - No M deps    ?
???????????????????
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
**Completed:** 2025-01-15

Removed the legacy `Options` class and migrated to `IConfiguration` for output paths.

### ? Phase 13: Complete Interface Migration (Complete)
**Completed:** 2025-01-15

Migrated all components from concrete service injection to interface injection.

---

## Future Phases

### Phase 14: ManagerService Role Evaluation (Proposed)

**Objective:** Evaluate whether ManagerService is still needed or can be eliminated.

#### Analysis Questions
1. Can `ComfyWSClientId` move to a websocket service?
2. Can `CurrentProgress` and `IsConverging` move to `IProgressService`?
3. Are orchestration methods still needed or can components use services directly?

#### Current ManagerService Methods (to evaluate)
- **Still Needed:** Workflow management, parameter loading, path pattern conversion
- **Consider Moving:** `CurrentProgress` ? IProgressService, `IsConverging` ? IProgressService

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 11 End | Phase 12 End | Phase 12.5 End | Phase 13 End |
|--------|---------------|---------------|--------------|--------------|----------------|--------------|
| ManagerService lines | ~1600 | ~450 | ~350 | ~320 | ~280 | ~280 |
| Facade properties | 27 | 0 | 0 | 0 | 0 | 0 |
| Orchestration properties | - | 14 | 5 | 3 | 3 | 3 |
| Services extracted | 0 | 11 | 11 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 | 11 | 11 |
| Interface-only DI | - | 9 | 9 | 9 | 9 | 11 |
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
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();      // NEW in Phase 13
builder.Services.AddSingleton<IProgressService, ProgressService>();      // NEW in Phase 13
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

---

**End of Document**
