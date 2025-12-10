# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Planning
**Last Updated:** 2025-01-13

---

## Problem Statement

The `ManagerService` (~1500+ lines) is currently a "God Object" that handles too many responsibilities:

### Current Responsibilities (Identified)

| Category | Responsibilities | Lines (Approx.) |
|----------|-----------------|-----------------|
| **State Management** | AppState, Parameters (Txt2Img, Img2Img, Upscale, Img2Vid), SelectedImages, Canvas states | ~300 |
| **Event Broadcasting** | 25+ events (OnSDModelsChange, OnOptionsChange, OnStateHasChanged, etc.) | ~100 |
| **Settings/Persistence** | LoadSettings, SaveSettings, LoadState, SaveState, NormalizeState | ~150 |
| **Model/Asset Management** | SDModels, VAEs, Samplers, Schedulers, Upscalers, GetWorkflowModels, WorkflowAssets | ~250 |
| **Backend Orchestration** | IsWebuiUp, IsComfyUIUp, backend-dependent resource loading, options | ~150 |
| **Workflow Management** | GetCurrentWorkflow, SetCurrentWorkflow, WorkflowBase, GetWorkflowsForMode | ~200 |
| **Gallery/Project Management** | Folders, Projects, SetCurrentFolder, SetCurrentProject | ~100 |
| **Parameter Initialization** | InitializeParameters, Script initializers (ControlNet, ADetailer, etc.) | ~400 |
| **Utility Methods** | Prompt parsing, path conversion, various helpers | ~150 |

### Problems

1. **Single Point of Failure**: Any change risks breaking unrelated functionality
2. **Tight Coupling**: Services depend on `ManagerService` for unrelated operations
3. **Testing Difficulty**: Can't unit test individual concerns
4. **Event Chaos**: Components subscribe to events for unrelated concerns
5. **Initialization Order**: Complex startup sequence prone to race conditions
6. **Memory Overhead**: All state held in memory regardless of usage

---

## Proposed Solution

### Architecture: Service Orchestrator Pattern

Transform `ManagerService` into a lightweight **Orchestrator** that:
- Coordinates between specialized services
- Provides backward-compatible facade for gradual migration
- Handles cross-cutting concerns (initialization sequence)

### Target Service Decomposition

```
???????????????????????????????????????????????????????????????????????
?                        ManagerService (Orchestrator)                 ?
?  - Coordinates initialization                                        ?
?  - Provides backward-compatible facades                              ?
?  - Handles cross-service communication                               ?
???????????????????????????????????????????????????????????????????????
                                    ?
        ?????????????????????????????????????????????????????????
        ?                           ?                           ?
        ?                           ?                           ?
?????????????????????   ?????????????????????   ?????????????????????
?   StateService    ?   ?   ModelService    ?   ?  WorkflowService  ?
?  - AppState       ?   ?  - Checkpoints    ?   ?  - Current WF     ?
?  - Parameters     ?   ?  - Diffusion      ?   ?  - WF for mode    ?
?  - Load/Save      ?   ?  - VAEs, CLIPs    ?   ?  - Assets         ?
?  - Normalization  ?   ?  - Samplers       ?   ?  (already exists) ?
?????????????????????   ?????????????????????   ?????????????????????
        ?                           ?                           ?
        ?                           ?                           ?
?????????????????????   ?????????????????????   ?????????????????????
?  SettingsService  ?   ?  BackendService   ?   ?  GalleryService   ?
?  - App settings   ?   ?  - WebUI/ComfyUI  ?   ?  - Folders        ?
?  - JSON persist   ?   ?  - Health check   ?   ?  - Projects       ?
?  - Validation     ?   ?  - Options        ?   ?  - Selection      ?
?????????????????????   ?????????????????????   ?????????????????????
        ?                           ?                           ?
        ?                           ?                           ?
?????????????????????   ?????????????????????   ?????????????????????
?   EventService    ?   ? ParameterFactory  ?   ?  SessionService   ?
?  - Central hub    ?   ?  - Script params  ?   ?  - Canvas state   ?
?  - Pub/sub        ?   ?  - Defaults from  ?   ?  - Image editor   ?
?  - Typed events   ?   ?    settings       ?   ?  - Videos         ?
?????????????????????   ?????????????????????   ?????????????????????
```

### Design Patterns to Apply

| Pattern | Purpose | Application |
|---------|---------|-------------|
| **Facade** | Backward compatibility | `ManagerService` wraps new services |
| **Mediator** | Decouple communication | `EventService` for cross-service events |
| **Factory** | Object creation | `ParameterFactory` for script parameters |
| **Strategy** | Backend abstraction | `BackendService` with WebUI/ComfyUI strategies |
| **Repository** | State persistence | `StateService` abstracts DB/JSON |
| **Observer** | Event system | Typed events replace `Action` delegates |

---

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| Keep `ManagerService` as facade during migration | Avoids breaking changes, allows incremental migration |
| Extract `StateService` first | Most isolated concern, foundation for other extractions |
| Use interfaces for all new services | Enables testing and future flexibility |
| Maintain singleton lifetime | State must be shared across components |
| Typed events over `Action` delegates | Better IntelliSense, compile-time safety |

### Conventions

- All new services will have interfaces (e.g., `IStateService`)
- Events use `EventArgs`-derived classes in `BlazorWebApp.Events` namespace
- Services follow naming: `{Domain}Service.cs`
- Backward-compatible properties/methods marked with `[Obsolete]` pointing to new location

---

## Implementation Phases

### Phase 1: Foundation - Event System & Interfaces
**Objective:** Create infrastructure for typed events and service interfaces
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `BlazorWebApp/Events` folder with typed event args
- [ ] Create `IEventService` interface with pub/sub methods
- [ ] Implement `EventService` with typed event aggregation
- [ ] Create interface stubs for planned services
- [ ] Register `EventService` in DI

#### Success Criteria
- EventService compiles and is injectable
- At least 3 typed event args classes created
- No changes to existing functionality

---

### Phase 2: Extract StateService
**Objective:** Move state persistence and parameters to dedicated service
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IStateService` interface
- [ ] Create `StateService` with:
  - AppState property
  - Parameter properties (Txt2Img, Img2Img, etc.)
  - LoadState/SaveState methods
  - NormalizeState logic
- [ ] Inject `StateService` into `ManagerService`
- [ ] Delegate state operations from `ManagerService` to `StateService`
- [ ] Mark delegated `ManagerService` members as `[Obsolete]`

#### Success Criteria
- StateService handles all state persistence
- ManagerService.State still works (facade)
- No breaking changes to consumers

---

### Phase 3: Extract SettingsService
**Objective:** Move settings management to dedicated service
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `ISettingsService` interface
- [ ] Create `SettingsService` with:
  - Settings property
  - LoadSettings/SaveSettings methods
  - Settings validation
- [ ] Inject into ManagerService
- [ ] Delegate settings operations

#### Success Criteria
- Settings persist correctly
- SettingsService is injectable standalone

---

### Phase 4: Extract BackendService
**Objective:** Consolidate backend (WebUI/ComfyUI) orchestration
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IBackendService` interface
- [ ] Create `BackendService` with:
  - IsWebuiUp, IsComfyUIUp properties
  - Health check methods
  - LoadBackendDependentResources
  - Options management
- [ ] Wire backend state change events through EventService

#### Success Criteria
- Backend availability managed in one place
- Components can inject `IBackendService` directly

---

### Phase 5: Extract ModelService
**Objective:** Consolidate model/asset management
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IModelService` interface
- [ ] Create `ModelService` with:
  - Model lists (Checkpoints, Diffusion, VAEs, CLIPs, etc.)
  - GetWorkflowModels, SetCurrentModel
  - Asset options resolution
- [ ] Coordinate with existing `IAssetResolverService`

#### Success Criteria
- Model loading/selection works correctly
- Clear separation from workflow management

---

### Phase 6: Extract GalleryService  
**Objective:** Move gallery/project management to dedicated service
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IGalleryService` interface
- [ ] Create `GalleryService` with:
  - Folders/Projects lists
  - SetCurrentFolder/SetCurrentProject
  - SelectedImageIds management
- [ ] Move related events to EventService

#### Success Criteria
- Gallery page works with new service
- ManagerService delegates correctly

---

### Phase 7: Extract ParameterFactory
**Objective:** Move script parameter creation to factory
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IParameterFactory` interface
- [ ] Create `ParameterFactory` with all Create* methods:
  - CreateControlNet, CreateADetailer, CreateCutoff, etc.
- [ ] InitializeParameters uses factory
- [ ] Factory injected into StateService

#### Success Criteria
- All script parameters created via factory
- Factory testable in isolation

---

### Phase 8: Refactor to Orchestrator
**Objective:** Transform ManagerService into lightweight coordinator
**Status:** [ ] Not Started

#### Tasks
- [ ] Remove delegated implementations (keep facades)
- [ ] Add initialization orchestration logic
- [ ] Update components to inject specific services where appropriate
- [ ] Document migration path for consumers
- [ ] Clean up unused `[Obsolete]` members (after grace period)

#### Success Criteria
- ManagerService < 300 lines
- All services independently testable
- Application fully functional

---

## Stress Points & Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Breaking existing components | High | Facade pattern, backward compatibility, incremental migration |
| Event subscription changes | Medium | EventService supports both old and new patterns initially |
| Initialization order | Medium | BackendService centralizes startup sequence |
| Circular dependencies | Medium | Interface-based injection, careful dependency design |
| State synchronization | High | Single StateService instance, clear ownership |
| Performance regression | Low | Minimal indirection, same singleton pattern |

---

## Changelog

| Date | Phase | Changes |
|------|-------|---------|
| 2025-01-13 | Planning | Initial plan created |

---

## Code Examples

### Typed Event Example
```csharp
// BlazorWebApp/Events/ModelChangedEventArgs.cs
public class ModelChangedEventArgs : EventArgs
{
    public string PreviousModel { get; init; }
    public string NewModel { get; init; }
    public ModeType Mode { get; init; }
}

// Usage in EventService
public void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs;
public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
```

### StateService Interface Example
```csharp
public interface IStateService
{
    AppState State { get; }
    Txt2ImgParameters ParametersTxt2Img { get; }
    Img2ImgParameters ParametersImg2Img { get; }
    UpscaleParameters ParametersUpscale { get; }
    Img2VidParameters ParametersImg2Vid { get; }
    
    Task LoadState();
    Task SaveState();
    void InitializeParameters(ModeType[] modes);
}
```

### Facade Pattern in ManagerService
```csharp
public class ManagerService
{
    private readonly IStateService _stateService;
    
    // Facade - delegates to StateService
    [Obsolete("Use IStateService.State instead")]
    public AppState State => _stateService.State;
    
    // New code should inject IStateService directly
}
```

---

## References

- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Mediator Pattern](https://refactoring.guru/design-patterns/mediator)
- [Facade Pattern](https://refactoring.guru/design-patterns/facade)
- Current codebase: `BlazorWebApp/Services/ManagerService.cs`
- Existing interface pattern: `BlazorWebApp/Services/IAssetResolverService.cs`

---

## Open Questions for Discussion

1. **Event Migration Strategy**: Should we support both old `Action` events and new typed events during transition, or force migration per phase?

2. **Component Updates**: Should each phase include updating affected components to use new services, or defer all component updates to Phase 8?

3. **Testing Priority**: Should we add unit tests as we extract each service, or create a separate testing phase?

4. **Scoped vs Singleton**: Some services (like `SessionService` for canvas state) might benefit from scoped lifetime for multi-tab scenarios. Worth considering?

5. **WebUI Deprecation**: Since WebUI support is being deprecated, should we simplify by removing WebUI-specific code during this refactor, or keep it separate?

---

*Document version: 1.0*
*Created: 2025-01-13*
