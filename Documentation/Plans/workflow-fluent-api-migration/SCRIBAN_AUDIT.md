# Scriban Legacy Code Audit

## Purpose
This document audits all files in the codebase that may contain Scriban-related logic or deprecated code that should be removed as part of the workflow migration.

## Audit Status
**Date Created:** Phase 4
**Build Status:** ? Passing

---

## Summary of Findings

### Critical Issues Found
1. **`OrchestratorService.SetDefaultBaseModel()`** - Uses `workflow.Pipeline` which is deprecated Scriban logic
2. **Comments referencing `.sbn`** - Several files have outdated comments mentioning `.sbn` files

### Already Cleaned
1. **`Workflow.cs`** - Removed duplicate `NodeRegistry` and `SubgraphContext` classes

---

## Files Audited

### Services Directory

| File | Status | Contains Deprecated Code | Action Required |
|------|--------|-------------------------|-----------------|
| `WorkflowService.cs` | ? Clean | No | None |
| `OrchestratorService.cs` | ?? **HAS ISSUES** | `SetDefaultBaseModel()` uses `workflow.Pipeline` | Remove or update method |
| `GenerationParameterService.cs` | ?? Needs Review | TBD | Review for pipeline references |
| `DatabaseService.cs` | ? Clean | No | None |
| `StateService.cs` | ? Clean | No | None |
| `IOService.cs` | ? Clean | No | None |

### Models Directory

| File | Status | Contains Deprecated Code | Action Required |
|------|--------|-------------------------|-----------------|
| `Workflow.cs` | ? Cleaned | Removed NodeRegistry, SubgraphContext | ? Complete |
| `FragmentKeys.cs` | ?? Comments only | Comment mentions `.sbn` | Update comment |
| `FragmentParameters.cs` | ?? Comments only | Comment mentions `.sbn` | Update comment |
| `GenerationParameters.cs` | ?? Comments only | Comment mentions "Pipeline" | Update comment |
| `FragmentSchema.cs` | ?? Needs Review | TBD | Review for Scriban parsing |
| `FieldSchema.cs` | ?? Needs Review | TBD | Review for Scriban validation |

### Workflows Directory

| File | Status | Contains Deprecated Code | Action Required |
|------|--------|-------------------------|-----------------|
| `Builders/NodeRegistry.cs` | ? Clean | No | None |
| `Builders/NodeBuilder.cs` | ? Clean | No | None |
| `Builders/ComfyWorkflowBuilder.cs` | ? Clean | No | None |
| `Models/IWorkflowBuilder.cs` | ? Clean | No | None |
| `Models/IFragmentBuilder.cs` | ? Clean | No | None |
| `Models/FragmentMetadata.cs` | ?? Comments only | Comment mentions `.sbn` | Update comment |
| `Models/WorkflowMetadata.cs` | ?? Comments only | Comment mentions `.sbn` | Update comment |

---

## Detailed File Analysis

### BlazorWebApp/Services/OrchestratorService.cs
**Status:** ?? **NEEDS FIX**

**Issue:** `SetDefaultBaseModel()` method (lines 229-243) uses `workflow.Pipeline`:
```csharp
if (workflow?.Pipeline == null) return;

var defaultModel = workflow.Pipeline
    .Select(s => modelKeys.FirstOrDefault(k => s.Parameters?.ContainsKey(k) == true))
    // ...
```

**Problem:** C# workflows don't use `Pipeline` - they have their defaults in the `WorkflowMetadata.Assets` property.

**Solution:** Either:
1. Remove this method entirely (preferred - C# workflows handle defaults themselves)
2. Update to use `workflow.Assets?.FirstOrDefault(a => a.Type == AssetType.DiffusionModel)?.DefaultValue`

---

### BlazorWebApp/Models/Workflow.cs
**Status:** ? CLEANED

**Removed Classes:**
- `NodeRegistry` - Duplicate of Workflows.Builders.NodeRegistry
- `SubgraphContext` - Used only for Scriban subgraph rendering

**Remaining Classes (kept for compatibility):**
- `Workflow` - UI model for workflow metadata
- `WorkflowSource` - Input source definitions
- `WorkflowStep` - May be removable if no longer referenced
- `OutputMapping` - May be removable if no longer referenced

---

### Comment-Only Issues (Low Priority)

These files have outdated comments but no functional issues:

1. **FragmentKeys.cs** (line 5): "derived from the fragment .sbn schemas"
2. **FragmentParameters.cs** (line 13): "e.g., sampler.sbn"
3. **FragmentMetadata.cs** (line 8): "replaces the #meta block parsed from Scriban .sbn files"
4. **WorkflowMetadata.cs** (line 10): "replaces the parsed metadata from Scriban .sbn files"
5. **GenerationParameters.cs** (line 11): "Keys match Pipeline[].id"

---

## Action Items

### Priority 1 (Required for Clean Build)
- [x] Remove duplicate `NodeRegistry` from `Workflow.cs`
- [x] Remove `SubgraphContext` from `Workflow.cs`
- [x] Add `Merge` method to `NodeRegistry` in Builders
- [x] Add `GetReference` method to `NodeRegistry` in Builders
- [x] Fix test file to use correct `NodeRegistry` namespace

### Priority 2 (Remove Deprecated Logic)
- [x] Fix `OrchestratorService.SetDefaultBaseModel()` - now uses `workflow.Assets` instead of deprecated `Pipeline`
- [ ] Review if `WorkflowStep` and `OutputMapping` are still needed
- [ ] Check `GenerationParameterService` for any pipeline references

### Priority 3 (Cleanup Comments)
- [ ] Update comments in `FragmentKeys.cs`
- [ ] Update comments in `FragmentParameters.cs`
- [ ] Update comments in `FragmentMetadata.cs`
- [ ] Update comments in `WorkflowMetadata.cs`
- [ ] Update comments in `GenerationParameters.cs`

---

## Files Already Deleted (Phase 3)

- `TemplateCacheService.cs`
- `WorkflowTemplateParser.cs`
- `FragmentConditionValidator.cs`
- `WorkflowValidationService.cs`
- `IWorkflowValidationService.cs`
- `FragmentConditionGenerator.cs`
- `FragmentSchemaService.cs`

---

## Changelog

| Action | File | Details |
|--------|------|---------|
| Cleaned | `Workflow.cs` | Removed duplicate NodeRegistry and SubgraphContext |
| Added | `NodeRegistry.Merge()` | Added missing Merge method |
| Added | `NodeRegistry.GetReference()` | Added for test compatibility |
| Added | `NodeRegistry.GetOutput()` | Added alias for GetRef |
| Added | `NodeBuilder.ClassType()` | Added alias for Type() |
| Added | `NodeBuilder.InputFromNode()` | Added direct node reference method |
| Added | `NodeBuilder.InputFromRegistry()` | Added registry-based input method |
| Fixed | Test file | Updated to use `BlazorWebApp.Workflows.Builders.NodeRegistry` |
