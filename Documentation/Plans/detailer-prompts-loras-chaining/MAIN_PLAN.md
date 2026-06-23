# Detailer Prompts, LoRAs & Chaining - Implementation Plan

## Status
**Current Phase:** Complete (all 5 phases delivered)

### Phase Summary
| Phase | Title                                                | Points | Status |
| ----- | ---------------------------------------------------- | ------ | ------ |
| 1     | Data Model & Persistence                             | 3      | [x]    |
| 2     | DetailerForm UI (Single Pass)                        | 5      | [x]    |
| 3     | Workflow Integration (All Workflows, Single Pass)    | 8      | [x]    |
| 4     | Chained Detailers (Multi-Pass)                       | 13     | [x]    |
| 5     | Documentation                                        | 3      | [x]    |

Build verified passing after each phase boundary.

### Lessons Learned
- `PrepareGenerationParametersAsync` in `ImageService` inlines main `parameters.Loras` into the
  main prompt as `<lora:...>` syntax for `PCLazyLoraLoader` consumption. Adding an explicit
  main-scope `LoraLoaderFragment.BuildAll` to SD would have double-applied LoRAs, so the plan
  was narrowed to **detailer-scope** LoRA loops in Phase 3. Workflows that already emit an
  explicit main-scope loop stayed unchanged.
- `DetailerFragment` and `LoraLoaderFragment` hardcoded node IDs (`"detailer"`,
  `"detailer_bbox_provider"`, `"lora_loader_{i}"`). These collided when the new workflow
  loop called them twice per generation for chained detailers. Both fragments were updated
  to prefix node IDs with the scope string, making them reentrant without any consumer changes.
- Pass 0 intentionally kept the legacy `"detailer_"` scope and unindexed parameter keys
  (`detailer_prompt`, `detailer_seed`, ...) so saved snapshots from the single-pass era
  migrate for free. Passes >= 1 use `"detailer_{i}_"` scope and `pass_{i}_detailer_xxx` keys.
- `GetStringOrFallback` was added both as an instance method (on `FragmentParameters`, to
  work in files that only import `BlazorWebApp.Workflows.*`) and as an extension (for nullable
  `FragmentParameters?` callsites) - both paths exist because workflow templates commonly
  access the type without importing the `BlazorWebApp.Models` namespace.
- The Scriban-era plan at
  `Documentation/Plans/dynamic-generation-refactor/DETAILER_PROMPTS_LORA_PLAN.md` was archived
  in place with a header redirecting to this plan as the authoritative source.

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete.
   - User must explicitly approve before updating phase document.
   - Build runs only after user requests or after completing all file edits.

### Progress Tracking Symbols
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)
- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic

### Key Rules
- Each step is a commitable checkpoint.
- Minimal, focused changes per step - no unrelated refactors.
- Detours are appended to this plan (new phase, or split existing as 9 -> 9 + 9.5).
- Phase documents must hold enough context to resume work in a fresh session.

---

## Problem Statement

The Detailer enhancement (FaceDetailer-based pipeline) currently supports only the core detection and sampler parameters. Users need:

1. **Distinct prompt injection** per Detailer pass: when blank the main prompt is used; otherwise it overrides (for both positive and negative).
2. **Standalone LoRA panel** for the Detailer: a second LoRA list with enabled/strength per entry, independent from main generation (snapshot "copy from main" support, but not linked - editing one list must not mutate the other).
3. **Chained Detailers**: run multiple Detailer passes sequentially (head / hands / feet / etc.), each with its own prompt + LoRAs + detection & sampler parameters. ComfyUI supports this natively; the output of pass N feeds pass N+1. UI should be tab-based with create/remove actions (like the multi-image-input component), no fixed count.
4. **Workflow parity**: the feature must work across every workflow that ships a Detailer block. All workflows should also gain the app-generated LoRA loader loop (`LoraLoaderFragment.BuildAll`) for both main and detailer scopes - acceptable for `PCLazyLoraLoader`-based pipelines (SD) to keep their prompt-embedded LoRAs in parallel.
5. **Documentation**: update workflow integration docs and conversion prompts so future workflow conversions automatically integrate the new Detailer scope conventions.

Remnants of a previous (scrapped) Detailer prompt/LoRA design exist in `Documentation/Plans/dynamic-generation-refactor/DETAILER_PROMPTS_LORA_PLAN.md` but target the old Scriban `.sbn` fragment system and are **not** applicable to the current C# `IFragmentBuilder` architecture.

---

## Proposed Solution

### Data Model
- Replace the single-instance `detailer` fragment parameters with an **indexed collection** of detailer passes, persisted as fragment data on `GenerationParameters`.
- Add `List<Lora> DetailerLoras` per pass, stored on `GenerationParameters` keyed by pass index (see Phase 4 for the exact serialization choice).
- Keep a single top-level `Detailer` UI section; multiple passes live inside it as tabs.

### Scope Convention
Extend the existing scope strings to support indexed detailer passes:

| Pass       | Scope Prefix    |
|------------|-----------------|
| Main       | `""` (empty)    |
| Detailer 0 | `detailer_0_`   |
| Detailer N | `detailer_{N}_` |

Phase 1-3 implement only pass 0 (behaves exactly like today's single detailer). Phase 4 generalizes to N passes while keeping `detailer_` as an alias for `detailer_0_` for backwards compatibility with saved parameters.

### UI Structure (DetailerForm)
```
+-- Detailer -----------------------------------+
|  [ + Add pass ]   [x] close-tab per pass      |
|  +-- Tab: "Pass 1" -- "Pass 2" -- "+" -----+  |
|  | Tabbed prompts (Positive / Negative)    |  |
|  |   - MudTextField AutoGrow=true MaxLines |  |
|  |   - helper: "Leave blank to use main"   |  |
|  |   [ Copy prompts from main ]            |  |
|  | -- Parameters (sampler, detection, ...) |  |
|  | -- LoRAs (dedicated LoraForm)           |  |
|  |   [ Copy LoRAs from main ]              |  |
|  +-----------------------------------------+  |
+-----------------------------------------------+
```
- Copy buttons are **split** (prompts only / LoRAs only) per user request.
- Tabbed prompts mirror `PromptsForm`'s MudTabs interface for visual consistency in narrow columns.
- `AutoGrow="true" MaxLines="12"` on the prompt fields (MudBlazor 6.20 feature already adopted in `PromptsForm`).

### Workflow Integration
- **Every workflow** gains an explicit `LoraLoaderFragment.BuildAll(builder, registry, parameters.Loras)` call for the main scope. Existing workflows that already do this remain unchanged; workflows that rely solely on `PCLazyLoraLoader` (SD) gain the explicit loop **in addition** - harmless because explicit `LoraLoader` nodes chain after `PCLazyLoraLoader` via the registry's `model_output`/`clip_output` overwrites. Prompt-embedded LoRA syntax continues to work.
- For **every detailer-capable workflow**, after the scoped loader (scope `detailer_{i}_`) the detailer-scoped LoRA loop is appended: `_loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: $"detailer_{i}_")`.
- `detailer_prompt` / `detailer_negative_prompt` fallback to main positive/negative when blank or missing (normalize all workflows to this pattern).

### Key Decisions
| Decision | Rationale |
|----------|-----------|
| Separate per-pass LoRA lists rather than a `Scope` property on `Lora` | Matches "standalone panel, not linked" wording; trivial snapshot via `new Lora(clone)`; avoids touching every `Lora` consumer. |
| Inline tabbed prompts in `DetailerForm` rather than new `SimplePromptsForm` component | Only one consumer; re-uses the existing tabbed-prompt idiom already in `PromptsForm`; no extra indirection. |
| Add explicit `LoraLoader` loop to **all** workflows, even those using `PCLazyLoraLoader` | User requested parity across workflows; both approaches coexist without conflict since the registry composition is order-based. |
| Indexed scope strings (`detailer_{i}_`) rather than a single shared detailer scope with per-pass suffixes | Directly reuses the existing scope machinery in `LoraLoaderFragment` / `LoadCheckpointFragment` / `DetailerFragment`; requires zero changes to those fragments. |
| Pass 0 is stored under both `detailer_0_` and legacy `detailer_` keys for one migration cycle | Preserves saved user parameters; allows incremental rollout. |
| Tab UI modelled on the multi-image-input component | Visual and UX consistency. |

### Conventions
- `IFragmentBuilder` contract unchanged; new detailer passes are emitted from the workflow template loop, not from a new fragment type.
- Registry keys always suffixed with scope prefix (`{scope}model_output`, etc.). Never reference unscoped keys from a scoped context.
- Each chained detailer pass reads `image_output` (the latest, which is always the previous pass's output after it registers) and overwrites `image_output` itself. No new registry keys are needed for chaining.
- The UI never touches `ParameterService.Current.Loras` from within `DetailerForm`. Only `DetailerLoras[passIndex]` is mutated there.

---

## Implementation Phases

### Phase 1: Data Model & Persistence
**Objective:** Add per-pass detailer LoRA storage and a single-pass prompt/LoRA path that matches today's behavior (no chaining yet, no UI yet).
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Add `List<Lora> DetailerLoras { get; set; } = new();` to `GenerationParameters` (single list for now, pass 0 only). Update `Clone()`.
- [ ] Step 2 - Audit `GenerationParametersJsonConverter` and any other serialization/DB code for `Loras` usage; mirror for `DetailerLoras`.
- [ ] Step 3 - Ensure `DetailerLoras` is preserved across workflow switches (`SaveCurrentWorkflowStateAsync` / `InitializeFromWorkflowAsync`) - only if relevant; otherwise document a no-op.

#### Success Criteria
- Unit-test-level proof that `GenerationParameters.Clone()` deep-copies the list (new `Lora` instances).
- JSON round-trip preserves `DetailerLoras`.
- No change in user-visible behaviour.

---

### Phase 2: DetailerForm UI (Single Pass)
**Objective:** Expose prompt injection + LoRA panel + split "Copy from main" buttons for a single detailer pass, using `AutoGrow` tabbed prompts consistent with `PromptsForm`.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Add a `MudTabs` block at top of `DetailerForm` with Positive / Negative panels; each panel hosts a `MudTextField` with `AutoGrow="true" MaxLines="12"` helper text "Leave blank to use main prompt". Values bind to `detailer_prompt` / `detailer_negative_prompt` on the fragment.
- [ ] Step 2 - Below the tabs, add two small buttons: `Copy Prompts from Main`, `Clear Prompts`.
- [ ] Step 3 - Add a `<LoraForm Loras="@ParameterService.Current.DetailerLoras" Backend="Backend.ComfyUI" IsDualModel="@isDualModel" OnLorasUpdated="HandleDetailerLorasUpdated" />` section with its own label.
- [ ] Step 4 - Add two small buttons beside the LoRA form: `Copy LoRAs from Main`, `Clear LoRAs`. Copy clones via `new Lora(main)` for each entry (snapshot, not reference).
- [ ] Step 5 - Wire `OnChanged` invocations so toggling `CollapsibleFeatureSection` IsActive picks up the new data.

#### Success Criteria
- Empty prompt field -> uses main prompt (verified in Phase 3 workflow build).
- Populated prompt field -> overrides main in the built workflow JSON.
- Copying prompts does not link them (editing main after the copy does not mutate detailer).
- Copying LoRAs does not link them (independent list instances).
- `AutoGrow` expands up to 12 lines; no overflow issues at half-width layout.
- Tabs styling matches `PromptsForm` visual rhythm.

---

### Phase 3: Workflow Integration (All Workflows, Single Detailer Pass)
**Objective:** Apply explicit `LoraLoaderFragment.BuildAll` to every workflow for the main scope, and to every detailer-capable workflow for the detailer scope. Normalize prompt fallback.
**Complexity:** 8 points
**Status:** [ ] Not Started

Affected templates (detailer-capable):
- `SDTxt2ImgWorkflow`
- `AnimaTxt2ImgWorkflow`
- `ChromaTxt2ImgWorkflow`
- `ErnieTxt2ImgWorkflow`
- `FluxTxt2ImgWorkflow`
- `Flux2KleinTxt2ImgWorkflow`
- `QwenTxt2ImgWorkflow`
- `ZImageTxt2ImgWorkflow`
- `ZImageImg2ImgWorkflow`

All other workflows: ensure main-scope LoraLoaderFragment.BuildAll is present.

#### Steps
- [ ] Step 1 - Inventory each workflow: has main LoRA loop? has detailer? has prompt fallback? - update per-workflow notes in `PHASE_3.md`.
- [ ] Step 2 - For each workflow missing the main LoRA loop, insert `_loraLoaderFragment.BuildAll(builder, registry, parameters.Loras)` immediately after the main model loader (before sampler). SD workflow: verify `PCLazyLoraLoader` + explicit LoraLoader coexist correctly in produced JSON.
- [ ] Step 3 - For each detailer-capable workflow, insert `_loraLoaderFragment.BuildAll(builder, registry, parameters.DetailerLoras, scope: "detailer_")` after the detailer-scoped loader (before `_detailerFragment.Build`). 
- [ ] Step 4 - Normalize prompt fallback in every detailer block: `detailer_prompt` empty/null -> main positive; `detailer_negative_prompt` empty/null -> main negative.
- [ ] Step 5 - Smoke-test produced ComfyUI JSON for at least SD, Flux, and one UNet-only workflow (Anima/ZImage).

#### Success Criteria
- `run_build` succeeds.
- Produced JSON for each workflow shows scoped LoRA nodes when `DetailerLoras` is non-empty.
- With zero DetailerLoras, the produced JSON is identical to today's output for that workflow.
- Prompt injection behaves as specified (blank = main, filled = override).

---

### Phase 4: Chained Detailers (Multi-Pass)
**Objective:** Generalize the Detailer block to N passes with a tabbed UI (create / delete tabs), per-pass scope `detailer_{i}_`, and sequential image chaining.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Design
- **Fragment storage:** single fragment id `detailer` holds `passes: List<FragmentParameters>` or uses indexed keys (`detailer_0_prompt`, `detailer_1_prompt`, ...). Decision to be finalised in Step 1 based on what round-trips cleanest through `FragmentParameters`.
- **LoRA storage:** `Dictionary<int, List<Lora>> DetailerLorasByPass` on `GenerationParameters`. Helper: `GetDetailerLoras(int index)`.
- **UI:** `MudTabs` with dynamic panels. `+` panel at the end adds a new pass. Each tab has a close affordance (cannot close the last remaining pass). The inner content is the same body built in Phase 2.
- **Workflow loop:** in each detailer-capable workflow, replace the single `if (detailerFragment?.IsActive)` block with `foreach (var pass in parameters.GetDetailerPasses())` that runs loader -> LoRA loop -> DetailerFragment per pass, each with its own scope. Every pass reads `image_output` and writes `image_output`, producing the desired chain.
- **Legacy compatibility:** if saved params only contain unscoped `detailer_` keys, treat as a single pass 0 on load.

#### Steps
- [ ] Step 1 - Confirm storage shape (flat indexed keys vs. nested passes list). Write a short decision note in `PHASE_4.md`.
- [ ] Step 2 - Extend `GenerationParameters`: `DetailerLorasByPass`, `GetDetailerLoras(int)`, `GetOrCreateDetailerPass(int)`, pass count accessor.
- [ ] Step 3 - Update `DetailerForm` to a `MudTabs` host with dynamic `MudTabPanel`s; tab `+` for add, close icon per tab, renaming optional (future). Extract the single-pass body (from Phase 2) into a nested component or a `RenderFragment` for reuse.
- [ ] Step 4 - Update `ParameterApplier` / metadata parsers / `GeneratedImagesInfo` to tolerate or round-trip the indexed detailer data (flagged by earlier `.Loras` grep).
- [ ] Step 5 - Replace the single-pass detailer block in each detailer-capable workflow with the multi-pass loop.
- [ ] Step 6 - Add/adjust tests verifying:
    - 1 pass -> same JSON as Phase 3.
    - 2+ passes -> N scoped loader subgraphs, N FaceDetailer nodes chained via `image_output`.
    - Removing a middle tab does not leave orphaned scopes.

#### Success Criteria
- Users can add, remove, reorder (stretch goal, may defer) detailer passes.
- Each pass has independent prompt/LoRA/parameters.
- Produced workflow chains `image_output` through every active pass.
- Legacy saved params still load and appear as pass 0.

---

### Phase 5: Documentation
**Objective:** Capture conventions in the long-lived guides so new workflow conversions stay consistent.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Update `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`:
    - Explain `detailer_{i}_` scope convention.
    - Document the required LoRA loops for main and every detailer pass.
    - Document the prompt-fallback rule.
    - Add an ASCII block showing chained-detailer registry evolution.
- [ ] Step 2 - Update `.github/prompts/workflow-conversion.prompt.md` with a checklist entry:
    - Main LoRA loop present.
    - Detailer scope(s) with LoRA loop present.
    - Prompt fallback normalized.
- [ ] Step 3 - Archive or link `Documentation/Plans/dynamic-generation-refactor/DETAILER_PROMPTS_LORA_PLAN.md` with a note pointing to this plan as the authoritative one.
- [ ] Step 4 - Mark all phases complete in `MAIN_PLAN.md`; write lessons-learned.

#### Success Criteria
- Any future workflow conversion prompted via `workflow-conversion.prompt.md` automatically produces the new Detailer wiring.
- `TEMPLATE_GUIDE.md` reflects current reality.

---

## Stress Points & Risks
| Risk | Mitigation | Complexity |
|------|------------|------------|
| SD workflow double-applies LoRAs (PCLazyLoraLoader + explicit LoraLoader) -> model over-weighted | Verify in Phase 3 Step 2; if the produced graph doubles LoRAs, gate the explicit loop behind `if (parameters.Loras.Any())` and document that PCLazy handles prompt-embedded LoRAs separately. | 3 |
| Saved parameter JSON with legacy `detailer_` keys breaks after Phase 4 | Compatibility shim in `GenerationParameters`: treat missing indexed keys as pass 0 using the legacy keys. | 3 |
| Tab-based UI state lost on workflow switch | Reuse the `IGenerationParameterService` persistence pathway; tab state derives purely from fragment data. | 2 |
| Chained detailers with divergent image scopes (e.g. upscale between passes) | Explicitly document that chaining happens post-upscale; out-of-order composition is not supported in this plan. | 1 |
| Copy-from-main buttons clone instead of reference - easy to regress | Cover with a small test: mutate main after copy, assert detailer unchanged. | 1 |
| Some workflows' main loader already runs LoRAs internally (Flux-style) - adding `BuildAll` again would be a no-op or conflict | Audit per workflow in Phase 3 Step 1; skip the explicit loop where the loader already invokes it. | 3 |

---

## Changelog
| Phase | Changes |
|-------|---------|
| Planning | Initial plan created. Decisions captured: separate per-pass LoRA lists, indexed `detailer_{i}_` scope, tabbed UI, split copy buttons, docs updates, legacy compatibility shim. |
| 1 | Added `DetailerLoras` (pass 0 backed by `DetailerLorasByPass[0]`), `DetailerLorasByPass` dictionary, `GetDetailerLoras(int)` helper on `GenerationParameters`. JSON converter round-trips both shapes (with legacy single-list read path). `GenerationParameterService` mirrors the dictionary through LoadParameters and the workflow switch save/restore path. |
| 2 | `DetailerForm.razor` rewritten: tabbed positive/negative prompts (AutoGrow, MaxLines=12), split Copy/Clear buttons for prompts and LoRAs, dedicated detailer LoRA panel bound to `GetDetailerLoras(currentPass)`. Added `GetStringOrFallback` helper on `FragmentParameters` + extension. |
| 3 | All 9 detailer-capable workflows (SD, Anima, Chroma, Ernie, Flux, Flux2Klein, Qwen, ZImage Txt/Img2) emit `_loraLoaderFragment.BuildAll(..., DetailerLoras, scope: "detailer_")` after the scoped loader. Prompt fallback normalized to `GetStringOrFallback`. Main-scope LoRA wiring left untouched to avoid double-applying with `PCLazyLoraLoader` (SD) and pre-existing loops in other workflows. |
| 4 | Chained detailers: `DetailerLorasByPass`, `pass_count` on fragment, `pass_{i}_` key prefix for pass >= 1. `DetailerForm` grew pass-navigation bar (add / select / remove). Each workflow's detailer block became a `for i in 0..pass_count-1` loop emitting per-pass loader + lora loop + detailer. `DetailerFragment` and `LoraLoaderFragment` node IDs scoped so multiple passes don't collide. |
| 5 | `TEMPLATE_GUIDE.md` updated with multi-pass conventions, prompt fallback rule, chained pipeline diagram. `.github/prompts/workflow-conversion.prompt.md` updated with the detailer loop checklist. Archived the Scriban-era `DETAILER_PROMPTS_LORA_PLAN.md` with a redirect header. |

---

## References
- `Documentation/Plans/IMPLEMENTATION_GUIDE.md` - planning conventions.
- `Documentation/Plans/dynamic-generation-refactor/DETAILER_PROMPTS_LORA_PLAN.md` - **outdated** (Scriban-era) reference only.
- `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md` - target doc for Phase 5.
- `BlazorWebApp/Workflows/Fragments/Enhancements/DetailerFragment.cs` - consumer of the scope.
- `BlazorWebApp/Workflows/Fragments/Core/LoraLoaderFragment.cs` - scope-aware LoRA emitter (`BuildAll`).
- `BlazorWebApp/Workflows/Fragments/Core/LoadCheckpointFragment.cs` - scope-aware model loader.
- `BlazorWebApp/Components/Shared/Generation/Fragments/PromptsForm.razor` - `AutoGrow` + tabbed prompts reference.
- `BlazorWebApp/Components/Shared/Generation/Fragments/DetailerForm.razor` - target UI file.
- `BlazorWebApp/Models/GenerationParameters.cs` - target data file.
- `.github/prompts/workflow-conversion.prompt.md` - conversion prompt, target for Phase 5 Step 2.
