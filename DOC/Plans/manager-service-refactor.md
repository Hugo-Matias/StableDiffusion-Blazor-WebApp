# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 17 - IOrchestratorService Interface Extraction ? **COMPLETE**
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
3. ? Migrated all components from `OrchestratorService` to `IOrchestratorService`:
   - Components/Img2Vid/GeneratedVideoTabs.razor
   - Components/Resources/CivitaiImageDialog.razor
   - Components/Resources/ResourceImageDialog.razor
   - Components/Shared/Generation/GeneratedImageTabs.razor
   - Components/Shared/Generation/WorkflowAssetSelector.razor
   - Components/Shared/Generation/WorkflowAssetsPanel.razor
   - Components/Shared/Image/ImageInfoDialog.razor
   - Components/Shared/Image/ImageViewer.razor
   - Components/Shared/AssetViewer.razor
   - Components/Shared/MainLayout.razor
   - Components/Txt2Img/GenerateFormTxt2Img.razor
   - Pages/Danbooru.razor
   - Pages/Img2Img.razor
   - Pages/Img2Vid.razor
   - Pages/Txt2Img.razor
4. ? Updated DI registration in Program.cs (dual registration pattern)
5. ? Created `OrchestratorServiceTests.cs` with 42 unit tests

#### Tests Created (42 tests)

| Category | Tests | Description |
|----------|-------|-------------|
| Constructor | 2 | PageSize setup, DateRange initialization |
| Event Publishing | 3 | InvokeParametersChanged, InvokeSessionVideosChanged |
| Parameter Initialization | 1 | Delegation to StateService |
| Model Management | 7 | GetWorkflowModels, GetSDVAEs, GetModelsForAssetType, GetCurrentModel, SetCurrentModel |
| Workflow Management | 8 | GetCurrentWorkflow, GetWorkflowById, GetWorkflowsForMode, SetCurrentWorkflow, ResetCurrentWorkflow |
| Workflow Assets | 5 | GetWorkflowAsset, SetWorkflowAsset, GetWorkflowAssetsForMode |
| Gallery | 5 | GetFolders, ReplaceSelectedImages, AddSelectedImage, RemoveSelectedImage, ClearSelectedImages |
| Session | 4 | ResetImageEditorState, SetImg2ImgInputImage, AddSessionVideo, ClearSessionVideos |
| Settings | 2 | LoadSettings, SaveSettings |
| State | 2 | LoadState, SaveState |
| Styles & Prompts | 3 | SetLoras null handling, SetLoras adding, ParseAndCleanCopiedPrompt |

#### Phase 17 Metrics

| Metric | Before Phase 17 | After Phase 17 | Change |
|--------|-----------------|----------------|--------|
| Unit tests | 223 | 265 | +42 ? |
| Services with tests | 12 | 13 | +1 |
| Services with interfaces | 11 | 12 | +1 ? |
| Components using interface | 0 | 15 | +15 ? |
| Build | ? | ? | - |

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 14 End | Phase 15 End | Phase 16 End | Phase 17 End |
|--------|---------------|---------------|--------------|--------------|--------------|--------------|
| Service name | ManagerService | ManagerService | ManagerService | OrchestratorService | OrchestratorService | OrchestratorService |
| Service lines | ~1600 | ~450 | ~240 | ~240 | ~240 | ~240 |
| Facade properties | 27 | 0 | 0 | 0 | 0 | 0 ? |
| Orchestration properties | - | 14 | 0 | 0 | 0 | 0 ? |
| Services extracted | 0 | 11 | 11 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 | 11 | **12** ? |
| Interface-only DI | - | 9 | 11 | 11 | 11 | **12** ? |
| Unit tests | 0 | 150 | 150 | 198 | 223 | **265** ? |
| Services with tests | 0 | 8 | 8 | 11 | 12 | **13** ? |

---

## Future Work

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
| ImageService | ImageServiceTests.cs | 25 | Phase 16 |
| **OrchestratorService** | **OrchestratorServiceTests.cs** | **42** | **Phase 17** ? |

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
| **Phase 17** | **IOrchestratorService interface, 42 tests** | **265** ? |

---

**End of Document**
