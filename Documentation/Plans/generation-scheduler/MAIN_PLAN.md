# Generation Scheduler (Jobs) - Implementation Plan

## Status

**Current Phase:** Phase 2 complete (8 pts, 37/37 tests cumulative). Ready for Phase 3.

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)

- **1**: Trivial (simple property change, config update)
- **2**: Simple (straightforward refactor, single file change)
- **3**: Moderate (multi-file change, simple logic)
- **5**: Medium (service extraction, interface creation)
- **8**: Complex (component migration, breaking changes)
- **13**: Very complex (architecture change, wide impact)
- **21+**: Epic (should be split into smaller phases)

### Key Rules

- **Each step = commitable checkpoint** for safe implementation
- **No time/date references** - use complexity points only
- **Detours are acceptable** after discussion - append to main plan
- **Phase documents must contain enough context** to resume in new sessions
- **Minimal, focused changes** - avoid over-engineering
- **User permission required** before moving to next phase
- **All events must use the pub/sub pattern via `EventService.cs`**
- **PowerShell automation:** create a `.ps1` file and run it; do not run jobs inline

---

## Problem Statement

The application currently performs generation one request at a time from the Generate page. There is no built-in way to run sequenced, multi-iteration generation campaigns that explore parameter variations, leverage wildcards/LLM for prompt exploration, or sequence multiple mini-batches with different configurations. The Artist Browser implements a hardcoded, purpose-built flavor of this but is not reusable for general parameter exploration.

We want a **generation scheduler** that lets users:

- Snapshot the current generation parameters from the Generate page
- Build a sequenced campaign of ordered **Actions**, each producing a set of generations
- Apply stackable **Directives** (unit operations: set model, append prompt text, toggle LoRA, swap VAE, add prompt style, etc.)
- Apply **Variations** that produce sequences of values (List, Range, Random, Wildcard, LLM, Search/Replace, Toggle) which are combined via cartesian product to drive multi-iteration generation
- Enforce a per-Action **Limit** that caps total iterations (and can truncate large cartesian products)
- Persist jobs in the database and resume running jobs after a pause, crash, or reload
- Monitor live progress with per-image and overall progress indicators, and view results inline via the existing `ImagesContainer`

---

## Proposed Solution

A new `/scheduler` page housing two tabs (**Runs** and **Editor**), backed by a `SchedulerService` that orchestrates execution. Jobs are persisted via a new `Job` entity and driven by polymorphic `Directive` and `Variation` models that are extensible by design.

### Terminology

| Concept       | Meaning                                                                                                                              |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| **Job**       | A persisted campaign definition (workflow + base params + actions + output config).                                                  |
| **Action**    | Ordered unit inside a job; produces a set of generations. Isolated from other actions (always draws from base params).               |
| **Directive** | Stackable unit operation applied to base params at the start of each iteration (e.g., set model, append prompt text). Order matters. |
| **Variation** | A dimension that produces a sequence of values; combined with other variations via cartesian product.                                |
| **Limit**     | Per-action cap on iteration count; if cartesian product is smaller, all combinations run.                                            |
| **Target**    | Addresses a parameter location: fragment value, LoRA, asset, prompt text, or output field.                                           |

### High-Level Flow

```
Generate page  ->  [Schedule] button  ->  /scheduler#editor (new job seeded with snapshot)
                                            |
                                            v
                                     Edit actions/directives/variations
                                            |
                                            v
                                     Save -> /scheduler#runs
                                            |
                                            v
                                     Run -> SchedulerService loops actions,
                                            expands variations, calls
                                            IRouterService.PostGenerationAsync,
                                            stores images via IImageService.
```

### Key Decisions

| Decision                                                         | Rationale                                                                                  |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Dedicated `/scheduler` page with two tabs (Runs / Editor)        | Keeps Generate page focused; editor needs room for action/directive/variation composition. |
| Actions are isolated (always start from base params)             | Predictable, independent re-runs; users can opt-in to persistence by repeating directives. |
| Directives are stackable and polymorphic                         | Easy to extend with new operations (append, replace, toggle, add style).                   |
| Variations are polymorphic with a uniform `Materialize` contract | Consistent handling of cartesian product and limit enforcement across all variation types. |
| Parameter targeting via a `ParameterTarget` abstraction          | Unifies fragment params, LoRAs, assets, prompt, and output under a single targeting model. |
| Limit is mandatory, defaults to computed cartesian product size  | Users see a predictable ceiling; infinite variations (random/LLM) require explicit count.  |
| Base params snapshot stored with the job                         | Job is self-contained; editing a job's base params requires returning to Generate page.    |
| Workflow is readonly after job creation                          | Workflow changes invalidate Directives/Variations; force new job instead.                  |
| LLM prefetch pipelined with image generation                     | Ollama prepares next prompt while ComfyUI generates the previous image.                    |
| Sequential execution (ComfyUI queue unused)                      | Simpler pause/resume/cancel semantics from our side.                                       |
| Event-driven progress via `EventService`                         | Matches existing pub/sub architecture.                                                     |

### Conventions

- Namespaces: `BlazorWebApp.Scheduler.*` for new models/services; existing `Services/` folder for the service layer.
- Polymorphic JSON serialization uses `System.Text.Json` with `[JsonDerivedType]` attributes and a discriminator property `"$type"`.
- Entity stored as a single JSON blob column in SQLite (following the pattern used for `GenerationParameters` and `AppState`).
- All events published via `IEventService`; no direct component-to-component coupling.
- Blazor components follow existing folder conventions under `Components/Scheduler/`.

---

## Implementation Phases

### Phase 1: Core Models & Serialization

**Objective:** Define the data model for Jobs, Actions, Directives, Variations, and Targets with stable serialization.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Scaffold `ParameterTarget` hierarchy (`FragmentTarget`, `LoraTarget`, `AssetTarget`, `PromptTarget`, `OutputTarget`) [3 pts]
- [ ] Step 2 - Scaffold `Directive` hierarchy with initial concrete types [3 pts]
- [ ] Step 3 - Scaffold `Variation` hierarchy with initial concrete types [3 pts]
- [ ] Step 4 - Define `Job`, `JobAction`, `JobRunState`, `JobOutputConfig`, `JobStatus` models [2 pts]
- [ ] Step 5 - Configure polymorphic JSON serialization with discriminators and write unit tests for round-trips [3 pts]

#### Success Criteria

- All models compile and serialize/deserialize losslessly
- Unit tests cover each Directive and Variation subtype
- `Job` can be saved and reloaded from JSON with full fidelity

---

### Phase 2: Persistence Layer

**Objective:** Persist Jobs to the database with run-state recovery after restart.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Add `Job` entity (JSON-backed columns for body + run state) and EF migration [3 pts]
- [ ] Step 2 - Create `IJobRepository` / `JobRepository` for CRUD operations [3 pts]
- [ ] Step 3 - Extend `IDatabaseService` with job accessors [2 pts]

#### Success Criteria

- Jobs can be created, listed, loaded, updated, deleted
- A job left in `Running` or `Paused` state survives restart and can resume

---

### Phase 3: Variation Engine

**Objective:** Materialize variation sequences, compute cartesian product + limit, expose a deterministic iterator.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Implement `Materialize` for each Variation type (deterministic values, random, wildcard via `IWildcardService`, S/R, toggle) [5 pts]
- [ ] Step 2 - Implement `LlmVariation.MaterializeAsync` using `OllamaService` [3 pts]
- [ ] Step 3 - Build `VariationSequencer` that computes total count, applies Limit, and yields an ordered sequence of `IterationValueSet` (one object per Variation, indexed by cartesian position) [5 pts]
- [ ] Step 4 - Implement permutation order option (Sequential nested loops by default; Random sampling mode) [3 pts]
- [ ] Step 5 - Unit tests covering edge cases (empty variations, limit below total, limit above total, random with seed) [3 pts]

#### Success Criteria

- `VariationSequencer.GetPlan(action)` returns a `VariationPlan` describing total count vs limit and truncation behavior
- `IterationValueSet` iteration aligns with user-visible order in the UI

---

### Phase 4: Scheduler Service & Execution Engine

**Objective:** Execute a job end-to-end, handling actions, iteration loops, lifecycle, and events.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Create `ISchedulerService` / `SchedulerService` with lifecycle methods (`RunAsync`, `PauseAsync`, `ResumeAsync`, `StopAsync`, `SkipCurrentAsync`) [5 pts]
- [ ] Step 2 - Implement `ParameterApplier` that writes a value to a `ParameterTarget` on a `GenerationParameters` instance [3 pts]
- [ ] Step 3 - Implement `DirectiveExecutor` that applies each Directive type (SetValue, AppendPrompt, ReplacePrompt, AddLora, RemoveLora, ToggleLora, AddPromptStyle, SwapAsset, SetOutput) [5 pts]
- [ ] Step 4 - Build the action loop: for each iteration, clone base params, apply directives, apply variation values, invoke `IRouterService.PostGenerationAsync`, save results via `IImageService` [5 pts]
- [ ] Step 5 - Implement output routing per action (`Project` / `Folder` override applied at save time) [2 pts]
- [ ] Step 6 - Add LLM pipelining: kick off next-iteration LLM materialization while current generation is in flight [3 pts]
- [ ] Step 7 - Publish lifecycle + progress events (`JobStartedEventArgs`, `JobProgressChangedEventArgs`, `JobCompletedEventArgs`, `JobActionChangedEventArgs`, `JobImageGeneratedEventArgs`) [2 pts]
- [ ] Step 8 - Persist run state after each iteration for resume capability [2 pts]

#### Success Criteria

- A complete job with 2+ actions, mixed directives, and multiple variations produces the expected number of generations with correct parameter application
- Pause/Resume/Stop/Skip Current work correctly without losing progress
- Run state survives process restart mid-job

---

### Phase 5: Scheduler Page - Runs Tab

**Objective:** List, monitor, and control job runs.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Create `/scheduler` page route with three tabs (`Runs`, `Editor`, `Results`) [2 pts]
- [ ] Step 2 - Runs tab listing jobs with status, step count, last run, actions (Run, Resume, Edit, Duplicate, Delete, View Results) [3 pts]
- [ ] Step 3 - Run detail view: overall progress bar, current action indicator, action buttons (Start/Restart, Resume, Pause, Stop, Skip Current) [5 pts]
- [ ] Step 4 - "View Results" action navigates to the Results tab filtered to the selected job [2 pts]

#### Success Criteria

- Users can start, pause, resume, stop, and skip current image from the Runs tab
- Run detail surfaces the overall progress and per-action progress clearly
- Navigation between Runs and Results tabs preserves job context

---

### Phase 6: Scheduler Page - Results Tab

**Objective:** Provide a dedicated results view for live monitoring and historical browsing, with per-job filtering.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Context

Results for jobs are regular `Image` rows saved through `IImageService` into the `Project` / `Folder` resolved by the job's (or action's) output config. The Results tab provides a scheduler-centric lens on that data:

- When a job is running, the tab shows a live preview card followed by completed images in real time.
- When no job is running (or the user selects a prior job), the tab acts as a filtered gallery over previous outputs produced by that job.

Images remain fully accessible from the regular Gallery; this tab is purely an organizational/live-monitoring layer.

#### Steps

- [ ] Step 1 - Job selector at top of the tab (dropdown or card list) with "All Jobs" option and auto-selection of the currently running job [2 pts]
- [ ] Step 2 - Live preview card pinned at the top: current generation thumbnail, per-image progress bar, action/iteration indicator, target summary (which directives/variation values produced this) [5 pts]
- [ ] Step 3 - Overall job progress bar above the image container (completed/total, failed count, elapsed time) [2 pts]
- [ ] Step 4 - Embed `ImagesContainer` filtered to images produced by the selected job; paginated with auto-advance when new images arrive during a live run [5 pts]
- [ ] Step 5 - Image metadata association: store `JobId`, `ActionOrder`, `IterationIndex` on generated images (either via a thin join table `JobImage` or a nullable `JobId` column on `Image`) so the Results tab can filter reliably [5 pts]
- [ ] Step 6 - Filters & sorting inside the tab: by action, by iteration range, by status (completed/failed), by score/favorite - reusing existing filter components where possible [3 pts]
- [ ] Step 7 - Live update via `JobImageGeneratedEventArgs`: new cards appear in the grid, paginating forward if current page is full [3 pts]

#### Success Criteria

- During a live run, the Results tab shows both the in-flight preview and completed images side by side
- Selecting a completed job shows only its images, with full `ImagesContainer` actions (select, score, favorite, etc.)
- Results remain queryable from the regular Gallery independently
- Switching the job selector updates the grid without re-navigating

#### Open Decision - Image Association Storage

| Option                                                              | Pros                                          | Cons                                        |
| ------------------------------------------------------------------- | --------------------------------------------- | ------------------------------------------- |
| Add `JobId` nullable column on `Image`                              | Minimal schema change; easy queries           | Couples `Image` entity to scheduler concept |
| Join table `JobImage (JobId, ImageId, ActionOrder, IterationIndex)` | Clean separation; richer association metadata | Extra table and joins                       |

Recommendation: start with the join table for cleaner separation and to carry `ActionOrder` / `IterationIndex` metadata without polluting the `Image` entity. Will be finalized in Phase 2 when EF migration is scoped.

---

### Phase 7: Scheduler Page - Editor Tab

**Objective:** Compose jobs via UI: base params summary, actions, directives, variations.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Editor layout: name, description, workflow (readonly), base params summary card, output config (default Project/Folder), actions list [3 pts]
- [ ] Step 2 - Base params summary component (readonly view of workflow + fragment values + LoRAs) with "Edit in Generate" button that navigates back preserving job context [5 pts]
- [ ] Step 3 - Action card component: label, limit, permutation order, directives list, variations list, computed size display [5 pts]
- [ ] Step 4 - Directive editor dialog with type selector + per-type form (initial types: SetValue, AppendPrompt, ReplacePrompt, AddLora, RemoveLora, ToggleLora, AddPromptStyle, SwapAsset, SetOutput) [8 pts]
- [ ] Step 5 - Variation editor dialog with type selector + per-type form (List, Range, Random, Wildcard, LLM, SearchReplace, Toggle) [8 pts]
- [ ] Step 6 - Target picker component (shared between Directive and Variation editors): selects fragment/param from the current workflow metadata, or a LoRA/Asset/Prompt/Output target [5 pts]
- [ ] Step 7 - Total generations indicator (sum of per-action computed sizes capped by limits) [2 pts]

#### Success Criteria

- Users can compose a complete job from scratch or from a snapshot
- Target picker only exposes targets valid for the selected workflow
- All directive and variation subtypes are creatable and editable

---

### Phase 8: Generate Page Integration

**Objective:** Add "Schedule" button to Generate page to snapshot current params into a new/existing job.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Add `Schedule` button next to Generate button in `PromptsForm` / `GenerateButton` area [2 pts]
- [ ] Step 2 - Implement snapshot + navigation flow: clone `State.GenerationParameters`, store via session or query param, navigate to `/scheduler` Editor tab [3 pts]
- [ ] Step 3 - Support "edit base params" round-trip: if user came from a job via "Edit in Generate", a banner in Generate page indicates "Editing job X - Save back to job" with a commit button [5 pts]
- [ ] Step 4 - Validate workflow match on round-trip; disallow workflow change while editing an existing job's base params [2 pts]

#### Success Criteria

- One-click capture of current Generate state into a new job
- Round-trip edit flow preserves the in-progress job without duplication
- Attempting to change workflow during round-trip edit is blocked with a clear message

---

### Phase 9: Navigation, Polish, Events

**Objective:** Hook into app navigation, ensure consistency with the rest of the UI, and finalize event wiring.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Add `/scheduler` entry to main navigation menu [1 pt]
- [ ] Step 2 - Add Job event args to `Events/` folder following existing patterns [2 pts]
- [ ] Step 3 - Subscribe/unsubscribe lifecycle in components (`IDisposable`) [2 pts]
- [ ] Step 4 - Theming and MudBlazor component consistency [3 pts]

#### Success Criteria

- Navigation integrates cleanly with existing layout
- No memory leaks from event subscriptions

---

### Phase 10 (Nice-to-Have): Grid Output

**Objective:** Optional grid assembly (XYZ-style) after a job completes.
**Complexity:** 5 points
**Status:** [ ] Not Started - Deferred

#### Steps

- [ ] Step 1 - `GridAssemblyService` using `MagickService` to stitch a grid image with axis labels [5 pts]
- [ ] Step 2 - Configurable per-action or per-job [2 pts]

---

### Phase 11 (Future): Multi-Workflow Chaining

**Objective:** Allow actions to use different workflows and chain outputs between actions.
**Complexity:** 13 points
**Status:** [ ] Not Started - Future Consideration

---

## Stress Points & Risks

| Risk                                                 | Mitigation                                                                                                | Complexity |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------- | ---------- |
| Polymorphic JSON with EF Core JSON-column converters | Use `[JsonDerivedType]` + custom converter; unit test round-trips exhaustively                            | 5          |
| Target validity across workflow changes              | Workflow is readonly post-creation; validate targets against workflow metadata at save/load               | 3          |
| Long-running job blocking UI / losing state          | Run in background task with periodic persistence; decouple via events                                     | 5          |
| Cartesian blow-up with many variations               | Total size always computed and displayed; Limit mandatory and defaults to computed size                   | 3          |
| LLM axis generating slow/unpredictable results       | Prefetch pipelined with generation; fall back to base prompt if LLM fails; timeout per request            | 5          |
| Wildcard collections changing between save and run   | Collections materialized at run time; capture snapshot optionally in a later phase                        | 2          |
| Resume after crash mid-iteration                     | Persist run state after each successful generation; on resume, continue from last completed index         | 3          |
| Race between Pause and in-flight generation          | Treat Pause as "stop after current completes"; Stop cancels in-flight via `IComfyUIService.PostInterrupt` | 3          |

---

## Code Examples

These sketches illustrate the intended shape of the key abstractions. Names and property sets will be refined during Phase 1.

### Parameter Targeting

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FragmentTarget),  "fragment")]
[JsonDerivedType(typeof(LoraTarget),      "lora")]
[JsonDerivedType(typeof(AssetTarget),     "asset")]
[JsonDerivedType(typeof(PromptTarget),    "prompt")]
[JsonDerivedType(typeof(OutputTarget),    "output")]
public abstract class ParameterTarget
{
    /// <summary>Short display label for the UI.</summary>
    public abstract string DisplayName { get; }
}

public sealed class FragmentTarget : ParameterTarget
{
    public string FragmentId { get; set; } = "";   // e.g., FragmentKeys.Fragments.MainSampler
    public string ParamKey   { get; set; } = "";   // e.g., FragmentKeys.Params.Steps
    public override string DisplayName => $"{FragmentId}.{ParamKey}";
}

public sealed class LoraTarget : ParameterTarget
{
    public string LoraName { get; set; } = "";
    public override string DisplayName => $"LoRA:{LoraName}";
}

public sealed class AssetTarget : ParameterTarget
{
    public string AssetKey { get; set; } = "";     // "Model", "Vae", "Clip", ...
    public override string DisplayName => $"Asset:{AssetKey}";
}

public sealed class PromptTarget : ParameterTarget
{
    public bool IsNegative { get; set; }
    public override string DisplayName => IsNegative ? "Prompt (negative)" : "Prompt (positive)";
}

public sealed class OutputTarget : ParameterTarget
{
    public OutputField Field { get; set; }         // Project | Folder
    public override string DisplayName => $"Output:{Field}";
}

public enum OutputField { Project, Folder }
```

### Directives (stackable unit operations)

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SetValueDirective),        "set")]
[JsonDerivedType(typeof(AppendPromptDirective),    "append_prompt")]
[JsonDerivedType(typeof(ReplacePromptDirective),   "replace_prompt")]
[JsonDerivedType(typeof(AddLoraDirective),         "add_lora")]
[JsonDerivedType(typeof(RemoveLoraDirective),      "remove_lora")]
[JsonDerivedType(typeof(ToggleLoraDirective),      "toggle_lora")]
[JsonDerivedType(typeof(AddPromptStyleDirective),  "add_style")]
[JsonDerivedType(typeof(SwapAssetDirective),       "swap_asset")]
[JsonDerivedType(typeof(SetOutputDirective),       "set_output")]
public abstract class Directive
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }

    /// <summary>
    /// Applies this directive to the parameters in-place.
    /// Called once per iteration, before Variations are applied.
    /// </summary>
    public abstract void Apply(DirectiveContext ctx);
}

public sealed class SetValueDirective : Directive
{
    public ParameterTarget Target { get; set; } = default!;
    public object? Value { get; set; }
    public override void Apply(DirectiveContext ctx) =>
        ctx.Applier.Apply(ctx.Parameters, Target, Value);
}

public sealed class AppendPromptDirective : Directive
{
    public string Text { get; set; } = "";
    public bool IsPrefix { get; set; }
    public bool IsNegative { get; set; }
    public string Separator { get; set; } = ", ";
    public override void Apply(DirectiveContext ctx) =>
        ctx.PromptOps.Append(ctx.Parameters, Text, IsPrefix, IsNegative, Separator);
}

public sealed class ReplacePromptDirective : Directive
{
    public string Search { get; set; } = "";
    public string Replace { get; set; } = "";
    public bool IsNegative { get; set; }
    public bool CaseSensitive { get; set; }
    public override void Apply(DirectiveContext ctx) =>
        ctx.PromptOps.Replace(ctx.Parameters, Search, Replace, IsNegative, CaseSensitive);
}

public sealed class AddLoraDirective : Directive
{
    public Lora Lora { get; set; } = default!;
    public override void Apply(DirectiveContext ctx) =>
        ctx.LoraOps.Add(ctx.Parameters, Lora);
}

public sealed class ToggleLoraDirective : Directive
{
    public string LoraName { get; set; } = "";
    public bool Enable { get; set; }
    public override void Apply(DirectiveContext ctx) =>
        ctx.LoraOps.Toggle(ctx.Parameters, LoraName, Enable);
}

public sealed class AddPromptStyleDirective : Directive
{
    public List<string> StyleNames { get; set; } = new();
    public override void Apply(DirectiveContext ctx) =>
        ctx.StyleOps.Apply(ctx.Parameters, StyleNames);
}

public sealed class SwapAssetDirective : Directive
{
    public string AssetKey { get; set; } = "";
    public string AssetValue { get; set; } = "";
    public override void Apply(DirectiveContext ctx) =>
        ctx.Parameters.Assets[AssetKey] = AssetValue;
}

public sealed class SetOutputDirective : Directive
{
    public string? ProjectName { get; set; }
    public string? FolderName { get; set; }
    public override void Apply(DirectiveContext ctx) =>
        ctx.OutputOps.SetOverride(ProjectName, FolderName);
}

/// <summary>Context injected into each Directive.Apply call; groups helper operations and the live parameters.</summary>
public sealed record DirectiveContext(
    GenerationParameters Parameters,
    ParameterApplier     Applier,
    IPromptOperations    PromptOps,
    ILoraOperations      LoraOps,
    IStyleOperations     StyleOps,
    IOutputOperations    OutputOps);
```

### Variations (sequence-producing dimensions)

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ListVariation),          "list")]
[JsonDerivedType(typeof(RangeVariation),         "range")]
[JsonDerivedType(typeof(RandomVariation),        "random")]
[JsonDerivedType(typeof(WildcardVariation),      "wildcard")]
[JsonDerivedType(typeof(LlmVariation),           "llm")]
[JsonDerivedType(typeof(SearchReplaceVariation), "search_replace")]
[JsonDerivedType(typeof(ToggleVariation),        "toggle")]
public abstract class Variation
{
    public string? Label { get; set; }
    public ParameterTarget Target { get; set; } = default!;

    /// <summary>
    /// Predicted number of values this variation will produce.
    /// For non-deterministic variations (Random, LLM, Wildcard-with-repeats),
    /// this is the configured count.
    /// </summary>
    public abstract int GetCount();

    /// <summary>
    /// Materializes the concrete value list for this action run.
    /// Called once before the action begins iterating.
    /// </summary>
    public abstract Task<IReadOnlyList<object>> MaterializeAsync(VariationContext ctx);

    /// <summary>
    /// Applies a materialized value to the parameters.
    /// Default implementation uses the applier + target; override for custom apply logic.
    /// </summary>
    public virtual void Apply(GenerationParameters parameters, object value, ParameterApplier applier) =>
        applier.Apply(parameters, Target, value);
}

public sealed class ListVariation : Variation
{
    public List<object> Values { get; set; } = new();
    public override int GetCount() => Values.Count;
    public override Task<IReadOnlyList<object>> MaterializeAsync(VariationContext _) =>
        Task.FromResult<IReadOnlyList<object>>(Values);
}

public sealed class RangeVariation : Variation
{
    public double Start { get; set; }
    public double End   { get; set; }
    public double Step  { get; set; }
    public bool   IsInteger { get; set; }
    public override int GetCount() =>
        Math.Max(0, (int)Math.Floor((End - Start) / Step) + 1);
    public override Task<IReadOnlyList<object>> MaterializeAsync(VariationContext _)
    {
        var list = new List<object>(GetCount());
        for (var v = Start; v <= End + 1e-9; v += Step)
            list.Add(IsInteger ? (object)(long)v : v);
        return Task.FromResult<IReadOnlyList<object>>(list);
    }
}

public sealed class RandomVariation : Variation
{
    public double Min { get; set; }
    public double Max { get; set; }
    public int    Count { get; set; } = 1;
    public int?   Seed { get; set; }
    public bool   IsInteger { get; set; }
    public override int GetCount() => Count;
    public override Task<IReadOnlyList<object>> MaterializeAsync(VariationContext _)
    {
        var rng = Seed.HasValue ? new Random(Seed.Value) : new Random();
        var list = new List<object>(Count);
        for (var i = 0; i < Count; i++)
        {
            var v = Min + rng.NextDouble() * (Max - Min);
            list.Add(IsInteger ? (object)(long)v : v);
        }
        return Task.FromResult<IReadOnlyList<object>>(list);
    }
}

public sealed class WildcardVariation : Variation
{
    public string CollectionName { get; set; } = "";
    public int?   Count { get; set; }          // null -> all entries (no repeats)
    public bool   AllowRepeats { get; set; }
    public bool   Weighted { get; set; }
    public override int GetCount() => Count ?? 0; // resolved post-materialization if null
    public override async Task<IReadOnlyList<object>> MaterializeAsync(VariationContext ctx)
    {
        var all = await ctx.Wildcards.GetAllEntryValues(CollectionName);
        if (!AllowRepeats)
        {
            var take = Count ?? all.Count;
            return all.Cast<object>().Take(take).ToList();
        }
        var n = Count ?? 1;
        var pick = new List<object>(n);
        for (var i = 0; i < n; i++)
        {
            var value = Weighted
                ? await ctx.Wildcards.GetRandomEntryWeighted(CollectionName)
                : await ctx.Wildcards.GetRandomEntry(CollectionName);
            pick.Add(value ?? "");
        }
        return pick;
    }
}

public sealed class LlmVariation : Variation
{
    public string  ModelName { get; set; } = "";
    public string  BasePrompt { get; set; } = "";
    public string? SystemPrompt { get; set; }
    public int     Count { get; set; } = 1;
    public bool    IsNegative { get; set; }
    public override int GetCount() => Count;
    public override async Task<IReadOnlyList<object>> MaterializeAsync(VariationContext ctx)
    {
        // Prefetched by the scheduler while prior generations are in flight.
        // Implementation delegates to OllamaService to produce `Count` variations
        // of BasePrompt guided by SystemPrompt.
        return await ctx.Llm.GenerateVariationsAsync(ModelName, BasePrompt, SystemPrompt, Count);
    }
}

public sealed class SearchReplaceVariation : Variation
{
    public string Search { get; set; } = "";
    public List<string> Replacements { get; set; } = new();
    public bool IsNegative { get; set; }
    public bool CaseSensitive { get; set; }
    public override int GetCount() => Replacements.Count;
    public override Task<IReadOnlyList<object>> MaterializeAsync(VariationContext _) =>
        Task.FromResult<IReadOnlyList<object>>(Replacements.Cast<object>().ToList());
    public override void Apply(GenerationParameters p, object value, ParameterApplier _)
    {
        // Applies S/R on the prompt text rather than setting a parameter value.
        // Implementation uses PromptOperations.Replace.
    }
}

public sealed class ToggleVariation : Variation
{
    public object? OnValue { get; set; }
    public object? OffValue { get; set; }
    public override int GetCount() => 2;
    public override Task<IReadOnlyList<object>> MaterializeAsync(VariationContext _) =>
        Task.FromResult<IReadOnlyList<object>>(new[] { OnValue!, OffValue! });
}

public sealed record VariationContext(
    IWildcardService    Wildcards,
    ILlmVariationSource Llm,
    ParameterApplier    Applier);
```

### Action and Job

```csharp
public enum PermutationOrder { Sequential, Random }

public sealed class JobAction
{
    public int Order { get; set; }
    public string? Label { get; set; }

    /// <summary>
    /// Hard cap on iterations. Defaults to cartesian product size on creation.
    /// Set to int.MaxValue to run everything.
    /// </summary>
    public int Limit { get; set; } = int.MaxValue;

    public PermutationOrder PermutationOrder { get; set; } = PermutationOrder.Sequential;
    public int? RandomPermutationSeed { get; set; }

    public JobOutputConfig? OutputOverride { get; set; }

    public List<Directive> Directives { get; set; } = new();
    public List<Variation> Variations { get; set; } = new();
}

public sealed class JobOutputConfig
{
    public string? ProjectName { get; set; }
    public string? FolderName  { get; set; }
}

public enum JobStatus { Draft, Queued, Running, Paused, Completed, Failed, Cancelled }

public sealed class Job
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;

    public Guid WorkflowId { get; set; }
    public GenerationParameters BaseParameters { get; set; } = new();
    public JobOutputConfig OutputConfig { get; set; } = new();

    public List<JobAction> Actions { get; set; } = new();

    /// <summary>Transient run tracker; persisted to support resume after restart.</summary>
    public JobRunState? RunState { get; set; }
}

public sealed class JobRunState
{
    public int CurrentActionIndex { get; set; }
    public int CurrentIterationIndex { get; set; }
    public int TotalIterations { get; set; }
    public int CompletedImages { get; set; }
    public int FailedImages { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### Iteration Flow (pseudocode)

```csharp
foreach (var action in job.Actions.OrderBy(a => a.Order))
{
    // 1. Materialize all variations once, in parallel.
    var materialized = await Task.WhenAll(
        action.Variations.Select(v => v.MaterializeAsync(variationContext)));

    // 2. Compute cartesian plan and apply Limit.
    var plan = VariationSequencer.GetPlan(action, materialized);
    events.Publish(new JobActionChangedEventArgs(action.Order, plan.EffectiveCount));

    // 3. Iterate.
    foreach (var iteration in plan)
    {
        ct.ThrowIfCancellationRequested();
        await WaitIfPaused(ct);

        var parameters = job.BaseParameters.Clone();
        var ctx = new DirectiveContext(parameters, ...);

        // Apply directives in declared order.
        foreach (var d in action.Directives.Where(x => x.Enabled))
            d.Apply(ctx);

        // Apply variation values for this iteration.
        for (int i = 0; i < action.Variations.Count; i++)
            action.Variations[i].Apply(parameters, iteration.Values[i], applier);

        // LLM prefetch next iteration in background.
        _ = PrefetchNextLlmAsync(plan, iteration.Index);

        var images = await router.PostGenerationAsync(parameters, workflow);
        await imageService.SaveAsync(images, ResolveOutput(action, job));

        PersistRunState(job, action, iteration.Index);
        events.Publish(new JobProgressChangedEventArgs(...));
    }
}
```

---

## Changelog

| Phase    | Changes              |
| -------- | -------------------- |
| Planning | Initial plan created |

---

## References

- [Generate Page](../../../BlazorWebApp/Pages/Generate.razor.cs)
- [GenerationParameters Model](../../../BlazorWebApp/Models/GenerationParameters.cs)
- [FragmentKeys Registry](../../../BlazorWebApp/Models/FragmentKeys.cs)
- [RouterService](../../../BlazorWebApp/Services/RouterService.cs)
- [WildcardService](../../../BlazorWebApp/Services/WildcardService.cs)
- [OllamaService](../../../BlazorWebApp/Services/OllamaService.cs)
- [ArtistBrowserService.GenerateBatchPreviewsAsync](../../../BlazorWebApp/Services/ArtistBrowserService.cs) - reference implementation of a simple hardcoded batch loop
- [EventService](../../../BlazorWebApp/Services/EventService.cs) - required pub/sub pattern for all events
- [AppDbContext](../../../BlazorWebApp/Data/AppDbContext.cs) - JSON-backed entity pattern to follow for `Job`
