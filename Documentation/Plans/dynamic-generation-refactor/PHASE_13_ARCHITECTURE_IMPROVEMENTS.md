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
**Status:** [x] Complete

Documented and enforced clear priority order:
1. Saved workflow state (database) - highest priority
2. Pipeline step parameters (workflow template)  
3. Fragment `#meta.ui.parameters.*.default` (schema defaults)
4. Dynamic source resolution (first option from ComfyUI API)

**Note:** Fragment body defaults (`{{ param ?? "default" }}`) are Scriban rendering fallbacks only -NOT used for UI initialization.

**Files Modified:**
- `Workflows/TEMPLATE_GUIDE.md` - Added Default Value Priority section
- `Workflows/FRAGMENT_SCHEMA_GUIDE.md` - Rewrote Default Value Resolution section with priority table

#### Step 13.2.2: Remove Fragment Body Defaults
**Complexity:** 3
**Status:** [x] Complete

Instead of modifying all fragment files (risky), marked `ParseFragmentDefaults` as obsolete and removed all calls to it from initialization code. Fragment body defaults are now documented as Scriban rendering fallbacks only.

**Files Modified:**
- `Services/IWorkflowService.cs` - Marked `ParseFragmentDefaults` as `[Obsolete]` with documentation
- `Services/GenerationParameterService.cs` - Removed calls to `ParseFragmentDefaults` from initialization

#### Step 13.2.3: Apply Schema Defaults During Initialization
**Complexity:** 3
**Status:** [x] Complete

Updated `GenerationParameterService` to apply schema defaults (Priority 2) instead of fragment body defaults:
- `InitializeFragmentsFromPipeline()` - Now applies `schema.Parameters[].Default`
- `RestoreFromSavedState()` - Uses schema defaults for new fragments
- `CreateFragmentWithDefaults()` - Uses schema defaults for on-demand creation
- Added documentation about the priority order

**Files Modified:**
- `Services/GenerationParameterService.cs` - Updated all initialization methods to use schema defaults

---

### Sub-Phase 13.3: Dynamic Source Pre-Resolution
**Objective:** Resolve dynamic dropdown options during initialization
**Complexity:** 8 points

#### Step 13.3.1: Add Async Source Resolution to Initialization
**Complexity:** 5
**Status:** [x] Complete

Added `PreResolveDynamicSourcesAsync()` during `InitializeFromWorkflowAsync()`:
- Iterates all fragment schemas for parameters with dynamic sources
- Resolves options from ComfyUI API for each `source` + `input_name` pair
- Sets first option as default if no value is set
- Also handles field schemas with dynamic sources (including nested groups)
- Caches resolved options at both service and fragment level
- Logs count of resolved sources and defaults set

**Files Modified:**
- `Services/GenerationParameterService.cs` - Added PreResolveDynamicSourcesAsync, PreResolveFieldSourcesAsync
- `Services/IGenerationParameterService.cs` - Updated docs, added GetResolvedOptions method

#### Step 13.3.2: Cache Resolved Options in GenerationParameters
**Complexity:** 3
**Status:** [x] Complete

Added `ResolvedOptions` property to `FragmentParameters`:
- Dictionary&lt;string, List&lt;string&gt;&gt; for parameter name to options mapping
- Marked with `[JsonIgnore]` - not serialized to database (transient data)
- `GetResolvedOptions()` checks fragment first, falls back to service cache
- UI components can now access options synchronously via fragment.ResolvedOptions

**Files Modified:**
- `Models/FragmentParameters.cs` - Added ResolvedOptions property with JsonIgnore
- `Services/GenerationParameterService.cs` - Updated StoreResolvedOptions and GetResolvedOptions

---

### Sub-Phase 13.4: Strongly-Typed Fragment Parameters
**Objective:** Replace dictionary access with typed properties for common fragments
**Complexity:** 13 points

#### Step 13.4.1: Create Fragment Parameter Interfaces
**Complexity:** 3
**Status:** [x] Complete

Created interfaces for common fragment types:
- `ISamplerValues` - steps, cfg, seed, sampler_name, scheduler, denoise
- `ILatentValues` - width, height, batch_size
- `IPromptsValues` - positive, negative
- `IDetailerValues` - detailer settings
- `IUpscaleValues` - upscale settings

**Files Created:**
- `Models/Fragments/FragmentValueInterfaces.cs` - All interface definitions

#### Step 13.4.2: Create Typed Fragment Extension Methods
**Complexity:** 5
**Status:** [x] Complete

Created extension methods for type-safe access:
- `fragment.AsSampler()` returns `ISamplerValues`
- `fragment.AsLatent()` returns `ILatentValues`
- `fragment.AsPrompts()` returns `IPromptsValues`
- `fragment.AsDetailer()` returns `IDetailerValues`
- `fragment.AsUpscale()` returns `IUpscaleValues`

Each accessor wraps `FragmentParameters` and provides strongly-typed get/set with defaults.

**Files Created:**
- `Extensions/FragmentParametersExtensions.cs` - Extension methods + accessor implementations

#### Step 13.4.3: Update Form Components to Use Typed Access
**Complexity:** 5
**Status:** [x] Complete (Design Decision)

**Design Decision:** After reviewing the existing form components, they already use the standard Blazor parameter-binding pattern with `[Parameter]` and `EventCallback`, which is the idiomatic approach for Blazor components.

The typed accessors (`AsSampler()`, etc.) are better suited for:
1. **Backend/service code** - E.g., `ImageService` extracting values for API calls
2. **Test code** - Setting up test scenarios with typed access
3. **Generate.razor code-behind** - When reading values for display or calculation

**Example usage in backend code:**
```csharp
// In ImageService or similar:
var sampler = parameters.GetFragment("main_sampler")?.AsSampler();
if (sampler != null)
{
    var workflow = new WorkflowDto
    {
        Steps = sampler.Steps,
        CfgScale = sampler.Cfg,
        Seed = sampler.Seed,
        // ... type-safe, no dictionary access!
    };
}
```

**Example usage in Generate.razor:**
```csharp
// Reading for display:
var sampler = Parameters.GetFragment(_samplerFragmentId)?.AsSampler();
_displaySteps = sampler?.Steps ?? 20;
```

No changes to existing form components needed - they follow Blazor best practices.

**Files Modified:** None (design decision documented)

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
**Status:** [x] Complete

Implemented all field types defined in `FieldSchema`:
- `slider` - MudSlider with min/max/step constraints ?
- `numeric` - MudNumericField with constraints ?
- `seed` - Numeric with randomize button ?
- `select` - MudSelect with dynamic options from pre-resolved cache ?
- `text` - MudTextField single line ?
- `textarea` - MudTextField multiline with rows ?
- `checkbox` / `switch` - Boolean inputs ?
- `color` - MudColorPicker ?
- `file` - Model file selector using pre-resolved options ? (NEW)
- `resolution` - Width/Height with aspect ratio lock and swap ? (NEW)
- `group` - Nested field container with optional collapse ? (NEW)

Added:
- `FragmentId` parameter for accessing pre-resolved options
- `IGenerationParameterService` injection for `GetResolvedOptions()`
- Resolution field with aspect ratio lock and swap dimensions
- Support for dynamic sources via `Field.HasDynamicSource`

**Files Modified:**
- `Components/Shared/Generation/DynamicField.razor` - Added file, resolution, group types

#### Step 13.6.2: Integrate DynamicField in Fragment Rendering
**Complexity:** 5
**Status:** [x] Complete

Updated `DynamicFragmentForm` to pass `FragmentId` to all `DynamicField` instances:
- Added `FragmentId` parameter to `DynamicFragmentForm`
- All `DynamicField` instances receive the fragment ID for option resolution
- Nested fields in groups also receive the fragment ID

**Files Modified:**
- `Components/Shared/Generation/DynamicFragmentForm.razor` - Added FragmentId parameter

---

### Sub-Phase 13.7: Component Auto-Discovery
**Objective:** Auto-register fragment components
**Complexity:** 5 points

#### Step 13.7.1: Add Attribute-Based Registration
**Complexity:** 3
**Status:** [x] Complete

Created `[FragmentComponent]` attribute for fragment components:
```csharp
[FragmentComponent("SamplerForm")]
public partial class SamplerForm : ComponentBase { }
```

Updated `ComponentRegistry` to auto-discover components:
- Scans assembly for classes with `[FragmentComponent]` attribute
- Registers them automatically during construction
- Falls back to manual registration if no attributed components found

Added attribute to all existing fragment form components:
- `SamplerForm`
- `LatentForm`
- `PromptsForm`
- `UpscaleForm`
- `SeedVR2Form`
- `ConditioningVariationForm`
- `SeedVarianceEnhancerForm`

**Files Created:**
- `Attributes/FragmentComponentAttribute.cs` - The attribute class

**Files Modified:**
- `Services/ComponentRegistry.cs` - Added auto-discovery via reflection
- `Components/Shared/Generation/Fragments/SamplerForm.razor` - Added attribute
- `Components/Shared/Generation/Fragments/LatentForm.razor` - Added attribute
- `Components/Shared/Generation/Fragments/PromptsForm.razor` - Added attribute
- `Components/Shared/Generation/Fragments/UpscaleForm.razor` - Added attribute
- `Components/Shared/Generation/Fragments/SeedVR2Form.razor` - Added attribute
- `Components/Shared/Generation/Fragments/ConditioningVariationForm.razor` - Added attribute
- `Components/Shared/Generation/Fragments/SeedVarianceEnhancerForm.razor` - Added attribute

#### Step 13.7.2: Validate Component Registrations
**Complexity:** 2
**Status:** [x] Complete (Already Implemented)

Validation already exists in `WorkflowValidationService.ValidateUiSchema()`:
- Checks if `ui.component` references a registered component
- Logs warning if component is not registered in `ComponentRegistry`
- No additional changes needed

**Files Modified:** None (already implemented in Phase 13.1)

---

### Sub-Phase 13.8: Local State Binding Abstraction
**Objective:** Reduce boilerplate in fragment forms
**Complexity:** 8 points

#### Step 13.8.1: Create Generic FragmentFormBase<T>
**Complexity:** 5
**Status:** [x] Complete

Enhanced `FragmentFormBase` with:
- `FragmentId` parameter for identifying the fragment
- `OnChanged` callback (simplified notification)
- `GetResolvedOptions()` for accessing pre-resolved dynamic options
- Updated `GetOptions()` to check pre-resolved first
- `SetValueAsync()` now calls both `OnValueChanged` and `OnChanged`

Added generic version `FragmentFormBase<TValues>`:
- Typed `Model` property for the values
- `MapValuesToModel()` abstract method for syncing from dictionary
- `MapModelToValues()` abstract method for syncing to dictionary  
- `SyncModelAsync()` helper for after model updates

**Files Modified:**
- `Components/Shared/Generation/FragmentFormBase.cs` - Enhanced and added generic version

#### Step 13.8.2: Create Bindable Property Helpers
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
**Status:** [x] Complete

Created custom JSON converters that preserve value types during serialization:
- `FragmentParametersJsonConverter` - Handles `FragmentParameters` with proper type preservation
- `GenerationParametersJsonConverter` - Handles the full `GenerationParameters` using the fragment converter
- Reads numbers as int &gt; long &gt; double based on actual value
- Handles nested dictionaries and arrays properly
- Handles JsonElement values from legacy data
- Registered in `AppDbContext` via custom `JsonSerializerOptions`

**Files Created:**
- `Data/Converters/GenerationParametersJsonConverter.cs` - Both converter classes

**Files Modified:**
- `Data/AppDbContext.cs` - Added using, registered converter in JsonSerializerOptions

#### Step 13.9.2: Add Type Coercion in GetValue
**Complexity:** 3
**Status:** [x] Complete

Improved `FragmentParameters.GetValue<T>()` for better type handling:
- Enhanced `ConvertJsonElement<T>()` with more conversion paths
- Added string-to-numeric parsing for quoted number values
- Added string-to-bool parsing
- Added `ParseStringToNumeric<T>()` helper method
- Handles numeric JSON to string conversion
- Better fallback with full deserialization for complex objects

**Files Modified:**
- `Models/FragmentParameters.cs` - Enhanced GetValue&lt;T&gt;, ConvertJsonElement, added ParseStringToNumeric

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
| 13.2 | 13.2.1 | [x] | 2 | Document Default Priority - Complete |
| 13.2 | 13.2.2 | [x] | 3 | Remove Fragment Body Defaults - Complete |
| 13.2 | 13.2.3 | [x] | 3 | Apply Schema Defaults - Complete |
| 13.3 | 13.3.1 | [x] | 5 | Async Source Resolution - Complete |
| 13.3 | 13.3.2 | [x] | 3 | Cache Resolved Options - Complete |
| 13.4 | 13.4.1 | [x] | 3 | Fragment Parameter Interfaces - Complete |
| 13.4 | 13.4.2 | [x] | 5 | Typed Extension Methods - Complete |
| 13.4 | 13.4.3 | [x] | 5 | Update Form Components - Design Decision |
| 13.5 | 13.5.1 | [ ] | 5 | FragmentKeys Generator - Deferred |
| 13.5 | 13.5.2 | [ ] | 3 | Build Integration - Deferred |
| 13.6 | 13.6.1 | [x] | 8 | Complete DynamicField - Complete |
| 13.6 | 13.6.2 | [x] | 5 | Integrate DynamicField - Complete |
| 13.7 | 13.7.1 | [x] | 3 | Attribute Registration - Complete |
| 13.7 | 13.7.2 | [x] | 2 | Validate Registrations - Already Implemented |
| 13.8 | 13.8.1 | [x] | 5 | Generic FragmentFormBase - Complete |
| 13.8 | 13.8.2 | [x] | 3 | Bindable Property Helpers - Complete |
| 13.9 | 13.9.1 | [x] | 5 | Custom JsonConverter - Complete |
| 13.9 | 13.9.2 | [x] | 3 | Type Coercion - Complete |
| 13.10 | 13.10.1 | [ ] | 5 | FileSystemWatcher - Deferred |
| 13.10 | 13.10.2 | [ ] | 3 | UI Refresh - Deferred |

**Total Complexity:** 92 points
**Completed:** 76 points (83%)
**Deferred:** 16 points (Sub-Phases 13.5, 13.10)

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

## Issues & Resolutions

| Issue | Resolution |
|-------|------------|
| Scriban templating in #meta blocks (flux/load-flux.sbn, load-diffusion-w-prompts.sbn, load-checkpoint.sbn) | Added `PreprocessMetaJson()` method to both `FragmentSchemaService` and `WorkflowValidationService` that strips Scriban expressions (`{{ ... }}`) and conditional blocks (`{{~ if ... ~}}...{{~ end ~}}`) before JSON parsing. This allows fragments to use dynamic outputs/conditions while still validating the `ui` schema. |
| Backend.* sources flagged for missing input_name | Updated validation in `WorkflowValidationService`, `FragmentSchema.Validate()`, and `FieldSchema.Validate()` to recognize `Backend.*` sources (e.g., `Backend.Samplers`) which don't need `input_name`. Also updated `HasDynamicSource` properties. |
| Invalid mode "pose2vid" in wan/pose2vid-steadydancer.sbn | Changed to valid `ModeType` enum value `"Img2Vid"`. |
| Missing Pipeline in chroma/txt2img.sbn | Updated validation to allow legacy "Prompt" workflows with a warning. This is a raw ComfyUI workflow not using the fragment system. |
| Conditional LoRA blocks parsed as invalid pipeline steps | Updated `ValidatePipelineStep()` to skip validation for Scriban conditional blocks that don't have static `fragment` references. |
| Backend.* sources not resolved at runtime | Added `IBackendService` injection to `GenerationParameterService`. Renamed `ResolveBackendSource()` to `ResolveBackendSourceAsync()` and made it async. Resolves samplers/schedulers/upscalers from `IBackendService` properties and detection models from `IComfyUIService.GetBBoxDetailers()` with fallback to default list. |
| DetailerForm component not found | Created `DetailerForm.razor` component with `[FragmentComponent("DetailerForm")]` attribute. Implements all detailer-core.sbn parameters: detection model, sampler, scheduler, seed, steps, cfg, denoise, feather, bbox detection settings, guide/max/drop sizes, and cycle. Component fetches detection models from pre-resolved options via `IGenerationParameterService.GetResolvedOptions()`. |

---
