# Phase 3 - Service Layer Refactoring

## Status
**Phase:** 3  
**Build Status:** Pending | **Tests:** Pending

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**
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
- **NO BACKWARDS COMPATIBILITY** - Remove Scriban code immediately after replacement
- **DELETE deprecated files** after successful refactoring

---

## Objective

Refactor `WorkflowService` and related services to fully support the C# fluent workflow API while maintaining backwards compatibility with remaining Scriban templates during the transition. This phase focuses on cleanup and optimization, NOT breaking remaining workflows.

---

## Context

### Dependencies
- Phase 1 complete (Core Infrastructure) [x]
- Phase 1.5 complete (Type-Safe Enhancements) [x]
- Phase 2 complete (Z-Image Txt2Img Conversion) [x]

### Current State
- `WorkflowService` has been updated to:
  - Discover C# `IWorkflowBuilder` implementations via reflection
  - Route to `IWorkflowBuilder.Build()` for C# workflows
  - Fall back to Scriban template rendering for legacy workflows
- `GenerationParameterService` has been updated to:
  - Initialize fragments from C# `IWorkflowBuilder.GetFragments()` for C# workflows
  - Fall back to pipeline parsing for Scriban workflows
  - Build `FragmentSchema` from `FragmentMetadata` for UI rendering

### Remaining Scriban Infrastructure
The following code/files still exist and will be cleaned up in this phase or later:

**Services that can be cleaned up (have C# alternatives):**
- `WorkflowService.RenderFragment()` - Used by Scriban templates only
- `WorkflowService.RenderTemplate()` - Scriban rendering
- `WorkflowService.ExtractMetadata()` - Regex parsing of #meta blocks
- `WorkflowService.EvaluateConditions()` - Scriban condition evaluation
- `WorkflowTemplateParser.cs` - Scriban template parsing
- `TemplateCacheService.cs` - Scriban template caching (may still be needed)

**Services that need to stay (still used by remaining workflows):**
- Pipeline parsing in `GetPipelineSteps()` - Still needed for Scriban workflows
- `FragmentSchemaService` - Still parses #meta blocks for Scriban fragments
- Schema parsing - Still needed until all fragments are converted

### Strategy
**Incremental cleanup**: Remove code that is provably unused, keep code needed by remaining Scriban workflows until those workflows are converted. Focus on:
1. Cleaning up duplicate/dead code paths
2. Consolidating discovery logic
3. Removing unnecessary regex patterns
4. Simplifying service interfaces

---

## Execution Checklist

### Step 1: Audit Current WorkflowService Usage
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Document which methods are used by C# workflows
- [ ] Document which methods are used by Scriban workflows
- [ ] Identify truly dead code that can be removed
- [ ] Create removal plan that won't break remaining workflows

#### Files to Analyze
- `BlazorWebApp/Services/WorkflowService.cs`
- `BlazorWebApp/Services/IWorkflowService.cs`
- `BlazorWebApp/Services/GenerationParameterService.cs`

---

### Step 2: Clean Up WorkflowService Discovery
**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks
- [ ] Consolidate workflow discovery logic
- [ ] Ensure C# workflows are discovered first and take precedence
- [ ] Remove redundant discovery code paths
- [ ] Add logging for workflow discovery debugging
- [ ] Test that all workflows (C# and Scriban) are still discovered

---

### Step 3: Clean Up ComposeWorkflow Method
**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks
- [ ] Review `ComposeWorkflowFromGenerationParameters()` for dead code
- [ ] Ensure C# path is clean and efficient
- [ ] Keep Scriban path working for remaining templates
- [ ] Add comments distinguishing C# vs Scriban paths
- [ ] Test workflow composition for both types

---

### Step 4: Review and Clean Interface
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Review `IWorkflowService` interface
- [ ] Mark Scriban-specific methods with `[Obsolete]` attribute
- [ ] Add new interface methods needed for C# workflows
- [ ] Document migration path in interface comments
- [ ] Ensure interface is clean and well-documented

---

### Step 5: Clean Up GenerationParameterService
**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks
- [ ] Review `InitializeFromWorkflowInternal()` for dead code
- [ ] Ensure C# workflow path is efficient
- [ ] Review `DiscoverFragments()` for optimization
- [ ] Clean up `BuildSchemaFromMetadata()` if needed
- [ ] Test fragment initialization for both workflow types

---

### Step 6: Evaluate Template Services for Removal
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Check if `TemplateCacheService` is still needed
- [ ] Check if `WorkflowTemplateParser` can be deprecated
- [ ] Check if `FragmentConditionValidator` is still used
- [ ] Document which services can be removed after all workflows convert
- [ ] Add `[Obsolete]` attributes to deprecated services

---

### Step 7: Test Full Application
**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks
- [ ] Test Z-Image workflow (C#) generates and runs
- [ ] Test at least one Scriban workflow still works
- [ ] Test workflow switching between C# and Scriban
- [ ] Test state persistence across workflow types
- [ ] Verify no regressions in UI

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 - Audit Usage | [ ] | 3 | |
| 2 - Clean Discovery | [ ] | 5 | |
| 3 - Clean Compose | [ ] | 5 | |
| 4 - Clean Interface | [ ] | 3 | |
| 5 - Clean GenParams | [ ] | 5 | |
| 6 - Evaluate Services | [ ] | 3 | |
| 7 - Test Application | [ ] | 5 | |
| **Total** | **0%** | **29** | **0/7 complete** |

---

## Issues & Resolutions

(None yet)

---

## Key Technical Decisions

### 1. Keep Scriban Path Working
**Decision:** Do NOT remove Scriban rendering code yet
**Rationale:** Other workflows (Flux, Wan, Qwen, SD, Chroma) still use Scriban templates

### 2. Use [Obsolete] Attribute
**Decision:** Mark deprecated methods with `[Obsolete("Use IWorkflowBuilder instead")]`
**Rationale:** Provides IDE warnings without breaking compilation

### 3. Incremental Cleanup
**Decision:** Clean up code incrementally, not all at once
**Rationale:** Reduces risk of breaking working functionality

---

## Files Expected to be Modified

- `BlazorWebApp/Services/WorkflowService.cs` - Main refactoring target
- `BlazorWebApp/Services/IWorkflowService.cs` - Interface cleanup
- `BlazorWebApp/Services/GenerationParameterService.cs` - Minor cleanup

## Files Expected to be Deprecated (NOT Deleted Yet)

- `BlazorWebApp/Services/WorkflowTemplateParser.cs` - Mark obsolete
- `BlazorWebApp/Services/TemplateCacheService.cs` - Evaluate necessity
- `BlazorWebApp/Services/FragmentConditionValidator.cs` - Mark obsolete

---

## Next Steps After Phase 3

After Phase 3, the service layer will be clean and optimized. Phase 4 focuses on converting core shared fragments used by multiple workflows. This will enable faster conversion of remaining workflows in Phases 5-8.

---

**Phase Status:** Not Started
