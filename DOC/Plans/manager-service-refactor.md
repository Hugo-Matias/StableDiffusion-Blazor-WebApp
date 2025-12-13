# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 10 - Orchestration Property Relocation ? **COMPLETE**
**Last Updated:** 2025-01-15

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **~400-line orchestrator** that coordinates between specialized services with **zero facade properties**.

### Current Architecture

```
????????????????????????????????????????????????????????????????????????
?                    ManagerService (Orchestrator)                     ?
?  - Coordinates between specialized services                         ?
?  - Workflow management and asset coordination                       ?
?  - NO facade properties (all removed in Phase 9.5)                  ?
?  - NO generation state (moved to IImageService in Phase 10)         ?
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
???????????????????   ???????????????????   ???????????????????
        ?                           ?                           ?
        ?                           ?                           ?
???????????????????   ???????????????????   ???????????????????
?  EventService   ?   ?  ImageService   ?   ? SessionService  ?
?  - Pub/sub      ?   ?  - Generation   ?   ?  - Canvas state ?
?  - Typed events ?   ?  - Saving       ?   ?  - Editor state ?
?  - Mediator     ?   ?  - Progress     ?   ?  - Videos       ?
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
**Completed:** 2025-01-15

#### Summary
Removed all backward compatibility facades from ManagerService, eliminated WebUI remnants, and cleaned up DI registrations.

#### Phase 9.5 Metrics

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Facade properties | 27 | 0 | -100% |
| Facade usages in components | ~305 | 0 | -100% |
| Components with ManagerService | 31 | 19 | -39% |
| WebUI remnants removed | - | 3 | - |
| Interface-only DI registrations | 9 | 11 | +2 |
| ManagerService lines | 658 | ~450 | -32% |

---

### ? Phase 10: Orchestration Property Relocation (Complete)
**Completed:** 2025-01-15

#### Objective
Move remaining orchestration properties from ManagerService to appropriate specialized services, and remove WebUI-era code no longer applicable to ComfyUI.

#### Task A: Move Generation Results to IImageService ?
**Scope:** Moved `Images`, `GeneratedImageEntities`, `Progress` to `IImageService`

**Changes Made:**

1. **IImageService interface** - Simplified to only include:
   - `GeneratedImages Images { get; }` - Raw generated images + workflow JSON info
   - `ImagesDto GeneratedImageEntities { get; set; }` - Database entities
   - `GeneratedVideos GeneratedVideos { get; }` - Video results
   - `InferenceProgress Progress { get; set; }` - Real-time progress

2. **ImageService** - Now owns all generation state, simplified `SaveImages()` method

3. **ManagerService** - Removed 6 properties:
   - `Images`, `ImagesInfo`, `GridImage`, `GeneratedImageEntities`, `Progress`, `GeneratedUpscaleImage`

4. **Components updated:**
   - `GeneratedImageTabs.razor` - Uses `IImageService.Images.Info` instead of `ImagesInfo.InfoTexts[0]`
   - `GeneratedVideoTabs.razor` - Uses `IImageService.Progress`
   - `GenerateFormTxt2Img.razor` - Uses `IImageService.Images.Info` for RestoreSeed()
   - `Txt2Img.razor` / `Img2Img.razor` - Uses `ImageService.GeneratedImageEntities`

5. **ComfyUIWebsocketService** - Uses `ImageService.Progress` directly

#### Task B: Remove Duplicate Styles Property ?
- Removed `Styles` property and `GetStyles()` method from ManagerService
- Components use `IEventService` to publish `StylesChangedEventArgs`

#### Task C: Remove Duplicate ButtonTags Property ?
- Removed `ButtonTags` property and `GetButtonTags()` method from ManagerService

#### Task D: WebUI Cleanup ?
**Removed as no longer applicable:**

| Item | Reason |
|------|--------|
| `GeneratedImagesInfo` class | WebUI-specific response format; ComfyUI uses `Images.Info` JSON |
| `UpscaledImageDto` class | WebUI upscale endpoint; not used in ComfyUI |
| `GeneratedUpscaleImage` property | Never populated for ComfyUI |
| `GridImage` property | WebUI grid feature; ComfyUI doesn't use grids |
| `ImagesInfo` property | Wrapper for `Images.Info`; now use directly |
| `SerializeInfo()` method | No longer needed |
| `outdirGrid` parameter | Grid directories no longer used |
| `SaveUpscaleImage()` method | Threw NotImplementedException |

#### Files Removed
- `BlazorWebApp/Models/GeneratedImagesInfo.cs`
- `BlazorWebApp/Data/Dtos/UpscaledImageDto.cs`

#### Phase 10 Metrics

| Metric | Before Phase 10 | After Phase 10 | Change |
|--------|-----------------|----------------|--------|
| ManagerService properties | 14 | 6 | -57% |
| IImageService properties | 2 | 4 | +2 (focused) |
| Files removed | 0 | 2 | Legacy cleanup |
| Components updated | 0 | 5 | - |
| Build | ? | ? | - |
| Tests | 150 | 150 | - |

#### Remaining ManagerService Properties
After Phase 10:
- `Options` - Backend options (consider moving to IBackendService in future)
- `CivitaiModels/Images/Creators` - Civitai API results (Phase 11)
- `ComfyWSClientId` - WebSocket client ID
- `ResourceTypeDirectories` - Resource paths
- `CurrentProgress` - Int progress value with event
- `IsConverging` - Generation running flag

---

## Future Phases

### Phase 11: ICivitaiService Extraction (Proposed)

**Objective:** Create a dedicated service for Civitai API interactions and state management.

#### Scope
- Extract `CivitaiModels`, `CivitaiImages`, `CivitaiCreators` from ManagerService
- Create `ICivitaiService` interface
- Move Civitai browsing/download logic from components to service
- Add caching layer for API responses

#### Benefits
- Better separation of concerns
- Easier testing of Civitai functionality
- Potential for background download management

---

### Phase 12: Options Property Evaluation (Proposed)

**Objective:** Evaluate and potentially simplify the `Options` class and its usage.

#### Analysis Needed
1. Which `Options` properties are still used?
2. Which come from ComfyUI backend vs appsettings.json?
3. Can output paths be configured via IConfiguration instead?
4. Is `PostOptions()` still needed for ComfyUI?

#### Potential Outcomes
- Simplify `Options` class to ComfyUI-specific settings only
- Move output path configuration to appsettings.json
- Remove unused WebUI-era properties

---

### Phase 13: Complete Interface Migration (Proposed)

**Objective:** Ensure all services are accessible only via interfaces.

#### Remaining Concrete Injections
| Service | Used By | Action Needed |
|---------|---------|---------------|
| `DatabaseService` | ~15 components | Create `@inject IDatabaseService DB` |
| `ProgressService` | 3 components | Requires OnUpdate event on interface |
| `ImageService` | CivitaiService | Update CivitaiService to use IImageService |

#### Considerations
- `DatabaseService` concrete usage is widespread - large refactor
- `ProgressService.OnUpdate` event needs interface exposure
- May want to defer to avoid destabilizing working code

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 10 End |
|--------|---------------|---------------|--------------|
| ManagerService lines | ~1600 | ~450 | ~400 |
| Facade properties | 27 | 0 | 0 ? |
| Orchestration properties | - | 14 | 6 |
| Services extracted | 0 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 |
| Unit tests | 0 | 150 | 150 |
| Files removed (cleanup) | - | - | 2 |

---

## DI Registration Reference

### Interface-Only (Clean Pattern)
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
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();
```

### Dual Registration (Required)
```csharp
// Components still inject concrete type
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());

// ProgressContainer subscribes to OnUpdate event
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddSingleton<IProgressService>(sp => sp.GetRequiredService<ProgressService>());

// ComfyUIWebsocketService sets Progress directly
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<ImageService>());
```

---

## Commit History Reference

| Checkpoint | Description | Tests |
|------------|-------------|-------|
| Phase 8.5 | Legacy events removed | 138 |
| Phase 9 | Service interfaces created | 150 |
| Phase 9.5 | Facades removed, DI cleanup | 150 |
| Phase 10 Task B+C | Styles/ButtonTags removed | 150 |
| Phase 10 Task A | Generation state to IImageService | 150 |
| Phase 10 Task D | WebUI cleanup, files removed | 150 |

---

**End of Document**
