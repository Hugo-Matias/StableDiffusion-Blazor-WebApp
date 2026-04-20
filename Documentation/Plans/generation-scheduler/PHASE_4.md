# Phase 4 - Scheduler Service & Execution Engine

## Status

**Phase:** 4 - COMPLETE
**Build Status:** Green | **Tests:** 87/87 (30 new Scheduler tests)

---

## Objective

Execute a `Job` end-to-end: walk its actions in order, for each iteration clone the base parameters, apply directives + variation values, invoke the generation pipeline, route outputs, publish progress events, and persist run state for resume.

---

## Execution Checklist

### Step 1: Scheduler lifecycle events

**Complexity:** 2 | **Status:** [x] COMPLETE

- [x] `BlazorWebApp/Events/JobStartedEventArgs.cs`, `JobProgressChangedEventArgs.cs`, `JobActionChangedEventArgs.cs`, `JobImageGeneratedEventArgs.cs`, `JobCompletedEventArgs.cs`
- [x] All published via `IEventService`

### Step 2: `ParameterApplier`

**Complexity:** 3 | **Status:** [x] COMPLETE

- [x] `IParameterApplier` + `ParameterApplier` handling `FragmentTarget`, `PromptTarget`, `LoraTarget` (strength), `AssetTarget`, `OutputTarget`
- [x] Resilient float coercion for LoRA strength (float/double/int/long/decimal/string)

### Step 3: `DirectiveExecutor`

**Complexity:** 3 | **Status:** [x] COMPLETE

- [x] `IDirectiveExecutor` + `DirectiveExecutor` covering all 9 directive types
- [x] Prompt mutations write to the prompts fragment so later `ImageService` wildcard/style expansion sees the edited text
- [x] `AddLoraDirective` clones the incoming `Lora` to prevent cross-iteration state bleed
- [x] `AddPromptStyleDirective` appends style-name tokens to the positive prompt for downstream `ParseStyles` resolution

### Step 4: `SchedulerService` + action loop

**Complexity:** 5 | **Status:** [x] COMPLETE

- [x] `ISchedulerService` with `RunAsync`, `ResumeAsync`, `PauseAsync`, `StopAsync`, `SkipCurrentAsync`
- [x] Plans built up front for all actions so `TotalIterations` is known for progress events
- [x] Action loop: clone base params -> apply directives -> apply iteration values -> invoke runner -> update progress/state
- [x] Per-iteration `CancellationTokenSource` (`_skipCts`) linked to the job-scope stop CTS enables Skip without cancelling the whole run
- [x] Pause is observed between iterations; Stop cancels the entire job CTS; Skip cancels only the in-flight iteration

### Step 5: Pluggable generation runner + output routing

**Complexity:** 3 | **Status:** [x] COMPLETE

- [x] `IJobGenerationRunner` abstraction
- [x] `ImageServiceJobGenerationRunner` resolves `ProjectName` -> `Project.Id` via `IDatabaseService.GetProject`, swaps `IStateService.State.Gallery.ProjectId` for the duration of the call, and always restores the original in a finally block
- [x] A static `SemaphoreSlim` serializes the state swap so concurrent scheduler runs do not stomp each other

### Step 6: Run-state persistence + resume

**Complexity:** 2 | **Status:** [x] COMPLETE

- [x] `SaveRunStateAsync` called after each iteration and on pause/cancel/fail
- [x] `ResumeAsync` reads `Job.RunState` and continues at the saved action/iteration indices
- [x] `JobRunState.CurrentIterationIndex` stores "next iteration to run" (one past the last successful iteration) so resume is off-by-one-safe
- [x] Status transitions: `Draft`/`Queued` -> `Running` -> `Paused`/`Completed`/`Failed`/`Cancelled`

### Step 7: Tests

**Complexity:** 3 | **Status:** [x] COMPLETE - 30 new tests, 87/87 total

- [x] `ParameterApplierTests.cs` (8 tests)
- [x] `DirectiveExecutorTests.cs` (12 tests covering all directive types + disabled no-op + prompt append/prepend/replace)
- [x] `SchedulerServiceTests.cs` (10 tests): no-variation single iteration, variation apply per iteration, directive precedence, two-action flow, output override, missing workflow fail path, Stop mid-run, Pause+Resume continuation, Skip Current, runner-throws-continues

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                 |
| ---- | ------ | ---------- | ------------------------------------------------------------------------------------- |
| 1    | [x]    | 2          | 5 event args classes                                                                  |
| 2    | [x]    | 3          | ParameterApplier with output-config routing                                           |
| 3    | [x]    | 3          | DirectiveExecutor covering all 9 directive types                                      |
| 4    | [x]    | 5          | SchedulerService with Run/Resume/Pause/Stop/SkipCurrent                               |
| 5    | [x]    | 3          | ImageServiceJobGenerationRunner with state-swap output routing                        |
| 6    | [x]    | 2          | Per-iteration run-state save with "next-to-run" iteration indexing for correct resume |
| 7    | [x]    | 3          | 30 tests added; 87/87 scheduler green                                                 |

---

## Artifacts

**Events:**

- `BlazorWebApp/Events/JobStartedEventArgs.cs`
- `BlazorWebApp/Events/JobProgressChangedEventArgs.cs`
- `BlazorWebApp/Events/JobActionChangedEventArgs.cs`
- `BlazorWebApp/Events/JobImageGeneratedEventArgs.cs`
- `BlazorWebApp/Events/JobCompletedEventArgs.cs`

**Engine:**

- `BlazorWebApp/Scheduler/Engine/IParameterApplier.cs` + `ParameterApplier.cs`
- `BlazorWebApp/Scheduler/Engine/IDirectiveExecutor.cs` + `DirectiveExecutor.cs`
- `BlazorWebApp/Scheduler/Engine/IJobGenerationRunner.cs` + `ImageServiceJobGenerationRunner.cs`

**Service:**

- `BlazorWebApp/Scheduler/ISchedulerService.cs`
- `BlazorWebApp/Scheduler/SchedulerService.cs`

**DI registrations in `BlazorWebApp/Program.cs`** (all scoped).

**Tests:**

- `BlazorWebApp.Tests/Scheduler/ParameterApplierTests.cs`
- `BlazorWebApp.Tests/Scheduler/DirectiveExecutorTests.cs`
- `BlazorWebApp.Tests/Scheduler/SchedulerServiceTests.cs`

## Notes & Decisions

- **Output routing via state swap:** Rather than refactoring `ImageService` to accept an explicit project id, `ImageServiceJobGenerationRunner` temporarily swaps `IStateService.State.Gallery.ProjectId`. This is safe because a static semaphore serializes access. A future refactor can pass the project id through the generation pipeline explicitly.
- **LLM pipelining deferred:** The plan's Step 6 (kick off next-iteration LLM materialization in parallel with the current generation) is deferred. The current implementation materializes the full `VariationPlan` up front during `BuildPlanAsync`, so LLM latency is paid once per plan rather than once per iteration when it would otherwise block.
- **CurrentIterationIndex semantics:** stores the next iteration to run, not the last completed. This avoids the resume off-by-one where a paused job would re-run the iteration that had just completed.
- **Skip semantics:** `SkipCurrentAsync` cancels the in-flight generation and is counted as a failed image (since no output was produced). Progress continues to the next iteration.
- **Pause throws OperationCanceledException:** A paused job terminates the `RunAsync` task with status=Paused. Callers resume with `ResumeAsync(jobId)` which reads the persisted run state.
- **Single-job concurrency:** The service enforces one running job at a time per instance via a check on `_runningJobId`. Starting a second run throws.
- **AddPromptStyleDirective:** Currently appends style-name tokens to the positive prompt as a hint for `ImageService.PrepareGenerationParametersAsync`'s `ParseStyles` step. Richer resolution (looking up style entities from app state at scheduler time) is left for a later refinement.
- **JobStatus enum uses `Cancelled`** (double-L) throughout.
