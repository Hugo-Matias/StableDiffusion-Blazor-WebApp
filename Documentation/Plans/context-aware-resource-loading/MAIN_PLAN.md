# Context-Aware Resource Loading - Implementation Plan

## Status

**Current Phase:** Complete (Phases 8/10 deferred)

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

- Each step = commitable checkpoint for safe implementation
- No time/date references - use complexity points only
- Detours are acceptable after discussion - append to main plan
- Phase documents must contain enough context to resume in new sessions
- Minimal, focused changes - avoid over-engineering
- User permission required before moving to next phase
- All events must use the pub/sub pattern via EventService.cs

---

## Problem Statement

Currently, the resource loading system (checkpoints/diffusion models in WorkflowAssetSelector, LoRAs in LoraForm) presents **all available files** from ComfyUI without regard to the currently selected workflow base (`ModelBase`). This leads to:

1. **Cluttered dropdowns**: Users see SD 1.5, SDXL, Flux, Wan, and other base models mixed together in asset selectors
2. **Incompatible selections**: Users can select a Flux checkpoint for an SD workflow, which would fail at generation
3. **No filtering on LoRA form**: The LoRA autocomplete shows all LoRAs regardless of compatibility with the current workflow base
4. **Unused data point**: The `Resource.BaseModel` field (sourced from CivitAI) contains compatibility information but is not leveraged for filtering

### Key Challenge: CivitAI-to-ModelBase Mapping

CivitAI uses granular base model strings (e.g., `"SD 1.5"`, `"SDXL 1.0"`, `"Flux.1 D"`, `"Wan Video 14B i2v 720p"`), while the app uses a coarser `ModelBase` enum (`StableDiffusion`, `Flux`, `Wan`, etc.). The mapping is many-to-one and not straightforward.

**CivitAI base model strings** (from `basemodels.json`):

- SD family: `SD 1.4`, `SD 1.5`, `SD 1.5 LCM`, `SD 1.5 Hyper`, `SD 2.0`, `SD 2.1`, `SDXL 0.9`, `SDXL 1.0`, `SDXL Lightning`, `SDXL Hyper`, `Pony`, `Pony V7`, `Illustrious`, `NoobAI`
- Flux family: `Flux.1 S`, `Flux.1 D`, `Flux.1 Krea`, `Flux.1 Kontext`, `Flux.2 D`, `Flux.2 Klein 9B`, `Flux.2 Klein 4B`
- Wan family: `Wan Video`, `Wan Video 1.3B t2v`, `Wan Video 14B t2v`, `Wan Video 14B i2v 480p`, `Wan Video 14B i2v 720p`, `Wan Image 2.7`, `Wan Video 2.7`
- Others: `Chroma`, `Qwen`, `Qwen 2`, `LTXV`, `LTXV2`, `LTXV 2.3`, `Ernie`, `ZImageTurbo`, `ZImageBase`, `Anima`

**App `ModelBase` enum**: `StableDiffusion`, `Flux`, `Chroma`, `Qwen`, `ZImage`, `Wan`, `Anima`, `Ernie`, `LTX`

---

## Proposed Solution

### Approach

Instead of a centralized mapping service that translates between `ModelBase` and CivitAI strings (which is too coarse -- e.g., `StableDiffusion` covers both SD 1.5 and SDXL, which are incompatible), the compatibility mapping lives **on each workflow** via a new `CompatibleResourceBaseModels` property.

Each workflow declares which CivitAI base model strings its resources are compatible with. This is more specific and handles edge cases (SDXL vs SD 1.5, Pony, Flux Klein vs Flux Dev) at the workflow level where the author has full knowledge.

The filtering is applied at two levels:

1. **Asset Selectors** (checkpoints/diffusion models): Filter the ComfyUI model list by cross-referencing with `Resource` records whose `BaseModel` matches one of the workflow's `CompatibleResourceBaseModels`
2. **LoRA Form**: Filter the LoRA autocomplete dropdown using the same mechanism, with a user-togglable option to show/hide unmatched/untracked resources

### Key Decisions

| Decision                                                                     | Rationale                                                                                                                                   |
| ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Workflow-level `CompatibleResourceBaseModels` instead of centralized mapping | `ModelBase` is too broad (SD 1.5 vs SDXL). Workflow authors know exactly what's compatible. Avoids a central mapping that can become stale. |
| `CompatibleResourceBaseModels` uses CivitAI base model strings               | Direct match against `Resource.BaseModel` without translation layer.                                                                        |
| In-memory resource cache with event-based invalidation                       | Avoid disk/DB operations per search. Cache invalidated via pub/sub on resource create/edit/delete.                                          |
| Resources without BaseModel or no Resource record shown by default           | Untracked/externally loaded files should not be hidden. User toggle to exclude them.                                                        |
| Filter at the service/data level, not UI level                               | Keeps components thin and logic testable.                                                                                                   |
| Checkpoints/diffusion models and LoRAs are primary focus                     | Other asset types (VAE, CLIP, ClipVision) are nice-to-have and deferred.                                                                    |

### Architecture Overview

```
Current Workflow
    |
    +-- CompatibleResourceBaseModels: ["SDXL 1.0", "SDXL Lightning", "Pony", "Illustrious", "NoobAI"]
    |
    v
ResourceFilterService (with in-memory Resource cache)
    |
    +-- Matches ComfyUI filenames against cached Resource records
    +-- Checks Resource.BaseModel is in workflow's CompatibleResourceBaseModels
    +-- Untracked files (no Resource record) pass through by default
    |
    +-- Used by AssetResolverService / WorkflowAssetSelector (checkpoints/diffusion models)
    +-- Used by LoraForm (lora autocomplete filtering)
```

---

## Implementation Phases

### Phase 1: Workflow Compatibility Metadata

**Objective:** Add `CompatibleResourceBaseModels` to workflow metadata and propagate through the model chain
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Add `CompatibleResourceBaseModels` property to `WorkflowMetadata` and `Workflow` models [3 pts]
  - Added `List<string> CompatibleResourceBaseModels` to `WorkflowMetadata` (default `[]`)
  - Added `List<string>? CompatibleResourceBaseModels` to `Workflow` (nullable)
  - Updated `ConvertBuilderToWorkflow` in `WorkflowService.cs` to propagate (sets null if empty)

- [x] Step 2 - Populate `CompatibleResourceBaseModels` on all existing workflow templates [5 pts]
  - SDTxt2ImgWorkflow: SD 1.4, SD 1.5, SD 1.5 LCM/Hyper, SD 2.x, SDXL 0.9/1.0/LCM/Lightning/Hyper/Distilled, Pony, Illustrious, NoobAI
  - FluxTxt2ImgWorkflow: Flux.1 S, Flux.1 D, Flux.1 Krea
  - Flux2KleinTxt2Img + Flux2KleinImg2ImgEdit: Flux.2 D, Flux.2 Klein 9B/9B-base, 4B/4B-base
  - QwenTxt2Img + QwenImg2ImgEdit: Qwen, Qwen 2
  - WanImg2Vid: Wan Video 2.2 I2V-A14B (specific to double-model architecture)
  - WanSteadyDancer: Wan Video 14B i2v 480p, 720p
  - ZImage workflows: ZImageTurbo, ZImageBase
  - Chroma: Chroma | LTX: LTXV2, LTXV 2.3 | Anima: Anima | Ernie: Ernie
  - Note: Pony V7 excluded (AuraFlow-based, incompatible with SD)

- [x] Step 3 - Unit tests for metadata propagation [2 pts]
  - `GetWorkflows_AllWorkflows_ShouldHaveNonEmptyCompatibleResourceBaseModels`
  - `GetWorkflows_CompatibleResourceBaseModels_ShouldPropagateThroughConversion`
  - `GetWorkflows_DeterministicId_ShouldBeUnaffectedByCompatibleResourceBaseModels`

#### Success Criteria

- All workflow templates declare their compatible CivitAI base models
- The `Workflow` model received by UI components includes the compatibility list
- No regressions in workflow discovery or ID generation

---

### Phase 2: Resource Cache Service

**Objective:** Create an in-memory resource cache with event-based invalidation to avoid per-query DB/disk operations
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Create `IResourceCacheService` interface and `ResourceCacheService` implementation [3 pts]
  - Created `Services/IResourceCacheService.cs` with `GetCachedResourcesAsync`, `FindByFilenameAsync`, `FindByBaseModelsAsync`, `InvalidateCache`
  - Created `Services/ResourceCacheService.cs` with lazy loading, `SemaphoreSlim` for thread safety, dictionary-based filename index
  - Subscribes to `ResourcesChangedEventArgs` via `IEventService` for invalidation
  - Registered as singleton in `Program.cs`

- [x] Step 2 - Unit tests for the cache service [2 pts]
  - 16 tests passing: lazy loading, single DB call, type filtering, filename lookup (exact/case-insensitive/with path), base model filtering (matching/case-insensitive/type filter/null exclusion/no matches), null/empty inputs, cache invalidation (manual and event-driven)

#### Success Criteria

- Resources are loaded from DB once and served from memory
- Cache invalidates when resources are created, edited, or deleted
- Filename lookups are performant (dictionary-based)

---

### Phase 3: Resource Filtering Service

**Objective:** Create a service that filters ComfyUI model/LoRA lists using the resource cache and workflow compatibility data
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Create `IResourceFilterService` interface and `ResourceFilterService` implementation [3 pts]
  - Created `Services/IResourceFilterService.cs` with `FilterAssetsByWorkflowAsync` and `FilterLorasByWorkflowAsync`
  - Created `Services/ResourceFilterService.cs` using `IResourceCacheService` for lookups
  - Shared `FilterByWorkflowAsync` core: HashSet for O(1) compatibility check, case-insensitive
  - Untracked (no record or null/empty BaseModel) included/excluded via `includeUntracked` parameter
  - No filtering when workflow has null/empty `CompatibleResourceBaseModels`
  - Registered as singleton in `Program.cs`

- [x] Step 2 - Unit tests for filtering service [2 pts]
  - 18 tests passing: no-op filtering (null/empty compatibility, empty input), compatible included (exact/case-insensitive/multiple), incompatible excluded, untracked handling (no record/null/empty BaseModel with includeUntracked true/false), LoRA path uses same logic, mixed scenarios, order preservation

- [ ] Step 2 - Unit tests for the filtering service [2 pts]
  - Test filtering with matching resources
  - Test that untracked resources (no Resource record) pass through by default
  - Test that resources with incompatible BaseModel are excluded
  - Test `includeUntracked = false` hides untracked resources
  - Test empty workflow compatibility list returns all
  - Test with empty model lists

#### Success Criteria

- Compatible resources are correctly filtered based on workflow's compatibility list
- Untracked resources pass through by default
- Performance is acceptable (no DB queries per filter call -- uses cache)

---

### Phase 4: Asset Selector Integration

**Objective:** Wire the filtering into the WorkflowAssetSelector for checkpoints/diffusion models
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Integrate ResourceFilterService into AssetResolverService [3 pts]
  - Added `GetFilteredAssetOptions(AssetType, Workflow)` to `IAssetResolverService` and implementation
  - Injected `IResourceFilterService` into `AssetResolverService`
  - Graceful degradation: falls back to unfiltered list if filtering removes all options
  - Updated `WorkflowAssetSelector.razor` to use `GetFilteredAssetOptions` instead of `GetCachedAssetOptions`
  - Selector already reloads on `WorkflowChangedEventArgs` (clears cache and re-loads)

- [x] Step 2 - Test the filtering in the asset selector [2 pts]
  - 3 integration tests in `AssetResolverFilterTests.cs` verifying filter service contract
  - Build verification confirms no regressions

#### Success Criteria

- Checkpoint/diffusion model dropdowns only show compatible models for the active workflow
- Switching workflow updates the available models
- No breaking changes to existing generation flow

---

### Phase 5: LoRA Form Filtering

**Objective:** Filter the LoRA autocomplete in `LoraForm.razor` by workflow compatibility and add user toggle for untracked resources
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Modify LoraForm to apply base model filtering on search results [3 pts]
  - Injected `IOrchestratorService` and `IResourceFilterService` into `LoraForm.razor`
  - `SearchLoras` now calls `Router.SearchLoras()` then applies `FilterLorasByWorkflowAsync()` using current workflow
  - Falls back to unfiltered results if no workflow is set

- [x] Step 2 - Add UI toggle for untracked LoRA visibility [2 pts]
  - Added `MudToggleIconButton` with filter icon next to "Loras" header
  - `_includeUntracked` field (default true) controls whether untracked LoRAs appear in search
  - Toggle shows tooltip explaining current state
  - Toggled state = filter icon with X mark + warning color

#### Success Criteria

- LoRA autocomplete shows only compatible LoRAs for the active workflow
- Toggle allows users to show/hide untracked LoRAs
- Resources without base model or without Resource record shown by default

---

### Phase 6: Documentation & Tooling Updates

**Objective:** Update workflow integration docs, VSCode prompt file, and template guides to include the new `CompatibleResourceBaseModels` field. Reference `basemodels.json` as the source of truth for valid CivitAI base model strings.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Update `TEMPLATE_GUIDE.md` [2 pts]
  - Added `CompatibleResourceBaseModels` to Workflow Metadata table with description
  - Added dedicated subsection explaining purpose and how to pick values from `basemodels.json`
  - Updated code example (metadata + Anima example) to include the property

- [x] Step 2 - Update `workflow-conversion.prompt.md` [2 pts]
  - Added Phase 2e: "Compatible Resource Base Models" step to planning discussion
  - Added `CompatibleResourceBaseModels` to Phase 4 workflow class creation checklist

- [x] Step 3 - Update `FRAGMENT_SCHEMA_GUIDE.md` [1 pt]
  - Updated WorkflowMetadata reference in Data Flow Summary to include `CompatibleResourceBaseModels`

---

### Phase 7: Resource Card Actions & LoRA Prompt Syntax

**Objective:** Modernize resource card actions to use workflow-aware "Send To" pattern (matching image info panel behavior) and fix LoRA prompt syntax extraction during parameter loading.
**Complexity:** 8 points
**Status:** [x] Complete

#### Context

The current resource card actions are outdated:

- **LoRAs**: Offer "Add to Txt2Img" / "Add to Img2Img" for Positive and Negative prompts. This doesn't fit the dynamic generation page + workflow system.
- **Checkpoints**: Offer a "Load Checkpoint" option that doesn't target a specific workflow.
- **LoRA prompt syntax** (`<lora:filename:strength>`): During "Send Parameters To" / `LoadGenerationParametersFromImage`, LoRA tags are stripped from the prompt text but are **not** loaded into the LoRA loader panel.

#### Steps

- [x] Step 1 - Fix LoRA prompt syntax extraction in parameter loading [3 pts]
  - Updated `StateService.LoadGenerationParametersFromImage` to call `Parser.ExtractLorasFromPrompt` on both prompts
  - Extracted LoRAs added to `GenerationParameters.Loras` with duplicate prevention
  - Cleaned prompts (LoRA tags stripped) set on the prompts fragment
  - Existing `ParseAndCleanCopiedPrompt` path unchanged

- [x] Step 2 - Rework resource card LoRA actions [3 pts]
  - Replaced Txt2Img/Img2Img Positive/Negative buttons with single "Add to LoRA Loader" button
  - LoRAs added as Positive by default; Negative controlled via LoRA card toggle
  - Non-LoRA types (TextualInversion, Hypernetwork) retain Prompt/Negative Prompt buttons

- [x] Step 3 - Rework resource card checkpoint/model actions [2 pts]
  - Replaced "Load Checkpoint" with workflow-aware "Send to workflow" showing compatible workflows
  - Compatibility computed by matching resource BaseModel against workflow CompatibleResourceBaseModels
  - Clicking workflow calls `Orchestrator.SetCurrentWorkflow()` to switch
  - Shows warning when no compatible workflows found

#### Success Criteria

- LoRA tags in prompts are extracted and loaded into the LoRA panel during "Send Parameters To"
- Resource card actions reflect the workflow system (no more Txt2Img/Img2Img split)
- LoRA card sends to the correct workflow's loader
- Checkpoint card sends to a compatible workflow's asset selector

---

### Phase 9: Centralized Resource Filter Toolbar

**Objective:** Move all Generation page resource filtering to a centralized toolbar with granular per-base-model chip toggles and master controls. Filter state persists across page navigations but resets on workflow change.
**Complexity:** 10 points
**Status:** [x] Complete

#### Context

Broad workflows like SD declare many compatible base models (SD 1.5, SDXL, Pony, Illustrious, NoobAI, etc.). When working with a specific model (e.g., Illustrious), users still see incompatible LoRAs/checkpoints from other base models in the same family. The current per-component filtering (LoraForm toggle, AssetSelector workflow filter) is too coarse.

The CivitAI page already has a chip-based filter UI (`ToolbarCivitaiSettings.razor`) that serves as a UX reference.

#### Steps

- [x] Step 1 - Create `ResourceFilterStateService` (Scoped) with filter state management [3 pts]
  - Created `IResourceFilterStateService` and `ResourceFilterStateService` (Scoped)
  - Tracks `CurrentWorkflowId`; `EnsureInitializedForWorkflowAsync` resets only when workflow ID changes
  - `AvailableBaseModels` populated from cache, `EnabledBaseModels` initialized to all-enabled
  - Publishes `ResourceFilterChangedEventArgs` on toggle/state changes
  - `GetEffectiveBaseModels()` returns enabled subset, or null when AllowAll/uninitialized
  - 17 unit tests in `ResourceFilterStateServiceTests.cs`

- [x] Step 2 - Add `GetDistinctBaseModelsAsync` to `IResourceCacheService` [1 pt]
  - Returns candidate base model strings that have at least one resource in cache
  - Preserves order from candidates list
  - Implemented in `ResourceCacheService` using in-memory scan

- [x] Step 3 - Add `FilterByBaseModelsAsync` to `IResourceFilterService` [1 pt]
  - Explicit base models overload (decoupled from Workflow object)
  - Shared `FilterCoreAsync` extracted to avoid duplication with `FilterByWorkflowAsync`

- [x] Step 4 - Create `ToolbarResourceFilters.razor` toolbar component [3 pts]
  - "All Models" chip (AllowAll toggle), "Untracked" chip (IncludeUntracked toggle)
  - Per-base-model chips with line-through styling when disabled
  - Base model chips disabled (greyed) when AllowAll is active
  - Shown in `TopToolbar.razor` when `CurrentPage.StartsWith("/generate")`
  - Subscribes to `WorkflowChangedEventArgs` and `ResourceFilterChangedEventArgs`

- [x] Step 5 - Refactor consumers to use centralized filter state [2 pts]
  - `AssetResolverService`: injected `IResourceFilterStateService`, uses `GetEffectiveBaseModels()` in `GetFilteredAssetOptions`
  - `LoraForm.razor`: removed per-component `_includeUntracked` toggle, reads `FilterState`, subscribes to `ResourceFilterChangedEventArgs`
  - `WorkflowAssetSelector.razor`: subscribes to `ResourceFilterChangedEventArgs` to reload assets

#### Success Criteria

- Toolbar shows chips only for base models that have actual resources
- Disabling a chip immediately filters out resources of that base model from both asset selectors and LoRA search
- Filter state persists when navigating away and back to the generation page (same workflow)
- Filter state resets when switching workflows
- "Allow All" bypasses all filtering; "Show Untracked" controls untracked resource visibility

---

### Phase 8: Nice-to-Have - Other Resource Types (Deferred)

**Objective:** Extend filtering to VAE, CLIP, ClipVision asset types
**Complexity:** 3 points
**Status:** [ ] Not Started (Deferred)

This phase is intentionally deferred per user requirements. It can be revisited after the primary (checkpoint/diffusion model/LoRA) filtering is validated.

---

## Stress Points & Risks

| Risk                                                       | Mitigation                                                                                             | Complexity |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | ---------- |
| CivitAI base model strings change over time                | Workflow compatibility lists are easy to update per-workflow; basemodels.json serves as reference      | 2          |
| New workflows created without CompatibleResourceBaseModels | Filtering gracefully degrades: empty list = no filtering. Can add build-time/test warnings.            | 1          |
| Resource cache memory usage for large collections          | Cache only metadata (Id, Filename, BaseModel, TypeId). Lazy-loaded, invalidated via events.            | 2          |
| Untracked resources (no Resource record) dominate the list | Default `includeUntracked=true` ensures visibility; user toggle to hide them                           | 1          |
| Per-workflow compatibility lists become verbose/repetitive | Consider shared constants or helper methods for common families (e.g., `CivitaiBaseModels.FluxFamily`) | 2          |
| Resource events not fired in all mutation paths            | Audit resource create/edit/delete paths to ensure events are published                                 | 3          |

---

## Resolved Questions

1. **SDXL vs SD 1.5 distinction**: Resolved by moving compatibility to the workflow level. Each workflow explicitly declares which CivitAI base model strings are compatible. SD workflows list SD 1.x variants; an SDXL workflow would list SDXL variants, Pony, Illustrious, NoobAI, etc. No need to split the `ModelBase` enum.

2. **Pony/Illustrious/NoobAI**: Handled naturally -- these are just CivitAI strings that a workflow includes in its `CompatibleResourceBaseModels` list when appropriate.

3. **Batch vs. Individual resource lookups**: Resolved with in-memory cache. Resources loaded from DB once, served from memory. Cache invalidated via pub/sub events on resource create/edit/delete.

4. **Sparse Resource records**: Default `includeUntracked=true` shows untracked files. User toggle on LoRA selector to hide/show them. Can expand toggle to other selectors later.

---

## Changelog

| Phase    | Changes                                                                                                                                                                                                            |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Planning | Initial plan created                                                                                                                                                                                               |
| Planning | Revised: workflow-level compatibility replaces centralized mapping service. Added resource cache with event invalidation. Resolved open questions.                                                                 |
| Planning | Added Phase 6 (Documentation & Tooling Updates) for TEMPLATE_GUIDE, workflow-conversion prompt, and FRAGMENT_SCHEMA_GUIDE. Docs will reference basemodels.json as source of truth. Renumbered deferred phase to 7. |
