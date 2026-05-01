# Fragment Defaults Migration - Implementation Plan

## Status
**Current Phase:** Planning

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

---

## Problem Statement

Fragment `Build()` methods contain hard-coded literal fallback values:

```csharp
var samplerName = fragment?.GetString("sampler_name", "exponential/res_2s") ?? "exponential/res_2s";
var steps       = fragment?.GetInt("steps", 9) ?? 9;
var cfg         = fragment?.GetDouble("cfg", 1.0) ?? 1.0;
```

These literals are duplicated between `Metadata.DefaultValue` (which drives first-load UI init)
and `Build()` fallbacks (which drive generation-time fallbacks). When a workflow needs non-standard
defaults (e.g. LTX uses cfg=1.0 instead of cfg=7.0) the values must be maintained in two or more
places with no compile-time link between them.

The same issue exists in `Metadata` definitions:

```csharp
new FragmentParameter { Name = "steps", DefaultValue = 9 }  // hard-coded
```

And in `SamplerWanFragment.Build()`, which ignores fragment state entirely and always constructs
`new Parameters()`, meaning persisted state from the UI is never applied.

---

## Proposed Solution

Every fragment that has user-facing configurable defaults exposes:

```csharp
public Parameters Defaults { get; init; } = new();
```

where `Parameters` is the existing inner class (or a new one if not present).

Both `Metadata` and `Build()` read from `this.Defaults`:

```csharp
// Metadata
new FragmentParameter { Name = "steps", DefaultValue = Defaults.Steps }

// Build()
var steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps;
```

Workflow fields set per-workflow values at declaration:

```csharp
private readonly SamplerFragment _sampler = new()
{
    Defaults = new() { Steps = 9, Cfg = 1.0, SamplerName = "exponential/res_2s" }
};
```

This is the **single source of truth**: the workflow declares defaults once, and both the
UI initialization path (`InitializeFragmentsFromBuilder`) and the generation path (`Build()`)
read from the same object.

### Key Decisions

| Decision | Rationale |
|---|---|
| Use `Parameters` inner class as the `Defaults` type | Already established; avoids a parallel type hierarchy |
| `Defaults { get; init; } = new()` (init-only) | Allows object-initializer syntax at declaration site; immutable after construction |
| Only migrate fragments with configurable defaults | Empty-string fragments (Prompts, LoadImage) have no meaningful per-workflow overrides |
| Fix `SamplerWanFragment` to read fragment state | It currently ignores persisted fragment values entirely - this is a correctness bug |
| `Metadata` is a computed property - reads `Defaults` at access time | `Defaults` is set before any `Metadata` access, so values are always current |

### Conventions
- The `Defaults` property is declared before `Metadata` in the class body
- `Parameters` inner class defaults represent the canonical "generic" fallback (what ComfyUI itself uses)
- Per-workflow deviations from the canonical default are set via `Defaults = new() { ... }` at declaration
- Fragments that exclusively use the explicit `Parameters`-overload Build() (hidden infrastructure
  fragments) are lower priority but still benefit for correctness if called via the interface

---

## Fragment Inventory

### Already Migrated
| Fragment | Location | Notes |
|---|---|---|
| `SamplerFragment` | `Core/` | `Defaults` property + `Metadata` + `Build()` all wired |
| `SamplerStandardFragment` | `Core/` | Same pattern |

### Not In Scope (empty-string or structural constants)
| Fragment | Reason |
|---|---|
| `LoadImageFragment` | Default is always `""` (file picker value) |
| `LoadCheckpointFragment` | Model name is a file picker; prompts are always `""` |
| `PromptsFragment` / `WanPromptsFragment` / `TextEncodeWanFragment` | Always `""` |
| `LoadClipVisionFragment` | Single filename - all Wan workflows use the same model |

---

## Implementation Phases

### Phase 1 - Core Sampler Fragments
**Objective:** Migrate the remaining sampler fragments that have user-visible step/cfg/sampler settings.
**Complexity:** 5 points
**Status:** [ ] Not Started

Three fragments, all already have a `Parameters` inner class.

#### Steps
- [ ] 1.1 - `SamplerCustomAdvancedFragment`: add `Defaults`, wire `Metadata` and `Build()`
- [ ] 1.2 - `SamplerAdvancedFragment` (Wan): add `Defaults`, wire `Metadata` and `Build()`
- [ ] 1.3 - `SamplerWanFragment`: add `Defaults`, fix interface `Build()` to read fragment state, wire fallbacks to `Defaults.*`
- [ ] 1.4 - Update Wan workflows (`WanSteadyDancerWorkflow`, `WanImg2VidWorkflow`) with explicit `Defaults` where their values deviate from the class defaults
- [ ] 1.5 - Build verification

#### Fragment Detail

**SamplerCustomAdvancedFragment** - hard-coded in interface `Build()`:
| Param | Current literal | Notes |
|---|---|---|
| `sampler_name` | `"euler"` | class default already `"euler"` |
| `steps` | `20` | class default already `20` |
| `cfg` | `5.0` | class default already `5.0` |
| `seed` | `42` | class default already `42` |

**SamplerAdvancedFragment** - hard-coded in interface `Build()`:
| Param | Current literal | Notes |
|---|---|---|
| `seed` | `42` | class default already `42` |
| `steps` | `8` | class default already `8` |
| `cfg` | `1.0` | class default already `1.0` |
| `sampler_name` | `"euler"` | class default already `"euler"` |
| `scheduler` | `"simple"` | class default already `"simple"` |

**SamplerWanFragment** - interface `Build()` ignores `fragment` state entirely:
Current bug: `BuildInternal(builder, registry, new Parameters(), scope, scopeTitle)` - always uses class defaults.
Fix: read from `parameters.GetFragment(Metadata.Id)` with `Defaults.*` fallbacks.
| Param | Current class default |
|---|---|
| `Scheduler` | `"dpm++_sde"` |
| `Steps` | `4` |
| `Cfg` | `1` |
| `Shift` | `5` |
| `Seed` | `42` |
| `Denoise` | `1` |

#### Success Criteria
- All three fragments compile with `Defaults` property
- `Metadata.DefaultValue` reads from `Defaults.*`
- Interface `Build()` reads from `fragment` state with `Defaults.*` as fallback
- `SamplerWanFragment.Build()` no longer ignores persisted fragment state
- Wan workflows declare `Defaults = new() { ... }` where needed

---

### Phase 2 - Latent and Resolution Fragments
**Objective:** Migrate fragments that control image/latent dimensions and scale settings.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] 2.1 - `EmptyLatentFragment`: add `Parameters` class + `Defaults`, wire `Metadata` and `Build()`
- [ ] 2.2 - `ZImageUpscaleFragment`: add `Parameters` class + `Defaults`, wire `Metadata` and `Build()`
- [ ] 2.3 - `LtxSamplerFragment` (UI-only): add `Parameters` class + `Defaults`, wire `Metadata.DefaultValue` entries
- [ ] 2.4 - Update workflows that use these fragments with explicit `Defaults` where they deviate
- [ ] 2.5 - Build verification

#### Fragment Detail

**EmptyLatentFragment** - hard-coded in interface `Build()`:
| Param | Current literal | Notes |
|---|---|---|
| `width` | `1024` | Varies per model family (SD=512, Flux=1024) |
| `height` | `1024` | Varies per model family |
| `batch_size` | `1` | Universal default |
| `latent_class` | `"EmptySD3LatentImage"` | Varies: SD uses `EmptyLatentImage`, Flux2 differs |

**ZImageUpscaleFragment** - hard-coded in interface `Build()`:
| Param | Current literal |
|---|---|
| `upscale_by` | `1.5` |
| `strength` | `0.2` |

**LtxSamplerFragment** - UI-only (no `Build()` nodes), but `Metadata.DefaultValue` hard-coded:
| Param | Current literal |
|---|---|
| `cfg` | `1.0` |
| `seed` | `-1L` |

#### Success Criteria
- All three fragments compile with `Defaults` property
- `EmptyLatentFragment.Build()` reads `width`/`height`/`latent_class` from `Defaults.*`
- Workflows using `EmptyLatentFragment` with non-1024 dimensions declare `Defaults = new() { Width = ..., Height = ... }`
- `LtxSamplerFragment.Metadata` reads `DefaultValue` from `Defaults.*`

---

### Phase 3 - Enhancement Fragments
**Objective:** Migrate enhancement/upscale fragments which have the most configurable defaults.
**Complexity:** 8 points
**Status:** [ ] Not Started

These are the most complex fragments. They have many parameters and some cross-fragment reads
(e.g., `UpscaleFragment` reads sampler settings from the `main_sampler` fragment).

#### Steps
- [ ] 3.1 - `UpscaleFragment`: add `Defaults`, wire `Metadata` and `Build()`; note cross-fragment reads remain unchanged
- [ ] 3.2 - `DetailerFragment`: add `Defaults`, wire `Metadata` and `Build()`
- [ ] 3.3 - `SeedVR2UpscaleFragment`: add `Defaults`, wire `Metadata` and `Build()`
- [ ] 3.4 - `SeedVarianceEnhancerFragment`: add `Defaults`, wire `Metadata` and `Build()`
- [ ] 3.5 - Update workflows that use these fragments with `Defaults = new() { ... }` where needed
- [ ] 3.6 - Build verification

#### Fragment Detail

**UpscaleFragment** - hard-coded in `Build()`:
| Param | Current literal | Notes |
|---|---|---|
| `upscale_model` | `"4x-UltraSharpV2.safetensors"` | Per-workflow model choice |
| `upscale_width` | `0` | 0 = auto |
| `upscale_height` | `0` | 0 = auto |
| `upscale_steps` | `20` | Per-workflow steps |
| `upscale_denoise` | `1.0` | |
| `upscale_scale` | `2.0` | Per-workflow scale factor |
| `latent_width` | `1024` | Cross-fragment read from `latent` |
| `latent_height` | `1024` | Cross-fragment read from `latent` |

Note: `SamplerName`, `Scheduler`, `Cfg`, `Seed` in `UpscaleFragment.Build()` are cross-fragment
reads from `main_sampler` - those literal fallbacks (`"multistep/res_2m"`, `"beta"`, `1.0`, `42`)
should also be migrated to `Defaults.*`.

**DetailerFragment** - 12 params, all hard-coded:
`DetectionModel`, `Sampler`, `Scheduler`, `Seed`, `Steps`, `Cfg`, `Denoise`, `Feather`,
`BboxThreshold`, `BboxDilation`, `BboxCropFactor`, `DropSize`, `GuideSize`, `MaxSize`, `Cycle`

**SeedVR2UpscaleFragment** - 10 params all hard-coded:
`Model`, `VaeModel`, `Seed`, `Resolution`, `BatchSize`, `InputNoiseScale`, `LatentNoiseScale`,
`BlocksToSwap`, `VaeTileSize`, `VaeTileOverlap`

**SeedVarianceEnhancerFragment** - 8 params all hard-coded:
`RandomizePercent`, `Strength`, `NoiseInsert`, `StepsSwitchoverPercent`, `Seed`,
`MaskStartsAt`, `MaskPercent`, `LogToConsole`

#### Success Criteria
- All four fragments compile with `Defaults` property
- All `Build()` literal fallbacks replaced with `Defaults.*`
- Workflows calling enhancement fragments can override defaults via `Defaults = new() { ... }`
- No regression in existing enhancement workflows

---

### Phase 4 - Utility and Infrastructure Fragments
**Objective:** Migrate remaining utility fragments with per-workflow-meaningful defaults.
**Complexity:** 5 points
**Status:** [ ] Not Started

These fragments are mostly hidden infrastructure (no UI), but their defaults still affect
output quality or file organization.

#### Steps
- [ ] 4.1 - `SaveFragment`: has `Parameters` class; fix interface `Build()` to use `Defaults.FilenamePrefix`
- [ ] 4.2 - `SaveVideoFragment`: add `Defaults`, wire `Build()`
- [ ] 4.3 - `LoadVideoFragment`: add `Parameters` class + `Defaults`, wire `Build()`
- [ ] 4.4 - `LoadImageScaledFragment`: add `Parameters` class + `Defaults`, wire `Build()`
- [ ] 4.5 - `ModelSamplingAuraFlowFragment`: add `Parameters` class + `Defaults`, wire `Build()`
- [ ] 4.6 - `WanLoadImageFragment`: add `Parameters` class + `Defaults`, wire `Build()`
- [ ] 4.7 - Update workflows that declare these fragments with `Defaults = new() { ... }` where values deviate
- [ ] 4.8 - Build verification

#### Fragment Detail

**SaveFragment** - has `Parameters` but interface `Build()` uses literal `"tmp/img"` instead of `Defaults.FilenamePrefix`
**SaveVideoFragment** - hardcoded `frame_rate=16`, `image_input_name`, `filename_prefix`
**LoadVideoFragment** - hardcoded `force_rate=16`, `custom_width=480`, `custom_height=832`, `frame_load_cap=176`
**LoadImageScaledFragment** - hardcoded `upscale_method="lanczos"`, `megapixels=1`
**ModelSamplingAuraFlowFragment** - hardcoded `model_shift=3.0`
**WanLoadImageFragment** - hardcoded `width=768`, `height=768`

#### Success Criteria
- All six fragments compile with `Defaults` property
- Interface `Build()` no longer contains any hard-coded literal fallback values
- Workflows using `SaveFragment` can set their output folder via `Defaults = new() { FilenamePrefix = "Flux2/Output" }`

---

### Phase 5 - Documentation
**Objective:** Record the `Defaults` pattern as the canonical convention across all
workflow-authoring documentation.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] 5.1 - Update `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`:
  - Add "Fragment Defaults" section to the Fragment documentation
  - Show the `Defaults { get; init; }` property pattern with a code example
  - Document that `Metadata.DefaultValue` and `Build()` fallbacks must both read from `Defaults.*`
  - Note: fragments with only empty-string defaults (Prompts, LoadImage) are exempt
- [ ] 5.2 - Update `.github/prompts/workflow-conversion.prompt.md`:
  - Add to "Step 4: Implement Fragment Fields" that sampler/upscale/latent fragments must set `Defaults = new() { ... }` at declaration
  - Add a checklist item: "No hard-coded literals in `Build()` fallbacks - use `Defaults.*`"
- [ ] 5.3 - Update this MAIN_PLAN.md status to Documentation

#### Success Criteria
- A developer reading TEMPLATE_GUIDE.md understands the `Defaults` pattern before writing their first fragment
- workflow-conversion.prompt.md includes the `Defaults` declaration as a required step
- No documentation contradicts the new convention

---

## Stress Points and Risks

| Risk | Mitigation | Complexity |
|---|---|---|
| `SamplerWanFragment` interface `Build()` never reads fragment state | Fix is part of Phase 1 Step 1.3; it's a correctness bug, not just a style issue | 3 |
| `UpscaleFragment` reads cross-fragment values (`main_sampler`) with literal fallbacks | These also move to `Defaults.*` - the upscale fragment's defaults become the fallback for those cross-reads too | 5 |
| Workflow fields using the explicit-Parameters overload already work; migration could be no-op for them | Explicit overloads still work. The `Defaults` value only matters for the interface Build() path. No regression possible. | 1 |
| `EmptyLatentFragment.latent_class` varies by model family but is not currently a UI-visible parameter | Confirm workflows set latent_class via the explicit overload and that the interface Build() fallback is only hit on init. If Metadata doesn't expose `latent_class` as a fragment param, its `Defaults.LatentClass` only affects `Build()` not init. Acceptable. | 2 |
| `Metadata` is a computed `get` property - if `Defaults` is null at access time, NullReferenceException | `Defaults` has `init; = new()` guarantee. Cannot be null. | 1 |
| Phase 3 enhancement fragments have 10+ parameters each | Work through them mechanically; no logic changes, purely wiring. | 5 |

---

## Changelog

| Phase | Changes |
|---|---|
| Planning | Initial plan created. Sampler fragments (SamplerFragment, SamplerStandardFragment) already migrated as of previous session. |

---

## References

- [SamplerFragment.cs](../../../BlazorWebApp/Workflows/Fragments/Core/SamplerFragment.cs) - Reference implementation (already migrated)
- [SamplerStandardFragment.cs](../../../BlazorWebApp/Workflows/Fragments/Core/SamplerStandardFragment.cs) - Reference implementation (already migrated)
- [TEMPLATE_GUIDE.md](../../../BlazorWebApp/Workflows/TEMPLATE_GUIDE.md) - Fragment authoring conventions (to be updated in Phase 5)
- [workflow-conversion.prompt.md](../../../.github/prompts/workflow-conversion.prompt.md) - Workflow conversion agent (to be updated in Phase 5)
