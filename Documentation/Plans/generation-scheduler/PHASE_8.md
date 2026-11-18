# Phase 8 - Generate Page Integration

## Status

**Phase:** 8 - COMPLETE
**Build Status:** Green | **Tests:** 92/92 (3 new snapshot service tests).

---

## Objective

Let users snapshot their live Generate parameters into a new Scheduler job with one click, landing them in the Editor tab pre-populated and ready to add actions.

## Scope Decisions

Delivered steps 1 and 2 from the plan. Steps 3 (round-trip "edit base params in Generate") and 4 (workflow-match validation on round-trip) are deferred - they depend on a bidirectional handoff that duplicates state that can already be re-snapshotted cheaply via the same button. Future iterations can add a "jobId" round-trip with a banner when that becomes user-requested.

## Artifacts

- `BlazorWebApp/Scheduler/ScheduleSnapshotService.cs` - scoped buffer (`Set` / `Consume` / `HasPending`).
- `BlazorWebApp/Program.cs` - registers `IScheduleSnapshotService` as Scoped.
- `BlazorWebApp/Components/Shared/Generation/Fragments/PromptsForm.razor` - adds an `EventNote` icon button beside `GenerateButton`; `ScheduleCurrent()` clones `State.GenerationParameters`, wraps it in a fresh `Job` with one default `JobAction`, stores it in the snapshot service, and navigates to `/scheduler/editor`.
- `BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor` - `OnInitializedAsync` calls `SnapshotService.Consume()`; if a snapshot is present it becomes `_editing` and a Snackbar confirms the handoff.
- `BlazorWebApp.Tests/Scheduler/ScheduleSnapshotServiceTests.cs` - 3 tests (default empty state, set/consume clears, set overwrites).

## Implementation Notes

- Scoped (per-circuit) lifetime ensures snapshots can't leak across users in Blazor Server while still surviving the navigation from `/generate` to `/scheduler/editor` within the same circuit.
- The Generate button remains the primary action; the Schedule button is a filled info-colored icon button in a horizontal stack, chosen to stay visually secondary.
- `GenerationParameters.Clone()` is used for the snapshot so later edits in the Generate page do not mutate the queued job.
- Workflow validation (step 4) is implicitly handled: `Job.WorkflowId` is taken from the live parameters and the Editor's Workflow select exposes the full list, so users can change it deliberately before saving.

## Execution Checklist

| Step | Description                             | Status                                |
| ---- | --------------------------------------- | ------------------------------------- |
| 1    | Schedule button in PromptsForm          | [x]                                   |
| 2    | Snapshot service + navigation to Editor | [x]                                   |
| 3    | Round-trip "edit base params" banner    | Deferred                              |
| 4    | Workflow-match validation on round-trip | Deferred (implicit via Editor select) |
