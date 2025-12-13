# Phase 8.5: ManagerService Orchestrator Refactor - Summary

## ?? Quick Reference

**Status:** ?? **IN PROGRESS** - Started 2025-01-14  
**Duration:** 2-3 days  
**Priority:** ?? **CRITICAL** - Blocks Phase 9  
**Current ManagerService:** ~1200 lines  
**Target ManagerService:** < 300 lines

---

## ?? Objective

Convert ManagerService from a "God Object" with facades and legacy events to a **lightweight orchestrator** that coordinates cross-service operations.

---

## ? Current Problems

After completing Phase 8 (Component Migration - 80% complete), ManagerService still has:

1. **25+ Action events** - Legacy event system (should be EventService)
2. **Multiple facade properties** - Delegating to State, Settings, Models, Gallery, Session
3. **Orchestration methods** - Some extracted, some not
4. **Component dependencies** - Some migrated components still use `M` for orchestration

**Why This Matters:**
- Blocks Phase 9 (Service Interface Extraction & Testing)
- Services depend on ManagerService facades (circular dependencies)
- Cannot properly interface services with ManagerService dependencies
- Testing is difficult with mixed event systems

---

## ? Success Criteria

- [ ] ManagerService < 300 lines (currently ~1200)
- [ ] All Action events removed (0 remaining)
- [ ] All facade properties removed
- [ ] Orchestration methods extracted to appropriate services
- [ ] Components updated to use specialized services directly
- [ ] All 166 tests still passing
- [ ] Build passes without errors
- [ ] Application fully functional

---

## ?? 3-Day Implementation Plan

### **Day 1: Event System Migration** (4-6 hours)

**Goals:**
- Remove all Action events
- Complete migration to EventService
- Create missing EventArgs classes

**Tasks:**
1. Audit Action event usage in codebase
2. Create missing EventArgs:
   - OptionsChangedEventArgs
   - WorkflowChangedEventArgs
   - SamplersSchedulersChangedEventArgs
3. Update orchestration methods to use EventService.Publish()
4. Remove all Action event declarations

**Deliverables:**
- [ ] 0 Action events remaining
- [ ] All methods use EventService
- [ ] Build passes

---

### **Day 2: Orchestration Method Extraction** (6-8 hours)

**Goals:**
- Move orchestration methods to appropriate services
- Remove model management facades
- Update components

**Tasks:**

**Parameter Loading (Extract to StateService):**
- Move `LoadImageInfoParameters()` ? `StateService.LoadParametersFromImage()`
- Move `SetGenerationParameter()` ? `StateService.SetParameterFromImage()`
- Keep `ParseAndCleanCopiedPrompt()` as utility or move to PromptService

**Workflow Management (Extract to WorkflowService/StateService):**
- Move `SetWorkflowBase()` ? `StateService.SetWorkflowBase()`
- Move `ResetWorkflowAssetsToDefaults()` ? StateService (private)
- Move `RefreshWorkflowsFromDisk()` ? `WorkflowService.RefreshWorkflows()`
- Move `MigrateLegacyModelSettings()` ? `StateService.MigrateLegacySettings()`

**Model Management (Remove Facades):**
- Remove `GetWorkflowModels()` wrapper
- Remove `SetCurrentModel()` wrapper
- Update components to call ModelService directly

**Deliverables:**
- [ ] Methods moved to appropriate services
- [ ] Components updated
- [ ] Build passes, tests pass

---

### **Day 3: Facade Removal & Final Cleanup** (6-8 hours)

**Goals:**
- Remove all facade properties
- Update components with orchestration dependencies
- Finalize ManagerService as lightweight orchestrator

**Tasks:**

**Remove Facade Properties:**
```csharp
// REMOVE these properties from ManagerService:
- AppState State
- Txt2ImgParameters ParametersTxt2Img
- Img2ImgParameters ParametersImg2Img
- UpscaleParameters ParametersUpscale
- Img2VidParameters ParametersImg2Vid
- AppSettings Settings
- List<SDModel> CheckpointModels / DiffusionModels
- List<string> SDVAEs / ClipModels / ClipVisionModels
- List<Sampler> Samplers / Schedulers / Upscalers
- List<Folder> Folders / Projects
- List<int> SelectedImageIds
- string CanvasImageData / CanvasMaskData / etc.
- bool IsComfyUIUp
```

**Update Components:**
- ImageInfoDialog ? Use `State.LoadParametersFromImage()`
- ImageViewer ? Use `State.SetParameterFromImage()`
- AssetViewer ? Use `State.SetParameterFromImage()`
- GeneratedImageTabs ? Handle Progress appropriately
- Generation Pages ? Use extracted workflow methods

**Final Cleanup:**
- Remove unused `Invoke*()` methods
- Document remaining orchestration methods
- Add XML comments

**Deliverables:**
- [ ] ManagerService < 300 lines
- [ ] All facade properties removed
- [ ] All components updated
- [ ] All 166 tests passing
- [ ] Application fully functional

---

## ?? Components Requiring Updates

These components currently use `M` for orchestration and need updates:

| Component | Current M Usage | Target Service | Day |
|-----------|----------------|----------------|-----|
| ImageInfoDialog | LoadImageInfoParameters() | State.LoadParametersFromImage() | Day 2-3 |
| ImageViewer | SetGenerationParameter() | State.SetParameterFromImage() | Day 2-3 |
| AssetViewer | SetGenerationParameter() | State.SetParameterFromImage() | Day 2-3 |
| GeneratedImageTabs | Progress, GeneratedImageEntities | ProgressService / ImageService | Day 3 |
| Txt2Img.razor | Workflow methods, GeneratedImageEntities | Extracted methods | Day 3 |
| Img2Img.razor | Workflow methods, GeneratedImageEntities | Extracted methods | Day 3 |
| Img2Vid.razor | Workflow methods | Extracted methods | Day 3 |
| MainLayout.razor | SetWorkflowBase(), LoadBackendDependentResources() | State / Backend | Day 3 |

---

## ?? Expected ManagerService Final State

**After Phase 8.5:**

```csharp
public class ManagerService
{
    // Injected Services
    private readonly IDatabaseService _db;
    private readonly IIOService _io;
    private readonly ProgressService _progress;
    private readonly IConfiguration _configuration;
    private readonly ComfyUIService _capi;
    private readonly WorkflowService _workflow;
    private readonly IStateService _state;
    private readonly IEventService _events;
    private readonly ISettingsService _settings;
    private readonly IBackendService _backend;
    private readonly IModelService _models;
    private readonly IGalleryService _gallery;
    private readonly ISessionService _session;

    // Orchestration-specific properties (don't belong to any single service)
    public Options Options { get; set; }
    public GeneratedImages Images { get; set; }
    public GeneratedImagesInfo ImagesInfo { get; set; }
    public ImagesDto GeneratedImageEntities { get; set; }
    public string? GridImage { get; set; }
    public InferenceProgress Progress { get; set; }
    
    // Data that doesn't fit elsewhere
    public List<PromptStyle> Styles { get; set; }
    public PromptButton ButtonTags { get; set; }
    public CmdFlags CmdFlags { get; set; }
    public CivitaiModelsDto CivitaiModels { get; set; }
    public CivitaiImagesDto CivitaiImages { get; set; }
    public CivitaiCreatorsDto CivitaiCreators { get; set; }
    public string ComfyWSClientId { get; set; }
    public Dictionary<string, string> ResourceTypeDirectories { get; set; }

    // Constructor
    public ManagerService(...) { }

    // Orchestration Methods Only
    public async Task LoadBackendDependentResources() 
    {
        // Coordinates Backend + Models
        await _backend.LoadBackendDependentResources();
        await _models.GetADetailerModels();
        _events.Publish(new SamplersSchedulersChangedEventArgs());
    }

    // Utility methods that don't fit in any single service
    public string ConvertPathPattern(string pattern, ModeType mode) { }
    public string GetCurrentSaveFolder(Outdir? outdir) { }
    public async Task GetResourceTypeDirectories() { }
    
    // Legacy compatibility (if needed temporarily)
    public void GetButtonTags() { }
}
```

**Lines:** ~200-250 (from ~1200)  
**Responsibilities:** Cross-service orchestration only  
**No Longer Contains:** Facades, Action events, single-service methods

---

## ?? Testing Strategy

After each day:
1. **Build:** `dotnet build` - Should pass without errors
2. **Tests:** `dotnet test` - All 166 tests should pass
3. **Application Test:**
   - Txt2Img generation
   - Img2Img generation
   - Img2Vid generation
   - Gallery navigation
   - Model switching
   - Settings persistence
   - State persistence

---

## ?? Phase 8.5 Completion Checklist

- [ ] **Day 1 Complete**
  - [ ] All Action events removed
  - [ ] EventService migration complete
  - [ ] Missing EventArgs created
  - [ ] Build passes

- [ ] **Day 2 Complete**
  - [ ] Parameter methods moved to StateService
  - [ ] Workflow methods moved to WorkflowService/StateService
  - [ ] Model facades removed
  - [ ] Components updated
  - [ ] Tests pass

- [ ] **Day 3 Complete**
  - [ ] All facade properties removed
  - [ ] Components with orchestration updated
  - [ ] ManagerService < 300 lines
  - [ ] All 166 tests passing
  - [ ] Application fully functional

- [ ] **Phase 8.5 Success Criteria Met**
  - [ ] ManagerService is lightweight orchestrator
  - [ ] No facade properties
  - [ ] No Action events
  - [ ] All components use specialized services
  - [ ] Ready for Phase 9

---

## ?? Related Documentation

- [Manager Service Refactor Plan](./manager-service-refactor.md) - Main refactor plan with Phase 8.5 details
- [Component Migration Log](./COMPONENT_MIGRATION_LOG.md) - Phase 8 completion status
- [Service Interface Extraction Plan](./SERVICE_INTERFACE_EXTRACTION_PLAN.md) - Phase 9 (paused until Phase 8.5 complete)

---

**Last Updated:** 2025-01-14  
**Status:** ?? In Progress  
**Next Action:** Begin Day 1 - Event System Migration  
**Estimated Completion:** 2025-01-17
