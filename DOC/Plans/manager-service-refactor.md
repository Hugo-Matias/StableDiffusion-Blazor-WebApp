# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 9 - Service Interfaces & Testing  
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

### ? Phase 1: Foundation - Event System & Testing (Complete)
- Created `BlazorWebApp.Tests` xUnit project
- Implemented `EventService` with typed events
- 15/15 tests passing

### ? Phase 2: Extract StateService (Complete)
- `IStateService` interface with full implementation
- State persistence and parameters
- 21/21 tests passing

### ? Phase 3: Extract SettingsService (Complete)
- `ISettingsService` interface with full implementation
- JSON persistence to BlazorDiffusion.json
- 31/31 tests passing

### ? Phase 4: Extract BackendService (Complete)
- `IBackendService` interface with full implementation
- ComfyUI health checks, Samplers, Schedulers, Upscalers
- 15/15 tests passing

### ? Phase 5: Extract ModelService (Complete)
- `IModelService` interface with full implementation
- Checkpoint, Diffusion, VAE, CLIP model management
- 23/23 tests passing

### ? Phase 5.5: Testing & Interface Extraction (Complete)
- `IComfyUIService` interface extracted
- Mock builders and test fixtures created
- BackendService and ModelService tests added

### ? Phase 6: Extract GalleryService (Complete)
- `IGalleryService` interface with full implementation
- Folders, Projects, Image Selection
- 13/13 tests passing

### ? Phase 7: Extract SessionService (Complete)
- `ISessionService` interface with full implementation
- Canvas, Image Editor, Session Videos
- 20/20 tests passing

### ? Phase 7.5: WebUI Deprecation (Complete)
- All Automatic1111 WebUI code removed
- ComfyUI naming simplified
- 14 components removed

### ? Phase 8: Component Migration (Complete)
- 45/56 components migrated to specialized services
- All components use EventService for typed events
- No legacy Action event subscriptions remain

### ? Phase 8.5: ManagerService Cleanup (Complete)
- ManagerService reduced from ~1600 to 658 lines (59% reduction)
- All legacy Action events removed
- All migration comments removed
- Code consolidated and organized

---

## Current Phase: Phase 9 - Service Interfaces & Testing

### Objective
Create interfaces for remaining services and achieve comprehensive test coverage.

### Services WITH Interfaces ?

| Service | Interface | Tests | Status |
|---------|-----------|-------|--------|
| EventService | `IEventService` | 15 | ? Complete |
| StateService | `IStateService` | 21 | ? Complete |
| SettingsService | `ISettingsService` | 31 | ? Complete |
| BackendService | `IBackendService` | 15 | ? Complete |
| ModelService | `IModelService` | 23 | ? Complete |
| GalleryService | `IGalleryService` | 13 | ? Complete |
| SessionService | `ISessionService` | 20 | ? Complete |
| ComfyUIService | `IComfyUIService` | 0 | ?? Interface exists |

**Total:** 138/138 tests passing ?

### Services WITHOUT Interfaces (Phase 9 Priority)

| Service | Complexity | Priority | Notes |
|---------|------------|----------|-------|
| **ManagerService** | HIGH | ?? CRITICAL | Core orchestrator |
| **ImageService** | HIGH | ?? HIGH | Generation workflows |
| **WorkflowService** | LOW | ?? MEDIUM | Template management |
| **ProgressService** | LOW | ?? LOW | Simple state |
| **RouterService** | HIGH | ?? MEDIUM | API routing |
| **MagickService** | LOW | ?? LOW | ImageMagick wrapper |
| **CivitaiService** | MEDIUM | ?? LOW | External API |
| **CsvService** | LOW | ?? LOW | CSV operations |
| DatabaseService | VERY HIGH | ? Deferred | Already has adapter |
| IOService | MEDIUM | ? Deferred | File operations |

### Phase 9 Task Breakdown

#### Week 1: Critical Interfaces
- [ ] Create `IManagerService` interface
- [ ] Create `IImageService` interface
- [ ] Update DI registrations
- [ ] Write orchestration tests (20-25 tests)
- [ ] Write generation tests (20-25 tests)

#### Week 2: Support Interfaces
- [ ] Create `IWorkflowService` interface
- [ ] Create `IRouterService` interface
- [ ] Create `IProgressService` interface
- [ ] Write tests (15-20 tests)

#### Week 3: Utility Interfaces
- [ ] Create `IMagickService` interface
- [ ] Create `ICivitaiService` interface
- [ ] Create `ICsvService` interface
- [ ] Write tests (10-15 tests)

#### Week 4: Integration & Cleanup
- [ ] Integration testing
- [ ] Documentation updates
- [ ] Final cleanup

### Target Test Count: 250+

---

## Key Metrics

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| ManagerService Lines | ~1600 | 658 | -59% |
| Services with Interfaces | 1 | 8 | +700% |
| Unit Tests | 0 | 150 | +150 |
| Components Migrated | 0 | 45 | +45 |
| Legacy Action Events | 25+ | 0 | -100% |

---

## Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| Facade properties kept | Backward compatibility during migration |
| EventService for all events | Typed, testable, decoupled |
| Interface per service | Enables mocking and testing |
| Singleton services | Shared state across application |

---

## Notes

- All components now use `IEventService` for event subscriptions
- All legacy Action events have been removed from ManagerService
- ManagerService is now a lightweight orchestrator (658 lines)
- Ready for Phase 9 interface extraction and testing

**End of Document**
