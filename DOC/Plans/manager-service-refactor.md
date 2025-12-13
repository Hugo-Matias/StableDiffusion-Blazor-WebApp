# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 16 - ImageService Test Coverage ? **COMPLETE**
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
?  ? Tests       ?   ?  ? Tests ?    ?   ?  ? Tests       ?
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

| Category | Tests | Description |
|----------|-------|-------------|
| Constructor | 2 | Service creation and property initialization |
| GetCurrentSaveFolder | 5 | Null handling, Extras path, pattern application, empty path |
| ConvertPathPattern | 10 | Null/empty, seed, steps, cfg, sampler, model_name, multiple tags, unknown tags, mixed content |
| Mode-Specific Patterns | 2 | Img2Vid parameters, null parameter defaults |
| SaveImages | 2 | Images property initialization state |
| Progress | 1 | Progress property settable |
| GeneratedImageEntities | 1 | Entities property settable |
| OnChange Event | 1 | Event subscribable |
| DownloadImageAsPng | 1 | File exists with no overwrite |

#### Phase 16 Metrics

| Metric | Before Phase 16 | After Phase 16 | Change |
|--------|-----------------|----------------|--------|
| Unit tests | 198 | 223 | +25 ? |
| Services with tests | 11 | 12 | +1 |
| Build | ? | ? | - |

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 14 End | Phase 15 End | Phase 16 End |
|--------|---------------|---------------|--------------|--------------|--------------|
| Service name | ManagerService | ManagerService | ManagerService | OrchestratorService | OrchestratorService |
| Service lines | ~1600 | ~450 | ~240 | ~240 | ~240 |
| Facade properties | 27 | 0 | 0 | 0 | 0 ? |
| Orchestration properties | - | 14 | 0 | 0 | 0 ? |
| Services extracted | 0 | 11 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 | 11 |
| Interface-only DI | - | 9 | 11 | 11 | 11 |
| Unit tests | 0 | 150 | 150 | 198 | **223** ? |
| Services with tests | 0 | 8 | 8 | 11 | **12** ? |

---

## Future Work

### Phase 17: IOrchestratorService Interface Extraction (Proposed)
**Priority: Medium** - Create interface for consistency and testability.

#### Objective
Extract `IOrchestratorService` interface and migrate all consumers.

#### Scope
1. Create `IOrchestratorService` interface
2. Migrate all components from `OrchestratorService` to `IOrchestratorService`
3. Update DI to interface-only pattern
4. Create `OrchestratorServiceTests`

#### Benefits
- Consistent with all other services
- Enables mocking for component tests
- Completes the interface migration pattern

---

### Phase 18: Integration Tests (Proposed)
**Priority: Medium** - Test multi-service workflows end-to-end.

#### Objective
Create integration tests that verify services work correctly together.

#### Scope
| Test Category | Description |
|---------------|-------------|
| Workflow Execution | Load template ? Render ? Execute |
| State Persistence | Save ? Reload ? Verify round-trip |
| Event Propagation | Publish ? Subscribe ? Handle across services |
| Backend Integration | Health check ? Model loading ? Generation |

---

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
| **ImageService** | **ImageServiceTests.cs** | **25** | **Phase 16** ? |
| OrchestratorService | - | - | Future |

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
| Phase 15 | Renamed to OrchestratorService, expanded tests | 198 |
| **Phase 16** | **ImageService test coverage** | **223** |

---

**End of Document**
