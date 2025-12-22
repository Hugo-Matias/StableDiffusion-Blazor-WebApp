# Phase 4 - Dynamic Form Components

## Status
**Phase:** 4  
**Build Status:** &check; Passing | **Tests:** 23/23 (WorkflowService) - 2 unrelated failures in WildcardService

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

Create reusable Blazor components that render UI based on fragment schemas. This includes dynamic field rendering for experimental nodes and base components for designed forms.

---

## Context

### Dependencies
- Phase 1-3 complete
- Models: `FragmentSchema`, `FieldSchema`, `ParameterConstraints`, `FragmentParameters`
- Services: `IGenerationParameterService`, `IComponentRegistry`, `IWorkflowService`

### Component Location
`BlazorWebApp/Components/Shared/Generation/`

### Reference
- Existing components in `Components/Shared/Generation/` for patterns
- AppSettings.cs for constraint patterns
- FRAGMENT_SCHEMA_GUIDE.md for schema format

---

## Execution Checklist

### Step 4.1: Create DynamicField.razor
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Created `DynamicField.razor` component
- Implemented 9 field types: slider, numeric, seed, select, text, textarea, checkbox, switch, color
- Source resolution for Backend.Samplers, Backend.Schedulers, Backend.Upscalers
- Two-way binding via Values dictionary and OnValueChanged callback

---

### Step 4.2: Create DynamicFragmentForm.razor
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `DynamicFragmentForm.razor` component
- Iterates fields from FragmentSchema.Fields
- Supports nested groups (collapsible and non-collapsible)
- Visibility conditions based on parameter values

---

### Step 4.3: Create FragmentFormContainer.razor
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `FragmentFormContainer.razor` component
- Collapsible expansion panel with icon and title
- Enable/disable toggle for fragments
- Add instance button for chainable fragments
- Respects defaultCollapsed from schema

---

### Step 4.4: Create SourcesPanel.razor
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Created `SourcesPanel.razor` for multi-source tabbed interface
- Created `SourceItem.razor` for individual image/video inputs
- **Reuses existing `ImageInput` component** for images (drag/drop, clipboard, editor integration)
- Custom video handling with file upload
- Edit/BlankCanvas callbacks for image editor integration

---

### Step 4.5: Create FragmentFormBase.razor.cs
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `FragmentFormBase.cs` base class
- Typed value accessors: GetInt, GetLong, GetDouble, GetFloat, GetString, GetBool
- Constraint accessors: GetMinInt, GetMaxInt, GetStepInt, GetMinDouble, etc.
- SetValue/SetValueAsync with change notification
- GetConstraints and GetOptions helpers

---

### Step 4.6: Register initial components in ComponentRegistry
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Updated ComponentRegistry to register components by type
- Registered existing components: ConditioningVariationForm, SeedVarianceEnhancerForm
- Created and registered new stub components: PromptsFormNew, SamplerFormNew, DetailerFormNew, SeedVR2FormNew

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 4.1 | &check; | 5 | DynamicField.razor |
| 4.2 | &check; | 3 | DynamicFragmentForm.razor |
| 4.3 | &check; | 3 | FragmentFormContainer.razor |
| 4.4 | &check; | 5 | SourcesPanel.razor + SourceItem.razor |
| 4.5 | &check; | 3 | FragmentFormBase.cs |
| 4.6 | &check; | 2 | ComponentRegistry registration |

---

## Design Decisions

### Value Binding Pattern

Components receive `FragmentParameters` and work with `Values` dictionary:
```csharp
// In component
private int Steps
{
    get => Values.GetValueOrDefault("steps", 20) as int? ?? 20;
    set => SetValue("steps", value);
}

private void SetValue(string key, object? value)
{
    Values[key] = value;
    OnValueChanged.InvokeAsync((key, value));
}
```

### Source Resolution Pattern

For select fields with `source` property:
```csharp
private IEnumerable<string> GetOptions(FieldSchema field)
{
    if (field.Options != null)
        return field.Options;
    
    if (!string.IsNullOrEmpty(field.Source))
        return SourceResolver.Resolve(field.Source);
    
    return Enumerable.Empty<string>();
}
```

---

## Issues & Resolutions

### Issue 1: Missing IBackendService properties
**Impact:** Build error in DynamicField.razor
**Resolution:** Removed references to non-existent Vaes and DetectionModels properties. Added TODO comment for future addition.

### Issue 2: Duplicate OnClick with stopPropagation
**Impact:** Build error in FragmentFormContainer.razor
**Resolution:** Changed to lambda expression for OnClick handler.

### Issue 3: Case-sensitive method call typo
**Impact:** Build error - `toLowerInvariant` instead of `ToLowerInvariant`
**Resolution:** Fixed typo.

### Issue 4: SourceItem code duplication
**Discussion:** Can we reuse ImageInput for SourceItem?
**Resolution:** Refactored SourceItem to reuse ImageInput component for image sources, gaining drag/drop, clipboard paste, and image editor integration. Video sources keep custom file upload.

---

## Commit Checkpoints

- [x] After Step 4.3 complete (core dynamic components)
- [x] After Step 4.6 complete (phase complete)

---

## Phase Summary

### Accomplishments
1. Created `DynamicField.razor` - renders 9 field types from schema
2. Created `DynamicFragmentForm.razor` - renders all fields with grid layout
3. Created `FragmentFormContainer.razor` - collapsible wrapper with enable/disable
4. Created `SourcesPanel.razor` and `SourceItem.razor` - input image/video handling
5. Created `FragmentFormBase.cs` - base class for designed components
6. Created 4 new stub components: PromptsFormNew, SamplerFormNew, DetailerFormNew, SeedVR2FormNew
7. Updated ComponentRegistry with all component registrations

### Files Created

| File | Purpose |
|------|---------|
| `Components/Shared/Generation/DynamicField.razor` | Single field renderer |
| `Components/Shared/Generation/DynamicFragmentForm.razor` | Fragment form renderer |
| `Components/Shared/Generation/FragmentFormContainer.razor` | Collapsible container |
| `Components/Shared/Generation/SourcesPanel.razor` | Multi-source panel |
| `Components/Shared/Generation/SourceItem.razor` | Individual source input |
| `Components/Shared/Generation/FragmentFormBase.cs` | Base class for forms |
| `Components/Shared/Generation/PromptsFormNew.razor` | New prompts form |
| `Components/Shared/Generation/SamplerFormNew.razor` | New sampler form |
| `Components/Shared/Generation/DetailerFormNew.razor` | New detailer form |
| `Components/Shared/Generation/SeedVR2FormNew.razor` | New SeedVR2 form |

### Files Modified

| File | Changes |
|------|---------|
| `Services/ComponentRegistry.cs` | Added using, registered all components |

### Build Status
&check; All builds passing

---

**Phase Status:** Complete &check;

---

## Next Phase

**Phase 5: Unified Generation Page** - Create single generation page that works for all modes
