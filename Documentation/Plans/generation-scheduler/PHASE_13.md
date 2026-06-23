# Phase 13 - Project / Folder Selectors

## Status

**Phase:** 13 - NOT STARTED
**Complexity:** 3 points
**Depends on:** Phases 1-9 (delivered)

---

## Objective

Replace the free-text `ProjectName` / `FolderName` inputs in `SchedulerEditorTab` (both job-level `OutputConfig` and per-action `OutputOverride`) with dependent dropdown selectors sourced from the same service the Gallery uses. No creation UI - users manage Projects/Folders from the Gallery only.

Storage stays **name-based** (`string? ProjectName`, `string? FolderName`) - no schema migration.

## Scope

### Behaviour

- Project select: empty option + list of existing Project names. Empty option label depends on context:
  - Job-level `OutputConfig`: `\"(default)\"`.
  - Action-level `OutputOverride`: `\"(inherit from job)\"`.
- Folder select: dependent on selected Project.
  - When Project is empty -> Folder is disabled and empty.
  - When Project changes -> Folder resets to empty.
- Orphaned values: if a saved `ProjectName` / `FolderName` is missing from the data source on load, display a `MudChip` with `Color.Warning` next to the select showing `\"Missing: {name}\"` and require re-selection (or clear) before Save succeeds.

### Out of scope

- Creating Projects or Folders from the Scheduler.
- Switching to Id-based references (evaluated, rejected for this phase to avoid migration).

## Steps

- [ ] Step 1 - Pick the accessor [1 pt]
  - Investigate `IGalleryService` / `IDatabaseService` to confirm the cheapest read of Project names and Folder names per Project.
  - Add a thin helper (static extension or local repository method) only if the existing surface is insufficient.

- [ ] Step 2 - Create `BlazorWebApp/Components/Scheduler/ProjectFolderSelector.razor` [3 pts]
  - Parameters: `string? ProjectName`, `string? FolderName`, `EventCallback<string?> ProjectNameChanged`, `EventCallback<string?> FolderNameChanged`, `string EmptyLabel` (default `\"(default)\"`), `bool Dense = true`.
  - Internally fetches Project list on first render; fetches Folder list when Project changes.
  - Two `MudSelect<string?>` controls in a horizontal stack.
  - Warning chip shown when the bound value is not present in the fetched list.

- [ ] Step 3 - Wire into `SchedulerEditorTab.razor` [2 pts]
  - Replace the free-text fields for `_editing.OutputConfig.ProjectName` / `FolderName`.
  - Replace the free-text fields in the per-action `OutputOverride` section with `EmptyLabel=\"(inherit from job)\"`.

- [ ] Step 4 - Save-time validation [1 pt]
  - Before `SaveAsync`, if any selector shows a \"Missing\" warning, show an `ISnackbar` error and cancel save.

## Success Criteria

- No free-text Project or Folder inputs remain in the Scheduler editor.
- Folder options always reflect the selected Project; changing Project clears Folder.
- Opening a job that references a removed Project/Folder surfaces a visible warning and blocks save until resolved.
- Existing Scheduler tests stay green; no migrations added.

## Verification

- Manual: create job using selectors, save, reopen, confirm values round-trip.
- Manual: delete a Project referenced by an existing job (via Gallery), reopen the job in Scheduler, confirm warning chip and blocked save.
- Existing `SchedulerServiceTests` unaffected (pure UI change).

## Open Questions

- None.
