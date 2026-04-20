# Phase 7 - Scheduler Page: Editor Tab

## Status

**Phase:** 7 - COMPLETE
**Build Status:** Green | **Tests:** 89/89 (no new unit tests; UI delivery validated via build).

## Artifacts

- `BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor` - master/detail editor (jobs list + form).
- `BlazorWebApp/Components/Scheduler/SchedulerJsonEditorDialog.razor` - shared JSON editor dialog used for directive and variation bodies.
- `BlazorWebApp/Pages/Scheduler.razor` - Editor tab panel replaces the previous placeholder alert.

## Implementation Notes

- `MudList<T>` triggered a Razor overload-resolution issue under MudBlazor 8 (`EventCallback<T>` vs non-generic `EventCallback`); replaced with a `MudStack`-of-`MudButton` master list that sidesteps the generic resolution problem while preserving selection semantics.
- Added actions support full CRUD: add / remove / reorder (up/down) and renumbers `Order` on every structural change.
- `ComputeIterations(JobAction)` mirrors engine semantics (cartesian product of variation sizes, capped by `Limit`). `TotalGenerations` sums across actions and is displayed in the form header.
- Directive and Variation add-menus seed typed defaults matching the actual class schemas (`AppendPromptDirective.Text`, `ReplacePromptDirective.Search/Replace`, `AddPromptStyleDirective.StyleNames`, `WildcardVariation.CollectionName`, `LlmVariation.BasePrompt`, etc).
- JSON dialog uses `SchedulerJsonOptions.Default` - polymorphic `$type` discriminator round-trips cleanly for every directive/variation subtype.
- Duplicate uses JSON round-trip via `SchedulerJsonOptions.Compact`, mints a new Id, resets run-state and status, appends "(copy)" to the name.

---

## Objective

Provide a UI to create, edit, duplicate, and delete Scheduler jobs without dropping into code. The editor must support the full model surface (base params, actions, directives, variations) while acknowledging the many directive / variation subtypes already defined in the engine.

## Scope Decisions

The plan lists seven steps totaling ~36 sub-points of form work (step 4 alone spans 9 directive subtypes; step 5 covers 7 variation subtypes). Rather than ship seven half-built per-type forms, this phase delivers a **structured editor with typed forms for the common fields and a JSON-textarea escape hatch for the polymorphic directive / variation bodies**. This keeps the editor useful for every subtype today while per-subtype forms land incrementally in future iterations.

### What ships now

- Jobs master list (left column) with Select / New / Duplicate / Delete / Run.
- Job form (right column) with: Name, Description, Workflow (`MudSelect` over `IWorkflowService.GetWorkflows()`), default output Project / Folder.
- Actions editor: add / remove / reorder, per-row Label / Limit / PermutationOrder, computed iteration count (cartesian x limit cap).
- Directives and Variations editors: add-new selects a type from the full enum of subtypes, seeds a template object, opens a JSON editor dialog for fine-tuning. Existing items render their `$type` discriminator and summary, with Edit (JSON dialog) and Remove controls.
- Total generations indicator across actions (respecting per-action Limit).
- Save persists via `IJobRepository.UpdateAsync` (or `CreateAsync` for new jobs).

### What is intentionally deferred

- Per-subtype typed forms for every directive / variation (step 4 / step 5 full scope). The JSON dialog covers every subtype today via `System.Text.Json` polymorphism.
- "Edit base params in Generate" round-trip (that round-trip is Phase 8, step 3).
- Target picker tied to workflow metadata - the JSON editor carries the target verbatim so targets remain authorable, but validation against workflow metadata is future work.

## Execution Checklist

### Step 1: Editor layout

- [ ] `BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor` with master/detail layout.
- [ ] Jobs master list, New / Select / Duplicate / Delete actions.
- [ ] Detail form with Name / Description / Workflow / default output.

### Step 2: Actions editor

- [ ] Add / remove actions; edit Label / Limit / PermutationOrder / RandomPermutationSeed inline.
- [ ] Compute and display per-action iteration count (cartesian product of variation sizes, capped by Limit).

### Step 3: Directives editor

- [ ] Per action, list directives with `$type` tag and summary.
- [ ] "Add directive" opens a type picker with all 9 subtypes; seeds a default instance; opens JSON editor dialog.
- [ ] Edit / Remove buttons per row.

### Step 4: Variations editor

- [ ] Per action, list variations with `$type` tag and summary.
- [ ] "Add variation" opens a type picker with all 7 subtypes; seeds a default instance; opens JSON editor dialog.
- [ ] Edit / Remove buttons per row.

### Step 5: Totals + save

- [ ] Total generations indicator at the form level (sum of per-action sizes).
- [ ] Save button persists via `IJobRepository` with success snackbar.

### Step 6: Wire into page

- [ ] Replace the placeholder in `Pages/Scheduler.razor` with the new component.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                   |
| ---- | ------ | ---------- | ------------------------------------------------------- |
| 1    | [x]    | 2          | Master/detail layout                                    |
| 2    | [x]    | 3          | Actions add/remove/reorder + per-action iteration count |
| 3    | [x]    | 3          | Directive add menu (9 subtypes) + JSON dialog           |
| 4    | [x]    | 3          | Variation add menu (7 subtypes) + JSON dialog           |
| 5    | [x]    | 1          | Total count + Save/Duplicate/Delete                     |
| 6    | [x]    | 1          | Page panel wired                                        |
