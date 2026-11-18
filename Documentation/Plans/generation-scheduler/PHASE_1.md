# Phase 1 - Core Models & Serialization

## Status

**Phase:** 1 - COMPLETE
**Build Status:** Passing | **Tests:** 25/25 passing

---

## Objective

Define the data model for the Generation Scheduler feature:

- `ParameterTarget` hierarchy addressing fragment values, LoRAs, assets, prompt text, and output fields
- `Directive` hierarchy describing stackable unit operations applied to base parameters
- `Variation` hierarchy describing sequence-producing dimensions combined via cartesian product
- `Job`, `JobAction`, `JobRunState`, `JobOutputConfig`, `JobStatus` aggregate models
- Polymorphic JSON serialization via `[JsonDerivedType]` with `"$type"` discriminator
- Unit-test coverage proving lossless round-trip for every subtype

---

## Execution Checklist

### Step 1: `ParameterTarget` hierarchy - COMPLETE

**Complexity:** 3 | **Status:** [x] Complete

- [x] [BlazorWebApp/Scheduler/Targets/ParameterTarget.cs](../../../BlazorWebApp/Scheduler/Targets/ParameterTarget.cs)
- [x] [BlazorWebApp/Scheduler/Targets/FragmentTarget.cs](../../../BlazorWebApp/Scheduler/Targets/FragmentTarget.cs)
- [x] [BlazorWebApp/Scheduler/Targets/LoraTarget.cs](../../../BlazorWebApp/Scheduler/Targets/LoraTarget.cs)
- [x] [BlazorWebApp/Scheduler/Targets/AssetTarget.cs](../../../BlazorWebApp/Scheduler/Targets/AssetTarget.cs)
- [x] [BlazorWebApp/Scheduler/Targets/PromptTarget.cs](../../../BlazorWebApp/Scheduler/Targets/PromptTarget.cs)
- [x] [BlazorWebApp/Scheduler/Targets/OutputTarget.cs](../../../BlazorWebApp/Scheduler/Targets/OutputTarget.cs)
- [x] [BlazorWebApp/Scheduler/Targets/OutputField.cs](../../../BlazorWebApp/Scheduler/Targets/OutputField.cs)

**Design note:** `DisplayName` is exposed as a method `GetDisplayName()` rather than an abstract property, because `[JsonIgnore]` on an abstract property declaration does not propagate to derived overrides under `System.Text.Json` polymorphic serialization. Using a method avoids the need to decorate every override and guarantees the value never leaks into persisted JSON.

### Step 2: `Directive` hierarchy - COMPLETE

**Complexity:** 3 | **Status:** [x] Complete

- [x] `Directive.cs` - abstract base + discriminator table
- [x] `SetValueDirective` (`set`), `AppendPromptDirective` (`append_prompt`), `ReplacePromptDirective` (`replace_prompt`)
- [x] `AddLoraDirective` (`add_lora`), `RemoveLoraDirective` (`remove_lora`), `ToggleLoraDirective` (`toggle_lora`)
- [x] `AddPromptStyleDirective` (`add_style`), `SwapAssetDirective` (`swap_asset`), `SetOutputDirective` (`set_output`)

All types live under `BlazorWebApp.Scheduler.Directives` as pure configuration holders; apply logic lands in Phase 4.

### Step 3: `Variation` hierarchy - COMPLETE

**Complexity:** 3 | **Status:** [x] Complete

- [x] `Variation.cs` - abstract base + discriminator table; `Label`, `Target`
- [x] `PermutationOrder` enum (`Sequential`, `Random`)
- [x] `ListVariation` (`list`), `RangeVariation` (`range`), `RandomVariation` (`random`)
- [x] `WildcardVariation` (`wildcard`), `LlmVariation` (`llm`)
- [x] `SearchReplaceVariation` (`search_replace`), `ToggleVariation` (`toggle`)

**Design note:** Materialization methods are intentionally absent. The applier added in Phase 4 will consume `IWildcardService`, `OllamaService`, and a seeded RNG to produce concrete value sequences.

### Step 4: `Job` aggregate - COMPLETE

**Complexity:** 2 | **Status:** [x] Complete

- [x] `JobStatus` enum (`Draft`, `Queued`, `Running`, `Paused`, `Completed`, `Failed`, `Cancelled`)
- [x] `JobOutputConfig` - `ProjectName`, `FolderName`
- [x] `JobRunState` - progress counters, timestamps, resume indices
- [x] `JobAction` - `Order`, `Label`, `Limit`, `PermutationOrder`, `RandomPermutationSeed`, `OutputOverride`, directives + variations
- [x] `Job` - `Id`, `Name`, `Description`, `Status`, `WorkflowId`, `BaseParameters`, `OutputConfig`, actions, run state

All types live under `BlazorWebApp.Scheduler.Models`.

### Step 5: JSON serialization + round-trip tests - COMPLETE

**Complexity:** 3 | **Status:** [x] Complete

- [x] [BlazorWebApp/Scheduler/SchedulerJsonOptions.cs](../../../BlazorWebApp/Scheduler/SchedulerJsonOptions.cs) - `Default` (indented) and `Compact` static options (camelCase, enum-as-string, ignore-nulls-on-write, case-insensitive reads)
- [x] [BlazorWebApp.Tests/Scheduler/ParameterTargetSerializationTests.cs](../../../BlazorWebApp.Tests/Scheduler/ParameterTargetSerializationTests.cs) - 6 tests
- [x] [BlazorWebApp.Tests/Scheduler/DirectiveSerializationTests.cs](../../../BlazorWebApp.Tests/Scheduler/DirectiveSerializationTests.cs) - 10 tests
- [x] [BlazorWebApp.Tests/Scheduler/VariationSerializationTests.cs](../../../BlazorWebApp.Tests/Scheduler/VariationSerializationTests.cs) - 7 tests
- [x] [BlazorWebApp.Tests/Scheduler/JobSerializationTests.cs](../../../BlazorWebApp.Tests/Scheduler/JobSerializationTests.cs) - 2 tests (round-trips through both `Default` and `Compact` options)
- [x] All 25 tests passing

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                              |
| ---- | ------ | ---------- | -------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 3          | `DisplayName` converted to method (STJ polymorphic `[JsonIgnore]` does not flow through overrides) |
| 2    | [x]    | 3          |                                                                                                    |
| 3    | [x]    | 3          | Materialization deferred to Phase 4                                                                |
| 4    | [x]    | 2          |                                                                                                    |
| 5    | [x]    | 3          | 25/25 tests green                                                                                  |

**Total delivered:** 14 pts

---

## Issues & Resolutions

### Issue 1: Namespace collision with `BlazorWebApp.Models.Scheduler`

**Symptom:** Build error `CS0118: 'Scheduler' is a namespace but is used like a type` in `BlazorWebApp.Tests/TestFixtures/BackendTestFixtures.cs`.

**Cause:** Adding `BlazorWebApp.Tests.Scheduler` (test folder) + `BlazorWebApp.Scheduler` (feature namespace) made the unqualified `Scheduler` ambiguous because the file also used `using BlazorWebApp.Models;` (which defines a `Scheduler` POCO type).

**Resolution:** Fully qualified the POCO as `BlazorWebApp.Models.Scheduler` in `BackendTestFixtures.GetSampleSchedulers()` only. No other call sites needed updates.

### Issue 2: Abstract `[JsonIgnore]` property still serialized

**Symptom:** `DisplayName_PropertyIsNotSerialized` failing - `displayName` appeared in output JSON despite `[JsonIgnore]` on the abstract declaration.

**Cause:** System.Text.Json resolves `[JsonIgnore]` at the declared member level; overrides re-expose the property unless each override also carries `[JsonIgnore]`.

**Resolution:** Converted `DisplayName` to a `GetDisplayName()` method. Methods are never considered for serialization, so no attributes are needed on overrides.

---

## Commit Checkpoints

- [x] After Step 1 - targets scaffolded
- [x] After Step 2 - directives scaffolded
- [x] After Step 3 - variations scaffolded
- [x] After Step 4 - job aggregate scaffolded
- [x] After Step 5 - serialization verified, Phase 1 done
