# Phase 14 - Editor Draft Persistence

## Status

**Phase:** 14 - NOT STARTED
**Complexity:** 5 points
**Depends on:** Phases 1-9 (delivered). Compatible with Phase 12 / Phase 13.

---

## Objective

Protect in-progress edits in the Scheduler Editor against tab navigation, leaving the page, and app restart. **One draft slot** shared across jobs; switching to a different job while a dirty draft exists prompts the user.

## Scope

### Behaviour

- **Session-scoped draft** - held in a new `ISchedulerEditorState` (Scoped service) containing:
  - `Job? Draft`
  - `Guid? EditingJobId` (null for new unsaved jobs)
  - `bool IsDirty`
  - `DateTime? UpdatedAt`
- **Dirty marker** - Save button shows `*` suffix when `IsDirty`.
- **Navigation guard** - `NavigationManager.LocationChanging` blocks leaving `/scheduler` when dirty, showing a confirmation (reuses `ConfirmationDialog`).
- **Cross-restart persistence** - draft blob serialized to the DB using the existing JSON-backed pattern:
  - Preferred: a dedicated lightweight entity (e.g., `SchedulerDraft { Id=1, JsonBody, UpdatedAt }`) - single-row table, simpler than mixing into `AppState`.
  - Debounced write: 2 s after last change. Flush on navigation away from the editor.
- **Restart banner** - on first mount of `SchedulerEditorTab`, if a persisted draft exists show an `MudAlert` with actions `[Restore] / [Discard]`.
- **Cross-job switch prompt** - when the user selects Job B while `IsDirty && (EditingJobId == null || EditingJobId == A.Id)`:
  - Show `ConfirmationDialog` with **Save & switch / Discard & switch / Cancel**.
  - Save & switch: run the existing `SaveAsync` path for the current draft, then load B.
  - Discard & switch: clear the draft, load B.
  - Cancel: no-op.

### Events

- New `SchedulerDraftChangedEventArgs(Guid? EditingJobId, bool IsDirty)` published via `IEventService` whenever `IsDirty` transitions.

### Out of scope

- Per-job draft dictionary (multiple slots). Single slot only.
- Auto-save of committed jobs (unchanged - users still press Save explicitly).

## Steps

- [ ] Step 1 - Introduce `ISchedulerEditorState` / `SchedulerEditorState` (Scoped) [2 pts]
  - Properties listed above; `MarkDirty()`, `ClearDraft()`, `SetDraft(Job, Guid? editingJobId)`.
  - Raises `SchedulerDraftChangedEventArgs` through `IEventService`.
  - Registered in `Program.cs` alongside other Scheduler services.

- [ ] Step 2 - Add `SchedulerDraftChangedEventArgs` in `BlazorWebApp/Events/` following existing convention [1 pt].

- [ ] Step 3 - Persist the blob [3 pts]
  - New entity `SchedulerDraft` in `Data/Entities/` (single-row, primary key fixed to 1) with `string JsonBody` + `DateTime UpdatedAt`.
  - EF migration `Add_SchedulerDraft`.
  - `ISchedulerDraftStore` with `LoadAsync()`, `SaveAsync(Job, Guid?)`, `ClearAsync()`. Uses `SchedulerJsonOptions` for serialization to stay consistent with `Job` persistence.
  - `SchedulerEditorState` owns a debounced timer (2 s) calling `SaveAsync` on change; immediate flush on `FlushAsync()` for navigation / app shutdown.

- [ ] Step 4 - Wire into `SchedulerEditorTab.razor` [3 pts]
  - On init: if `SchedulerEditorState.Draft` is null but `ISchedulerDraftStore.LoadAsync()` returns a draft, show the restore banner.
  - Every user mutation invokes `SchedulerEditorState.MarkDirty()`; the service handles debounced persistence.
  - `SaveAsync` flow: on success, `ClearDraft()` and `ISchedulerDraftStore.ClearAsync()`.
  - `SelectJob(localJob)`: if dirty and target differs, show the three-button `ConfirmationDialog`.
  - Implement `IDisposable` to flush on dispose.

- [ ] Step 5 - Navigation guard via `NavigationManager.RegisterLocationChangingHandler` [1 pt]
  - Register inside `SchedulerEditorTab.OnInitialized`; unregister on dispose.
  - Prompt only when `IsDirty`.

- [ ] Step 6 - Save button dirty marker (`Save *`) and a small `MudTooltip` explaining autosave [1 pt].

## Success Criteria

- Switching tabs inside `/scheduler` preserves draft in memory.
- Leaving `/scheduler` while dirty shows a confirmation prompt.
- Killing the app mid-edit and restarting restores the draft via the banner.
- Switching jobs while dirty always prompts (no silent loss).
- Successful save clears both in-memory and persisted draft.
- Only one draft slot exists at any time.

## Verification

- Manual: edit a new job, switch to Gallery, confirm guard; confirm restart recovery.
- Manual: edit Job A, select Job B, confirm three-button prompt and each branch.
- Unit test: `SchedulerEditorState` dirty-flag transitions and event publication.
- Unit test: `SchedulerDraftStore` round-trip (save / load / clear).

## Open Questions

- Should `LocationChanging` also intercept hard URL changes (tab close)? Browser-level `beforeunload` is out of scope here; can be added later if needed.
