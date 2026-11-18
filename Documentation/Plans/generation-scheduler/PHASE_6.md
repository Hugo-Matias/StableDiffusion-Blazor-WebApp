# Phase 6 - Scheduler Page: Results Tab

## Status

**Phase:** 6 - COMPLETE
**Build Status:** Green | **Tests:** 89/89 Scheduler tests (2 new image-id tracking tests).

---

## Objective

Give users a scheduler-centric lens on generated images: select a job, see overall progress, and browse the outputs it produced, with live updates during a run. Images remain fully accessible from the regular Gallery; this tab is purely organizational.

## Scope Decisions

The plan step for adding `JobId` / `ActionOrder` / `IterationIndex` to `Image` (or a join table) is **intentionally replaced** with a zero-migration approach:

- Extend `JobRunState` with `GeneratedImageIds : List<int>`. Because `Job.Body` is persisted as JSON, no EF migration is required.
- `SchedulerService` appends the IDs of each generated image to that list after a successful iteration, before `SaveRunStateAsync`.
- Results tab queries the database via `IDatabaseService.GetImages(List<int>)` to hydrate the grid.

Deferred to a later iteration (noted so Phase 7/8 can revisit):

- Per-image `ActionOrder` / `IterationIndex` metadata - current approach tracks only the set of image IDs. If filtering by action/iteration becomes needed we switch to a parallel list of `(imageId, actionOrder, iterationIndex)` tuples on `JobRunState`.
- Live per-step preview thumbnail: the generation pipeline does not currently expose mid-step previews through `IImageService`, so the live preview card shows the most recent completed image instead.
- Advanced filters & sorting (plan step 6).

## Execution Checklist

### Step 1: Persistence

- [x] Added `public List<int> GeneratedImageIds { get; set; } = new();` to `JobRunState`.
- [x] `SchedulerService.RunIterationAsync` appends `images.Images.Where(i => i.Id > 0).Select(i => i.Id)` on success.
- [x] List is reset implicitly when `RunAsync` allocates a new `JobRunState` on fresh start (resume path keeps existing IDs).

### Step 2: Results tab component

- [x] `BlazorWebApp/Components/Scheduler/SchedulerResultsTab.razor` with `MudSelect` job picker, status chip, elapsed time, progress bar, latest-preview `MudPaper`, and `MudGrid` thumbnail grid.
- [x] Auto-selects the running job (or `?jobId=` query param) on init.

### Step 3: Live updates

- [x] Subscribed to `JobStartedEventArgs`, `JobProgressChangedEventArgs`, `JobImageGeneratedEventArgs`, `JobCompletedEventArgs`.
- [x] Handler narrows to matching jobId before re-fetching the image set.

### Step 4: Wire into page

- [x] `Pages/Scheduler.razor` replaces the placeholder with `<SchedulerResultsTab JobIdQuery="@_jobIdQuery" />` and parses `?jobId=<guid>` via `QueryHelpers.ParseQuery`.

## Tests Added

- `SchedulerServiceTests.Run_TracksGeneratedImageIds_InRunState` - 3-iteration job with 2 images per iteration populates 6 unique IDs.
- `SchedulerServiceTests.Run_SkipsImageIdsWithZeroOrNegativeId` - only persisted image IDs (Id > 0) are tracked.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                |
| ---- | ------ | ---------- | ---------------------------------------------------- |
| 1    | [x]    | 2          | `GeneratedImageIds` in `JobRunState` + runner append |
| 2    | [x]    | 3          | SchedulerResultsTab with selector + grid             |
| 3    | [x]    | 2          | Live refresh on 4 event subscriptions                |
| 4    | [x]    | 1          | Page wires query param and component                 |
