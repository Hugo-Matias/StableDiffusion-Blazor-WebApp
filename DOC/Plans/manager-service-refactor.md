# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 18 - Integration Tests ? **COMPLETE**
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
?  - ? IOrchestratorService interface                                ?
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
?  ? Tests       ?   ?  ? Tests       ?   ?  ? Tests       ?
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

- Renamed `ManagerService` to `OrchestratorService`
- Created ProgressServiceTests (28 tests)
- Created WorkflowServiceTests (12 tests)
- Created RouterServiceTests (10 tests)

### ? Phase 16: ImageService Test Coverage (Complete)
**Completed:** 2025-01-16

#### Objective
Create comprehensive unit tests for ImageService covering path patterns, folder generation, and service state.

#### Tests Created (25 tests)
- Constructor, GetCurrentSaveFolder, ConvertPathPattern, Mode-Specific Patterns, SaveImages, Progress, GeneratedImageEntities, OnChange Event, DownloadImageAsPng

### ? Phase 17: IOrchestratorService Interface Extraction (Complete)
**Completed:** 2025-01-16

#### Objective
Extract `IOrchestratorService` interface and migrate all consumers for consistency and testability.

#### Scope Completed
1. ? Created `IOrchestratorService` interface with all public methods
2. ? Updated `OrchestratorService` to implement `IOrchestratorService`
3. ? Migrated all components from `OrchestratorService` to `IOrchestratorService`
4. ? Updated DI registration in Program.cs (dual registration pattern)
5. ? Created `OrchestratorServiceTests.cs` with 42 unit tests

### ? Phase 18: Integration Tests (Complete)
**Completed:** 2025-01-16

#### Objective
Create integration tests that verify services work correctly together through real service coordination.

#### Scope Completed
1. ? Created `ServiceIntegrationTests.cs` in `BlazorWebApp.Tests/Integration/`
2. ? State Persistence Round-Trip Tests (2 tests)
3. ? Event Propagation Tests (3 tests)
4. ? Progress Service Integration Tests (2 tests)
5. ? Gallery and Session Service Integration Tests (3 tests)
6. ? Multi-Service Workflow Tests (3 tests)
7. ? Settings and State Interaction Tests (2 tests)

#### Tests Created (15 tests)

| Category | Tests | Description |
|----------|-------|-------------|
| State Persistence | 2 | Save/Load round-trip, Img2Vid parameter persistence |
| Event Propagation | 3 | Multiple subscribers, event type isolation, unsubscribe behavior |
| Progress Integration | 2 | Converging state events, progress value events |
| Gallery & Session | 3 | Selection events, input image events, video management |
| Multi-Service | 3 | State change propagation, gallery isolation, session isolation |
| Settings & State | 2 | Custom settings initialization, InitializeParameters with settings |

#### Key Test Coverage Areas

| Test Area | Services Involved | Verification |
|-----------|-------------------|--------------|
| State Persistence | StateService, DatabaseService | Parameters survive save/load cycle |
| Event Flow | EventService, ProgressService, SessionService, GalleryService | Events publish correctly to subscribers |
| Progress Tracking | ProgressService, EventService | Converging and progress state events fire |
| Gallery Selection | GalleryService, EventService | Selection changes publish events |
| Session Videos | SessionService | Add/Remove/Clear video operations work |
| Multi-Instance Isolation | GalleryService, SessionService | Service instances don't share state |
| Settings Propagation | SettingsService, StateService | Settings affect parameter defaults |

#### Phase 18 Metrics

| Metric | Before Phase 18 | After Phase 18 | Change |
|--------|-----------------|----------------|--------|
| Unit tests | 265 | 280 | +15 ? |
| Integration tests | 0 | 15 | +15 ? |
| Test categories | 13 | 14 | +1 |
| Build | ? | ? | - |

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 14 End | Phase 15 End | Phase 16 End | Phase 17 End | Phase 18 End |
|--------|---------------|---------------|--------------|--------------|--------------|--------------|--------------|
| Service name | ManagerService | ManagerService | ManagerService | OrchestratorService | OrchestratorService | OrchestratorService | OrchestratorService |
| Service lines | ~1600 | ~450 | ~240 | ~240 | ~240 | ~240 | ~240 |
| Facade properties | 27 | 0 | 0 | 0 | 0 | 0 | 0 ? |
| Orchestration properties | - | 14 | 0 | 0 | 0 | 0 | 0 ? |
| Services extracted | 0 | 11 | 11 | 11 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 | 11 | **12** | **12** ? |
| Interface-only DI | - | 9 | 11 | 11 | 11 | **12** | **12** ? |
| Unit tests | 0 | 150 | 150 | 198 | 223 | 265 | **280** ? |
| Integration tests | 0 | 0 | 0 | 0 | 0 | 0 | **15** ? |
| Services with tests | 0 | 8 | 8 | 11 | 12 | 13 | **14** ? |

---

## Future Work

### Phase 19: ComfyUI/Civitai Service Tests (Proposed)
**Priority: Low** - Requires HTTP mocking strategy.

#### Objective
Create tests for external API integration services.

#### Scope
| Service | Test Areas |
|---------|------------|
| ComfyUIService | API endpoints, response parsing, error handling |
| CivitaiService | Model search, downloads, progress tracking |

#### Prerequisites
- Add HTTP mocking package (e.g., `RichardSzalay.MockHttp`)
- Create mock response fixtures

---

### Phase 20: Blazor Component Tests (Proposed)
**Priority: Low** - UI component testing with bUnit.

#### Objective
Add component-level tests for Blazor UI components.

#### Scope
| Component | Test Areas |
|-----------|------------|
| GenerateFormTxt2Img | Parameter binding, validation |
| WorkflowAssetSelector | Asset loading, selection |
| GeneratedImageTabs | Image display, progress states |
| PromptFields | Prompt editing, style application |

#### Prerequisites
- Add bUnit package to test project
- Create component test fixtures

---

## Test Coverage Summary

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
| ProgressService | ProgressServiceTests.cs | 28 | Phase 15 |
| WorkflowService | WorkflowServiceTests.cs | 12 | Phase 15 |
| RouterService | RouterServiceTests.cs | 10 | Phase 15 |
| ImageService | ImageServiceTests.cs | 25 | Phase 16 |
| OrchestratorService | OrchestratorServiceTests.cs | 42 | Phase 17 |
| **Integration** | **ServiceIntegrationTests.cs** | **15** | **Phase 18** ? |

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

// OrchestratorService - dual registration for interface access
builder.Services.AddSingleton<OrchestratorService>();
builder.Services.AddSingleton<IOrchestratorService>(sp => sp.GetRequiredService<OrchestratorService>());
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
| Phase 15 | Renamed to OrchestratorService, expanded tests | 198 |
| Phase 16 | ImageService test coverage | 223 |
| Phase 17 | IOrchestratorService interface, 42 tests | 265 |
| **Phase 18** | **Integration tests** | **280** ? |

---

**End of Document**
