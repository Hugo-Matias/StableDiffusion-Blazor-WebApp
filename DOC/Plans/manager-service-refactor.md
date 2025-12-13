# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 11 - Civitai Service Decoupling ? **COMPLETE**
**Last Updated:** 2025-01-15

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **~350-line orchestrator** that coordinates between specialized services with **zero facade properties** and **no Civitai state**.

### Current Architecture

```
????????????????????????????????????????????????????????????????????????
?                    ManagerService (Orchestrator)                     ?
?  - Coordinates between specialized services                         ?
?  - Workflow management and asset coordination                       ?
?  - NO facade properties (all removed in Phase 9.5)                  ?
?  - NO generation state (moved to IImageService in Phase 10)         ?
?  - NO Civitai state (components use local state in Phase 11)        ?
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
**Completed:** 2025-01-15

Removed all backward compatibility facades from ManagerService, eliminated WebUI remnants, and cleaned up DI registrations.

---

### ? Phase 10: Orchestration Property Relocation (Complete)
**Completed:** 2025-01-15

Moved remaining orchestration properties from ManagerService to appropriate specialized services, and removed WebUI-era code.

#### Files Removed
- `BlazorWebApp/Models/GeneratedImagesInfo.cs`
- `BlazorWebApp/Data/Dtos/UpscaledImageDto.cs`

---

### ? Phase 11: Civitai Service Decoupling (Complete)
**Completed:** 2025-01-15

#### Objective
Remove Civitai state from ManagerService and decouple CivitaiService from ManagerService dependency.

#### Analysis Results

Before Phase 11, the following Civitai-related properties existed in ManagerService:

| Property | Status | Finding |
|----------|--------|---------|
| `CivitaiModels` | **Removed** | Only used by `CivitaiModelsPanel` - moved to component local state |
| `CivitaiImages` | **Already unused** | `CivitaiImagesPanel` already used local `_images` variable |
| `CivitaiCreators` | **Already unused** | `CivitaiCreatorsPanel` already used local `_creators` variable |

#### Changes Made

**1. CivitaiModelsPanel.razor** ?
- Removed `@inject ManagerService M`
- Added local `_models` state (matching pattern of other Civitai panels)
- Updated all references from `M.CivitaiModels` to `_models`
- Component now fully independent of ManagerService

**2. CivitaiService.cs** ?
- Removed `ManagerService` dependency from constructor
- Changed `ImageService _img` ? `IImageService _img` (interface-based)
- Added `IEventService _events` for progress notifications
- Development methods (`UpdateResourceDescriptions`, `UpdateResourceBaseModels`) now:
  - Accept optional `Action<int> onProgress` callback
  - Publish `ProgressChangedEventArgs` via EventService
  - No longer depend on `_m.CurrentProgress`

**3. ManagerService.cs** ?
- Removed `CivitaiModels` property
- Removed `CivitaiImages` property  
- Removed `CivitaiCreators` property
- ManagerService now has only 3 orchestration properties remaining

#### Phase 11 Metrics

| Metric | Before Phase 11 | After Phase 11 | Change |
|--------|-----------------|----------------|--------|
| ManagerService properties | 6 | 3 | -50% |
| CivitaiService dependencies | 3 services | 2 interfaces + 1 service | -1 concrete |
| Components with ManagerService | 19 | 18 | -1 |
| Build | ? | ? | - |
| Tests | 150 | 150 | - |

#### Remaining ManagerService Properties
After Phase 11 (only 3):
- `Options` - Backend options (consider moving to IBackendService in future)
- `ComfyWSClientId` - WebSocket client ID
- `ResourceTypeDirectories` - Resource paths (consider moving to IConfiguration)
- `CurrentProgress` - Int progress value with event
- `IsConverging` - Generation running flag

---

## Future Phases

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

#### Considerations
- `DatabaseService` concrete usage is widespread - large refactor
- `ProgressService.OnUpdate` event needs interface exposure
- May want to defer to avoid destabilizing working code

---

## Key Metrics Summary

| Metric | Phase 1 Start | Phase 9.5 End | Phase 10 End | Phase 11 End |
|--------|---------------|---------------|--------------|--------------|
| ManagerService lines | ~1600 | ~450 | ~400 | ~350 |
| Facade properties | 27 | 0 | 0 | 0 ? |
| Orchestration properties | - | 14 | 6 | 3 |
| Services extracted | 0 | 11 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 | 11 |
| Unit tests | 0 | 150 | 150 | 150 |
| Components freed from M | - | 12 | 17 | 18 |

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
| Phase 11 | Civitai decoupling complete | 150 |

---

**End of Document**
