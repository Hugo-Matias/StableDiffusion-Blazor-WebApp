# Dynamic Generation Page Refactor - Implementation Plan

## Status
**Current Phase:** Execution (Phase 6 - State &amp; Persistence - Nearly Complete)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** &rarr; 2. **Test and Debug Features** &rarr; 3. **Discuss Improvements** &rarr; 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits

### Progress Tracking Symbols
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)
- **1**: Trivial (simple property change, config update)
- **2**: Simple (straightforward refactor, single file change)
- **3**: Moderate (multi-file change, simple logic)
- **5**: Medium (service extraction, interface creation)
- **8**: Complex (component migration, breaking changes)
- **13**: Very complex (architecture change, wide impact)
- **21+**: Epic (should be split into smaller phases)

### Key Rules
- **Each step = commitable checkpoint** for safe implementation
- **No time/date references** - use complexity points only
- **Detours are acceptable** after discussion - append to main plan
- **Phase documents must contain enough context** to resume in new sessions
- **Minimal, focused changes** - avoid over-engineering
- **User permission required** before moving to next phase
- **All events must use IEventService** - no direct event subscriptions

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The current generation architecture suffers from **tight coupling** between:
1. **Parameter Models** (`Txt2ImgParameters`, `Img2ImgParameters`, etc.) - Ever-growing classes with mode-specific properties
2. **DTOs** (`Txt2ImgComfyUI`, `Img2ImgComfyUI`, etc.) - Duplicate structures for API communication
3. **UI Components** (`GenerateFormTxt2Img.razor`, etc.) - Mode-specific forms with hardcoded fields
4. **Workflow Templates** (`.sbn` files) - Require matching property names across all layers

### Current Pain Points

| Issue | Impact | Location |
|-------|--------|----------|
| Adding a new node requires 8+ file changes | High friction for new features | NODE_INTEGRATION_GUIDE.md |
| Mode-specific parameter classes grow indefinitely | Hard to maintain | `Models/*Parameters.cs` |
| Duplicate initialization in multiple places | Inconsistent defaults | `StateService.cs`, `ImageService.cs` |
| Manual DTO mapping | Error-prone, boilerplate | `ParameterMapper.cs` |
| Hardcoded UI forms per mode | No dynamic rendering | `Components/*/GenerateForm*.razor` |
| No support for node chaining | Limited workflow flexibility | N/A |
| AppSettings bloated with component constraints | Hard to maintain | `appsettings.json` |

### Goals

1. **Single unified generation page** that renders based on workflow definition
2. **Dynamic parameter storage** using dictionaries instead of typed properties
3. **Workflow-driven component rendering** where templates define what UI to show
4. **Simplified node integration** reducing 8+ file changes to 2-3 files
5. **Support for node chaining** (multiple samplers, detailers, etc.)
6. **Simplified AppSettings** by moving constraints to fragment schemas
7. **Remove all legacy parameter classes** in favor of `GenerationParameters`

---

## Proposed Solution

### Architecture Overview

```
???????????????????????????????????????????????????????????????????
?                     Workflow Template (.sbn)                     ?
?  ??????????? ??????????? ????????????????????????????????????? ?
?  ?  Assets  ? ? Sources  ? ?           Pipeline[]             ? ?
?  ? (models) ? ?(img/vid) ? ?  id, fragment, parameters(def)  ? ?
?  ??????????? ??????????? ????????????????????????????????????? ?
???????????????????????????????????????????????????????????????????
                              ?
         ???????????????????????????????????????????
         ?                    ?                    ?
??????????????????????  ??????????????????????  ??????????????????????
?  Fragment #meta ?  ?  Fragment #meta ?  ?  Fragment #meta ?
?  ????????????????  ?  ?  ????????????????  ?  ?  ????????????????  ?
?  ? outputs   ?  ?  ?  ? outputs   ?  ?  ?  ? outputs   ?  ?
?  ? conditions?  ?  ?  ? conditions?  ?  ?  ? (no ui)   ?  ?
?  ? ui:       ?  ?  ?  ? ui:       ?  ?  ?  ????????????????  ?
?  ?  component?  ?  ?  ?  fields[] ?  ?  ?  (utility node) ?
?  ????????????????  ?  ??????????????????????  ??????????????????????
? (designed comp) ?  (dynamic fields)   ?
??????????????????????  ??????????????????????
         ?                    ?
         ?                    ?
???????????????????????????????????????????????????????????????????
?                    GenerationParameters                          ?
?  ????????????????? ????????????????? ????????????????? ???????? ?
?  ?  Fragments   ? ?    Assets    ? ?   Sources    ? ?  Loras  ? ?
?  ? Dict<id,val> ? ? Dict<id,val> ? ? Dict<id,val> ? ?  List   ? ?
?  ????????????????? ????????????????? ????????????????? ???????? ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?                    ImageService / RouterService                  ?
?  GenerationParameters ? ComfyUI Workflow API ? Generated Output ?
???????????????????????????????????????????????????????????????????
```

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| Remove "Core" parameters - everything is a Fragment | Cleaner model, prompts are just another fragment |
| Use `"Sources"` for input images/videos | Distinguishes from node `inputs` (internal wiring) |
| Hybrid component approach | Designed components for mature nodes, dynamic fields for new/experimental |
| Fragment `#meta.ui` defines constraints only | Pipeline defines default values per workflow |
| Fragment ID links Pipeline to GenerationParameters | Unique ID enables multiple instances of same fragment |
| No legacy support | Old pages removed, clean migration |
| **RouterService accepts GenerationParameters directly** | Eliminates legacy parameter conversion layer |

### Conventions

- **Fragment ID** is the unique key linking Pipeline ? UI ? GenerationParameters
- **Designed components** are referenced by name in `ui.component`
- **Dynamic fields** are used when `ui.component` is null and `ui.fields[]` is present
- **Utility fragments** have no `ui` block and render no form
- **Sources** appear first in the parameters panel, below prompts
- **Chainable fragments** can have multiple instances (e.g., `main_sampler`, `refiner_sampler`)
- **All events use IEventService** - no direct event subscriptions on services

---

## Fragment-to-Component Linking

### The Link: Fragment ID

Each pipeline step has a unique `id` that serves as the key:

```json
{
  "Pipeline": [
    {
      "id": "main_sampler",
      "fragment": "sampler.sbn",
      "parameters": { "steps": 20, "cfg": 7, "seed": -1 }
    },
    {
      "id": "refiner_sampler",
      "fragment": "sampler.sbn",
      "parameters": { "steps": 10, "cfg": 5, "seed": -1 }
    }
  ]
}
```

The fragment's `#meta.ui` defines the component, the pipeline provides defaults.

### Linking Rules

| Scenario | Solution |
|----------|----------|
| 1 fragment ? 1 component | Standard case, `ui.component` names the component |
| 1 fragment ? 0 components | Utility fragment, no `ui` block |
| 1 fragment ? dynamic fields | `ui.component` is null, `ui.fields[]` provides schema |
| Multiple instances of same fragment | Different `id` in Pipeline, same fragment file |
| Complex node needing multiple UI sections | Split into multiple fragments |
| Component needing data from multiple fragments | **Not allowed** - violates architecture |

### Why No Cross-Fragment Components?

Each component reads from exactly one fragment instance. This ensures:
- Clear data ownership
- Predictable state updates
- Simpler debugging
- Clean separation of concerns

If nodes are logically coupled (e.g., sampler + upscale in HiRes), create a **composite fragment**.

---

## Implementation Phases

### Phase 1: Schema Definition &amp; Documentation
**Objective:** Define the UI schema format and document conventions
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Step 1.1 - Design complete UI schema JSON structure
- [x] Step 1.2 - Create `FRAGMENT_SCHEMA_GUIDE.md` documentation
- [x] Step 1.3 - Define field types and their properties
- [x] Step 1.4 - Define component registry conventions
- [x] Step 1.5 - Review with example fragments (sampler, prompts, upscale)

---

### Phase 2: Core Infrastructure
**Objective:** Create the foundational types and services for dynamic parameters
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps
- [x] Step 2.1 - Create `GenerationParameters` model
- [x] Step 2.2 - Create `FragmentParameters` model
- [x] Step 2.3 - Create `FragmentSchema` model (parsed from #meta.ui)
- [x] Step 2.4 - Create `SourceAsset` model for input images/videos
- [x] Step 2.5 - Create `ComponentRegistry` service
- [x] Step 2.6 - Extend `WorkflowService` to parse UI schema
- [x] Step 2.7 - Create `GenerationParameterService` for parameter CRUD

---

### Phase 3: Fragment Updates
**Objective:** Update key fragments with UI schema
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps
- [x] Step 3.1 - Update `prompts.sbn` with UI schema
- [x] Step 3.2 - Update `sampler.sbn` with UI schema
- [x] Step 3.3 - Update `upscale.sbn` with UI schema
- [x] Step 3.4 - Update `detailer-core.sbn` with UI schema
- [x] Step 3.5 - Update loader fragments (flux, sd, etc.) - utility, no UI
- [x] Step 3.6 - Validate all schemas parse correctly

---

### Phase 4: Dynamic Form Components
**Objective:** Create reusable form components that render from schema
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps
- [x] Step 4.1 - Create `DynamicField.razor` (single field from schema)
- [x] Step 4.2 - Create `DynamicFragmentForm.razor` (fragment form with dynamic fields)
- [x] Step 4.3 - Create `FragmentFormContainer.razor` (enable/disable, chaining UI)
- [x] Step 4.4 - Create `SourcesPanel.razor` (tabbed image/video inputs)
- [x] Step 4.5 - Create `FragmentFormBase.cs` (base class for designed components)
- [x] Step 4.6 - Register components in ComponentRegistry

---

### Phase 5: Unified Generation Page
**Objective:** Create single generation page that works for all modes
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps
- [x] Step 5.1 - Create `Generate.razor` page layout
- [x] Step 5.2 - Implement workflow selection and switching
- [x] Step 5.3 - Implement prompt fields with fragment binding
- [x] Step 5.4 - Implement SourcesPanel integration
- [x] Step 5.5 - Implement fragment form rendering from pipeline
- [x] Step 5.6 - Implement asset panel integration
- [x] Step 5.7 - Implement generate button and progress

---

### Phase 6: State &amp; Persistence
**Objective:** Update state management to use new parameter structure
**Complexity:** 8 points
**Status:** [~] Nearly Complete (pending manual testing)

#### Steps
- [x] Step 6.1 - Update `StateService` to handle `GenerationParameters`
- [x] Step 6.2 - Update `ImageService` to use new parameters
  - Added `GenerateImagesAsync(GenerationParameters, Workflow)`
  - Added `GenerateVideoAsync(GenerationParameters, Workflow)`
  - Migrated `OnChange` event to `IEventService` pattern (`ImagesGeneratedEventArgs`)
- [x] Step 6.3 - Router/ComfyUI factories (deferred to Phase 9 - internal conversion sufficient)
- [x] Step 6.4 - Implement parameter parsing (wildcards, seeds) - integrated in 6.2
- [~] Step 6.5 - Test state persistence and recovery (pending manual testing)

#### Success Criteria
- [x] State saves and loads correctly
- [x] Wildcards and seed randomization work
- [ ] No data loss between sessions (manual verification needed)

---

### Phase 7: Workflow Template Updates
**Objective:** Update all workflow templates with Pipeline IDs and Sources
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 7.1 - Update Flux workflow templates
- [ ] Step 7.2 - Update SD workflow templates
- [ ] Step 7.3 - Update Img2Img workflow templates
- [ ] Step 7.4 - Update Img2Vid workflow templates
- [ ] Step 7.5 - Validate all workflows render correctly

#### Success Criteria
- All workflows have unique Pipeline IDs
- Sources defined where needed
- Generation works for all workflow types

---

### Phase 8: Node Chaining Support
**Objective:** Enable multiple instances of same fragment type
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 8.1 - Implement fragment instance management in service
- [ ] Step 8.2 - Create UI for adding/removing chainable fragments
- [ ] Step 8.3 - Implement instance reordering
- [ ] Step 8.4 - Test with multiple samplers
- [ ] Step 8.5 - Test with multiple detailers

#### Success Criteria
- Can add multiple KSamplers to a workflow
- Each instance has isolated parameters
- Order affects pipeline execution

---

### Phase 9: Legacy Deprecation &amp; RouterService Refactor
**Objective:** Remove all legacy parameter classes and DTOs; RouterService accepts GenerationParameters directly
**Complexity:** 13 points (increased from 5)
**Status:** [ ] Not Started

#### Overview
This phase eliminates the temporary conversion layer added in Phase 6 and establishes `GenerationParameters` as the sole parameter model throughout the system.

#### Steps

##### Step 9.1: Update RouterService for GenerationParameters
- [ ] Add `IRouterService.PostGenerationAsync(GenerationParameters, Workflow)` method
- [ ] Build ComfyUI workflow payload directly from `GenerationParameters.Fragments`
- [ ] Remove mode-specific routing (`PostTxt2Img`, `PostImg2Img`, `PostImg2Vid`)

##### Step 9.2: Update ComfyUI DTOs
- [ ] Create `ComfyUIWorkflowBuilder.FromGenerationParameters()` factory
- [ ] Remove `Txt2ImgComfyUI.cs`
- [ ] Remove `Img2ImgComfyUI.cs`
- [ ] Remove `Img2VidComfyUI.cs`

##### Step 9.3: Remove Legacy Parameter Classes
- [ ] Remove `SharedParameters.cs`
- [ ] Remove `Txt2ImgParameters.cs`
- [ ] Remove `Img2ImgParameters.cs`
- [ ] Remove `Img2VidParameters.cs`
- [ ] Remove `UpscaleParameters.cs`
- [ ] Remove `DetailerParameters.cs` (if separate)
- [ ] Remove `ParameterMapper.cs`

##### Step 9.4: Update ImageService
- [ ] Remove `BuildLegacyParametersFromGenerationParams()` method
- [ ] Remove `BuildTxt2ImgFromGenerationParams()` method
- [ ] Remove `BuildImg2ImgFromGenerationParams()` method
- [ ] Remove `BuildImg2VidFromGenerationParams()` method
- [ ] Remove legacy `GetImages(ModeType)` method
- [ ] Remove legacy `GetVideo()` method
- [ ] Update `SaveImages()` to read from `GenerationParameters` directly

##### Step 9.5: Update StateService
- [ ] Remove `ParametersTxt2Img` property
- [ ] Remove `ParametersImg2Img` property
- [ ] Remove `ParametersImg2Vid` property
- [ ] Remove legacy parameter initialization
- [ ] Update `IStateService` interface

##### Step 9.6: Remove Legacy UI Components
- [ ] Remove `Txt2Img.razor` page
- [ ] Remove `Img2Img.razor` page
- [ ] Remove `Img2Vid.razor` page
- [ ] Remove `GenerateFormTxt2Img.razor`
- [ ] Remove `GenerateFormImg2Img.razor`
- [ ] Remove `GenerateFormImg2Vid.razor`
- [ ] Update navigation to only use `/generate` route

##### Step 9.7: Simplify AppSettings
- [ ] Remove component constraints from `AppSettings`
- [ ] Move all min/max/step values to fragment schemas
- [ ] Update `appsettings.json`
- [ ] Update settings documentation

##### Step 9.8: Update Parser.cs
- [ ] Remove `ParseParameters()` method (sync version)
- [ ] Update `ParseParametersAsync()` to work with `GenerationParameters` directly
- [ ] Or create new `ParseGenerationParametersAsync()` method

#### Success Criteria
- No references to `Txt2ImgParameters`, `Img2ImgParameters`, `Img2VidParameters` in codebase
- No references to `Txt2ImgComfyUI`, `Img2ImgComfyUI`, `Img2VidComfyUI` in codebase
- `RouterService` has single generation method accepting `GenerationParameters`
- `ImageService` has no legacy methods
- `StateService` only manages `GenerationParameters`
- AppSettings is significantly smaller
- All tests pass with new architecture

#### Files to Remove
```
Models/SharedParameters.cs
Models/Txt2ImgParameters.cs
Models/Img2ImgParameters.cs
Models/Img2VidParameters.cs
Models/UpscaleParameters.cs
Data/Dtos/ComfyUI/Txt2ImgComfyUI.cs
Data/Dtos/ComfyUI/Img2ImgComfyUI.cs
Data/Dtos/ComfyUI/Img2VidComfyUI.cs
Services/ParameterMapper.cs (if exists)
Pages/Txt2Img.razor
Pages/Img2Img.razor
Pages/Img2Vid.razor
Components/Shared/Generation/GenerateFormTxt2Img.razor
Components/Shared/Generation/GenerateFormImg2Img.razor
Components/Shared/Generation/GenerateFormImg2Vid.razor
```

---

### Phase 10: Documentation
**Objective:** Document the new architecture
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 10.1 - Create architecture overview document
- [ ] Step 10.2 - Update `NODE_INTEGRATION_GUIDE.md` with new workflow (2-3 files instead of 8+)
- [ ] Step 10.3 - Update all affected guides
- [ ] Step 10.4 - Create migration notes for users

#### Success Criteria
- Documentation is complete and accurate
- New developers can understand the system
- Node integration guide shows simplified process

---

## Stress Points &amp; Risks

| Risk | Mitigation | Complexity |
|------|------------|------------|
| Schema validation errors at runtime | Strict validation during parse, clear error messages | 3 |
| Complex workflows break during migration | Test each workflow type during Phase 7 | 5 |
| Parameter parsing (wildcards, seeds) breaks | Keep Parser.cs logic, integrate carefully in Phase 6 | 3 |
| Component binding performance | Lazy loading, minimize re-renders | 3 |
| State serialization format changes | Version the state format, handle gracefully | 3 |
| Legacy parameter removal breaks existing features | Thorough testing in Phase 9, keep feature parity | 8 |

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created |
| Planning | Removed Core in favor of Fragments-only model |
| Planning | Added Sources for input images/videos |
| Planning | Implemented hybrid component approach |
| Planning | Reordered phases: Schema first, then Infrastructure |
| Planning | Removed legacy support - clean migration only |
| Planning | Clarified fragment-to-component linking rules |
| Planning | Added AppSettings simplification to goals |
| Phase 1 | Created FRAGMENT_SCHEMA_GUIDE.md |
| Phase 1 | Defined 12 field types, 8 initial components |
| Phase 2 | Created GenerationParameters, FragmentParameters, SourceAsset models |
| Phase 2 | Created FragmentSchema, ParameterConstraints, FieldSchema models |
| Phase 2 | Created ComponentRegistry and GenerationParameterService |
| Phase 2 | Extended WorkflowService with schema parsing |
| Phase 2 | Created GenerationParametersChangedEventArgs with IEventService integration |
| Phase 3 | Updated 6 fragments with UI schema (prompts, sampler, upscale, detailer, etc.) |
| Phase 3 | Added 10 unit tests for schema parsing - all passing |
| Phase 3 | Aligned fragment constraints with AppSettings values |
| Phase 4 | Created DynamicField.razor - 9 field types supported |
| Phase 4 | Created DynamicFragmentForm.razor - grid layout with groups |
| Phase 4 | Created FragmentFormContainer.razor - collapsible, chainable |
| Phase 4 | Created SourcesPanel.razor + SourceItem.razor - reuses ImageInput |
| Phase 4 | Created FragmentFormBase.cs - base class for designed components |
| Phase 4 | Created 4 new stub components (PromptsFormNew, SamplerFormNew, etc.) |
| Phase 4 | Updated ComponentRegistry with all registrations |
| Phase 4 | Fixed MudFileUpload API (ButtonTemplate for MudBlazor 6.1.8) |
| Phase 5 | Created Generate.razor unified page |
| Phase 5 | Implemented workflow selector with mode grouping |
| Phase 5 | Implemented PromptsFormNew with FragmentFormBase binding |
| Phase 5 | Added source parsing to WorkflowService |
| Phase 5 | Added InitializeSourcesFromWorkflow to GenerationParameterService |
| Phase 5 | Updated GenerateButton for parameterless callbacks |
| Phase 5 | Implemented temporary parameter mapping for backward compatibility |
| Phase 6 | Added GenerationParameters to StateService for persistence |
| Phase 6 | Added GenerateImagesAsync/GenerateVideoAsync to ImageService |
| Phase 6 | Migrated ImageService.OnChange to IEventService pattern |
| Phase 6 | Created ImagesGeneratedEventArgs |
| Phase 6 | Updated GeneratedImageTabs to use EventService subscription |
| Phase 6 | Integrated wildcard/seed parsing in new flow |
| Phase 6 | Expanded Phase 9 scope to include full legacy deprecation |

---

## Code Examples

### GenerationParameters Model

```csharp
public class GenerationParameters
{
    /// <summary>
    /// All parameters organized by fragment instance ID.
    /// Keys match Pipeline[].id in the workflow template.
    /// </summary>
    public Dictionary<string, FragmentParameters> Fragments { get; set; } = new();
    
    /// <summary>
    /// Workflow assets (models, VAEs, CLIPs).
    /// Keys match workflow Asset.Parameter names.
    /// Managed by existing AssetResolverService.
    /// </summary>
    public Dictionary<string, string> Assets { get; set; } = new();
    
    /// <summary>
    /// Input images/videos for the workflow.
    /// Keys match workflow Sources[].id.
    /// </summary>
    public Dictionary<string, SourceAsset> Sources { get; set; } = new();
    
    /// <summary>
    /// LoRAs active for this generation.
    /// </summary>
    public List<Lora> Loras { get; set; } = new();
    
    /// <summary>
    /// Current workflow reference.
    /// </summary>
    public Guid? WorkflowId { get; set; }
}

public class FragmentParameters
{
    /// <summary>
    /// The fragment file this instance uses (e.g., "sampler.sbn").
    /// </summary>
    public string FragmentFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this fragment is active. Inactive fragments are skipped.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Order in the pipeline (for chainable fragments).
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Parameter values. Keys match fragment parameter names.
    /// </summary>
    public Dictionary<string, object?> Values { get; set; } = new();
}

public class SourceAsset
{
    /// <summary>
    /// Display label for the input.
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of source: "image" or "video".
    /// </summary>
    public string Type { get; set; } = "image";
    
    /// <summary>
    /// Base64 encoded data or file path.
    /// </summary>
    public string? Data { get; set; }
    
    /// <summary>
    /// Original filename if uploaded.
    /// </summary>
    public string? Filename { get; set; }
}
```

### New RouterService Interface (Phase 9)

```csharp
public interface IRouterService
{
    /// <summary>
    /// Executes a generation workflow using the unified GenerationParameters model.
    /// Replaces PostTxt2Img, PostImg2Img, PostImg2Vid.
    /// </summary>
    Task<GeneratedImages> PostGenerationAsync(GenerationParameters parameters, Workflow workflow);
    
    /// <summary>
    /// Executes a video generation workflow.
    /// </summary>
    Task<GeneratedVideos> PostVideoGenerationAsync(GenerationParameters parameters, Workflow workflow);
    
    // Legacy methods marked obsolete
    [Obsolete("Use PostGenerationAsync instead")]
    Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters);
    
    [Obsolete("Use PostGenerationAsync instead")]
    Task<GeneratedImages> PostImg2Img(Img2ImgParameters parameters);
    
    [Obsolete("Use PostVideoGenerationAsync instead")]
    Task<GeneratedVideos> PostImg2Vid(Img2VidParameters parameters);
}
```

### Simplified ImageService (Phase 9)

```csharp
public interface IImageService
{
    // New unified methods
    Task<ImagesDto> GenerateImagesAsync(GenerationParameters parameters, Workflow workflow);
    Task<GeneratedVideos> GenerateVideoAsync(GenerationParameters parameters, Workflow workflow);
    
    // Results
    GeneratedImages Images { get; }
    ImagesDto GeneratedImageEntities { get; set; }
    GeneratedVideos GeneratedVideos { get; }
    InferenceProgress Progress { get; set; }
    
    // Utility
    Task<ImagesDto> SaveImages(Outdir outdirSamples, string scriptName);
    Task<bool> DownloadImageAsPng(string url, string path, bool overwrite = true);
    
    // Legacy methods removed in Phase 9:
    // - GetImages(ModeType mode)
    // - GetVideo()
}
```

---

## Total Complexity

| Phase | Points | Status |
|-------|--------|--------|
| Phase 1: Schema Definition | 5 | &check; Complete |
| Phase 2: Core Infrastructure | 13 | &check; Complete |
| Phase 3: Fragment Updates | 8 | &check; Complete |
| Phase 4: Dynamic Components | 13 | &check; Complete |
| Phase 5: Unified Page | 13 | &check; Complete |
| Phase 6: State &amp; Persistence | 8 | [~] Nearly Complete |
| Phase 7: Workflow Templates | 5 | [ ] Not Started |
| Phase 8: Node Chaining | 8 | [ ] Not Started |
| Phase 9: Legacy Deprecation | 13 | [ ] Not Started |
| Phase 10: Documentation | 3 | [ ] Not Started |
| **Total** | **89 points** | |

---

## References

- [NODE_INTEGRATION_GUIDE.md](../../BlazorWebApp/Workflows/NODE_INTEGRATION_GUIDE.md) - Current integration process
- [TEMPLATE_GUIDE.md](../../BlazorWebApp/Workflows/TEMPLATE_GUIDE.md) - Workflow template conventions
- [IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md) - Planning conventions
- [FRAGMENT_SCHEMA_GUIDE.md](./FRAGMENT_SCHEMA_GUIDE.md) - Fragment UI schema documentation
