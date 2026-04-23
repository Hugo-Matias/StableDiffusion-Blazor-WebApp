# Phase 1 - Sidebar Nav Refactor

## Status

**Phase:** 1
**Build Status:** Passing (compile clean; prior build failure was an MSBuild file-lock from the running app, not a compile error)
**Phase Status:** Complete

---

## Objective

Replace the inner `MudTabs` in the old `LLMMainPanel` with a collapsible sidebar navigation menu, extract the three existing tabs (Process, System Prompts, History) into their own view components, and persist nav state in `AppState.Prompts.LLM`.

---

## Context

- Builds on the existing LLM Tools tab under `Pages/Prompts.razor`.
- All state persisted via the `State.AppState` JSON column (see `.github/copilot-instructions.md` persistence conventions). No EF migration is required - `AppState` is serialized as JSON, so adding new nested classes/properties is transparent.
- All events flow through `EventService` (pub/sub convention).
- New view components live under `Components/Prompts/LLM/Views/`.

---

## Execution Checklist

### Step 1: Add `LLMState` sub-state to `AppState.Prompts`

**Complexity:** 2
**Status:** [x]

#### Changes Made

- `BlazorWebApp/Models/AppState.cs` - added `AppStatePromptsLLM` class (`ActiveViewId`, `IsNavCollapsed`) and `LLM` property on `AppStatePrompts`.

### Step 2: Extract `ProcessView`, `SystemPromptsView`, `HistoryView`

**Complexity:** 5
**Status:** [x]

#### Changes Made

- `BlazorWebApp/Components/Prompts/LLM/Views/ProcessView.razor` - new; contains Single/Batch pipeline, template selector, undo/redo, comparison panel, batch results.
- `BlazorWebApp/Components/Prompts/LLM/Views/SystemPromptsView.razor` - new; wraps `SystemPromptEditor` and bubbles template-change events.
- `BlazorWebApp/Components/Prompts/LLM/Views/HistoryView.razor` - new; renders LocalStorage-backed history, emits restore callback.
- `BlazorWebApp/Components/Prompts/LLM/LLMMainPanel.razor` - deleted.

### Step 3: Create `LLMNavMenu.razor`

**Complexity:** 3
**Status:** [x]

#### Changes Made

- `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor` - new; `MudNavMenu` with three items, icon-only (56px) when collapsed, icon+label (100% width) when expanded, chevron toggle emits `OnCollapsedChanged`.

### Step 4: Rebuild `LLMToolsTab.razor`

**Complexity:** 3
**Status:** [x]

#### Changes Made

- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor` - composition root; owns `_selectedModel`, `_activeViewId`, `_isNavCollapsed`, `_templates`, and version counters (`_templatesVersion`, `_restoreVersion`, `_historyVersion`) used to signal children to re-sync without remounting.
- `BlazorWebApp/Components/Prompts/LLM/LLMSettingsPanel.razor` - outer `div` switched from `height: calc(100vh - 200px)` to `height: 100%` so it fits inside the vertical sidebar stack below the nav.
- Sidebar is a vertical flex column (nav on top, divider, settings panel fills remaining space).

### Step 5: Publish view-switch events through `EventService`

**Complexity:** 2
**Status:** [x]

#### Changes Made

- `BlazorWebApp/Events/LLMViewChangedEventArgs.cs` - new; carries `PreviousViewId` and `ViewId`.
- `LLMToolsTab.HandleViewSelected` publishes `LLMViewChangedEventArgs` through the injected `IEventService`, then persists via `State.SaveState()`.

### Step 6: Verify no regression in Process / System Prompts / History

**Complexity:** 2
**Status:** [x]

#### Notes

- User confirmed nav works at runtime. Layout adjusted to place settings below the nav per user request.
- History restore flow: `HistoryView.OnRestore` -> parent sets `_pendingRestore` + bumps `_restoreVersion` + switches active view -> `ProcessView.OnParametersSetAsync` applies the input/result.
- Template changes in `SystemPromptsView` trigger parent `LoadTemplates()` + bump `_templatesVersion` -> `ProcessView` re-syncs its selection.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                  |
| ---- | ------ | ---------- | ------------------------------------------------------ |
| 1    | [x]    | 2          | `AppStatePromptsLLM` added                             |
| 2    | [x]    | 5          | Three views extracted, `LLMMainPanel` deleted          |
| 3    | [x]    | 3          | `LLMNavMenu` with collapse toggle                      |
| 4    | [x]    | 3          | `LLMToolsTab` rebuilt; sidebar stacked vertically      |
| 5    | [x]    | 2          | `LLMViewChangedEventArgs` published via `EventService` |
| 6    | [x]    | 2          | User-verified; no regressions                          |

**Total:** 17 points (slightly above the 8-point estimate in `MAIN_PLAN.md`; extra complexity came from the parent-orchestrated re-sync pattern and the sidebar layout pass.)

---

## Issues & Resolutions

### Issue 1: Sidebar layout initially placed nav left of settings

**Impact:** Nav and settings displayed side by side; user requested nav-above-settings layout.
**Resolution:** Switched sidebar container to `flex-column`, added a `MudDivider`, set `LLMNavMenu` width to `100%` when expanded, and removed the fixed viewport-height on `LLMSettingsPanel` so it fills remaining space.

### Issue 2: Build reported `MSB3027`/`MSB3021` errors

**Impact:** Build task exited with code 1.
**Resolution:** Root cause was the running app holding `BlazorWebApp.exe` (PID 13140). All new/modified files compiled cleanly (verified via `get_errors`). Not a code defect.

---

## Commit Checkpoints

- [x] Step 1 complete - `AppState` extension
- [x] Step 2 complete - views extracted
- [x] Step 3 complete - nav menu
- [x] Step 4 complete - composition root + layout
- [x] Step 5 complete - event wiring
- [x] Step 6 complete - user verification

---

## Phase Summary

### Accomplishments

1. Inner `MudTabs` replaced by a scalable sidebar nav ready to host Phase 2-11 views.
2. Three existing tabs extracted into self-contained view components with clear parent-child contracts (parameters + event callbacks + version counters for re-sync signals).
3. Nav state (`ActiveViewId`, `IsNavCollapsed`) persisted via `AppState` JSON column, no migration required.
4. New `LLMViewChangedEventArgs` published through `EventService`, ready for future subscribers (analytics, context-sensitive info panel, etc.).
5. Sidebar layout: nav on top (full width when expanded, 56px when collapsed), divider, settings panel fills remaining space.

### Files Created

- `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/ProcessView.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/SystemPromptsView.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/HistoryView.razor`
- `BlazorWebApp/Events/LLMViewChangedEventArgs.cs`

### Files Modified

- `BlazorWebApp/Models/AppState.cs`
- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor`
- `BlazorWebApp/Components/Prompts/LLM/LLMSettingsPanel.razor`

### Files Deleted

- `BlazorWebApp/Components/Prompts/LLM/LLMMainPanel.razor`

### Deferred Items

- None.

---

**Phase Status:** Complete
