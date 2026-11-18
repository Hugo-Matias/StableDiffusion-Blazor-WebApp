# Phase 3 - Service Layer Refactoring (Complete Scriban Removal)

## Status
**Phase:** 3  
**Build Status:** ? Passing | **Tests:** Pending
**Phase Status:** ? COMPLETE

---

## Objective

**Complete removal of ALL Scriban dependencies** from the codebase. After this phase:
- Only C# `IWorkflowBuilder` workflows are functional
- Scriban templates (.sbn files) remain on disk for reference only (not loaded)
- Database only persists state for C# workflows
- Scriban NuGet package removed

**Breaking Change:** Users will only have access to Z-Image Txt2Img workflow until remaining workflows are converted in Phases 4-8.

---

## Execution Checklist

### Step 1: Remove Scriban Loading from WorkflowService
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Removed `_templateParser`, `_templateCache`, `_conditionValidator` dependencies
- Removed `LoadScribanWorkflows()` method
- `GetWorkflows()` now only discovers C# workflows via reflection
- Updated constructor to only require logger and fragmentSchemaService

---

### Step 2: Remove Scriban Composition Methods
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Removed `ComposeWorkflowFromScribanTemplate()` method
- Removed `ComposeWithScribanTemplate()` wrapper
- `ComposeWorkflowFromGenerationParameters()` now only uses C# builder path
- Removed `RenderFragment()` method
- Removed `RenderTemplate()` method
- Removed `GetFragmentIdFromStep()` and `GetFragmentIdFromContext()` methods

---

### Step 3: Remove Scriban Metadata/Condition Methods
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Removed `ExtractMetadata()` method
- Removed `EvaluateConditions()` method
- Removed `EvaluateCondition()` method
- Removed casing helper methods (`ToSnakeCase`, `ToPascalCase`, `ToCamelCase`, `ConvertCasing`)

---

### Step 4: Remove Pipeline/Schema Delegation
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Removed `GetPipelineSteps()` method
- Removed `ParsePipelineSteps()` method
- Removed `_pipelineCache` and related code
- `GetWorkflowFragmentSchemas()` now builds schemas from C# `FragmentMetadata`

---

### Step 5: Remove Asset Defaults Saving (Scriban-specific)
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Removed `SaveAssetDefaults()` method from interface and service
- Removed `FindWorkflowTemplatePath()` method
- Removed `EscapeJsonString()` helper
- Updated `WorkflowAssetsPanel.razor` to remove save defaults button

---

### Step 6: Clean IWorkflowService Interface
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Removed all obsolete methods
- Removed `ParsedPipelineStep` record
- Removed `LoadWorkflowTemplate()` method
- Interface now has clean regions: Builder Access, Discovery, Composition, Schema Access

---

### Step 7: Delete Scriban Service Files
**Complexity:** 2
**Status:** [x] Complete

#### Files Deleted
- `BlazorWebApp/Services/TemplateCacheService.cs`
- `BlazorWebApp/Services/WorkflowTemplateParser.cs`
- `BlazorWebApp/Services/FragmentConditionValidator.cs`
- `BlazorWebApp/Services/WorkflowValidationService.cs`
- `BlazorWebApp/Services/IWorkflowValidationService.cs`
- `BlazorWebApp/Services/FragmentConditionGenerator.cs`

---

### Step 8: Update GenerationParameterService
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Removed `InitializeFragmentsFromPipeline()` method
- `RestoreFromSavedState()` only handles C# workflows
- Removed pipeline step references
- `CreateFragmentWithDefaults()` uses C# builder metadata
- `DiscoverFragments()` only uses C# fragment metadata

---

### Step 9: Remove Scriban NuGet Package
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Removed `Scriban` package reference from `BlazorWebApp.csproj`
- Removed service registrations from `Program.cs`
- Build passes without Scriban

---

### Step 10: Update Test Files
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Updated `MockWorkflowServiceBuilder.cs` - simplified for C# workflow builders
- Updated `WorkflowServiceTests.cs` - removed Scriban-related tests, updated for new API

---

### Step 11: Delete FragmentSchemaService (Dead Code)
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- **Deleted** `BlazorWebApp/Services/FragmentSchemaService.cs` entirely
- The service was no longer needed - its cache was never populated
- `WorkflowService.GetWorkflowFragmentSchemas()` builds schemas directly from C# metadata
- Removed `IFragmentSchemaService` registration from `Program.cs`
- Removed `ClearSchemaCache()` from `IWorkflowService` interface
- Updated `WorkflowService` constructor - now only takes logger
- Updated test files to remove FragmentSchemaService dependency

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 - Remove Scriban Loading | [x] | 5 | WorkflowService refactored |
| 2 - Remove Composition Methods | [x] | 5 | All Scriban rendering removed |
| 3 - Remove Metadata Methods | [x] | 3 | Regex/condition code removed |
| 4 - Remove Pipeline/Schema | [x] | 3 | Schema now from C# metadata |
| 5 - Remove Asset Defaults | [x] | 2 | UI button removed |
| 6 - Clean Interface | [x] | 3 | IWorkflowService cleaned |
| 7 - Delete Service Files | [x] | 2 | 6 files deleted |
| 8 - Update GenParams Service | [x] | 5 | C# workflow only |
| 9 - Remove NuGet Package | [x] | 2 | Scriban removed |
| 10 - Update Test Files | [x] | 3 | Tests updated |
| 11 - Delete FragmentSchemaService | [x] | 2 | Dead service removed |
| **Total** | **100%** | **35** | **11/11 complete** |

---

## Files Deleted

- `BlazorWebApp/Services/TemplateCacheService.cs`
- `BlazorWebApp/Services/WorkflowTemplateParser.cs`
- `BlazorWebApp/Services/FragmentConditionValidator.cs`
- `BlazorWebApp/Services/WorkflowValidationService.cs`
- `BlazorWebApp/Services/IWorkflowValidationService.cs`
- `BlazorWebApp/Services/FragmentConditionGenerator.cs`
- `BlazorWebApp/Services/FragmentSchemaService.cs` ? **NEW**

## Files Modified

- `BlazorWebApp/Services/WorkflowService.cs` - Complete rewrite, C# only, no schema service dependency
- `BlazorWebApp/Services/IWorkflowService.cs` - Cleaned interface, removed ClearSchemaCache
- `BlazorWebApp/Services/GenerationParameterService.cs` - Removed pipeline code
- `BlazorWebApp/Components/Shared/Generation/WorkflowAssetsPanel.razor` - Removed save button
- `BlazorWebApp/Program.cs` - Removed all deleted service registrations
- `BlazorWebApp/BlazorWebApp.csproj` - Removed Scriban package
- `BlazorWebApp.Tests/MockBuilders/MockWorkflowServiceBuilder.cs` - Simplified, no schema service
- `BlazorWebApp.Tests/Services/WorkflowServiceTests.cs` - Updated tests, no schema service

---

## Breaking Changes

After Phase 3:
- **Only Z-Image Txt2Img workflow is available**
- Flux, Wan, Qwen, SD, Chroma workflows will NOT work until converted
- Any saved state for Scriban workflows will be ignored
- `.sbn` files remain on disk for reference but are not loaded
- SaveAssetDefaults functionality removed (C# workflows have defaults in code)

---

## Codebase Analysis (Post-Phase 3)

### Scriban Dependencies: FULLY REMOVED ?
- No Scriban NuGet package
- No `using Scriban` statements  
- No template parsing code
- All Scriban-related services deleted
- FragmentSchemaService deleted (was dead code)

### Service Layer: CLEAN ?
- `WorkflowService` - single dependency (logger)
- `IWorkflowService` - clean interface with 4 regions
- No unused services or interfaces

### Items Remaining for Phase 10
- Delete `.sbn` files from disk
- Update documentation (TEMPLATE_GUIDE.md, FRAGMENT_SCHEMA_GUIDE.md)

---

## Next Steps

Phase 4 will convert core shared fragments that are used by multiple workflows:
- UpscaleFragment
- DetailerFragment  
- LoraLoaderFragment
- ConditioningVariationFragment
- And more...

This enables faster conversion of remaining workflows in Phases 5-8.

---

**Phase Status:** ? COMPLETE
