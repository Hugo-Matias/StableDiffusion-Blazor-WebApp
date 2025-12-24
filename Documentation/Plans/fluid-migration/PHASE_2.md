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

### Step 2.4: Update RenderFragment to Use Fluid
**Complexity:** 3  
**Status:** [~] In Progress

#### Tasks
- [ ] Replace regex-based meta extraction with Fluid render
- [ ] Remove `#meta...#end` regex pattern matching
- [ ] Use metadata from FluidRenderContext.CapturedMetadata
- [ ] Maintain backward compatibility for output format

---

### Step 2.5: Update RenderTemplate Method
**Complexity:** 3  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create Fluid-based `RenderTemplate()` method
- [ ] Handle ScriptObject to Dictionary conversion
- [ ] Preserve formatting option handling
- [ ] Add LoRA path resolver support

---

### Step 2.6: Verify Build and Existing Tests
**Complexity:** 2  
**Status:** [ ] Not Started

#### Tasks
- [ ] Run full build
- [ ] Run existing WorkflowServiceTests
- [ ] Fix any breaking changes
- [ ] Update test mocks as needed

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 2.1 |  x     | 1          | DI registration |
| 2.2 |  x     | 2          | Constructor injection |
| 2.3 |  x     | 5          | Core Fluid rendering |
| 2.4 |        | 3          | Replace regex extraction |
| 2.5 |        | 3          | Template rendering |
| 2.6 |        | 2          | Verification |

**Total Phase Complexity:** 16 points

---

## Commit Checkpoints

- [ ] After Step 2.2 complete (DI wired up)
- [ ] After Step 2.4 complete (fragment rendering migrated)
- [ ] After Step 2.6 complete (phase complete, verified)

---

## Issues & Resolutions

*(To be filled during implementation)*

---

## Files to Modify

| File | Changes |
|------|---------|
| `Program.cs` | Add IFluidTemplateService DI registration |
| `Services/WorkflowService.cs` | Inject and use FluidTemplateService |
| `Services/IWorkflowService.cs` | No changes expected |
| `BlazorWebApp.Tests/MockBuilders/MockWorkflowServiceBuilder.cs` | Add mock for IFluidTemplateService |
| `BlazorWebApp.Tests/Services/WorkflowServiceTests.cs` | Update CreateWorkflowService helper |

---

## Key Architecture Notes

### Before (Scriban + Regex)
```csharp
// Current flow in RenderFragment
var metaMatch = Regex.Match(fragmentText, @"#meta\s*([\s\S]*?)\s*#end");
if (metaMatch.Success)
{
    var metaJson = metaMatch.Groups[1].Value.Trim();
    var renderedMeta = RenderTemplate(metaJson, context, preserveFormatting: true);
    (outputs, conditions) = ExtractMetadata(renderedMeta);
    fragmentText = fragmentText.Replace(metaMatch.Value, "").Trim();
}
var rendered = RenderTemplate(fragmentText, context, preserveFormatting: false);
```

### After (Fluid)
```csharp
// New flow with FluidTemplateService
var (rendered, metadata) = await _fluidService.RenderAsync(fragmentText, parameters, nodeRegistry);
if (!string.IsNullOrEmpty(metadata))
{
    (outputs, conditions) = ExtractMetadata(metadata);
}
// No regex needed - meta block was automatically removed from output
```

---

**Phase Status:** Step 2.1 In Progress
