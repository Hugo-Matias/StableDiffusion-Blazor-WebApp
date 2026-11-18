# Build Notes - Scheduler Feature

## Project Conventions

- Blazor Server + MudBlazor 8; `MudList<T>` has Razor overload-resolution quirks with `SelectedValueChanged` / `@bind-SelectedValue` - prefer a `MudStack` of `MudButton`s for master-list selection when the type is non-trivial.
- MudBlazor dialog cascading parameter is `MudDialogInstance` (not `IMudDialogInstance`).
- Entity `Workflow.Title` (not `.Name`); `JobStatus.Cancelled` (double L, not `Canceled`).
- `Image` entities have `Id > 0` after persistence; Scheduler uses that to filter tracked IDs.
- `JobRunState` is persisted inside `JobEntity.Body` as JSON via `SchedulerJsonOptions.Compact`, so adding new properties (e.g. `GeneratedImageIds`) does not require EF migrations.

## Service Lifetimes

- `IJobRepository`: Singleton (uses `IDbContextFactory`).
- `ISchedulerService`, variation engine, execution engine services, `IScheduleSnapshotService`: Scoped.

## Generate -> Scheduler handoff

- `IScheduleSnapshotService` (scoped) buffers a pending `Job` across the `/generate` -> `/scheduler/editor` navigation inside a single Blazor Server circuit.
- `PromptsForm.razor` wires the "Schedule" button; `SchedulerEditorTab.razor` consumes the snapshot on init.

## Test Gotchas

- 18 pre-existing test failures in Workflow/Parser/State/Wildcard test suites are culture-sensitive (`0,50` vs `0.50`) or rely on fragment-harness state unrelated to Scheduler. Scheduler tests all pass (92/92).
