# Phase 7 - Scriban Removal & Cleanup

## Status
**Phase:** 7  
**Build Status:** [x] Passing | **Tests:** [x] Pending validation

---

## Objective

Remove all Scriban dependencies and cleanup legacy code. The codebase now uses only Fluid for template rendering.

**Goals:**
1. Delete TemplateCacheService
2. Remove Scriban-specific code from WorkflowService
3. Remove legacy sync methods marked `[Obsolete]`
4. Update Program.cs to remove Scriban precompilation
5. Update all services to support only Fluid `.liquid` templates
6. Clean build with zero Scriban references

---

## Context

### Current State
- **Phase 5 Complete:** All workflow templates converted to Fluid (except Chroma - excluded)
- **Phase 6 Skipped:** Manual testing deferred
- **Scriban Usage Remaining:** None - completely removed

---

## Execution Checklist

### Step 7.1: Delete TemplateCacheService
**Complexity:** 2  
**Status:** [x] Complete

### Step 7.2: Clean WorkflowService
**Complexity:** 8  
**Status:** [x] Complete

- [x] Remove `IsFluidTemplate` checks
- [x] Remove `.sbn` file loading
- [x] Only load `.liquid` files
- [x] Remove legacy comments

### Step 7.3: Update Program.cs
**Complexity:** 3  
**Status:** [x] Complete

- [x] Remove TemplateCacheService registration
- [x] Remove precompilation logic

### Step 7.4: Update Project Files
**Complexity:** 1  
**Status:** [x] Complete

- [x] Remove `.sbn` copy rules from csproj
- [x] Only copy `.liquid` files

### Step 7.5: Update Test Files
**Complexity:** 2  
**Status:** [x] Complete

### Step 7.6: Update FragmentSchemaService
**Complexity:** 5  
**Status:** [x] Complete

- [x] Only parse Fluid `{% meta %}...{% endmeta %}` syntax
- [x] Remove legacy `#meta...#end` support
- [x] Only enumerate `.liquid` fragment files
- [x] Update `ParseFragmentDefaults()` for Fluid syntax only

### Step 7.7: Update WorkflowTemplateParser
**Complexity:** 3  
**Status:** [x] Complete

- [x] Remove Scriban references from comments
- [x] Parse only Fluid default syntax
- [x] Rename method to `ParseDefaultValue()`

### Step 7.8: Remove Workflow.IsFluidTemplate
**Complexity:** 2  
**Status:** [x] Complete

- [x] Remove `IsFluidTemplate` property from `Workflow` model
- [x] All workflows are now Fluid by default

### Step 7.9: Build and Test
**Complexity:** 3  
**Status:** [x] Complete

- [x] Run full build - **PASSED**

---

## Files Modified

| File | Changes |
|------|---------|
| `Services/TemplateCacheService.cs` | Deleted |
| `Services/WorkflowService.cs` | Removed all Scriban/legacy code, only Fluid path |
| `Services/IWorkflowService.cs` | Renamed `RenderFragmentWithFluidAsync` to `RenderFragmentAsync` |
| `Services/FragmentSchemaService.cs` | Only Fluid `{% meta %}` syntax support |
| `Services/WorkflowTemplateParser.cs` | Removed Scriban references, only Fluid parsing |
| `Models/Workflow.cs` | Removed `IsFluidTemplate` property |
| `Program.cs` | Removed TemplateCacheService, removed precompilation |
| `BlazorWebApp.csproj` | Removed `.sbn` copy rules |
| `Tests/MockBuilders/MockWorkflowServiceBuilder.cs` | Removed TemplateCacheService |
| `Tests/Services/WorkflowServiceTests.cs` | Removed TemplateCacheService |

---

## Summary

**Phase 7 Complete**

All Scriban-related code has been completely removed from the codebase:
- No more `.sbn` file support
- No more `IsFluidTemplate` property
- No more legacy fallback code paths
- No more Scriban-related comments
- Only Fluid `.liquid` templates are supported

The Chroma workflow template has been excluded from the build entirely (not copied to output). It will require a separate planning session to convert to the Pipeline-based format.

**Migration Support Added:**
- Added automatic `.sbn` ? `.liquid` file path migration in `GenerationParameterService.RestoreFromSavedState()`
- This ensures saved workflow states from the database work correctly after the migration

**$foreach Template Syntax:**
- `$foreach` loops use `{% raw %}{{ $index }}{% endraw %}` to protect PipelineExpander variables from Fluid parsing
- Item property placeholders like `{% raw %}{{ lora.Name }}{% endraw %}` are similarly protected
- This prevents Fluid from attempting to parse variables intended for the PipelineExpander

---

**Phase Status:** [x] Complete
