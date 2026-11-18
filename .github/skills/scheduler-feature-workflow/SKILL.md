---
name: scheduler-feature-workflow
description: "Implement scheduler features the repo way. Use when adding a scheduler job, directive, variation sequencer change, pause or resume behavior, snapshotting, draft persistence, or editing the scheduler state machine and its tests."
argument-hint: "Scheduler feature, directive, or lifecycle change"
---

# Scheduler Feature Workflow

Use this skill for changes in the scheduler engine, editor, or persistence layer.

## When To Use

- Add a scheduler job capability
- Add or modify directives
- Change pause, resume, skip, or sequencing behavior
- Persist editor draft or runtime job state
- Update scheduler service tests

## Procedure

1. Start from the closest existing scheduler pattern instead of inventing a new execution model.
2. Trace the owning slice before editing:
   - runtime orchestration in `SchedulerService`
   - persistence in `Scheduler/Persistence/`
   - snapshotting in `ScheduleSnapshotService`
   - variation behavior in the sequencer and materializer types
3. Preserve the split between editor-state persistence and runtime execution persistence.
4. When a scheduler aggregate is stored as JSON, use the existing serializer options and remember the `IsModified` pattern on in-place updates.
5. Publish important state transitions through `IEventService` when other surfaces need to react.
6. Keep repository code on the `IDbContextFactory<AppDbContext>` pattern.
7. Add or update narrow scheduler tests under `BlazorWebApp.Tests/Scheduler/`.
8. Run the narrow scheduler tests immediately after the first substantive edit before widening scope.

## Guardrails

- Do not couple the editor and runtime models more tightly than the existing stores require.
- Do not widen scheduler persistence schema casually; it is easy to break resume semantics.
- Do not skip tests for sequencing or directive behavior; regressions there are hard to see from a build alone.

## Key Anchors

- `../../../BlazorWebApp/Scheduler/SchedulerService.cs`
- `../../../BlazorWebApp/Scheduler/ScheduleSnapshotService.cs`
- `../../../BlazorWebApp/Scheduler/Persistence/IJobRepository.cs`
- `../../../BlazorWebApp/Scheduler/Persistence/JobRepository.cs`
- `../../../BlazorWebApp/Scheduler/Persistence/SchedulerDraftStore.cs`
- `../../../BlazorWebApp.Tests/Scheduler/`
- `../../../Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`
