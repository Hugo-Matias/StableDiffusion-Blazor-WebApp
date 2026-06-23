# Phase 2 - Compact Project Panel

## Status

**Phase:** 2  
**Build Status:** Succeeded with alternate output directory; standard build task blocked by running app file lock  
**Tests:** Focused Razor/CSS diagnostics clean

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**
   - Do NOT proceed until testing is complete.
   - User must approve before moving to the next phase.
   - Build runs only after user requests or after completing all file edits.

### Progress Symbols

- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)

**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules

- Keep expanded project panel behavior intact.
- Compact mode must preserve folder selection, project selection, project creation, project edit/delete, cover removal, folder deletion, folder ordering, and filter access.
- Use compact `Variant.Text` / `Dense` controls and icon-only actions where possible.
- Keep changes focused on project/folder panel density; image card density is Phase 3.

---

## Objective

Reduce vertical space used by folders/projects while preserving project selection and management actions.

---

## Context

Phase 1 added persisted `ProjectPanelMode`, defaulting to `Expanded`. This phase wires that state into `GallerySettings` and adds compact project-card rendering.

Relevant files:

- `BlazorWebApp/Components/Gallery/GallerySettings.razor`
- `BlazorWebApp/Components/Gallery/GallerySettings.razor.css`
- `BlazorWebApp/Components/Shared/Project/ProjectCard.razor`
- `BlazorWebApp/Components/Shared/Project/ProjectCard.razor.css`
- `Documentation/Plans/gallery-density-optimization/MAIN_PLAN.md`

---

## Execution Checklist

### Step 1: Add Compact Panel Branch

**Complexity:** 3
**Status:** [x] Complete and tested

#### Tasks

- [x] Add compact/expanded panel toggle.
- [x] Preserve current expanded tabs and project strip.
- [x] Render compact folder selector with `All` option.
- [x] Render compact toolbar actions and menu.

#### Changes Made

Added a compact/expanded branch in `GallerySettings.razor`. Expanded mode keeps the existing `MudTabs` folder tabs and project strip. Compact mode replaces the folder tabs with a dense `MudSelect`, shows the selected project context inline, and moves lower-frequency folder/project actions into a compact toolbar/menu.

---

### Step 2: Add Compact Project Card Density

**Complexity:** 3
**Status:** [x] Complete and tested

#### Tasks

- [x] Add compact parameter/classes to `ProjectCard`.
- [x] Add smaller cover dimensions, title, selected state, and action controls.
- [x] Keep expanded card styling unchanged.

#### Changes Made

Added a `Compact` parameter to `ProjectCard` and compact CSS rules for smaller card dimensions, tighter title treatment, compact selected state, and reduced hover/action footprint. Expanded card sizing and behavior remain unchanged.

---

### Step 3: Wire Actions And Persistence

**Complexity:** 2
**Status:** [x] Complete and tested

#### Tasks

- [x] Persist project panel mode changes through `State.SaveState()`.
- [x] Preserve folder and project selection events.
- [x] Keep folder reorder available in compact mode.

#### Changes Made

Wired the compact/expanded toggle to persisted `ProjectPanelMode`. Compact folder selection continues to call `Gallery.SetCurrentFolder`, compact project cards continue to call `Gallery.SetCurrentProject`, and compact folder reorder actions route through the existing folder ordering logic.

---

### Step 4: Validate Phase 2

**Complexity:** 2
**Status:** [x] Complete and tested

#### Tasks

- [x] Run focused diagnostics on touched source files.
- [x] Run project build after edits.
- [x] Record unrelated warnings separately.

#### Changes Made

Focused diagnostics are clean for the touched Razor and CSS files. The normal build task reached compilation but failed while copying locked files from the currently running `BlazorWebApp` process. A validation build using an alternate output directory succeeded.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                        |
| ---- | ------ | ---------- | ---------------------------------------------------------------------------- |
| 1    | [x]    | 3          | Compact `GallerySettings` branch implemented while preserving expanded mode. |
| 2    | [x]    | 3          | `ProjectCard` supports compact rendering through a scoped parameter/class.   |
| 3    | [x]    | 2          | Mode persistence and compact folder/project actions are wired.               |
| 4    | [x]    | 2          | Focused diagnostics clean; alternate-output build succeeded.                 |

---

## Issues & Resolutions

- Focused diagnostics initially reported nullable warnings around folders/projects and stale `ProjectCard` initialization warnings. The component code now guards nullable folder/project collections and initializes compact-touched fields safely.
- MudBlazor renders `MudSelect` with an outer `.mud-select` wrapper while applying the component `Class` to an inner input-control element. The compact folder selector is now wrapped in a local `.compact-folder-select-frame`, and sizing rules use `::deep` only for MudBlazor internals.
- Compact project rail sizing now uses `flex-flow: row nowrap` plus a deep `.project-card.compact` flex rule so child component CSS isolation cannot let the rail collapse into a stacked column.
- The standard build task failed because a running `BlazorWebApp` process locked `bin/Debug/net8.0/BlazorWebApp.exe` and `BlazorWebApp.dll`. Validation was rerun with `OutDir=.\artifacts\phase2-build\` and later `.\artifacts\compact-fix-build\`; both alternate-output builds succeeded, and temporary output was removed afterward.
- Existing package vulnerability warnings and broader repo nullability warnings remain unrelated to this phase.

---

## Commit Checkpoints

- [x] After Step 1 complete
- [x] After Step 2 complete
- [x] After Step 3 complete
- [x] After Step 4 validation complete

---

## Phase Summary

Phase 2 adds a persisted compact project panel mode for the Gallery. Compact mode reduces vertical space by replacing folder tabs with a dense selector, grouping secondary actions into icon/menu controls, and rendering smaller compact project cards. Expanded mode remains available and keeps the original tab/card experience intact.

**Phase Status:** Complete [x]
