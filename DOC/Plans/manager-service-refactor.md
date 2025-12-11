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
- Provides temporary facade during migration
- Handles cross-cutting concerns (initialization sequence)

### Target Service Decomposition

```
???????????????????????????????????????????????????????????????????????
?                        ManagerService (Orchestrator)                 ?
?  - Coordinates initialization                                        ?
?  - Temporary facades during migration                                ?
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
?  - App settings   ?   ?  - ComfyUI status ?   ?  - Folders        ?
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
| **Facade** | Temporary compatibility | `ManagerService` wraps new services during migration |
| **Mediator** | Decouple communication | `EventService` for cross-service events |
| **Factory** | Object creation | `ParameterFactory` for script parameters |
| **Repository** | State persistence | `StateService` abstracts DB/JSON |
| **Observer** | Event system | Typed events replace `Action` delegates |

---

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| No backward compatibility focus | Clean migration, components update per phase |
| Per-phase component migration | Keeps each phase testable and stable |
| xUnit for unit testing | Modern, widely adopted in .NET ecosystem |
| Keep delegation until Phase 8 | Allows focused extraction per phase |
| Typed event naming with "EventArgs" | Prevents naming collisions, clear intent |
| Migrate only current phase services | Avoid premature migrations |
| Remove WebUI code per phase | Simplifies during extraction |
| BackendService for abstraction | Separation of concerns from ComfyUIService |

### Conventions

- All new services will have interfaces (e.g., `IStateService`)
- Events use `{Domain}ChangedEventArgs` pattern in `BlazorWebApp.Events` namespace
- Services follow naming: `{Domain}Service.cs` with corresponding `I{Domain}Service.cs`
- Unit tests in separate project: `BlazorWebApp.Tests`
- Test structure mirrors service structure: `Services/{ServiceName}Tests.cs`
- Components only inject services migrated in current or previous phases

---

## Implementation Phases

### Phase 1: Foundation - Event System & Testing Infrastructure
**Objective:** Create infrastructure for typed events, service interfaces, and unit testing
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `BlazorWebApp.Tests` xUnit test project
- [ ] Add required NuGet packages (xUnit, Moq, FluentAssertions)
- [ ] Create `BlazorWebApp/Events` folder
- [ ] Create typed event args classes:
  - `StateChangedEventArgs`
  - `ModelChangedEventArgs`
  - `BackendAvailabilityChangedEventArgs`
- [ ] Create `IEventService` interface with pub/sub methods
- [ ] Implement `EventService` with typed event aggregation
- [ ] Write unit tests for `EventService`
- [ ] Create interface stubs for planned services (IStateService, ISettingsService, etc.)
- [ ] Register `EventService` in DI (Program.cs)

#### Success Criteria
- [ ] EventService compiles and is injectable
- [ ] EventService unit tests pass
- [ ] Test project structure established
- [ ] No changes to existing functionality

---

### Phase 2: Extract StateService
**Objective:** Move state persistence and parameters to dedicated service
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IStateService` interface
- [ ] Create `StateService` with:
  - AppState property
  - Parameter properties (Txt2Img, Img2Img, Upscale, Img2Vid)
  - LoadState/SaveState methods
  - NormalizeState logic
- [ ] Write unit tests for StateService
- [ ] Register StateService in DI as singleton
- [ ] Inject `StateService` into `ManagerService`
- [ ] Delegate state operations from `ManagerService` to `StateService` (keep facade)
- [ ] Remove old `Action` events from ManagerService (OnAppStateChanged, OnTxt2ImgParametersChanged, etc.)
- [ ] Add typed events to EventService
- [ ] Update components using state/parameters:
  - Identify components via code search
  - Inject `IStateService` where needed
  - Subscribe to typed events via `IEventService`
  - Test each component after update
- [ ] Run full application test

#### Component Migration Checklist
- [ ] (Components TBD during implementation via code search)

#### Success Criteria
- [ ] StateService unit tests pass
- [ ] ManagerService delegates correctly
- [ ] All migrated components use IStateService
- [ ] Application fully functional
- [ ] Old state events removed

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
- [ ] Write unit tests for SettingsService
- [ ] Register SettingsService in DI as singleton
- [ ] Inject into ManagerService and StateService (for parameter initialization)
- [ ] Delegate settings operations from ManagerService
- [ ] Update components using Settings
- [ ] Remove WebUI-specific settings handling (if any)
- [ ] Run full application test

#### Component Migration Checklist
- [ ] (Components TBD during implementation)

#### Success Criteria
- [ ] SettingsService unit tests pass
- [ ] Settings persist correctly
- [ ] Components use ISettingsService
- [ ] Application functional

---

### Phase 4: Extract BackendService
**Objective:** Consolidate backend (ComfyUI) orchestration
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IBackendService` interface
- [ ] Create `BackendService` with:
  - IsBackendAvailable property (replaces IsComfyUIUp/IsWebuiUp)
  - Health check methods
  - LoadBackendDependentResources
  - Options management
- [ ] Write unit tests for BackendService
- [ ] Register BackendService in DI as singleton
- [ ] Inject into ManagerService
- [ ] Remove `IsWebuiUp` property and all WebUI-specific code
- [ ] Wire backend state changes through EventService (BackendAvailabilityChangedEventArgs)
- [ ] Update components checking backend availability
- [ ] Run full application test

#### Component Migration Checklist
- [ ] (Components TBD during implementation)

#### Success Criteria
- [ ] BackendService unit tests pass
- [ ] WebUI code removed
- [ ] Backend availability managed in one place
- [ ] Components use IBackendService
- [ ] Application functional

---

### Phase 5: Extract ModelService
**Objective:** Consolidate model/asset management
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IModelService` interface
- [ ] Create `ModelService` with:
  - Model lists (Checkpoints, Diffusion, VAEs, CLIPs, ClipVision, SDADetailer)
  - Sampler/Scheduler lists
  - Upscaler lists
  - GetWorkflowModels, SetCurrentModel, SetCurrentVae
  - GetModelsForAssetType, GetAssetOptions
- [ ] Write unit tests for ModelService
- [ ] Register ModelService in DI as singleton
- [ ] Coordinate with existing `IAssetResolverService` (inject ModelService into AssetResolverService)
- [ ] Inject into ManagerService
- [ ] Remove model-related events from ManagerService (OnSDModelsChange, OnSamplersSchedulersChanged)
- [ ] Add typed events to EventService
- [ ] Update components using models/assets
- [ ] Run full application test

#### Component Migration Checklist
- [ ] (Components TBD during implementation)

#### Success Criteria
- [ ] ModelService unit tests pass
- [ ] Model loading/selection works correctly
- [ ] Clear separation from workflow management
- [ ] IAssetResolverService works with IModelService
- [ ] Application functional

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
  - Image selection methods (Add, Remove, Clear, Replace)
- [ ] Write unit tests for GalleryService
- [ ] Register GalleryService in DI as singleton
- [ ] Inject into ManagerService
- [ ] Remove gallery events from ManagerService (OnFolderChange, OnProjectsChange, OnSelectedImagesChanged)
- [ ] Add typed events to EventService
- [ ] Update gallery and image components
- [ ] Run full application test

#### Component Migration Checklist
- [ ] (Components TBD during implementation)

#### Success Criteria
- [ ] GalleryService unit tests pass
- [ ] Gallery page works with new service
- [ ] Image selection works correctly
- [ ] Application functional

---

### Phase 7: Extract ParameterFactory
**Objective:** Move script parameter creation to factory
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IParameterFactory` interface
- [ ] Create `ParameterFactory` with all Create* methods:
  - CreateControlNet
  - CreateADetailer
  - CreateCutoff
  - CreateDynamicPrompts
  - CreateUltimateUpscale
  - CreateMultiDiffusionTiledDiffusion
  - CreateMultiDiffusionTiledVae
  - CreateRegionalPrompter
  - CreateXYZPlot
  - CreateIncantationsModel
- [ ] Write unit tests for ParameterFactory
- [ ] Register ParameterFactory in DI as singleton
- [ ] Inject into StateService for InitializeParameters
- [ ] Remove Create* methods from ManagerService
- [ ] Update StateService to use factory
- [ ] Run full application test

#### Success Criteria
- [ ] ParameterFactory unit tests pass
- [ ] All script parameters created via factory
- [ ] Factory testable in isolation
- [ ] Application functional

---

### Phase 8: Refactor to Orchestrator
**Objective:** Transform ManagerService into lightweight coordinator
**Status:** [ ] Not Started

#### Tasks
- [ ] Remove all delegated implementations from ManagerService
- [ ] Keep only orchestration logic:
  - Initialization coordination
  - Cross-service communication (if any)
- [ ] Update remaining components to inject specific services
- [ ] Create migration guide document
- [ ] Remove all temporary facade properties/methods
- [ ] Verify ManagerService < 300 lines
- [ ] Run comprehensive application test
- [ ] Update architecture documentation

#### Success Criteria
- [ ] ManagerService < 300 lines
- [ ] All services independently testable
- [ ] No facade methods remaining
- [ ] Application fully functional
- [ ] Migration guide complete

---

## Testing Strategy

### Unit Test Structure
```
BlazorWebApp.Tests/
  Services/
    EventServiceTests.cs
    StateServiceTests.cs
    SettingsServiceTests.cs
    BackendServiceTests.cs
    ModelServiceTests.cs
    GalleryServiceTests.cs
    ParameterFactoryTests.cs
```

### Test Tooling
- **xUnit**: Test framework
- **Moq**: Mocking dependencies
- **FluentAssertions**: Readable assertions

### Test Coverage Goals
- All public methods tested
- Edge cases covered
- Event publication/subscription verified

---

## Stress Points & Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Breaking existing components | High | Per-phase component migration, thorough testing |
| Event subscription changes | Medium | Migrate events per phase, update subscriptions immediately |
| Initialization order | Medium | BackendService centralizes startup sequence |
| Circular dependencies | Medium | Interface-based injection, careful dependency design |
| State synchronization | High | Single StateService instance, clear ownership |
| Performance regression | Low | Minimal indirection, same singleton pattern |
| Large component updates | Medium | Migrate only current phase services, incremental approach |

---

## Changelog

| Date | Phase | Changes |
|------|-------|---------|
| 2025-01-13 | Planning | Initial plan created |
| 2025-01-13 | Planning | Refined with decisions: xUnit, per-phase migration, no backward compat focus |

---

## Code Examples

### Typed Event Example
```csharp
// BlazorWebApp/Events/StateChangedEventArgs.cs
namespace BlazorWebApp.Events
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangeType ChangeType { get; init; }
        public object? OldValue { get; init; }
        public object? NewValue { get; init; }
    }

    public enum StateChangeType
    {
        AppState,
        Txt2ImgParameters,
        Img2ImgParameters,
        UpscaleParameters,
        Img2VidParameters
    }
}

// BlazorWebApp/Events/ModelChangedEventArgs.cs
namespace BlazorWebApp.Events
{
    public class ModelChangedEventArgs : EventArgs
    {
        public string PreviousModel { get; init; }
        public string NewModel { get; init; }
        public ModeType Mode { get; init; }
    }
}
```

### EventService Interface & Implementation
```csharp
// BlazorWebApp/Services/IEventService.cs
public interface IEventService
{
    void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
}

// BlazorWebApp/Services/EventService.cs
public class EventService : IEventService
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs
    {
        var eventType = typeof(TEvent);
        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                ((Action<TEvent>)handler)(eventArgs);
            }
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs
    {
        var eventType = typeof(TEvent);
        if (!_subscribers.ContainsKey(eventType))
            _subscribers[eventType] = new List<Delegate>();
        _subscribers[eventType].Add(handler);
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs
    {
        var eventType = typeof(TEvent);
        if (_subscribers.TryGetValue(eventType, out var handlers))
            handlers.Remove(handler);
    }
}
```

### StateService Interface Example
```csharp
// BlazorWebApp/Services/IStateService.cs
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

### Component Migration Example
```csharp
// BEFORE (Phase 1)
@inject ManagerService _m

@code {
    protected override void OnInitialized()
    {
        _m.OnAppStateChanged += StateHasChanged;
    }
    
    private void UpdateState()
    {
        _m.State.SomeProperty = value;
        await _m.SaveState();
    }
}

// AFTER (Phase 2)
@inject IStateService _state
@inject IEventService _events

@code {
    protected override void OnInitialized()
    {
        _events.Subscribe<StateChangedEventArgs>(OnStateChanged);
    }
    
    private void OnStateChanged(StateChangedEventArgs e)
    {
        StateHasChanged();
    }
    
    private void UpdateState()
    {
        _state.State.SomeProperty = value;
        await _state.SaveState();
    }
    
    public void Dispose()
    {
        _events.Unsubscribe<StateChangedEventArgs>(OnStateChanged);
    }
}
```

### Temporary Facade Example (Until Phase 8)
```csharp
// BlazorWebApp/Services/ManagerService.cs
public class ManagerService
{
    private readonly IStateService _stateService;
    private readonly IEventService _eventService;
    
    // Temporary facade - delegates to StateService
    public AppState State => _stateService.State;
    public Txt2ImgParameters ParametersTxt2Img => _stateService.ParametersTxt2Img;
    
    // Temporary facade - delegates to EventService
    public void InvokeStateChanged()
    {
        _eventService.Publish(new StateChangedEventArgs 
        { 
            ChangeType = StateChangeType.AppState 
        });
    }
    
    // Will be removed in Phase 8
}
```

---

## References

- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Mediator Pattern](https://refactoring.guru/design-patterns/mediator)
- [Facade Pattern](https://refactoring.guru/design-patterns/facade)
- [xUnit Documentation](https://xunit.net/)
- Current codebase: `BlazorWebApp/Services/ManagerService.cs`
- Existing interface pattern: `BlazorWebApp/Services/IAssetResolverService.cs`

---

## Notes

- **Backend Service**: Maintains abstraction layer over ComfyUIService for separation of concerns, even though no other backends are planned
- **WebUI Removal**: All WebUI-specific code will be removed during extraction phases
- **Testing**: Unit tests created per phase for each new service
- **Component Migration**: Only inject services that have been migrated in current or previous phases
- **Facade Lifetime**: Temporary delegation methods in ManagerService remain until Phase 8 for stability

---

*Document version: 1.1*
*Last updated: 2025-01-13*
