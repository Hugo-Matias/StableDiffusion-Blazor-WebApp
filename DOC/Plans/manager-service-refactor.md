# ManagerService Split and Refactor - Implementation Plan

## Status
**Current Phase:** Phase 10 - Orchestration Property Relocation  
**Last Updated:** 2025-01-15

---

## Problem Statement

The `ManagerService` was originally a "God Object" (~1600+ lines) handling too many responsibilities. Through systematic refactoring, it has been reduced to a **~450-line orchestrator** that coordinates between specialized services with **zero facade properties**.

### Current Architecture

```
????????????????????????????????????????????????????????????????????????
?                    ManagerService (Orchestrator)                     ?
?  - Coordinates between specialized services                         ?
?  - Manages generation state (Images, Progress, Options)             ?
?  - Workflow management and asset coordination                       ?
?  - NO facade properties (all removed in Phase 9.5)                  ?
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
?  - Mediator     ?   ?  - Video gen    ?   ?  - Videos       ?
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

#### Detailed Execution Log

**Batch 1: Simple Components (12 components)**
| Component | Action | Notes |
|-----------|--------|-------|
| `CivitaiFileButton.razor` | ? Updated | M.State ? State.State |
| `CivitaiModelVersionInfoPanel.razor` | ? Updated | M.State ? State.State |
| `DanbooruSearchesDrawer.razor` | ? Updated | M.Settings ? Settings.Settings |
| `CivitaiImageCard.razor` | ? Updated | Removed unused M injection |
| `Prompts.razor` | ? Updated | M.State ? State.State |
| `PromptsPanel.razor` | ? Updated | Added IStateService, kept M for orchestration |
| `PromptDialog.razor` | ?? Skipped | Only uses M.GetStyles() (orchestration) |
| `WorkflowAssetsPanel.razor` | ? Updated | Added IStateService |
| `WorkflowAssetSelector.razor` | ?? Skipped | Only uses orchestration methods |
| `ConditioningVariationForm.razor` | ? Updated | M.Settings ? Settings.Settings |
| `AssetViewer.razor` | ?? Skipped | Already uses interfaces |
| `SelectionsDialog.razor` | ? Updated | M.SelectedImageIds ? Gallery.SelectedImageIds |

**Batch 2: Medium Components (10 components)**
| Component | Action | Notes |
|-----------|--------|-------|
| `Img2ImgCanvas.razor` | ? Updated | M.State/Canvas ? State.State/Session.Canvas |
| `CivitaiImageDialog.razor` | ? Updated | Added IStateService for parameters |
| `ResourceImageDialog.razor` | ? Updated | Added IStateService for parameters |
| `CivitaiModelsPanel.razor` | ?? Skipped | M for CivitaiModels (orchestration) |
| `Danbooru.razor` | ? Updated | Added IStateService/ISettingsService |
| `InfiniteScrollMasonry.razor` | ? Updated | M.SelectedImageIds ? Gallery.SelectedImageIds |
| `ImageInfoDialog.razor` | ?? Skipped | Already uses IStateService |
| `ImageViewer.razor` | ?? Skipped | Already uses IStateService |
| `GeneratedImageTabs.razor` | ?? Skipped | Already uses interfaces |
| `GeneratedVideoTabs.razor` | ?? Skipped | Already uses interfaces |

**Batch 3: Complex Components (4 components)**
| Component | Action | Notes |
|-----------|--------|-------|
| `WildcardsPanel.razor` | ? Updated | Full refactor - IStateService + ISettingsService |
| `LLMPromptEnhancerForm.razor` | ? Updated | **Removed M completely** |
| `GenerateFormImg2Vid.razor` | ? Updated | **Removed M completely** |
| `GenerateFormTxt2Img.razor` | ?? Skipped | Already uses interfaces |

**Batch 4: Pages (5 pages)**
| Page | Action | Notes |
|------|--------|-------|
| `Index.razor` | ? Updated | M.IsGalleryFiltered ? Gallery.IsGalleryFiltered, **removed M** |
| `Txt2Img.razor` | ?? Skipped | Only uses M for orchestration |
| `Img2Img.razor` | ?? Skipped | Only uses M for orchestration |
| `Img2Vid.razor` | ?? Skipped | Only uses M for orchestration |
| `MainLayout.razor` | ?? Skipped | Only uses M for orchestration |

**Batch 5: Services (4 services)**
| Service | Action | Notes |
|---------|--------|-------|
| `ResourcesService.cs` | ? Updated | Added IStateService, replaced facade usages |
| `RouterService.cs` | ? Updated | Added IBackendService + IStateService |
| `MagickService.cs` | ? Updated | **Replaced M with ISettingsService completely** |
| `CivitaiService.cs` | ?? Skipped | Only uses M.CurrentProgress (orchestration) |

**Task A: Remove Facades from ManagerService**
- ? Removed entire `#region Service Facades` block (27 properties)
- ? Updated all internal references to use `_state`, `_models`, `_gallery`, etc.
- ? Build passing, 150 tests passing

**Task B: WebUI Cleanup**
- ? Removed `ControlNetEnabled` property
- ? Removed `CmdFlags` property
- ? Moved `IsGalleryFiltered` to `IGalleryService`
- ?? `Options.SDModelCheckpoint` kept for path pattern conversion (legacy compat)

**Task C: DI Cleanup in Program.cs**
- ? `RouterService` ? Interface-only (`IRouterService`)
- ? `WorkflowService` ? Interface-only (`IWorkflowService`)
- ? Added comments explaining dual registrations
- ? Build passing, 150 tests passing

#### Phase 9.5 Metrics

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Facade properties | 27 | 0 | -100% |
| Facade usages in components | ~305 | 0 | -100% |
| Components with ManagerService | 31 | 19 | -39% |
| WebUI remnants removed | - | 3 | - |
| Interface-only DI registrations | 9 | 11 | +2 |
| ManagerService lines | 658 | ~450 | -32% |

#### New APIs Added
- `IGalleryService.IsGalleryFiltered` - moved from ManagerService

#### Files Completely Freed from ManagerService
- `LLMPromptEnhancerForm.razor`
- `GenerateFormImg2Vid.razor`
- `Index.razor`
- `MagickService.cs`

---

## Current Phase: Phase 10 - Orchestration Property Relocation

**Objective:** Move remaining orchestration properties from ManagerService to appropriate specialized services.

**Started:** 2025-01-15

### Pre-Phase Analysis

#### Current ManagerService Orchestration Properties

| Property | Type | Current Usages | Target Location |
|----------|------|----------------|-----------------|
| `Images` | `GeneratedImages` | `ImageService` (stores results) | `IImageService` |
| `ImagesInfo` | `GeneratedImagesInfo` | `ImageService` (stores metadata) | `IImageService` |
| `GridImage` | `string?` | `ImageService` (stores grid) | `IImageService` |
| `GeneratedImageEntities` | `ImagesDto` | Pages (display results) | `IImageService` |
| `GeneratedUpscaleImage` | `UpscaledImageDto` | Unused currently | `IImageService` |
| `Progress` | `InferenceProgress` | `ImageService` | `IImageService` or remove |
| ~~`Styles`~~ | ~~`List<PromptStyle>`~~ | ~~`PromptFields` loads directly~~ | ~~Removed~~ ? |
| ~~`ButtonTags`~~ | ~~`PromptButton`~~ | ~~`PromptFields` loads directly~~ | ~~Removed~~ ? |
| `CivitaiModels/Images/Creators` | DTOs | `CivitaiModelsPanel` | Defer to Phase 11 |
| `ResourceTypeDirectories` | `Dictionary<string,string>` | Config | Move to `IConfiguration` |

### Task A: Move Generation Results to IImageService ?

**Scope:** Move `Images`, `ImagesInfo`, `GridImage`, `GeneratedImageEntities`, `GeneratedUpscaleImage` to `IImageService`

**Why:** These properties represent generation output and belong with the generation logic in `ImageService`.

**Changes Required:**

1. **IImageService interface** - Add properties:
   ```csharp
   GeneratedImages Images { get; }
   GeneratedImagesInfo ImagesInfo { get; }
   string? GridImage { get; }
   ImagesDto GeneratedImageEntities { get; set; }
   UpscaledImageDto GeneratedUpscaleImage { get; set; }
   ```

2. **ImageService** - Add backing fields and properties, update internal `_m.Images` to `Images`

3. **ManagerService** - Remove properties, update `SerializeInfo()` to call `IImageService`

4. **Components using `M.GeneratedImageEntities`** - Update to use `IImageService`

**Files Affected:**
- `Services/IImageService.cs`
- `Services/ImageService.cs`
- `Services/ManagerService.cs`
- `Pages/Txt2Img.razor`
- `Pages/Img2Img.razor`
- `Pages/Img2Vid.razor`

### Task B: Remove Duplicate Styles Property ?
**Completed:** 2025-01-15

**Changes Made:**
- Removed `Styles` property from ManagerService
- Removed `GetStyles()` method from ManagerService
- Updated `PromptsPanel.razor` to publish `StylesChangedEventArgs` instead of calling `M.GetStyles()`
- Updated `PromptDialog.razor` to publish `StylesChangedEventArgs` instead of calling `M.GetStyles()`
- Updated `WildcardsPanel.razor` to publish `StylesChangedEventArgs` instead of calling `M.GetStyles()`
- Updated `PromptFields.razor` to subscribe to `StylesChangedEventArgs`

**Files Updated:**
- `Services/ManagerService.cs` - Removed property and method
- `Components/Prompts/PromptsPanel.razor` - Removed M injection, uses Events
- `Components/Prompts/PromptDialog.razor` - Removed M injection, uses Events
- `Components/Prompts/WildcardsPanel.razor` - Removed M injection, uses Events
- `Components/Shared/Generation/PromptFields.razor` - Added StylesChangedEventArgs subscription

### Task C: Remove Duplicate ButtonTags Property ?
**Completed:** 2025-01-15

**Changes Made:**
- Removed `ButtonTags` property from ManagerService
- Removed `GetButtonTags()` method from ManagerService
- Removed `System.Text.Json` using directive (no longer needed)

**Note:** `PromptFields.razor` already loads button tags directly from `IOService.GetJsonAsString()`, so no component changes were needed.

### Task D: Move Progress Property ?

**Scope:** `M.Progress` (InferenceProgress) is used during generation

**Analysis Needed:**
- Is this still used? `ImageService.StartProgressChecker()` sets `_m.Progress`
- WebSocket progress may have replaced this

**Decision:** Evaluate during Task A

### Execution Order

1. ~~Task C: Remove ButtonTags (trivial)~~ ?
2. ~~Task B: Remove Styles~~ ?
3. Task A: Move generation results (highest impact, unlocks other tasks) ?
4. Task D: Evaluate Progress during Task A ?

### Current Metrics

| Metric | Before Phase 10 | After Tasks B+C | Change |
|--------|-----------------|-----------------|--------|
| Orchestration properties | 14 | 12 | -2 |
| Methods removed | 0 | 2 | +2 |
| Components freed from M | 0 | 3 | +3 |
| Build | ? | ? | - |
| Tests | 150 | 150 | - |

### Acceptance Criteria

- [x] ButtonTags property removed
- [x] Styles property and GetStyles() removed
- [ ] `IImageService` owns all generation result properties
- [ ] ManagerService reduced by ~50 lines
- [ ] Build passing
- [ ] 150+ tests passing

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

| Metric | Phase 1 Start | Phase 9.5 End | Phase 10 Target |
|--------|---------------|---------------|-----------------|
| ManagerService lines | ~1600 | ~450 | <350 |
| Facade properties | 27 | 0 | 0 ? |
| Services extracted | 0 | 11 | 11 |
| Services with interfaces | 0 | 11 | 11 |
| Unit tests | 0 | 150 | 150+ |
| Duplicate data loading | - | 2 | 0 |

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

// CivitaiService uses concrete ImageService
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<ImageService>());
```

---

## Commit History Reference

| Checkpoint | Description | Tests |
|------------|-------------|-------|
| Phase 8.5 | Legacy events removed | 138 |
| Phase 9 | Service interfaces created | 150 |
| Phase 9.5 Batch 1 | Simple components updated | 150 |
| Phase 9.5 Batch 2 | Medium components updated | 150 |
| Phase 9.5 Batch 3 | Complex components updated | 150 |
| Phase 9.5 Batch 4 | Pages updated | 150 |
| Phase 9.5 Batch 5 | Services updated | 150 |
| Phase 9.5 Task A | Facades removed | 150 |
| Phase 9.5 Task C | DI cleanup | 150 |

---

**End of Document**
