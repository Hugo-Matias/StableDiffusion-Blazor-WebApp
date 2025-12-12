# Component Migration Log - Phase 8

## Status
**Phase:** Phase 8 - Orchestrator Refactor & Component Migration  
**Started:** 2025-01-14  
**Current Progress:** 10/70 components migrated (14%)

---

## Migration Strategy

### Service Injection Pattern
Replace `@inject ManagerService M` with specific service injections:

```razor
@inject IStateService State
@inject ISettingsService Settings
@inject IBackendService Backend
@inject IModelService Models
@inject IGalleryService Gallery
@inject ISessionService Session
@inject IEventService Events
```

### Property Reference Updates
- `M.State` ? `State.State`
- `M.Settings` ? `Settings.Settings`
- `M.CheckpointModels` ? `Models.CheckpointModels`
- `M.Folders` ? `Gallery.Folders`
- `M.CanvasImageData` ? `Session.CanvasImageData`

### Event Subscription Pattern
Replace Action delegates with EventService:
```csharp
// Old
M.OnAppStateChanged += StateHasChanged;

// New
Events.Subscribe<StateChangedEventArgs>(OnStateChanged);

// Cleanup in Dispose
Events.Unsubscribe<StateChangedEventArgs>(OnStateChanged);
```

---

## Component Groups

### Group 1: Simple Components (State/Settings only) - 10 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| ThemeSelector | ? | State, Settings | ? Not Started | |
| SettingsPanel | ? | State, Settings | ? Not Started | |
| StatusBar | ? | State, Backend | ? Not Started | |
| TopToolbar | Components/Shared | State, Gallery, Events | ? Complete | Migrated 2025-01-14 - Folder/Project/Theme/State selectors |
| NavBar | Components/Shared | State, Backend, Gallery, Events | ? Complete | Migrated 2025-01-14 - First component! |
| ProgressContainer | Components/Shared | ProgressService only | ? Not Started | No ManagerService - skip |
| ConfirmationDialog | Components/Shared | - | ? Not Started | No dependencies - skip |
| LoadingSpinner | Components/Shared | - | ? Not Started | No dependencies - skip |
| AssetViewer | Components/Shared | State | ? Not Started | Actually complex - move to Group 8 |
| JsonTreeView | Components/Shared | - | ? Not Started | No dependencies - skip |

---

### Group 2: Generation Forms (State + Models + Backend) - 18 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| GenerateFormTxt2Img | Components/Txt2Img | State, Models, Backend, Settings | ? Not Started | High priority |
| GenerateFormTxt2ImgComfyUI | Components/Txt2Img | State, Models, Backend, Settings | ? Not Started | High priority |
| GenerateFormImg2Img | Components/Img2Img | State, Models, Backend, Settings, Session | ? Not Started | High priority |
| GenerateFormImg2ImgComfyUI | Components/Img2Img | State, Models, Backend, Settings, Session | ? Not Started | High priority |
| GenerateFormImg2VidComfyUI | Components/Img2Vid | State, Models, Backend, Settings, Session | ? Not Started | High priority |
| PromptFields | Components/Shared/Generation | State, Settings | ? Not Started | Critical - many dependencies |
| PromptFieldsSimple | Components/Img2Vid | State, Settings | ? Not Started | |
| GenerateButton | Components/Shared/Generation | State, Backend | ? Not Started | |
| LoraForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| LoraCard | Components/Shared/Generation | State | ? Not Started | |
| ControlNetForm | Components/Shared/Generation | State, Settings, Session | ? Not Started | |
| ControlNetTabs | Components/Shared/Generation | State, Settings | ? Not Started | |
| ControlNetTabsDynamic | Components/Shared/Generation | State, Settings | ? Not Started | |
| WorkflowAssetsPanel | Components/Shared/Generation | State, Models | ? Not Started | |
| WorkflowAssetSelector | Components/Shared/Generation | State, Models | ? Not Started | |
| TagDrawer | Components/Shared/Generation | State | ? Not Started | |
| TagAccordion | Components/Shared/Generation | State | ? Not Started | |
| TextFieldAutocomplete | Components/Shared/Generation | - | ? Not Started | |

---

### Group 3: Script Forms (Settings + State) - 10 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| ADetailerForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| ADetailerModelForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| ADetailerModelFormComfyUI | Components/Shared/Generation | State, Settings | ? Not Started | |
| CutoffForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| DynamicPromptsForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| IncantationsForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| MultiDiffusionTiledDiffusionForm | Components/Shared/Generation | State, Settings, Backend | ? Not Started | |
| MultiDiffusionTiledVaeForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| RegionalPrompterForm | Components/Shared/Generation | State, Settings | ? Not Started | |
| XYZPlotForm | Components/Shared/Generation | State, Settings | ? Not Started | |

---

### Group 4: Gallery Components (Gallery + State) - 12 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| ImagesContainer | Components/Shared/Image | State, Gallery, Events | ? Complete | Migrated 2025-01-14 - Image grid display with selection |
| ImageCard | Components/Shared/Image | State, Gallery, Session, Backend, Events | ? Complete | Migrated 2025-01-14 - Individual image card with actions |
| ImageCarousel | Components/Shared/Image | State, Gallery | ? Not Started | |
| ImageViewer | Components/Shared/Image | State, Gallery | ? Not Started | |
| ImageViewerDialog | Components/Shared/Image | State, Gallery | ? Not Started | |
| ImageInfoDialog | Components/Shared/Image | State, Gallery | ? Not Started | |
| ImageInfoCopyParameterButtons | Components/Shared/Image | State | ? Not Started | |
| ImageProjectDialog | Components/Shared/Image | State, Gallery | ? Not Started | |
| ProjectCard | Components/Shared/Project | State | ? Complete | Migrated 2025-01-14 - Project display card |
| ProjectModal | Components/Shared/Project | Database only | ? Complete | No migration needed - doesn't use ManagerService |
| CreateProjectButton | Components/Shared/Project | State, Gallery | ? Complete | Migrated 2025-01-14 - Create project button |
| GallerySettings | Components/Gallery | State, Gallery, Events | ? Complete | Migrated 2025-01-14 - Gallery settings and filters |

---

### Group 5: Canvas/Session Components (Session + State) - 8 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| Img2ImgCanvas | Components/Img2Img | State, Session | ? Not Started | |
| ImageEditorModal | Components/ImageEditor | State, Session | ? Not Started | |
| LayerPanel | Components/ImageEditor | State, Session | ? Not Started | |
| ImageInput | Components/Shared/Image | Session | ? Not Started | |
| ImageUpload | Components/Shared/Image | Session | ? Not Started | |
| ImageDropzone | Components/Shared/Image | Session | ? Not Started | |
| VideoViewer | Components/Img2Vid | Session | ? Not Started | |
| VideoCard | Components/Img2Vid | Session | ? Not Started | |

---

### Group 6: Video Components (Session + State) - 3 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| GeneratedVideoTabs | Components/Img2Vid | State, Session | ? Not Started | |
| VideoInfoDialog | Components/Img2Vid | State, Session | ? Not Started | |
| UltimateUpscaleForm | Components/Img2Img | State, Settings, Backend, Session | ? Not Started | |

---

### Group 7: Resource Management (Models + State) - 15 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| ResourcePanel | Components/Resources | State, Models | ? Not Started | |
| ResourceCard | Components/Resources | State, Models | ? Not Started | |
| ResourceInfoDialog | Components/Resources | State, Models | ? Not Started | |
| ResourceVersionsDialog | Components/Resources | State, Models | ? Not Started | |
| ResourceImageCard | Components/Resources | State | ? Not Started | |
| ResourceImageDialog | Components/Resources | State | ? Not Started | |
| LoadResourceDialog | Components/Resources | State, Models | ? Not Started | |
| ResourceTemplatesBar | Components/Resources | State | ? Not Started | |
| ResourceTemplateDialog | Components/Resources | State | ? Not Started | |
| ResourceAuditPanel | Components/Resources | State, Models | ? Not Started | |
| CivitaiPanel | Components/Resources | State | ? Not Started | |
| CivitaiModelsPanel | Components/Resources | State, Models | ? Not Started | |
| CivitaiImagesPanel | Components/Resources | State | ? Not Started | |
| CivitaiCreatorsPanel | Components/Resources | State | ? Not Started | |
| CivitaiModelInfoDialog | Components/Resources | State, Models | ? Not Started | |

---

### Group 8: Complex/Page Components (Multiple services) - 10 components

| Component | Location | Services Required | Status | Notes |
|-----------|----------|-------------------|--------|-------|
| MainLayout | Components/Shared | State, Backend, Models, Gallery, Events | ? Not Started | **CRITICAL** - App initialization |
| GeneratedImageTabs | Components/Shared/Generation | State, Gallery, Session | ? Not Started | |
| StateDialog | Components/Shared | State | ? Complete | Migrated 2025-01-14 - Simple dialog |
| Txt2ImgComfyUI | Pages/ComfyUI | State, Models, Backend, Session | ? Not Started | |
| Img2ImgComfyUI | Pages/ComfyUI | State, Models, Backend, Session, Gallery | ? Not Started | |
| Img2VidComfyUI | Pages/ComfyUI | State, Models, Backend, Session | ? Not Started | |
| Index | Pages | State, Gallery, Events | ? Complete | Migrated 2025-01-14 - Gallery page with infinite scroll |
| Settings | Pages | State, Settings | ? Not Started | |
| Resources | Pages | State, Models | ? Not Started | |
| AssetViewer | Components/Shared | State, Session, Models, Gallery | ? Not Started | Complex - many M. references |

---

## Migration Progress

### Status Legend
- ? Not Started
- ?? In Progress
- ? Complete
- ? Blocked
- ?? Testing

### Statistics
- **Total Components:** 70
- **Not Started:** 56 (80%)
- **Complete:** 10 (14%)
  - NavBar.razor
  - StateDialog.razor
  - TopToolbar.razor
  - Index.razor
  - ProjectCard.razor
  - CreateProjectButton.razor
  - ProjectModal.razor (no migration needed)
  - GallerySettings.razor
  - ImagesContainer.razor
  - ImageCard.razor
- **Skipped (No ManagerService):** 4 (6%)
  - LoadingSpinner
  - ConfirmationDialog  
  - ProgressContainer
  - JsonTreeView

---

## Notes & Issues

### Known Issues
1. **Styles Dropdown Not Populating** - See manager-service-refactor.md Phase 8 Known Issues
2. **State Loading Events** - ? FIXED: StateService.LoadState() now publishes StateChangedEventArgs after loading
3. **Folder Selection "All"** - ? FIXED: GalleryService.SetCurrentFolder() now handles ID=0 case
4. **Project Card Selection Not Updating Images** - ? FIXED: ManagerService.SetCurrentProject() now delegates to GalleryService to fire ProjectChangedEventArgs

### Fixed Issues
1. **StateService Interface Mismatch** - Fixed LoadState to have two separate overloads (parameterless and with int stateId) matching IStateService interface
2. **State Loading Events** - StateService.LoadState() now publishes all StateChangedEventArgs events after loading state, ensuring UI components refresh properly
3. **GalleryService Folder ID=0** - Added handling for folder ID=0 ("All folders") case in SetCurrentFolder method
4. **ManagerService Project Selection** - Modified SetCurrentProject() to delegate to GalleryService.SetCurrentProject() ensuring new EventService-based components receive ProjectChangedEventArgs notifications
5. **Index Page Event Handlers** - Properly implemented async event handlers to refresh projects list and images when project changes

### Migration Patterns Discovered

#### Pattern 1: Adding Events Namespace
**All** migrated components need this using directive:
```razor
@using BlazorWebApp.Events
```

#### Pattern 2: Service Injection Replacements
```razor
// Old
@inject ManagerService M

// New
@inject IStateService State
@inject IGalleryService Gallery
@inject IBackendService Backend
@inject IEventService Events
```

#### Pattern 3: Property Access Patterns
```csharp
// Old
M.Projects
M.State.Generation.Workflows
M.IsComfyUIUp

// New
Gallery.Projects
State.State.Generation.Workflows
Backend.IsBackendAvailable
```

#### Pattern 4: Event Subscription Pattern
```csharp
// Old - OnInitialized
M.OnWebuiStateChanged += Refresh;
M.OnProjectsChange += Refresh;
M.OnWorkflowBaseChanged += Refresh;

// New - OnInitialized
Events.Subscribe<BackendAvailabilityChangedEventArgs>(OnBackendStateChanged);
Events.Subscribe<ProjectChangedEventArgs>(OnProjectsChanged);
Events.Subscribe<StateChangedEventArgs>(OnWorkflowBaseChanged);

// Handler methods
private void OnBackendStateChanged(BackendAvailabilityChangedEventArgs args) => Refresh();
private void OnProjectsChanged(ProjectChangedEventArgs args) => Refresh();
private void OnWorkflowBaseChanged(StateChangedEventArgs args) => Refresh();
```

#### Pattern 5: Dispose Pattern
```csharp
// Old
public void Dispose()
{
    M.OnWebuiStateChanged -= Refresh;
    M.OnProjectsChange -= Refresh;
    M.OnWorkflowBaseChanged -= Refresh;
}

// New
public void Dispose()
{
    Events.Unsubscribe<BackendAvailabilityChangedEventArgs>(OnBackendStateChanged);
    Events.Unsubscribe<ProjectChangedEventArgs>(OnProjectsChanged);
    Events.Unsubscribe<StateChangedEventArgs>(OnWorkflowBaseChanged);
}
```

#### Pattern 6: Type Name Conflicts
When a component uses the entity `State` and also injects `IStateService State`, use fully qualified names:

```csharp
// Problem: 'State' refers to both IStateService and the entity type
private State? _selectedState;  // Ambiguous familier!

// Solution: Use fully qualified type name for entity
private BlazorWebApp.Data.Entities.State? _selectedState;  // Clear!

// OR rename the parameter/variable
[Parameter] public BlazorWebApp.Data.Entities.State StatePreset { get; set; }
```

**Common Conflicts:**
- `State` - Injected service vs Data entity
- Component parameter names should avoid service names

#### Pattern 7: ManagerService Delegation During Migration
When a ManagerService method needs to work with both old (Action events) and new (EventService) systems:

```csharp
// In ManagerService - Delegate to specialized service which fires new events
public async Task SetCurrentProject(int id)
{
    await GetFolders();
    await GetProjects();
    
    // Delegate to GalleryService which updates state and fires ProjectChangedEventArgs
    await _gallery.SetCurrentProject(id);
    
    // Fire old events for backward compatibility (will be removed in Phase 8)
    OnProjectChange?.Invoke();
    OnProjectChangeTask?.Invoke();
}
```

This ensures both migrated components (using EventService) and unmigrated components (using Action events) continue to work during the migration period.

### Breaking Changes
- Parameters named `State` must be renamed or fully qualified due to `IStateService State` injection
---

## Next Steps

1. ? Create this migration log
2. ? Start with Group 1 (Simple Components) - NavBar ?, StateDialog ?, TopToolbar ?
3. ?? Continue with more simple components (in progress)
4. ? Progress through remaining groups
5. ? Migrate MainLayout last (most complex)

---

## Completion Checklist

- [ ] All 70 components migrated (8/70 = 11%)
- [ ] All components tested individually
- [ ] Full application smoke test
- [ ] No `M.Property` references in components (except orchestration)
- [ ] All components use EventService for subscriptions
- [ ] All tests passing (128+ tests)
- [ ] Build passes without errors ?
- [ ] No compiler warnings
- [ ] Styles dropdown working
- [ ] Documentation updated ?
