# Phase 2 - Core Service Migration

## Status
**Phase:** 2  
**Build Status:** ? Passing | **Tests:** ? 50/50 Passing

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update This Document**
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

Integrate FluidTemplateService into WorkflowService, replacing the Scriban-based rendering with Fluid. This is the core architectural change that eliminates regex-based meta extraction.

---

## Context

- **Phase 1 Complete:** FluidTemplateService created with custom blocks/tags/filters
- **Current State:** WorkflowService uses Scriban + regex for fragment rendering
- **Target State:** WorkflowService uses FluidTemplateService for all template rendering
- **Strategy:** Instant cutoff (no feature flag) - replace Scriban calls directly

---

## Execution Checklist

### Step 2.1: Register FluidTemplateService in DI Container
**Complexity:** 1  
**Status:** [x] Complete

#### Tasks
- [x] Add `IFluidTemplateService` registration in `Program.cs`
- [x] Register as singleton (stateless, thread-safe with caching)

#### Notes
- Added `using BlazorWebApp.Services.Templating;`
- Registered after TemplateCacheService

---

### Step 2.2: Inject FluidTemplateService into WorkflowService
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Add `IFluidTemplateService` parameter to WorkflowService constructor
- [x] Store as readonly field `_fluidService`
- [x] Update test mocks to include IFluidTemplateService

#### Notes
- Updated `WorkflowServiceTests.cs` with new mock
- Updated `MockWorkflowServiceBuilder.cs` with FluidTemplateService injection
- All 50 tests passing (23 WorkflowService + 27 FluidTemplateService)

---

### Step 2.3: Create RenderFragmentWithFluid Method
**Complexity:** 5  
**Status:** [x] Complete

#### Tasks
- [x] Create new `RenderFragmentWithFluidAsync()` method
- [x] Build parameter dictionary from SubgraphContext
- [x] Call FluidTemplateService.RenderAsync
- [x] Extract metadata from captured context
- [x] Parse outputs and conditions from metadata JSON
- [x] Add method to IWorkflowService interface

#### Notes
- Method is async (returns `Task<>`)
- Uses existing `ExtractMetadata()` and `EvaluateConditions()` methods
- No regex needed for meta extraction - Fluid handles it natively
- Clean separation: Fluid renders, WorkflowService handles business logic

---

### Step 2.4: Create ComposeWorkflowFromGenerationParametersAsync
**Complexity:** 5  
**Status:** [x] Complete

#### Tasks
- [x] Create async version of `ComposeWorkflowFromGenerationParameters`
- [x] Replace `RenderFragment()` call with `RenderFragmentWithFluidAsync()`
- [x] Add method signature to IWorkflowService interface
- [x] Mark old sync method with `[Obsolete]` attribute
- [x] Update ComfyUIService to use async method

#### Notes
- Both call sites in ComfyUIService updated (`PostGenerationAsync` and `PostVideoGenerationAsync`)
- Old sync method kept with `[Obsolete]` for backward compatibility during migration
- Workflow template rendering still uses Scriban (only fragments use Fluid)
- File reading changed to async (`File.ReadAllTextAsync`)

---

### Step 2.5: Update RenderTemplate Method (Skipped)
**Complexity:** 3  
**Status:** [-] Skipped (not needed for Fluid path)

#### Notes
- `RenderTemplate` is only used by the old sync `RenderFragment` method
- The new async path uses FluidTemplateService directly
- Can be removed in Phase 7 cleanup

---

### Step 2.6: Verify Build and Tests
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Run full build - passing
- [x] Run WorkflowServiceTests (23 tests) - passing
- [x] Run FluidTemplateServiceTests (27 tests) - passing
- [x] Total: 50 tests passing

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 2.1 | ?      | 1          | DI registration |
| 2.2 | ?      | 2          | Constructor injection |
| 2.3 | ?      | 5          | RenderFragmentWithFluidAsync |
| 2.4 | ?      | 5          | ComposeWorkflowAsync |
| 2.5 | -      | 0          | Skipped |
| 2.6 | ?      | 2          | Verification |

**Total Phase Complexity:** 15 points (estimated 16)

---

## Commit Checkpoints

- [x] After Step 2.2 complete (DI wired up)
- [x] After Step 2.4 complete (async composition migrated)
- [x] After Step 2.6 complete (phase complete, verified)

---

## Issues & Resolutions

| Issue | Resolution |
|-------|------------|
| Missing opening parenthesis in `if` statement | Fixed syntax error |

---

## Files Modified

| File | Changes |
|------|---------|
| `Program.cs` | Added IFluidTemplateService DI registration |
| `Services/WorkflowService.cs` | Added FluidTemplateService injection, `RenderFragmentWithFluidAsync`, `ComposeWorkflowFromGenerationParametersAsync` |
| `Services/IWorkflowService.cs` | Added async method signatures |
| `Services/ComfyUIService.cs` | Updated to use `ComposeWorkflowFromGenerationParametersAsync` |
| `BlazorWebApp.Tests/Services/WorkflowServiceTests.cs` | Added FluidTemplateService mock |
| `BlazorWebApp.Tests/MockBuilders/MockWorkflowServiceBuilder.cs` | Added FluidTemplateService injection |

---

## Key Architecture Notes

### Migration Path
```
                    ???????????????????????????????????????????
                    ?            ComfyUIService               ?
                    ?                                         ?
                    ?  PostGenerationAsync()                  ?
                    ?  PostVideoGenerationAsync()             ?
                    ???????????????????????????????????????????
                                     ? calls
                                     ?
???????????????????????????????????????????????????????????????????????????
?                          WorkflowService                                ?
?                                                                         ?
?  ???????????????????????????????    ???????????????????????????????    ?
?  ? ComposeWorkflowAsync (NEW)  ?    ? ComposeWorkflow (OLD)       ?    ?
?  ? Uses Fluid                  ?    ? Uses Scriban + Regex        ?    ?
?  ? ? Active                   ?    ? ?? Obsolete                 ?    ?
?  ???????????????????????????????    ???????????????????????????????    ?
?                 ?                                                       ?
?                 ? calls per fragment                                    ?
?                 ?                                                       ?
?  ???????????????????????????????    ???????????????????????????????    ?
?  ? RenderFragmentWithFluidAsync?    ? RenderFragment (OLD)        ?    ?
?  ? Uses FluidTemplateService   ?    ? Uses Scriban + Regex        ?    ?
?  ? ? Active                   ?    ? ?? Obsolete                 ?    ?
?  ???????????????????????????????    ???????????????????????????????    ?
?                 ?                                                       ?
???????????????????????????????????????????????????????????????????????????
                  ? calls
                  ?
???????????????????????????????????????????????????????????????????????????
?                      FluidTemplateService                               ?
?                                                                         ?
?  RenderAsync() ? {% meta %} captured to side-channel                   ?
?               ? {% get_ref %} resolved from NodeRegistry               ?
?               ? {{ var | json }} proper JSON encoding                   ?
?                                                                         ?
?  ? No regex extraction needed                                          ?
?  ? Native Liquid parsing handles meta blocks                           ?
???????????????????????????????????????????????????????????????????????????
```

---

## Phase Summary

### Accomplishments
1. ? Registered `FluidTemplateService` in DI container
2. ? Injected `IFluidTemplateService` into `WorkflowService`
3. ? Created `RenderFragmentWithFluidAsync()` - async fragment rendering with Fluid
4. ? Created `ComposeWorkflowFromGenerationParametersAsync()` - async workflow composition
5. ? Updated `ComfyUIService` to use async methods
6. ? All 50 tests passing

### Key Validation
- **Core path migrated:** ComfyUI generation now uses Fluid for fragment rendering
- **No regex for meta extraction:** Fluid natively handles `{% meta %}...{% endmeta %}`
- **Backward compatible:** Old sync methods marked `[Obsolete]` but still available
- **Tests passing:** 23 WorkflowServiceTests + 27 FluidTemplateServiceTests

### Ready for Phase 3
- Convert template files from `.sbn` (Scriban) to `.liquid` (Fluid) syntax
- Start with simple fragments without loops/conditionals

---

**Phase Status:** ? Complete - Ready for Phase 3 (Template Conversion)
