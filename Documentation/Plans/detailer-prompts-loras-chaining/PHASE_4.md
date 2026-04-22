# Phase 4 - Chained Detailers (Multi-Pass)

## Status
**Phase:** 4
**Build Status:** (pending)

## Objective
Generalize the single detailer block to N passes, each with its own prompt, LoRAs, sampler/detection parameters. Chained via `image_output` registry key.

## Design

### Storage model

| Concept | Storage |
|---------|---------|
| Pass count | `detailer.pass_count` (int) on the detailer fragment. Default = 1. |
| Per-pass parameters | Indexed keys on the detailer fragment. Pass 0 keeps legacy names (`detailer_prompt`, etc.). Passes >= 1 use prefix `pass_{i}_` (e.g. `pass_1_detailer_prompt`, `pass_1_detailer_seed`). |
| Per-pass LoRAs | New `Dictionary<int, List<Lora>> DetailerLorasByPass` on `GenerationParameters`. Pass 0 is the existing `DetailerLoras` list (kept as a convenience property backed by the dictionary). |

### Scope mapping

| Pass | Scope string |
|------|--------------|
| 0 | `detailer_` (legacy) |
| 1 | `detailer_1_` |
| N | `detailer_{N}_` |

Keeping pass 0 on the legacy `detailer_` scope preserves backward compat with saved registry/parameter snapshots and avoids touching any fragment/loader's default scope behaviour.

### UI
- `DetailerForm` becomes a `MudTabs` host. Tab per pass. `+` adds a pass; close icon per tab removes the current pass (min 1). The tab body is the existing form body, parameterized by a `_currentPass` index so property keys are resolved correctly.
- When user copies from main (prompts / loras), the active pass is the target.

### Workflow loop

Each detailer-capable workflow replaces its single `if (detailerFragment?.IsActive)` block with:

```csharp
if (detailerFragment?.IsActive == true)
{
    var passCount = Math.Max(1, detailerFragment.GetInt("pass_count", 1));
    for (int i = 0; i < passCount; i++)
    {
        var scope = i == 0 ? "detailer_" : $"detailer_{i}_";
        var prefix = i == 0 ? string.Empty : $"pass_{i}_";
        // loader (scoped) -> detailer loras (scoped) -> detailer (scoped, reads image_output, writes image_output)
        ...
    }
}
```

## Execution checklist

### Step 1: Data model
- [x] `DetailerLorasByPass` dictionary on `GenerationParameters`, with `GetDetailerLoras(int)` helper that auto-creates.
- [x] Keep `DetailerLoras` as a view over pass 0 for UI compatibility.
- [x] Update `Clone()` and `GenerationParametersJsonConverter`.
- [x] `GenerationParameterService.LoadParameters` / `InitializeFromWorkflowAsync` mirror of dictionary.

### Step 2: UI
- [x] Tabbed `DetailerForm` with add/close actions.
- [x] `_currentPass` drives key prefix.
- [x] Copy/clear operates on active pass.

### Step 3: Workflow loop
- [x] All 9 detailer-capable workflows loop 0..passCount-1 with proper scopes.

### Step 4: Legacy compat
- [x] `detailer_*` keys load as pass 0 (automatic since pass 0 uses legacy names).

## Issues / Decisions
- To limit blast radius, pass-0 keys stay on the legacy names. Only pass >= 1 carries the `pass_{i}_` prefix. Saves a migration pass.
- The registration keys in `NodeRegistry` for pass 0 remain `detailer_model_output` etc. Pass 1 uses `detailer_1_model_output`. Each pass reads `image_output` (main registry) and overwrites it, so chaining is free.
