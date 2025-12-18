# Dynamic Generation Page Refactor - Implementation Plan

## Status
**Current Phase:** Execution (Phase 3)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update Phase Document**
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

---

## Proposed Solution

### Architecture Overview

```
???????????????????????????????????????????????????????????????????
?                     Workflow Template (.sbn)                     ?
?  ???????????? ???????????? ???????????????????????????????????? ?
?  ?  Assets  ? ? Sources  ? ?           Pipeline[]             ? ?
?  ? (models) ? ?(img/vid) ? ?  id, fragment, parameters(def)  ? ?
?  ???????????? ???????????? ???????????????????????????????????? ?
???????????????????????????????????????????????????????????????????
                              ?
         ???????????????????????????????????????????
         ?                    ?                    ?
???????????????????  ???????????????????  ???????????????????
?  Fragment #meta ?  ?  Fragment #meta ?  ?  Fragment #meta ?
?  ?????????????  ?  ?  ?????????????  ?  ?  ?????????????  ?
?  ? outputs   ?  ?  ?  ? outputs   ?  ?  ?  ? outputs   ?  ?
?  ? conditions?  ?  ?  ? conditions?  ?  ?  ? conditions?  ?
?  ? ui:       ?  ?  ?  ? ui:       ?  ?  ?  ? (no ui)   ?  ?
?  ?  component?  ?  ?  ?  component?  ?  ?  ?????????????  ?
?  ?  params   ?  ?  ?  ?  fields[] ?  ?  ?  (utility node) ?
?  ?????????????  ?  ?????????????????  ?  ???????????????????
? (designed comp) ?  (dynamic fields)   ?
???????????????????  ???????????????????
         ?                    ?
         ?                    ?
???????????????????????????????????????????????????????????????????
?                    GenerationParameters                          ?
?  ???????????????? ???????????????? ???????????????? ??????????? ?
?  ?  Fragments   ? ?    Assets    ? ?   Sources    ? ?  Loras  ? ?
?  ? Dict<id,val> ? ? Dict<id,val> ? ? Dict<id,val> ? ?  List   ? ?
?  ???????????????? ???????????????? ???????????????? ??????????? ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?                   Dynamic Generate Page                          ?
?  ????????????????????????????????????????????????????????????   ?
?  ?                    Prompt Fields                          ?   ?
?  ????????????????????????????????????????????????????????????   ?
?  ???????????????  ???????????????  ???????????????              ?
?  ?   Sources   ?  ?  Designed   ?  ?  Dynamic    ?              ?
?  ?   Panel     ?  ?  Component  ?  ?   Fields    ?              ?
?  ? (img/video) ?  ? (from comp) ?  ? (fallback)  ?              ?
?  ???????????????  ???????????????  ???????????????              ?
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

### Conventions

- **Fragment ID** is the unique key linking Pipeline ? UI ? GenerationParameters
- **Designed components** are referenced by name in `ui.component`
- **Dynamic fields** are used when `ui.component` is null and `ui.fields[]` is present
- **Utility fragments** have no `ui` block and render no form
- **Sources** appear first in the parameters panel, below prompts
- **Chainable fragments** can have multiple instances (e.g., `main_sampler`, `refiner_sampler`)

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

### Phase 1: Schema Definition & Documentation
**Objective:** Define the UI schema format and document conventions
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1.1 - Design complete UI schema JSON structure
- [ ] Step 1.2 - Create `FRAGMENT_SCHEMA_GUIDE.md` documentation
- [ ] Step 1.3 - Define field types and their properties
- [ ] Step 1.4 - Define component registry conventions
- [ ] Step 1.5 - Review with example fragments (sampler, prompts, upscale)

#### Success Criteria
- Schema format is fully documented
- All field types are defined with validation rules
- Component naming conventions established

---

### Phase 2: Core Infrastructure
**Objective:** Create the foundational types and services for dynamic parameters
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 2.1 - Create `GenerationParameters` model
- [ ] Step 2.2 - Create `FragmentParameters` model
- [ ] Step 2.3 - Create `FragmentSchema` model (parsed from #meta.ui)
- [ ] Step 2.4 - Create `SourceAsset` model for input images/videos
- [ ] Step 2.5 - Create `ComponentRegistry` service
- [ ] Step 2.6 - Extend `WorkflowService` to parse UI schema
- [ ] Step 2.7 - Create `GenerationParameterService` for parameter CRUD

#### Success Criteria
- New models compile and serialize correctly
- WorkflowService extracts UI schema from fragments
- ComponentRegistry resolves fragment types to component names

---

### Phase 3: Fragment Updates
**Objective:** Update key fragments with UI schema
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 3.1 - Update `prompts.sbn` with UI schema
- [ ] Step 3.2 - Update `sampler.sbn` with UI schema
- [ ] Step 3.3 - Update `upscale.sbn` with UI schema
- [ ] Step 3.4 - Update `detailer-core.sbn` with UI schema
- [ ] Step 3.5 - Update loader fragments (flux, sd, etc.) - utility, no UI
- [ ] Step 3.6 - Validate all schemas parse correctly

#### Success Criteria
- Core fragments have valid UI schema
- WorkflowService correctly parses all updated fragments
- Schema validation catches malformed definitions

---

### Phase 4: Dynamic Form Components
**Objective:** Create reusable form components that render from schema
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 4.1 - Create `DynamicField.razor` (single field from schema)
- [ ] Step 4.2 - Create `DynamicFragmentForm.razor` (fragment form with dynamic fields)
- [ ] Step 4.3 - Create `FragmentFormContainer.razor` (enable/disable, chaining UI)
- [ ] Step 4.4 - Create `SourcesPanel.razor` (tabbed image/video inputs)
- [ ] Step 4.5 - Migrate existing form logic to designed components with schema binding
- [ ] Step 4.6 - Create component stubs for all registered components

#### Success Criteria
- Dynamic fields render all defined types correctly
- Designed components read constraints from schema
- Two-way binding works for all field types
- SourcesPanel handles multiple input types

---

### Phase 5: Unified Generation Page
**Objective:** Create single generation page that works for all modes
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 5.1 - Create `Generate.razor` page layout
- [ ] Step 5.2 - Implement prompt fields with fragment binding
- [ ] Step 5.3 - Implement SourcesPanel integration
- [ ] Step 5.4 - Implement workflow selection and switching
- [ ] Step 5.5 - Implement fragment form rendering from pipeline
- [ ] Step 5.6 - Implement asset panel integration (existing AssetResolverService)
- [ ] Step 5.7 - Implement generate button and progress

#### Success Criteria
- Single page handles Txt2Img, Img2Img, Img2Vid
- Forms render dynamically based on selected workflow
- Generation works end-to-end

---

### Phase 6: State & Persistence
**Objective:** Update state management to use new parameter structure
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 6.1 - Update `StateService` to handle `GenerationParameters`
- [ ] Step 6.2 - Update `ImageService` to use new parameters
- [ ] Step 6.3 - Update database state serialization
- [ ] Step 6.4 - Implement parameter parsing (wildcards, seeds) in new flow
- [ ] Step 6.5 - Test state persistence and recovery

#### Success Criteria
- State saves and loads correctly
- Wildcards and seed randomization work
- No data loss between sessions

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

### Phase 9: Cleanup & Simplification
**Objective:** Remove deprecated code and simplify AppSettings
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 9.1 - Remove old parameter classes (`Txt2ImgParameters`, etc.)
- [ ] Step 9.2 - Remove old DTO classes (`Txt2ImgComfyUI`, etc.)
- [ ] Step 9.3 - Remove old generation pages (`Txt2Img.razor`, etc.)
- [ ] Step 9.4 - Simplify `AppSettings` (remove component constraints)
- [ ] Step 9.5 - Remove `ParameterMapper.cs`
- [ ] Step 9.6 - Update `NODE_INTEGRATION_GUIDE.md` with new workflow

#### Success Criteria
- Codebase is clean with no dead code
- AppSettings is significantly smaller
- New node integration requires only 2-3 files

---

### Phase 10: Documentation
**Objective:** Document the new architecture
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 10.1 - Create architecture overview document
- [ ] Step 10.2 - Update all affected guides
- [ ] Step 10.3 - Create migration notes for users

#### Success Criteria
- Documentation is complete and accurate
- New developers can understand the system

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|------|------------|------------|
| Schema validation errors at runtime | Strict validation during parse, clear error messages | 3 |
| Complex workflows break during migration | Test each workflow type during Phase 7 | 5 |
| Parameter parsing (wildcards, seeds) breaks | Keep Parser.cs logic, integrate carefully in Phase 6 | 3 |
| Component binding performance | Lazy loading, minimize re-renders | 3 |
| State serialization format changes | Version the state format, handle gracefully | 3 |

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

### Template with Sources and Pipeline IDs

```json
{
  "Title": "SteadyDancer",
  "Base": "Wan",
  "Mode": "img2vid",
  "Assets": [
    { "parameter": "Model", "label": "Model", "type": "DiffusionModel", "default": "...", "order": 1 }
  ],
  "Sources": [
    { "id": "source_video", "label": "Source Video", "type": "video", "required": true },
    { "id": "reference_image", "label": "Reference Pose", "type": "image", "required": true }
  ],
  "Pipeline": [
    {
      "id": "prompts",
      "fragment": "prompts.sbn",
      "parameters": {
        "positive": {{ Prompt | json }},
        "negative": {{ NegativePrompt | json }}
      }
    },
    {
      "id": "main_sampler",
      "fragment": "sampler.sbn",
      "parameters": {
        "sampler_name": "euler",
        "scheduler": "normal",
        "steps": 20,
        "cfg": 7,
        "seed": -1
      }
    }
  ]
}
```

### Fragment with Designed Component

```json
#meta
{
  "outputs": {
    "latent_output": {"node": "{{ sampler_id }}", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.{{ sampler_id }}.IsActive"]
  },
  "ui": {
    "component": "SamplerForm",
    "title": "Sampler",
    "icon": "fa-solid fa-dice",
    "collapsible": true,
    "chainable": true,
    "parameters": {
      "sampler_name": { "source": "Backend.Samplers" },
      "scheduler": { "source": "Backend.Schedulers" },
      "steps": { "min": 1, "max": 150, "step": 1 },
      "cfg": { "min": 1, "max": 30, "step": 0.5 },
      "seed": { "min": -1 }
    }
  }
}
#end
```

### Fragment with Dynamic Fields (no custom component)

```json
#meta
{
  "outputs": { ... },
  "ui": {
    "component": null,
    "title": "Experimental Node",
    "collapsible": true,
    "fields": [
      { 
        "parameter": "strength", 
        "label": "Strength", 
        "type": "slider", 
        "min": 0, 
        "max": 1, 
        "step": 0.01 
      },
      { 
        "parameter": "mode", 
        "label": "Mode", 
        "type": "select", 
        "options": ["fast", "quality"] 
      }
    ]
  }
}
#end
```

### Designed Component with Schema Binding

```razor
@* SamplerForm.razor *@
@inject IBackendService Backend

<MudGrid>
    <MudItem xs="6">
        <MudSelect T="string" 
                   @bind-Value="@Values["sampler_name"]" 
                   Label="Sampler">
            @foreach (var sampler in Backend.Samplers)
            {
                <MudSelectItem Value="@sampler.Name" />
            }
        </MudSelect>
    </MudItem>
    <MudItem xs="6">
        <MudSelect T="string" 
                   @bind-Value="@Values["scheduler"]" 
                   Label="Scheduler">
            @foreach (var scheduler in Backend.Schedulers)
            {
                <MudSelectItem Value="@scheduler.Name" />
            }
        </MudSelect>
    </MudItem>
    <MudItem xs="6">
        <MudSlider T="int" 
                   @bind-Value="@GetInt("steps")"
                   Min="@Schema.Parameters["steps"].Min"
                   Max="@Schema.Parameters["steps"].Max"
                   Step="@Schema.Parameters["steps"].Step"
                   Variant="Variant.Filled" ValueLabel>
            <small>Steps:</small> @GetInt("steps")
        </MudSlider>
    </MudItem>
    <!-- ... more fields ... -->
</MudGrid>

@code {
    [Parameter] public Dictionary<string, object?> Values { get; set; } = new();
    [Parameter] public FragmentSchema Schema { get; set; } = new();
    
    private int GetInt(string key) => Convert.ToInt32(Values.GetValueOrDefault(key, 0));
}
```

---

## Simplified AppSettings (After Migration)

```csharp
public class AppSettings
{
    public ThemeSettings Theme { get; set; }
    public PathSettings Paths { get; set; }
    public BackendSettings Backend { get; set; }
    public GenerationSettings Generation { get; set; }
    // ... other non-component settings
}

public class GenerationSettings
{
    /// <summary>
    /// Quick resolution presets for the resolution picker.
    /// </summary>
    public List<ResolutionPreset> QuickResolutions { get; set; }
    
    /// <summary>
    /// LLM enhancer settings.
    /// </summary>
    public LLMEnhancerSettings LLMEnhancer { get; set; }
    
    /// <summary>
    /// Random images settings for gallery.
    /// </summary>
    public RandomImagesSettings RandomImages { get; set; }
    
    // All min/max/step/default values moved to fragment schemas!
}
```

---

## Total Complexity

| Phase | Points |
|-------|--------|
| Phase 1: Schema Definition | 5 |
| Phase 2: Core Infrastructure | 13 |
| Phase 3: Fragment Updates | 8 |
| Phase 4: Dynamic Components | 13 |
| Phase 5: Unified Page | 13 |
| Phase 6: State & Persistence | 8 |
| Phase 7: Workflow Templates | 5 |
| Phase 8: Node Chaining | 8 |
| Phase 9: Cleanup | 5 |
| Phase 10: Documentation | 3 |
| **Total** | **81 points** |

---

## References

- [NODE_INTEGRATION_GUIDE.md](../../BlazorWebApp/Workflows/NODE_INTEGRATION_GUIDE.md) - Current integration process
- [TEMPLATE_GUIDE.md](../../BlazorWebApp/Workflows/TEMPLATE_GUIDE.md) - Workflow template conventions
- [IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md) - Planning conventions
