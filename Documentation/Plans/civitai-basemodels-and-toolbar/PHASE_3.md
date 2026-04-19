# Phase 3 - Context-Aware Toolbar

## Status
**Phase:** 3  
**Build Status:** Not yet built | **Tests:** N/A

---

## Implementation Guidelines

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

---

## Objective

Refactor the top toolbar to be context-aware: show page-specific settings alongside the existing global settings based on the current route. First implementation: CivitAI page settings for toggling base model families/models.

---

## Context

- `TopToolbar.razor` - Current toolbar with folder/project/theme/state presets
- `MainLayout.razor` - Hosts toolbar in `MudExpansionPanel`, has `NavigationManager` injected
- Routes: `/` (gallery), `/civitai`, `/danbooru`, `/generate`, `/prompts`, `/resources`
- `AppSettings.cs` already has `DisabledFamilies` and `DisabledModels` on `CivitaiSettingsModel`
- `CivitaiService.BaseModelsData` has families, groups, and models data
- Events use pub/sub pattern via `EventService.cs`

---

## Execution Checklist

### Step 3.1: Add Route Context to TopToolbar
**Complexity:** 2
**Status:** [~] In Progress

#### Tasks
- [ ] Inject `NavigationManager` in `TopToolbar.razor`
- [ ] Determine current page route
- [ ] Add conditional rendering slot below existing global content

---

### Step 3.2: Create CivitAI Toolbar Settings Panel
**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `ToolbarCivitaiSettings.razor` component
- [ ] Show family toggles (enable/disable entire families)
- [ ] Show individual model toggles within families
- [ ] Persist changes to `AppSettings.DisabledFamilies` / `DisabledModels`
- [ ] Publish event so `CivitaiModelsPanel` refreshes its dropdown

---

### Step 3.3: Wire Up and Test
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Toolbar shows CivitAI settings only on `/civitai`
- [ ] Changes persist across sessions
- [ ] Base model dropdown reflects toggled families/models

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 3.1 | [~] | 2 | |
| 3.2 | [ ] | 5 | |
| 3.3 | [ ] | 2 | |

---

## Commit Checkpoints

- [ ] After Step 3.1 complete
- [ ] After Step 3.3 complete (full feature working)

---

**Phase Status:** In Progress [~]
