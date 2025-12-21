# Phase 13 - Architecture Improvements &amp; Technical Debt Resolution

## Status
**Phase:** 13  
**Build Status:** &#9745; Passing | **Tests:** &#9744; Not Updated

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** &rarr; 2. **Test and Debug Features** &rarr; 3. **Discuss Improvements** &rarr; 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules
- **Each step = commit checkpoint** - test thoroughly before proceeding
- **Minimal changes only** - focused on phase objectives
- **Document all issues and resolutions** in this file
- **This document must have enough context** to resume in a new session
- **User permission required** before next step

---

## Objective

Address all technical debt and pain points identified in the Generation Implementation Report. This phase focuses on improving maintainability, reducing coupling, and implementing missing features.

---

## Context

### Pain Points Summary

The Generation Implementation Report identified the following categories of pain points:

| Category | Count | Priority |
|----------|-------|----------|
| Scriban Files Setup | 5 | Medium-High |
| C# Template/Fragment Mapping | 5 | High |
| State Serialization | 5 | Medium-High |
| Component Rendering | 5 | Medium |

### Current Architecture Issues

1. **Fragment ID Coupling** - IDs must match across templates, UI, and FragmentKeys constants
2. **Dual Defaults** - Pipeline parameters and fragment body can have conflicting defaults
3. **JsonElement Deserialization** - Type loss on JSON round-trip causes runtime issues
4. **No Schema Validation** - Template syntax errors only surface at render time
5. **Dynamic Source Async Gap** - Options resolved in UI, not during initialization
6. **Regex-based Parsing** - Fragile for Sources/Assets extraction
7. **Manual Component Registration** - Easy to forget when adding new fragments
8. **No Dynamic Field Rendering** - `schema.Fields` defined but not implemented
9. **Local State Sync Pattern** - Verbose and error-prone in form components

---

## Implementation Plan

### Sub-Phase 13.1: Template Validation &amp; Error Handling
**Objective:** Catch template errors at startup instead of runtime
**Complexity:** 13 points

#### Step 13.1.1: Create WorkflowValidationService
**Complexity:** 5
**Status:** [x] Complete

Created a comprehensive validation service that:
- Validates all `.sbn` workflow templates at startup
- Validates Pipeline structure (required `id` and `fragment` properties)
- Validates fragment references exist on disk
- Validates Sources/Assets arrays for proper structure
- Validates fragment `#meta` blocks for JSON syntax
- Validates UI schema (component references, parameter constraints)
- Pre-compiles Scriban templates and caches them
- Reports detailed errors with file locations and line numbers
- Runs in Development mode only (doesn't block production)

**Files Created:**
- `Services/IWorkflowValidationService.cs` - Interface + result models + error types enum
- `Services/WorkflowValidationService.cs` - Full implementation

**Files Modified:**
- `Program.cs` - Service registration + startup validation

#### Step 13.1.2: Fragment Schema Validation
**Complexity:** 3
**Status:** [x] Complete

Added comprehensive schema validation:
- Added `Validate()` method to `FragmentSchema` class
- Validates parameter constraints (min &lt; max, step &gt; 0)
- Validates dynamic source requires `input_name`
- Validates Fields array for slider, select, file, group types
- Validates no duplicate component + fields definition
- Added `ValidateFragmentSchema(fragmentFile)` to `IFragmentSchemaService`
- Added `GetAllFragmentFiles()` to enumerate all fragments
- Added `ValidateAllFragmentSchemas()` for bulk validation
- Integrated schema validation into `WorkflowValidationService.ValidateFragment()`

**Files Modified:**
- `Models/FragmentSchema.cs` - Added `Validate()` method
- `Services/FragmentSchemaService.cs` - Added validation methods to interface and implementation
- `Services/WorkflowValidationService.cs` - Integrated FragmentSchemaService for deeper validation

#### Step 13.1.3: Scriban Template Pre-compilation
**Complexity:** 5
**Status:** [x] Complete

Created `TemplateCacheService` for shared template caching:
- Pre-compiles all workflow templates and fragments at startup
- Caches compiled `Template` objects for reuse
- Provides `GetOrCompile()` for on-demand compilation with caching
- Reports compilation errors with line/column numbers
- `WorkflowService.RenderTemplate()` now uses cache
- Startup logs template pre-compilation statistics

**Files Created:**
- `Services/TemplateCacheService.cs` - Interface + implementation

**Files Modified:**
- `Program.cs` - Register service + startup pre-compilation
- `Services/WorkflowService.cs` - Inject and use template cache
- `BlazorWebApp.Tests/MockBuilders/MockWorkflowServiceBuilder.cs` - Updated for new dependency
- `BlazorWebApp.Tests/Services/WorkflowServiceTests.cs` - Updated for new dependency

---

### Sub-Phase 13.2: Default Value Consolidation
**Objective:** Single source of truth for default values
**Complexity:** 8 points

#### Step 13.2.1: Define Default Value Priority
**Complexity:** 2
**Status:** [ ] Not Started

Document and enforce clear priority order:
1. Saved workflow state (database)
2. Pipeline step parameters (workflow template)
3. Fragment `#meta.ui.parameters.*.default` (schema)
4. Fragment body defaults (Scriban `??` operator) - **REMOVE**

**Files to Modify:**
- `Workflows/TEMPLATE_GUIDE.md` - Document convention
- `Workflows/FRAGMENT_SCHEMA_GUIDE.md` - Update documentation

#### Step 13.2.2: Remove Fragment Body Defaults
**Complexity:** 3
**Status:** [ ] Not Started

Update all fragments to remove inline defaults, use schema defaults only:
- Remove `{{ param ?? "default" }}` patterns from fragment bodies
- Ensure schema `parameters.*.default` is set
- Pipeline step provides workflow-specific overrides

**Files to Modify:**
- All `.sbn` fragment files
- `Services/WorkflowTemplateParser.cs` - Remove `ParseScribanDefaultValue()`

#### Step 13.2.3: Apply Schema Defaults During Initialization
**Complexity:** 3
**Status:** [ ] Not Started

Update `GenerationParameterService.InitializeFragmentsFromPipeline()`:
- Apply schema defaults for missing values after pipeline defaults
- Log warnings for missing defaults

**Files to Modify:**
- `Services/GenerationParameterService.cs`

---

### Sub-Phase 13.3: Dynamic Source Pre-Resolution
**Objective:** Resolve dynamic dropdown options during initialization
**Complexity:** 8 points

#### Step 13.3.1: Add Async Source Resolution to Initialization
**Complexity:** 5
**Status:** [ ] Not Started

During `InitializeFromWorkflowAsync()`:
- For each fragment with UI schema
- For each parameter with dynamic source
- Resolve options from ComfyUI API
- Set first option as default if no value set

**Files to Modify:**
- `Services/GenerationParameterService.cs` - Add source pre-resolution
- `Services/IGenerationParameterService.cs` - Update interface docs

#### Step 13.3.2: Cache Resolved Options in GenerationParameters
**Complexity:** 3
**Status:** [ ] Not Started

Store resolved options so UI doesn't need async calls:
- Add `ResolvedOptions` dictionary to `FragmentParameters`
- UI components read from cache

**Files to Modify:**
- `Models/FragmentParameters.cs` - Add `ResolvedOptions` property
- Fragment form components - Read from cache

---

### Sub-Phase 13.4: Strongly-Typed Fragment Parameters
**Objective:** Replace dictionary with typed properties for common fragments
**Complexity:** 13 points

#### Step 13.4.1: Create Fragment Parameter Interfaces
**Complexity:** 3
**Status:** [ ] Not Started

Create interfaces for common fragment types:
- `ISamplerFragmentValues` - steps, cfg, seed, sampler_name, scheduler, denoise
- `ILatentFragmentValues` - width, height, batch_size
- `IPromptsFragmentValues` - positive, negative

**Files to Create:**
- `Models/Fragments/ISamplerFragmentValues.cs`
- `Models/Fragments/ILatentFragmentValues.cs`
- `Models/Fragments/IPromptsFragmentValues.cs`

#### Step 13.4.2: Create Typed Fragment Extension Methods
**Complexity:** 5
**Status:** [ ] Not Started

Extension methods for type-safe access:
```csharp
public static class FragmentParametersExtensions
{
    public static ISamplerFragmentValues AsSampler(this FragmentParameters fragment);
    public static ILatentFragmentValues AsLatent(this FragmentParameters fragment);
}
```

**Files to Create:**
- `Extensions/FragmentParametersExtensions.cs`

#### Step 13.4.3: Update Form Components to Use Typed Access
**Complexity:** 5
**Status:** [ ] Not Started

Replace dictionary access with typed methods:
- `SamplerForm.razor` - Use `AsSampler()`
- `LatentForm.razor` - Use `AsLatent()`
- `PromptsForm.razor` - Use `AsPrompts()`

**Files to Modify:**
- `Components/Shared/Generation/Fragments/*.razor`

---

### Sub-Phase 13.5: Auto-Generate FragmentKeys
**Objective:** Generate FragmentKeys from templates to avoid drift
**Complexity:** 8 points

#### Step 13.5.1: Create FragmentKeys Generator Tool
**Complexity:** 5
**Status:** [ ] Not Started

Create a source generator or build-time tool:
- Scan all workflow templates
- Extract unique fragment IDs from Pipeline
- Extract parameter names from fragment schemas
- Generate `FragmentKeys.Generated.cs`

**Files to Create:**
- `Build/FragmentKeysGenerator.cs` (or T4 template)
- `Models/FragmentKeys.Generated.cs`

#### Step 13.5.2: Add Build Integration
**Complexity:** 3
**Status:** [ ] Not Started

Run generator as pre-build step:
- MSBuild target or dotnet tool
- Fail build if generated file is out of sync with source control

**Files to Modify:**
- `BlazorWebApp.csproj` - Add pre-build target
- `.github/workflows/*.yml` - Add CI validation

---

### Sub-Phase 13.6: Dynamic Field Rendering
**Objective:** Implement schema-driven field rendering
**Complexity:** 13 points

#### Step 13.6.1: Complete DynamicField Component
**Complexity:** 8
**Status:** [ ] Not Started

Implement all field types defined in `FieldSchema`:
- `slider` - MudSlider with constraints
- `numeric` - MudNumericField with constraints
- `seed` - Numeric with shuffle/restore buttons
- `select` - MudSelect with dynamic options
- `text` - MudTextField single line
- `textarea` - MudTextField multiline
- `checkbox` / `switch` - Boolean inputs
- `color` - Color picker
- `file` - Model file selector
- `resolution` - Width/Height with aspect ratio
- `group` - Nested field container

**Files to Modify:**
- `Components/Shared/Generation/DynamicField.razor`

#### Step 13.6.2: Integrate DynamicField in Fragment Rendering
**Complexity:** 5
**Status:** [ ] Not Started

Update `RenderOptionalFragmentForm()` in Generate.razor:
- If no component registered, render `DynamicFragmentForm`
- `DynamicFragmentForm` iterates `schema.Fields`
- Each field renders as `DynamicField`

**Files to Modify:**
- `Pages/Generate.razor`
- `Components/Shared/Generation/DynamicFragmentForm.razor`

---

### Sub-Phase 13.7: Component Auto-Discovery
**Objective:** Auto-register fragment components
**Complexity:** 5 points

#### Step 13.7.1: Add Attribute-Based Registration
**Complexity:** 3
**Status:** [ ] Not Started

Create attribute for fragment components:
```csharp
[FragmentComponent("SamplerForm")]
public partial class SamplerForm : ComponentBase { }
```

Scan assembly at startup to register.

**Files to Create:**
- `Attributes/FragmentComponentAttribute.cs`

**Files to Modify:**
- `Services/ComponentRegistry.cs` - Add auto-discovery
- All fragment form components - Add attribute

#### Step 13.7.2: Validate Component Registrations
**Complexity:** 2
**Status:** [ ] Not Started

During startup validation:
- For each fragment schema with `ui.component`
- Verify component is registered
- Log warning if missing

**Files to Modify:**
- `Services/WorkflowValidationService.cs`

---

### Sub-Phase 13.8: Local State Binding Abstraction
**Objective:** Reduce boilerplate in form components
**Complexity:** 8 points

#### Step 13.8.1: Create FragmentFormBase&lt;T&gt; Generic Base
**Complexity:** 5
**Status:** [ ] Not Started

Create base class with automatic local state sync:
```csharp
public abstract class FragmentFormBase<TValues> : ComponentBase
    where TValues : class, new()
{
    [Parameter] public string FragmentId { get; set; }
    protected TValues Values { get; private set; }
    
    protected override void OnParametersSet()
    {
        Values = Parameters.GetFragment(FragmentId)?.As<TValues>() ?? new();
    }
}
```

**Files to Modify:**
- `Components/Shared/Generation/FragmentFormBase.cs`

#### Step 13.8.2: Create Bindable Property Helper
**Complexity:** 3
**Status:** [ ] Not Started

Create helper for two-way binding with fragment values:
```razor
<FragmentSlider 
    FragmentId="@FragmentId"
    Parameter="steps"
    Min="1" Max="150" Step="1"
    Label="Steps" />
```

**Files to Create:**
- `Components/Shared/Generation/FragmentSlider.razor`
- `Components/Shared/Generation/FragmentSelect.razor`
- `Components/Shared/Generation/FragmentNumeric.razor`

---

### Sub-Phase 13.9: JSON Serialization Improvements
**Objective:** Fix JsonElement type loss issues
**Complexity:** 8 points

#### Step 13.9.1: Add Custom JsonConverter for FragmentParameters
**Complexity:** 5
**Status:** [ ] Not Started

Create converter that preserves types:
- Store type hints for non-primitive types
- Handle numeric precision (int vs long vs double)
- Handle null values correctly

**Files to Create:**
- `Data/Converters/FragmentParametersJsonConverter.cs`

**Files to Modify:**
- `Data/AppDbContext.cs` - Register converter

#### Step 13.9.2: Add Type Coercion in GetValue
**Complexity:** 3
**Status:** [ ] Not Started

Improve `FragmentParameters.GetValue<T>()`:
- Better handling of JsonElement
- Numeric type coercion (int &harr; long &harr; double)
- String parsing for common types

**Files to Modify:**
- `Models/FragmentParameters.cs`

---

### Sub-Phase 13.10: Template Hot-Reload (Development Only)
**Objective:** Enable template changes without app restart
**Complexity:** 8 points

#### Step 13.10.1: Add FileSystemWatcher for Templates
**Complexity:** 5
**Status:** [ ] Not Started

Watch `.sbn` files for changes:
- Only in Development environment
- Debounce rapid changes
- Clear caches on change
- Publish event for UI refresh

**Files to Create:**
- `Services/TemplateWatcherService.cs`

**Files to Modify:**
- `Program.cs` - Register in Development only

#### Step 13.10.2: Add UI Refresh on Template Change
**Complexity:** 3
**Status:** [ ] Not Started

Subscribe to template change events:
- Clear workflow cache
- Refresh current workflow
- Show toast notification

**Files to Modify:**
- `Pages/Generate.razor`
- `Services/WorkflowService.cs` - Add cache invalidation

---

## Progress Tracking

| Sub-Phase | Step | Status | Complexity | Notes |
|-----------|------|--------|------------|-------|
| 13.1 | 13.1.1 | [x] | 5 | WorkflowValidationService - Complete |
| 13.1 | 13.1.2 | [x] | 3 | Fragment Schema Validation - Complete |
| 13.1 | 13.1.3 | [x] | 5 | Template Pre-compilation - Complete |
| 13.2 | 13.2.1 | [ ] | 2 | Document Default Priority |
| 13.2 | 13.2.2 | [ ] | 3 | Remove Fragment Body Defaults |
| 13.2 | 13.2.3 | [ ] | 3 | Apply Schema Defaults |
| 13.3 | 13.3.1 | [ ] | 5 | Async Source Resolution |
| 13.3 | 13.3.2 | [ ] | 3 | Cache Resolved Options |
| 13.4 | 13.4.1 | [ ] | 3 | Fragment Parameter Interfaces |
| 13.4 | 13.4.2 | [ ] | 5 | Typed Extension Methods |
| 13.4 | 13.4.3 | [ ] | 5 | Update Form Components |
| 13.5 | 13.5.1 | [ ] | 5 | FragmentKeys Generator |
| 13.5 | 13.5.2 | [ ] | 3 | Build Integration |
| 13.6 | 13.6.1 | [ ] | 8 | Complete DynamicField |
| 13.6 | 13.6.2 | [ ] | 5 | Integrate DynamicField |
| 13.7 | 13.7.1 | [ ] | 3 | Attribute Registration |
| 13.7 | 13.7.2 | [ ] | 2 | Validate Registrations |
| 13.8 | 13.8.1 | [ ] | 5 | Generic FragmentFormBase |
| 13.8 | 13.8.2 | [ ] | 3 | Bindable Property Helpers |
| 13.9 | 13.9.1 | [ ] | 5 | Custom JsonConverter |
| 13.9 | 13.9.2 | [ ] | 3 | Type Coercion |
| 13.10 | 13.10.1 | [ ] | 5 | FileSystemWatcher |
| 13.10 | 13.10.2 | [ ] | 3 | UI Refresh |

**Total Complexity:** 92 points
**Completed:** 13 points (14%)

---

## Priority Order

Based on impact and risk, recommended execution order:

### High Priority (Do First)
1. **13.1** Template Validation (13 pts) - Catches errors early
2. **13.3** Dynamic Source Pre-Resolution (8 pts) - Fixes initialization blocker
3. **13.9** JSON Serialization (8 pts) - Fixes data loss bugs

### Medium Priority
4. **13.2** Default Value Consolidation (8 pts) - Reduces confusion
5. **13.4** Strongly-Typed Parameters (13 pts) - Better DX
6. **13.6** Dynamic Field Rendering (13 pts) - Completes feature

### Lower Priority
7. **13.8** Local State Abstraction (8 pts) - Reduces boilerplate
8. **13.7** Component Auto-Discovery (5 pts) - Nice to have
9. **13.5** FragmentKeys Generator (8 pts) - Nice to have
10. **13.10** Hot-Reload (8 pts) - Dev convenience

---

## Success Criteria

### Phase Complete When:
- [ ] All templates validated at startup with clear error messages
- [ ] No runtime Scriban syntax errors
- [ ] Dynamic source dropdowns populated on fragment activation
- [ ] JSON round-trip preserves all types correctly
- [ ] Default values come from single source (pipeline + schema only)
- [ ] DynamicField renders all field types from schema
- [ ] Components auto-discovered, no manual registration
- [ ] Form components use reduced boilerplate

### Metrics
| Metric | Before | After |
|--------|--------|-------|
| Files to change for new fragment | 4+ | 1-2 |
| Runtime template errors | Possible | Caught at startup |
| Manual component registrations | 7+ | 0 |
| Lines per form component | ~150 | ~50 |
| JsonElement casts in forms | Many | Zero |

---

## Issues &amp; Resolutions

| Issue | Resolution |
|-------|------------|
| (To be filled during implementation) | |

---

## Commit Checkpoints

- [x] After Sub-Phase 13.1 complete (Validation) - 3 steps done, 13 pts
- [ ] After Sub-Phase 13.3 complete (Source Resolution)
- [ ] After Sub-Phase 13.9 complete (JSON)
- [ ] After Sub-Phase 13.2 complete (Defaults)
- [ ] After Sub-Phase 13.4 complete (Typed Parameters)
- [ ] After Sub-Phase 13.6 complete (Dynamic Fields)
- [ ] After remaining sub-phases complete

---

## References

- [Generation Implementation Report](./GENERATION_IMPLEMENTATION_REPORT.md)
- [MAIN_PLAN.md](./MAIN_PLAN.md)
- [FRAGMENT_SCHEMA_GUIDE.md](./FRAGMENT_SCHEMA_GUIDE.md)
- [SERVICE_ANALYSIS.md](./SERVICE_ANALYSIS.md)

---

**Phase Status:** In Progress [~]
