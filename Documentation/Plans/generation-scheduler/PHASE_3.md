# Phase 3 - Variation Engine

## Status

**Phase:** 3 - COMPLETE
**Build Status:** Green | **Tests:** 57/57 (20 new Scheduler tests)

---

## Objective

Turn `Variation` configuration types into concrete iteration value sequences and combine them into a deterministic, cartesian iteration plan that the execution engine (Phase 4) will consume.

Deliverables:

- `IVariationMaterializer` - resolves a single `Variation` into a list of typed values. Handles deterministic variations (List, Range, Toggle, SearchReplace) and service-backed variations (Random, Wildcard, LLM).
- `VariationPlan` - holds per-variation materialized values plus the total vs. capped iteration count.
- `IVariationSequencer` - produces the plan and yields an ordered stream of `IterationValueSet` per cartesian index, respecting `PermutationOrder` and `Limit`.

Out of scope: applying values to `GenerationParameters` (Phase 4), invoking generations (Phase 4), UI concerns.

---

## Execution Checklist

### Step 1: Materialized value types + deterministic materializer

**Complexity:** 3 | **Status:** [x] COMPLETE

- [x] `IterationValue` - `(Variation, object?)` record
- [x] `IterationValueSet` - immutable ordered list + iteration index
- [x] `VariationPlan` - per-variation values, `TotalCount`, `EffectiveCount`, `WasTruncated`, permutation metadata
- [x] `VariationMaterializer` covering `ListVariation`, `RangeVariation` (inclusive stepped with int coercion), `ToggleVariation`, `SearchReplaceVariation`
- [x] `RandomVariation` materialization seeded by `Random(Seed.Value)` or `Random.Shared`

### Step 2: Service-backed materialization (wildcard + LLM)

**Complexity:** 3 | **Status:** [x] COMPLETE

- [x] `WildcardVariation`: no-repeats + no-count uses `GetAllEntryValues`; otherwise loops `GetRandomEntry` / `GetRandomEntryWeighted` with dedup when `AllowRepeats=false`
- [x] `LlmVariation`: loops `OllamaService.ExpandPrompt` `Count` times
- [x] Async signature `Task<IReadOnlyList<object?>> MaterializeAsync(Variation, CancellationToken)` with cancellation support

### Step 3: Sequencer (cartesian product + limit + permutation order)

**Complexity:** 2 | **Status:** [x] COMPLETE

- [x] `IVariationSequencer.BuildPlanAsync(JobAction)` materializes all variations and applies `Limit`
- [x] `Enumerate(VariationPlan)` yields `IterationValueSet` per index
- [x] Sequential: mixed-radix decomposition with leftmost variation as slowest-varying outer loop
- [x] Random: Fisher-Yates shuffle over the full index space using `Random(RandomPermutationSeed ?? Random.Shared.Next())`, then trimmed to `EffectiveCount`
- [x] Empty variations produce one empty iteration set; empty materialized value lists are substituted with a single `null` slot to preserve multiplication

### Step 4: Tests

**Complexity:** 3 | **Status:** [x] COMPLETE - 20 new tests, 57/57 total

- [x] `VariationMaterializerTests` (13 tests): List, Range (double/integer/invalid), Toggle, SearchReplace, Random (seed determinism, int coercion, invalid range), Wildcard (all-entries mode, weighted path, dedup on no-repeats, null collection)
- [x] `VariationSequencerTests` (7 tests): no variations, 3x2 cartesian, Limit truncation, Limit above total, random reproducibility by seed, random no-replacement with limit, empty materialized list substitutes null slot
- [x] All pre-existing Scheduler tests still green

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                 |
| ---- | ------ | ---------- | --------------------------------------------------------------------- |
| 1    | [x]    | 3          | Engine types + deterministic + random materialization                 |
| 2    | [x]    | 3          | Wildcard (all/random/weighted + dedup) and LLM paths                  |
| 3    | [x]    | 2          | Mixed-radix sequential + seeded Fisher-Yates random                   |
| 4    | [x]    | 3          | 20 tests added (13 materializer + 7 sequencer); 57/57 scheduler green |

---

## Artifacts

- `BlazorWebApp/Scheduler/Engine/IterationValue.cs`
- `BlazorWebApp/Scheduler/Engine/IterationValueSet.cs`
- `BlazorWebApp/Scheduler/Engine/VariationPlan.cs`
- `BlazorWebApp/Scheduler/Engine/IVariationMaterializer.cs`
- `BlazorWebApp/Scheduler/Engine/VariationMaterializer.cs`
- `BlazorWebApp/Scheduler/Engine/IVariationSequencer.cs`
- `BlazorWebApp/Scheduler/Engine/VariationSequencer.cs`
- DI registrations added to `BlazorWebApp/Program.cs` (scoped)
- `BlazorWebApp.Tests/Scheduler/VariationMaterializerTests.cs`
- `BlazorWebApp.Tests/Scheduler/VariationSequencerTests.cs`

## Notes & Decisions

- **Null slot substitution:** When a variation materializes zero values (e.g. an unknown wildcard collection), the sequencer substitutes a single-null placeholder so the cartesian product doesn't collapse to zero. Phase 4 will decide whether to skip the `ParameterApplier` call for `null` values.
- **Range float tolerance:** `RangeVariation` uses `step/2` tolerance on the inclusive upper bound to avoid floating-point misses (`1..3 step 0.5` yields 5 values including `3.0`).
- **Integer coercion:** Both `Range` and `Random` emit `long` values when `IsInteger=true` so JSON round-trips stay numeric without inventing a float.
- **Wildcard dedup guard:** When `AllowRepeats=false` and duplicates are drawn, the loop retries but bails out after `count*4` collisions to avoid infinite loops on thin collections.
- **LLM cost awareness:** `LlmVariation` serializes its `ExpandPrompt` calls (one per `Count`). Parallelization is deferred to avoid hammering the Ollama backend; revisit in Phase 8 if needed.
- **Scoped vs singleton:** Materializer and sequencer are registered `Scoped` because they transitively depend on scoped Ollama/Wildcard services.
