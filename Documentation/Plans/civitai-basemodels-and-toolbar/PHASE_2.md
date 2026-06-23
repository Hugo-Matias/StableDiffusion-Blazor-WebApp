# Phase 2 - Grouped Base Model Selector

## Status
**Phase:** 2  
**Build Status:** Not yet built | **Tests:** N/A

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

---

## Objective

Update the CivitAI model search panel to display base models grouped by family (e.g., "Stability AI", "Black Forest Labs", "Tencent") using `MudSelectGroup`, matching CivitAI's own UI pattern.

---

## Context

- Data source: `CivitaiService.BaseModelsData` loaded from `Data/CivitAI/basemodels.json`
- Each model has optional `family` and `familyDisplayName` fields
- Models without a family should appear ungrouped
- Hidden models (`hidden: true`) are already filtered out in Phase 1
- The API `baseModels` query param uses the model `name` string — grouping is purely UI
- Target file: `BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor`

---

## Execution Checklist

### Step 2.1: Update MudSelect with Grouped Headers
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Group models by `familyDisplayName` using disabled `MudSelectItem` headers with CSS styling
- [x] Models without a family appear under "Other" group
- [x] "All" option remains at the top with toggle behavior (deselects others / gets deselected)
- [x] Multi-select support with `IEnumerable<string>` for API
- [x] Backward-compatible `StringOrListConverter` for legacy DB records

#### Changes Made
- `CivitaiModelsPanel.razor` - Multi-select with disabled header items, `HandleBaseModelsChanged` for All toggle logic
- `CivitaiModelsPanel.razor.css` - CSS for group headers (hidden checkbox, uppercase overline, border separator)
- `CivitaiModelsRequest.cs` - `BaseModels` changed to `IEnumerable<string>`
- `AppState.cs` - `BaseModels` changed to `List<string>` with `[JsonConverter(typeof(StringOrListConverter))]`
- `CivitaiService.cs` - API query builder sends multiple `baseModels=` params
- `AppSettings.cs` - Added `DisabledFamilies` and `DisabledModels` lists
- `Data/Converters/StringOrListConverter.cs` - Created for backward compatibility

#### Notes
- MudBlazor v6.1.8 does not support `MudSelectGroup` (v7+ only)
- MudBlazor v7 upgrade attempted but 100+ breaking changes across codebase (deferred)
- Used disabled `MudSelectItem` with CSS as v6-compatible alternative

---

### Step 2.2: Verify Search/Filter Behavior
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Build passes
- [x] Selecting a grouped model correctly sets the `baseModels` API param
- [x] All toggle works correctly

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 2.1 | [x] | 3 | Disabled MudSelectItem headers (v6 compat) |
| 2.2 | [x] | 1 | Build passes, API works |

---

## Commit Checkpoints

- [x] After Step 2.2 complete

---

**Phase Status:** Complete [x]
