# Wan LoRA Dual-Model Refactor - Implementation Plan

## Status
**Current Phase:** Execution (Phase 5)

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

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The Wan workflow uses a **dual-model architecture** (high noise model + low noise model). LoRAs in this context can target one or both models independently. The current implementation has several issues:

1. **Single `Strength` property** on `Lora` is shared across both high and low models - no way to set different weights per model.
2. **`HighPath` and `LowPath`** are separate string properties on `Lora`, but they are conceptually part of the same resource. There is no structured pairing - just two loose paths.
3. **CivitAI downloads** have no awareness of dual-model LoRAs. Downloading a LoRA from CivitAI creates a single-file resource with no way to associate it as the "high" or "low" variant of a dual-model LoRA.
4. **`LoraForm` / `LoraCard` UI** only shows a single strength slider and has no UI for managing high/low paths or per-model weights.
5. **`ResourcesService.LoadPrompt`** creates a `Lora` with a single strength and no dual-path awareness.

### Current Architecture (Key Files)

| File | Role | Issues |
|------|------|--------|
| `BlazorWebApp/Models/Lora.cs` | LoRA data model | Single `Strength`, loose `HighPath`/`LowPath` strings |
| `BlazorWebApp/Components/Shared/Generation/LoraCard.razor` | LoRA UI card | Single strength slider, no dual-model UI |
| `BlazorWebApp/Components/Shared/Generation/LoraForm.razor` | LoRA list/search UI | No dual-model awareness |
| `BlazorWebApp/Workflows/Fragments/wan/LoraLoaderModelOnlyFragment.cs` | Workflow fragment | Takes single `LoraStrength` |
| `BlazorWebApp/Workflows/Templates/wan/WanImg2VidWorkflow.cs` | Wan I2V workflow | Uses `lora.Strength` for both models |
| `BlazorWebApp/Services/ResourcesService.cs` | Resource loading | Single strength, no dual-path handling |
| `BlazorWebApp/Services/CivitaiService.cs` | CivitAI downloads | Single file per resource, no dual-model concept |

---

## Proposed Solution

Refactor the `Lora` model to support **independent high/low weights** and treat the high/low file paths as a **single unified resource**. Neither path is required, so a LoRA can be high-only, low-only, or both.

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| Replace single `Strength` with `HighStrength` + `LowStrength` | Each model needs independent weight control |
| Keep `HighPath` / `LowPath` on `Lora` but make them first-class | They already exist; we enhance them with proper UI and weight support |
| Add dual-model awareness to `LoraCard` UI | Users need to see/set weights per model variant |
| Workflow fragments consume per-model strength | `WanImg2VidWorkflow` passes `HighStrength` to high loader, `LowStrength` to low loader |
| CivitAI download supports assigning a file as high/low variant of an existing LoRA | Avoids creating duplicate LoRA entries; builds the resource incrementally |

### Conventions
- Maintain backward compatibility: `Strength` remains as a fallback/default for non-dual workflows
- `HighStrength` and `LowStrength` default to `null` (nullable float); when null, fall back to `Strength`
- UI conditionally shows dual-model controls only when the current workflow context is dual-model (Wan)

---

## Implementation Phases

### Phase 1: Lora Model Refactor
**Objective:** Extend `Lora` model with per-model strength properties and update the clone constructor
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps
- [x] Step 1.1 - Add `HighStrength` and `LowStrength` (nullable float) to `Lora.cs`
- [x] Step 1.2 - Add computed `EffectiveHighStrength` / `EffectiveLowStrength` properties that fall back to `Strength`
- [x] Step 1.3 - Update `Lora(Lora clone)` constructor to copy new properties
- [x] Step 1.4 - Simplified `GenerationParameters.Clone` to use `Lora(Lora clone)` constructor (was inline)
- [x] Step 1.5 - Added 10 unit tests in `BlazorWebApp.Tests/Models/LoraTests.cs`

#### Success Criteria
- `Lora` model supports independent high/low strengths with fallback to `Strength`
- All existing tests pass
- Build succeeds

---

### Phase 2: Workflow Fragment + Template Updates
**Objective:** Wire per-model strengths through `LoraLoaderModelOnlyFragment` and `WanImg2VidWorkflow`
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Step 2.1 - Updated `WanImg2VidWorkflow.Build` to use `EffectiveHighStrength` / `EffectiveLowStrength`
- [x] Step 2.2 - `WanSteadyDancerWorkflow` uses asset-based LoRA (not `parameters.Loras`) - no changes needed
- [x] Step 2.3 - Added 2 workflow tests: independent strengths + null fallback (41/41 passing)

#### Success Criteria
- High model LoRA loader receives `EffectiveHighStrength`
- Low model LoRA loader receives `EffectiveLowStrength`
- Existing workflow tests pass, new tests cover per-model strengths

---

### Phase 3: LoRA UI Refactor (LoraCard + LoraForm)
**Objective:** Add dual-model weight controls to the UI
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps
- [x] Step 3.1 - Extended `LoraCard.razor` with conditional High/Low strength fields when `IsDualModel=true`
- [x] Step 3.2 - Added `bool IsDualModel` parameter cascaded from `Generate.razor` via `_selectedWorkflow?.Base == ModelBase.Wan`
- [x] Step 3.3 - Updated `LoraForm.razor` with Both/High/Low target buttons and path assignment logic
- [x] Step 3.4 - Adding a LoRA in dual-model mode sets HighPath/LowPath based on selected target; supports merging into existing LoRA

#### Success Criteria
- In dual-model workflows, LoRA cards show H/L toggle button for path assignment
- Single strength value applies to whichever model the LoRA is assigned to
- LoRAs default to High on add; user toggles to Low on the card
- In non-dual workflows, UI remains unchanged (no toggle visible)

---

### Phase 4: CivitAI Download & UI Polish
**Objective:** Clean up CivitAI download UI; LoRA path assignment handled at generation time via card toggle
**Complexity:** 2 points
**Status:** [x] Complete

#### Steps
- [x] Step 4.1 - Reviewed download flow; no changes needed to `CivitaiService.DownloadResource` - LoRAs download as normal files
- [x] Step 4.2 - Resource Type dropdown (Checkpoint/Diffusion) now only shows when model type is Checkpoint
- [x] Step 4.3 - Path assignment (High/Low) is handled at generation time via the LoraCard toggle, not at download time

#### Design Decision
The simplified toggle-on-card approach means CivitAI downloads don't need dual-model awareness.
Users download LoRA files normally, add them to generation, and toggle H/L per card.

#### Success Criteria
- Resource Type dropdown only visible for Checkpoint models
- LoRA downloads work unchanged
- Path assignment handled by LoraCard toggle at generation time

---

### Phase 5: Documentation
**Objective:** Update relevant documentation and guides
**Complexity:** 2 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 5.1 - Update `WAN_COMPONENTS_IMPLEMENTATION_GUIDE.md` with dual-model LoRA details
- [ ] Step 5.2 - Update `WAN_INTEGRATION_SUMMARY.md` if applicable
- [ ] Step 5.3 - Final MAIN_PLAN.md update with lessons learned

#### Success Criteria
- Documentation reflects the new dual-model LoRA architecture
- All plan documents are finalized

---

### Phase 6: Resource ModelTarget & CivitAI Dual-Model Integration
**Objective:** Add `ModelTarget` field to Resource entity for tagging files as High/Low; integrate with CivitAI download and Resource page loading
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Background
CivitAI authors upload dual-model LoRAs in two patterns:
- **Same version, different files**: e.g., `high_v2.safetensors` and `low_v2.safetensors` in one version
- **Different versions**: e.g., Version 1 = high, Version 2 = low

Both scenarios produce `Resource` entities with the same `Title`/`CivitaiModelId`, which get grouped into a single `LocalResource` with multiple `LocalResourceFile` entries. The missing piece is knowing which file is High and which is Low.

#### Approach
- Add `ModelTarget` enum (`Standard=0, High=1, Low=2`) to `Resource` entity (DB migration required)
- At CivitAI download time: add H/L selector per file in download area
- At Resource page load time: auto-resolve High/Low paths from tagged files into a single `Lora` entry
- Auto-detection fallback: parse filenames for common patterns (`*high*`, `*low*`)

#### Steps
- [ ] Step 6.1 - Add `ModelTarget` enum and property to `Resource` entity
- [ ] Step 6.2 - Create EF migration for the new column
- [ ] Step 6.3 - Add `ModelTarget` to `LocalResourceFile` and wire through `ResourcesService`
- [ ] Step 6.4 - Add H/L selector per file in `CivitaiFileButton` download UI
- [ ] Step 6.5 - Pass `ModelTarget` through `CivitaiService.DownloadResource` to persist on the entity
- [ ] Step 6.6 - Update `ResourcesService.LoadPrompt` to auto-populate `HighPath`/`LowPath` from tagged files
- [ ] Step 6.7 - Add filename auto-detection fallback for common patterns
- [ ] Step 6.8 - Tests for ModelTarget resolution logic

#### Success Criteria
- Downloaded LoRAs can be tagged as High/Low at download time
- Resource page loading auto-resolves High/Low paths into a single `Lora` entry
- Both CivitAI upload patterns (same version / different versions) are supported

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|------|------------|------------|
| Breaking existing non-Wan LoRA workflows | `Strength` is the single value; H/L toggle only visible in Wan context | 2 |
| Serialization compatibility (saved generation parameters) | `HighPath`/`LowPath` are nullable strings; old data without them works fine | 1 |
| LoraLoader missing clip input | Fixed: fragment now chains clip through LoRA loaders | 2 |
| Multiple LoRAs chaining with mixed high/low | Clip output chains through all LoRA loaders in sequence | 2 |
| DB migration for ModelTarget | New nullable column with default `Standard` - non-breaking | 3 |

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created from codebase review |
| Phase 1+2 | Lora model refactored with HighStrength/LowStrength, WanImg2VidWorkflow updated, 12 new tests added |
| Phase 3 | LoraCard H/L toggle button, LoraForm simplified (no selector), single Strength, Generate.razor IsDualModel wiring |
| Phase 4 | Resource Type dropdown conditional on Checkpoint; CivitAI download unchanged; path assignment via card toggle |
| Bugfix | LoraLoaderModelOnlyFragment missing clip input - added clip ref and chaining through LoRA loaders |
| Planning | Added Phase 6: Resource ModelTarget & CivitAI dual-model integration (deferred) |

---

## References
- `BlazorWebApp/Models/Lora.cs` - Core LoRA model
- `BlazorWebApp/Workflows/Templates/wan/WanImg2VidWorkflow.cs` - Primary dual-model workflow
- `BlazorWebApp/Workflows/Templates/wan/WanSteadyDancerWorkflow.cs` - Secondary Wan workflow
- `BlazorWebApp/Workflows/Fragments/wan/LoraLoaderModelOnlyFragment.cs` - LoRA loader fragment
- `BlazorWebApp/Components/Shared/Generation/LoraCard.razor` - LoRA UI card
- `BlazorWebApp/Components/Shared/Generation/LoraForm.razor` - LoRA list UI
- `BlazorWebApp/Services/ResourcesService.cs` - Resource loading service
- `BlazorWebApp/Services/CivitaiService.cs` - CivitAI download service
- `Documentation/Plans/IMPLEMENTATION_GUIDE.md` - Planning conventions
