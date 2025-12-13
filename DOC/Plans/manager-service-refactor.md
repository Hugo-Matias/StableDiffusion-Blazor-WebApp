# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 15 - Rename & Test Coverage ? **COMPLETE**
**Last Updated:** 2025-01-16

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **~240-line orchestrator** (now renamed to `OrchestratorService`) that coordinates between specialized services with **zero orchestration properties** remaining.

### Current Architecture

```
????????????????????????????????????????????????????????????????????????
?                   OrchestratorService (formerly ManagerService)      ?
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
?  ? Tests       ?   ?  ? Tests       ?   ?  ? Tests       ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
? SettingsService ?   ? BackendService  ?   ? GalleryService  ?
?  - App settings ?   ?  - ComfyUI API  ?   ?  - Folders      ?
?  - JSON persist ?   ?  - Health check ?   ?  - Projects     ?
?  - Validation   ?   ?  - OutputPaths  ?   ?  - Selection    ?
?  ? Tests       ?   ?  ? Tests       ?   ?  ? Tests       ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
?  EventService   ?   ?  ImageService   ?   ? SessionService  ?
?  - Pub/sub      ?   ?  - Generation   ?   ?  - Canvas state ?
?  - Typed events ?   ?  - Saving       ?   ?  - Editor state ?
?  - Mediator     ?   ?  - PathPatterns ?   ?  - Videos       ?
?  ? Tests       ?   ?                 ?   ?  ? Tests       ?
???????????????????   ???????????????????   ???????????????????
        ?                           ?
        ?                           ?
???????????????????   ???????????????????   ???????????????????
? CivitaiService  ?   ? ProgressService ?   ?  RouterService  ?
?  - API client   ?   ?  - IsConverging ?   ?  - API routing  ?
?  - Downloads    ?   ?  - Progress     ?   ?  - Txt2Img      ?
?  - No M deps    ?   ?  - Notify       ?   ?  - Img2Img/Vid  ?
?                 ?   ?  ? Tests       ?   ?  ? Tests       ?
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
Moved remaining orchestration properties to specialized services:
- `ComfyWSClientId` ? `IBackendService`
- `CurrentProgress` ? `IProgressService`
- `IsConverging` ? `IProgressService`

### ? Phase 15: Rename & Test Coverage Expansion (Complete)
**Completed:** 2025-01-16

#### Objective
1. Rename `ManagerService` to `OrchestratorService` for clarity
2. Expand unit test coverage for newly extracted services

#### Changes Made

**1. Renamed ManagerService ? OrchestratorService** ?
- Renamed class from `ManagerService` to `OrchestratorService`
- Renamed file from `ManagerService.cs` to `OrchestratorService.cs`
- Updated all 18 consuming files (Pages, Components, Services)
- Updated DI registration in `Program.cs`

**2. Created ProgressServiceTests** ? (28 tests)
- Constructor tests
- Add/Update/Remove progress tracker tests
- CurrentProgress property tests with event publishing
- IsConverging property tests with event publishing
- NotifyProgressChanged tests
- Integration/lifecycle tests

**3. Created WorkflowServiceTests** ? (12 tests)
- Workflow model default value tests
- WorkflowAsset model tests
- WorkflowStep and OutputMapping tests
- NodeRegistry tests (Register, GetReference, Merge)
- SubgraphContext tests
- Constructor tests

**4. Created RouterServiceTests** ? (10 tests)
- Constructor tests
- SearchLoras tests (empty search, with query, no results)
- PostTxt2Img tests (backend unavailable, model service usage)
- PostImg2Img tests (backend unavailable, no workflow)
- PostImg2Vid tests (backend unavailable, no workflow)
- Backend integration tests

**5. Updated MockWorkflowServiceBuilder** ?
- Fixed to use correct `IConfiguration` dependency for `IOService`

#### Phase 15 Metrics

| Metric | Before Phase 15 | After Phase 15 | Change |
|--------|-----------------|----------------|--------|
| Unit tests | 150 | 198 | +32% ? |
| Services with tests | 8 | 11 | +3 |
| ManagerService references | 18 files | 0 files | -100% ? |
| OrchestratorService references | 0 files | 18 files | New |
| Build | ? | ? | - |

#### Test Coverage Summary

| Service | Test File | Test Count | Status |
|---------|-----------|------------|--------|
| BackendService | BackendServiceTests.cs | ? | Existing |
| EventService | EventServiceTests.cs | ? | Existing |
| GalleryService | GalleryServiceTests.cs | ? | Existing |
| IOService | IOServiceTests.cs | ? | Existing |
| ModelService | ModelServiceTests.cs | ? | Existing |
| SessionService | SessionServiceTests.cs | ? | Existing |
| SettingsService | SettingsServiceTests.cs | ? | Existing |
| StateService | StateServiceTests.cs | ? | Existing |
| **ProgressService** | **ProgressServiceTests.cs** | **28** | **New** ? |
| **WorkflowService** | **WorkflowServiceTests.cs** | **12** | **New** ? |
| **RouterService** | **RouterServiceTests.cs** | **10** | **New** ? |
| ImageService | - | - | Future |
| OrchestratorService | - | - | Future |

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 11 End | Phase 12 End | Phase 14 End | Phase 15 End |
|--------|---------------|---------------|--------------|--------------|--------------|--------------|
| Service name | ManagerService | ManagerService | ManagerService | ManagerService | ManagerService | **OrchestratorService** |
| Service lines | ~1600 | ~450 | ~350 | ~320 | ~240 | ~240 |
| Facade properties | 27 | 0 | 0 | 0 | 0 | 0 ? |
| Orchestration properties | - | 14 | 5 | 3 | 0 | 0 ? |
| Services extracted | 0 | 11 | 11 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 | 11 | 11 |
| Interface-only DI | - | 9 | 9 | 9 | 11 | 11 |
| Unit tests | 0 | 150 | 150 | 150 | 150 | **198** ? |
| Services with tests | 0 | 8 | 8 | 8 | 8 | **11** ? |

---

## Future Work

### Potential Phase 16: Additional Test Coverage

Services that could benefit from additional tests:
1. **ImageService** - Complex generation workflow, file saving logic
2. **OrchestratorService** - Workflow coordination, parameter loading
3. **CivitaiService** - API integration (may need mocking strategy)
4. **ComfyUIService** - Backend API communication

### Potential Phase 17: OrchestratorService Evaluation

Evaluate whether OrchestratorService should be:
1. **Keep as-is** - Legitimate thin orchestrator pattern
2. **Further decompose** - Split remaining responsibilities
3. **Eliminate** - Move all methods to existing specialized services

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

// OrchestratorService (formerly ManagerService) - still uses concrete registration
builder.Services.AddSingleton<OrchestratorService>();
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
| **Phase 15** | **Renamed to OrchestratorService, expanded tests** | **198** |

---

**End of Document**
