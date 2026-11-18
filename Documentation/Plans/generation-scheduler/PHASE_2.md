# Phase 2 - Persistence Layer

## Status

**Phase:** 2 - COMPLETE
**Build Status:** Passing | **Tests:** 12/12 passing (37/37 cumulative Scheduler tests)

---

## Objective

Persist Scheduler jobs to SQLite so users can save drafts, list previous jobs, and survive app restart while a job is queued, running, or paused.

---

## Execution Checklist

### Step 1: `JobEntity` + DbContext wiring + migration - COMPLETE

**Complexity:** 3 | **Status:** [x] Complete

- [x] Extended [BlazorWebApp/Scheduler/SchedulerJsonOptions.cs](../../../BlazorWebApp/Scheduler/SchedulerJsonOptions.cs) to include `GenerationParametersJsonConverter` and `FragmentParametersJsonConverter` so `Job.BaseParameters` round-trips through the DB with full type fidelity
- [x] Created [BlazorWebApp/Data/Entities/JobEntity.cs](../../../BlazorWebApp/Data/Entities/JobEntity.cs) with denormalized columns (`Id` PK, `JobId` unique GUID, `Name`, `Status` string, `WorkflowId`, `CreatedAt`, `LastRunAt`, `UpdatedAt`) plus a `Body` JSON column holding the full `Job`
- [x] Updated [AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs) with `DbSet<JobEntity> Jobs`, a `ValueConverter<Job, string>` using `SchedulerJsonOptions.Compact`, unique index on `JobId`, and a secondary index on `Status`
- [x] Generated migration `20260420203413_Add_JobEntity` creating the `Jobs` table with the indexes above

### Step 2: `IJobRepository` + `JobRepository` - COMPLETE

**Complexity:** 3 | **Status:** [x] Complete

- [x] [BlazorWebApp/Scheduler/Persistence/IJobRepository.cs](../../../BlazorWebApp/Scheduler/Persistence/IJobRepository.cs) - `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `GetByIdAsync`, `ListAsync`, `GetRunningOrPausedAsync`, `SaveRunStateAsync`
- [x] [BlazorWebApp/Scheduler/Persistence/JobRepository.cs](../../../BlazorWebApp/Scheduler/Persistence/JobRepository.cs) - EF Core implementation following the `WorkflowStateService` precedent (DbContextFactory + structured logging); keeps denormalized columns in sync on every save
- [x] Registered in [Program.cs](../../../BlazorWebApp/Program.cs) as a singleton next to `IWorkflowStateService`

### Step 3: Repository tests - COMPLETE

**Complexity:** 2 | **Status:** [x] Complete

- [x] [BlazorWebApp.Tests/Scheduler/JobRepositoryTests.cs](../../../BlazorWebApp.Tests/Scheduler/JobRepositoryTests.cs) - 12 tests using EF `InMemory` via an inline `IDbContextFactory<AppDbContext>` helper
- [x] Coverage:
  - Create / Get / Update / Delete happy paths
  - Upsert semantics: `UpdateAsync` creates when missing, `DeleteAsync` is a no-op when missing
  - `GetByIdAsync` returns `null` for missing rows
  - `ListAsync` orders by `UpdatedAt` descending
  - `GetRunningOrPausedAsync` filters only `Running` + `Paused`
  - `SaveRunStateAsync` updates run state, status, and `LastRunAt` on first transition to `Running`
  - Denormalized columns remain queryable without deserializing `Body`
  - Polymorphic children (`RangeVariation`, `AppendPromptDirective`) survive the round-trip

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                                              |
| ---- | ------ | ---------- | ------------------------------------------------------------------------------------------------------------------ |
| 1    | [x]    | 3          | SchedulerJsonOptions extended with GP converters; `Body` column uses `Compact` options                             |
| 2    | [x]    | 3          | Plan said "Extend IDatabaseService"; instead followed `WorkflowStateService` precedent with a dedicated repository |
| 3    | [x]    | 2          | 12/12 tests green                                                                                                  |

**Total delivered:** 8 pts

---

## Issues & Resolutions

### Issue 1: `Job.BaseParameters` round-trip loses value types

**Symptom:** Potential type loss when serializing `Job` through the DB because `FragmentParameters` uses a `Dictionary<string, object?>`.

**Resolution:** Added `GenerationParametersJsonConverter` and `FragmentParametersJsonConverter` to `SchedulerJsonOptions`. Both `Default` and `Compact` options now preserve numeric/string/bool value types in fragment dictionaries identically to how `State.GenerationParameters` persists elsewhere in the app.

### Issue 2: `SaveRunStateAsync` did not persist mutations

**Symptom:** `SaveRunStateAsync_Updates_RunState_And_Status_And_LastRunAt` failed - loaded `Status` was the original `Queued` not the expected `Running`.

**Cause:** The `Body` column uses a `ValueConverter<Job, string>`. When code mutates the tracked entity's `Body` object in place (same reference), EF's change detection does not see a change to the JSON string value, so the column is not re-serialized on `SaveChanges`.

**Resolution:** Explicitly mark `Entry(existing).Property(e => e.Body).IsModified = true` after in-place mutations in both `UpdateAsync` and `SaveRunStateAsync`. For `CreateAsync` this is unnecessary because the entity is added fresh.

### Issue 3: Plan deviation - dedicated repository instead of `IDatabaseService` extension

**Decision:** MAIN_PLAN.md listed "Extend `IDatabaseService` with job accessors" as Step 3. `IDatabaseService` is already very large (all CRUD for projects, images, prompts, resources, wildcards, etc.), and newer features such as `IWorkflowStateService` have chosen to live as dedicated services instead of extending that interface. This phase follows the newer precedent with `IJobRepository` to keep the Scheduler feature self-contained.

---

## Commit Checkpoints

- [x] Entity + DbContext + migration committed
- [x] Repository + DI registration committed
- [x] Repository tests committed (12/12 green)
