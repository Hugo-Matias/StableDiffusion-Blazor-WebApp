# Phase 1 - Foundation: Tokens, AssetViewer Parameters, Adapter

## Status

**Phase:** 1
**Build Status:** Passing (0 errors, pre-existing warnings only)

---

## Objective

Land the additive primitives that the new CivitAI modals depend on, with **zero behavioural change for existing consumers** (Gallery, Danbooru, Img2Vid, generation tabs).

This phase introduces:

1. Global dialog tokens (`--app-dialog-*`) in `site.css` and the design language doc.
2. Additive opt-out / opt-in parameters on `AssetViewer.razor` (`ShowFavorite`, `ShowScore`, `ShowOpenInExplorer`, `ExternalSourceUrl`).
3. A feature-local `CivitaiAssetAdapter` that projects `CivitaiImageDto` -> transient `ImageEntity` for `AssetViewer` consumption.

---

## Context

- See [MAIN_PLAN.md](MAIN_PLAN.md) for the full plan, Concept 2 design, and stress points.
- `AssetViewer` is at [BlazorWebApp/Components/Shared/AssetViewer.razor](../../../BlazorWebApp/Components/Shared/AssetViewer.razor).
- `AssetInfoPanel` is at [BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor](../../../BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor).
- `CivitaiImageDto` lives under `BlazorWebApp/Data/CivitAI/`.
- Tokens follow the existing `--app-surface-*` pattern in [BlazorWebApp/wwwroot/site.css](../../../BlazorWebApp/wwwroot/site.css).

---

## Execution Checklist

### Step 1: Dialog tokens

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Add `--app-dialog-max-width`, `--app-dialog-padding`, `--app-dialog-radius` to `:root` in `BlazorWebApp/wwwroot/site.css`.
- [x] Document the three tokens in the Spacing tokens table in `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.

#### Changes Made

- `BlazorWebApp/wwwroot/site.css` - added a new "Dialog tokens" block under the existing surface-padding token, with values `1400px / 16px / 6px` and a comment pointing to the design language doc.
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - extended the "Spacing tokens (single source of truth)" table with three new rows for the dialog tokens.

#### Notes

- Tokens are defined only; no consumer references them yet (Step 2 / Phase 3 will).
- A more comprehensive "App-wide media modal" section will be added to the design language doc in Phase 5 (per the plan), once `AssetViewer` formally exposes the new parameter set.

---

### Step 2: AssetViewer additive parameters

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Add `[Parameter] public bool ShowFavorite { get; set; } = true;` to `AssetViewer.razor`.
- [x] Add `[Parameter] public bool ShowScore { get; set; } = true;`.
- [x] Add `[Parameter] public bool ShowOpenInExplorer { get; set; } = true;`.
- [x] Add `[Parameter] public string? ExternalSourceUrl { get; set; }`.
- [x] Wire them into the existing control-bar markup so unset values keep current behaviour byte-identical.
- [x] When `ExternalSourceUrl` is non-null, render an "Open source" affordance (anchor styled as `viewer-btn` with the `up-right-from-square` icon) in the actions cluster.

#### Changes Made

- `BlazorWebApp/Components/Shared/AssetViewer.razor` -
  - Added the four `[Parameter]` declarations in the parameters section, with a comment block explaining the local-vs-remote default split.
  - Wrapped the favorite/rating control-section so the entire block is suppressed when both `ShowFavorite` and `ShowScore` are false; gated the favorite button (and `FavoriteAllOnPage` companion) on `ShowFavorite`, and the rating container on `ShowScore`.
  - Gated the existing "Open in Explorer" button on `ShowOpenInExplorer`.
  - Added a tooltip-wrapped `<a>` element styled as `viewer-btn` in the actions cluster that renders only when `ExternalSourceUrl` is non-empty, with `target="_blank"` and `rel="noopener noreferrer"`.

#### Notes

- Existing consumers (Gallery, Danbooru, Img2Vid, generation tabs) do not pass any of the new parameters; defaults reproduce previous markup.
- Anchor instead of a button avoids a JS interop hop for the external link and benefits from the browser's middle-click / context-menu UX.

---

### Step 3: CivitaiAssetAdapter

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Create `BlazorWebApp/Components/Resources/CivitaiAssetAdapter.cs` (static helper).
- [x] Implement `Project(CivitaiImageDto dto)` -> transient `ImageEntity`.
- [x] Implement `Project(IEnumerable<CivitaiImageDto> dtos)` -> `List<ImageEntity>`.
- [x] Map: `Url -> Path`, `Width`/`Height`, `Meta.Prompt -> Prompt`, `Meta.NegativePrompt -> NegativePrompt`, `Meta.Seed -> Seed`, `Meta.Steps -> Steps`, `Meta.CfgScale -> CfgScale`, `Meta.Sampler` (name) -> `Scheduler` (string passthrough; `SamplerId` stays 0).
- [x] Video detection helper (`IsVideoUrl`) mirroring the AssetViewer extension list.
- [x] Documented the field-mapping table inline (XML doc on the class) including lossy / unmapped fields.
- [x] Adapter is purely static; no DbContext attach paths.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiAssetAdapter.cs` (new) - static helper providing:
  - `Project(CivitaiImageDto)` -> `ImageEntity` (transient, `Id = 0`).
  - `Project(IEnumerable<CivitaiImageDto>)` -> `List<ImageEntity>` (preserves order, skips nulls).
  - `IsVideoUrl(string)` -> bool extension-based check.
  - Class-level XML doc records the full field-mapping table and lossy fields (`Model`, `ModelHash`, `ClipSkip`, hires fields, `Resources`).

#### Notes

- The plan's "transient Sampler entity" option was unavailable: `ImageEntity` exposes only `int SamplerId` (no `Sampler` navigation). The fallback documented in the plan ("stores the string in `Scheduler` if `SamplerId` resolution is unavailable") is what's implemented; receiving workflows resolve the sampler by name on `IImageSendToService` send.
- `Seed` / `Steps` / `CfgScale` keep `ImageEntity`'s sentinel defaults (-1) when the meta value is absent / zero, so `AssetInfoPanel` correctly suppresses those rows.
- `DenoisingStrength` is parsed with invariant culture to avoid locale issues.
- Sentinel-zero check on `meta.Seed != 0` is a slight false-negative case (CivitAI seed `0` is theoretically valid) but matches the rest of the codebase's convention; can be revisited if it shows up in practice.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                                                |
| ---- | ------ | ---------- | -------------------------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 1          | Dialog tokens added to site.css and documented in design language doc.                                               |
| 2    | [x]    | 2          | AssetViewer parameter set landed; defaults preserve existing consumer behaviour.                                     |
| 3    | [x]    | 2          | `CivitaiAssetAdapter` static helper landed under `Components/Resources/` with documented field map and video helper. |

---

## Issues & Resolutions

_(none yet)_

---

## Commit Checkpoints

- [x] After Step 1 complete
- [x] After Step 2 complete
- [x] After Step 3 complete

---

## Phase Summary

Foundation phase landed three additive primitives with zero behavioural change for existing `AssetViewer` consumers:

1. Three `--app-dialog-*` tokens in `site.css` and the design-language doc, ready for Phase 3+ to consume.
2. Four additive parameters on `AssetViewer.razor` (`ShowFavorite`, `ShowScore`, `ShowOpenInExplorer`, `ExternalSourceUrl`). All default to the current behaviour; only CivitAI consumers will set them.
3. A feature-local static helper `CivitaiAssetAdapter` projecting `CivitaiImageDto` to transient `ImageEntity` with a documented field map and a small `IsVideoUrl` helper.

### Accomplishments

1. `AssetViewer` now has the surface needed for remote / borrowed assets without a refactor.
2. CivitAI feature has a single adapter entry point for image projection, ready for cards to consume in Phase 2.
3. Documentation table for spacing tokens extended to include the dialog cluster.

### Metrics

- Files added: 1 (`CivitaiAssetAdapter.cs`).
- Files modified: 3 (`site.css`, `AssetViewer.razor`, `04-UI-DESIGN-LANGUAGE.md`).
- Build: 0 errors, no new warnings.

### Deferred Items

- Larger "App-wide media modal" section in the design language doc is intentionally deferred to Phase 5 (per the plan), once `AssetViewer` has actual remote consumers wired up to validate the doc against real usage.
- Sentinel-zero edge case for CivitAI seed `0` documented in Step 3 notes; revisit only if it surfaces.

---

**Phase Status:** Complete [x]
