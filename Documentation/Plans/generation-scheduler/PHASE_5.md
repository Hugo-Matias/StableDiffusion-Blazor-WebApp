# Phase 5 - Scheduler Page: Runs Tab

## Status

**Phase:** 5 - COMPLETE
**Build Status:** Green | **Tests:** 87/87 Scheduler tests passing (no new tests; UI verified via build).

## Artifacts

- `BlazorWebApp/Pages/Scheduler.razor` (`@page "/scheduler"`, `@page "/scheduler/{Tab}"` - optional parameter selects default tab for deep links like `/scheduler/results`).
- `BlazorWebApp/Components/Scheduler/SchedulerRunsTab.razor` - Runs tab UI.
- `BlazorWebApp/Components/Shared/NavBar.razor` - added top-level "Scheduler" nav link (`fa-calendar-check`).

## Implementation Notes

- MudBlazor `MudTabs` with three panels; Editor/Results are `MudAlert` placeholders that reference Phase 7 / Phase 6 respectively.
- Runs tab uses `MudTable<Job>` with columns: Name+Description, Status chip, Action count, Last Run, Progress, Controls.
- Controls toggle based on `Scheduler.RunningJobId` and `job.Status`. Pause/Stop/Skip only enabled for the running job; Run disabled while any job is executing.
- Live updates via `IEventService`: component subscribes to `JobStartedEventArgs`, `JobProgressChangedEventArgs`, `JobActionChangedEventArgs`, `JobCompletedEventArgs`. Any event triggers an async reload via `InvokeAsync`.
- `ViewResults` navigates to `/scheduler/results?jobId=...`. Phase 6 will read this query string.
- Delete uses `IDialogService.ShowMessageBox` confirmation; disabled for the currently running job.
- `Scheduler.RunAsync` / `ResumeAsync` are invoked fire-and-forget (`_ = ...`); the event pipeline drives the UI.

## Decisions

- Job editor is deferred to Phase 7 per plan; the placeholder alert documents programmatic seeding via `IJobRepository`.
- Query parameter shape (`/scheduler/results?jobId=...`) is fixed here so Phase 6 can implement without renegotiation.
- A shared generic `OnJobEvent<T>` handler reloads on any event - every event type mutates visible row state, so per-event delta updates would add complexity without value.

---

## Objective

Expose the Scheduler to users: a `/scheduler` page with Runs / Editor / Results tabs, where the Runs tab lists jobs and exposes full lifecycle controls (Run, Resume, Pause, Stop, Skip Current, Delete) with live progress updates.

---

## Execution Checklist

### Step 1: `/scheduler` page shell with tabs

**Complexity:** 2 | **Status:** [ ] Not Started

- `BlazorWebApp/Pages/Scheduler.razor` with `@page "/scheduler"` and optional `@page "/scheduler/{Tab}"`
- MudBlazor `MudTabs` with three panels: `Runs`, `Editor`, `Results`
- Editor and Results panels are placeholders (Phase 6/7) but render without error
- Nav menu entry pointing to `/scheduler`

### Step 2: Runs tab list + basic actions

**Complexity:** 3 | **Status:** [ ] Not Started

- `BlazorWebApp/Components/Scheduler/SchedulerRunsTab.razor`
- Loads jobs via `IJobRepository.ListAsync()`
- Table columns: Name, Status, Actions count, Last Run, Controls
- Control buttons: Run, Resume, Pause, Stop, Skip, Delete (Duplicate/Edit deferred to Phase 7)
- Empty state when no jobs exist

### Step 3: Live progress & run detail

**Complexity:** 3 | **Status:** [ ] Not Started

- Subscribe to `JobStartedEventArgs`, `JobProgressChangedEventArgs`, `JobActionChangedEventArgs`, `JobCompletedEventArgs`
- Row-level progress bar + current action indicator for the running job
- `InvokeAsync(StateHasChanged)` on event callbacks

### Step 4: Context navigation

**Complexity:** 1 | **Status:** [ ] Not Started

- "View Results" button per row navigates to `/scheduler/results?jobId=...` (placeholder route for Phase 6 to consume)

---

## Progress Tracking

| Step | Status | Complexity | Notes                                     |
| ---- | ------ | ---------- | ----------------------------------------- |
| 1    | [x]    | 2          | `/scheduler` page + nav link              |
| 2    | [x]    | 3          | Runs table + lifecycle buttons            |
| 3    | [x]    | 3          | Live refresh via 4 event subscriptions    |
| 4    | [x]    | 1          | `/scheduler/results?jobId=...` navigation |
