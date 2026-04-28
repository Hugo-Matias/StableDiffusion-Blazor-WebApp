# CivitAI Modals Redesign - Implementation Plan

## Status

**Current Phase:** All phases complete

Phases 1 - 5 are complete. See [PHASE_1.md](PHASE_1.md), [PHASE_2.md](PHASE_2.md), [PHASE_3.md](PHASE_3.md), [PHASE_4.md](PHASE_4.md), and [PHASE_5.md](PHASE_5.md) for the per-step records.

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
- `[!]` Blocked / needs discussion

### Complexity Estimation (Fibonacci Points)

- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic

### Key Rules

- Each step = commitable checkpoint
- No time/date references - use complexity points only
- Detours acceptable after discussion - append to plan
- Phase docs must contain enough context to resume in new sessions
- Minimal, focused changes - avoid over-engineering
- User permission required before moving to next phase
- All events must use the pub/sub pattern via `EventService.cs`
- Children of layout / dialog slots render flush (no root `MudPaper` / `pa-*`)
- Form controls default to `Variant.Text`
- Simple action buttons use the shared `.send-to-btn` style

---

## Problem Statement

The CivitAI tab currently exposes two modals that fall short of the design language and the dynamic send-to system used elsewhere in the app:

### `CivitaiModelInfoDialog` (model popup)

- Hard-coded `min-width: 90vw; min-height: 90vh; pa-5` on `MudDialog` - violates surface policy and spacing tokens.
- Header crams name, link, NSFW dot, downloads, hearts, rating into two unstructured rows.
- 2/10 `MudGrid` split: left rail dumps creator + type chip + a free-flow `MudChipSet` of tags. Tags wrap arbitrarily and break the layout when a model has many of them.
- Right column hosts inner `MudTabs` (Files / Description) - **nested tabs inside a dialog** (the model dialog itself sits inside the page-level tabs, and `CivitaiModelVersionInfoPanel` adds a third vertical `MudTabs` for versions). Three tab dimensions compete for the same screen.
- Description is rendered via raw `MarkupString` with no max-height and no scroll container - long descriptions blow out the dialog.
- Per-version sub-grid (8/4) re-implements its own layout for images + sidebar + description rather than reusing shared primitives.

### `CivitaiImageDialog` (image popup)

- Plain `MudImage` + a static `MudButtonGroup` with two hard-coded `Txt2Img` / `Img2Img` buttons writing directly to `GenerationParameters` fragments. Completely outdated vs the dynamic `IImageSendToService` workflow system used by `AssetInfoPanel`.
- A wall of `MudTextField ReadOnly` for metadata - not the `meta-grid` pattern, no clickable parameter selection, no prompt blocks, no workflow-data tree.
- No nav between sibling images, no zoom/pan, no slideshow - feature parity gap with Gallery / Danbooru / Img2Vid.
- No video support (CivitAI image search returns videos too, already handled by `CivitaiImageCard`).

The repository already has a canonical media modal: `AssetViewer` + `AssetInfoPanel`. It is consumed by the main Gallery, Danbooru, Img2Vid generated tabs, and generation tabs. Adopting it for CivitAI eliminates duplication and elevates it to the **app-wide media modal standard**.

---

## Proposed Solution

Two parallel redesigns sharing one foundation:

1. **Image dialog**: replace `CivitaiImageDialog` with `AssetViewer` driven by a thin `CivitaiImageDto -> ImageEntity` adapter. Add a small set of additive opt-out parameters to `AssetViewer` to suppress controls that are meaningless for a transient remote asset (favorite, score, open-in-explorer) and one opt-in (`ExternalSourceUrl`) to surface a "Open on CivitAI" affordance.

2. **Model dialog (Concept 2 - "Hero-led detail")**: rebuild `CivitaiModelInfoDialog` around the layout system and the new media-modal standard.
   - Compact toolbar: version selector (pills, collapses to `MudSelect` when count > 5) + close.
   - Header band: type chip + name + creator + stats strip + NSFW indicator. Single row, no wrap.
   - Body two-column: **Hero preview** (left, prominent, click -> `AssetViewer` over the active version's image set) + **Spec card** (right: file/version selector, file actions via `.send-to-btn`, size/hash, trigger words rail, tags rail capped at 2 rows, created/updated/base model).
   - Image strip beneath hero (the rest of the version's images).
   - Description as a collapsed `MudExpansionPanel` below the fold, with a constrained max-height and internal scroll.
   - All sizing / padding driven by tokens; no `pa-5`, no `90vw`.

Both modals are documented in `04-UI-DESIGN-LANGUAGE.md` so `AssetViewer` becomes the explicit canonical media modal for the app.

### Key Decisions (confirmed by user)

| Decision                                                                                                                                   | Rationale                                                                                                                     |
| ------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| Adopt **Concept 2 (Hero-led detail)** for the model modal                                                                                  | Strongest hierarchy; matches CivitAI's own page metaphor; composes naturally with `AssetViewer`.                              |
| Image dialog becomes `AssetViewer` via adapter projection (Option A)                                                                       | Zero refactor of `AssetViewer` / `AssetInfoPanel`; reuses send-to-parameters out of the box; smallest surface, biggest reuse. |
| Versions use **pill bar with `MudSelect` collapse threshold = 5**                                                                          | Tentative; user will validate during execution. Threshold lives in a single constant for easy tuning.                         |
| Preview interaction opens `AssetViewer` over the **active version's images only**                                                          | Matches user's mental model; avoids cross-version slideshow surprises.                                                        |
| `CivitaiAssetAdapter` lives **feature-local** under `BlazorWebApp/Components/Resources/`                                                   | Only one DTO family; no reuse case yet. Promote to `Services/` only if a second consumer appears.                             |
| Send parameters from CivitAI image uses existing `IImageSendToService` parameter path                                                      | Sampler is a name-only string; receiving workflow resolves it. Same contract as Danbooru.                                     |
| `AssetViewer` gains additive opt-out parameters (`ShowFavorite`, `ShowScore`, `ShowOpenInExplorer`) and one opt-in (`ExternalSourceUrl`)   | Defaults are byte-identical for current callers (Gallery, Danbooru, Img2Vid, generation tabs); only CivitAI sets them.        |
| Hard-coded modal sizing (`90vw`, `85vw`, `pa-5`) is removed in favor of a token cluster (`--app-dialog-max-width`, `--app-dialog-padding`) | Single source of truth; matches existing `--app-surface-*` token pattern.                                                     |
| Tags rail uses `flex-wrap` capped at 2 rows + "+N more" expander                                                                           | Closes the "too many tags" overflow case decisively without arbitrary scrollbars.                                             |
| Description goes into a `MudExpansionPanel` (collapsed by default) with internal max-height + scroll                                       | Closes the "description too long" overflow case.                                                                              |

### Conventions

- All new send-to interactions route through `IImageSendToService` (no direct `GenerationParameters` writes from the dialog).
- Dialog shells use the `--app-dialog-*` tokens; children render flush (no root `MudPaper` / `pa-*`).
- Simple action buttons (Save All, Open on CivitAI, Save Image) use `.send-to-btn`. The single primary action of the spec card (Download file) uses `MudButton Variant="Filled" Color="Primary"`.
- Form controls inside the model dialog use `Variant.Text` (e.g., the file selector `MudSelect`).
- All version-sub-component splits live under `Components/Resources/`. No new top-level folder.
- Persistence: none of these changes touch the database or migrations.

---

## Implementation Phases

### Phase 1: Foundation - Tokens, AssetViewer Parameters, Adapter

**Objective:** Land the additive primitives that the new modals depend on, with zero behavioural change for existing consumers.

**Complexity:** 5 points

**Status:** [x] Complete

#### Steps

- [x] Step 1 (1 pt) - Add dialog tokens to `BlazorWebApp/wwwroot/site.css` (`--app-dialog-max-width`, `--app-dialog-padding`, `--app-dialog-radius`). Document them in `04-UI-DESIGN-LANGUAGE.md` under "Spacing tokens".
- [x] Step 2 (2 pts) - Extend `AssetViewer.razor` with additive parameters: `bool ShowFavorite = true`, `bool ShowScore = true`, `bool ShowOpenInExplorer = true`, `string? ExternalSourceUrl = null`. Wire them into the existing control-bar markup so unset = current behaviour. When `ExternalSourceUrl` is set, render a "Open on CivitAI" button in the actions cluster.
- [x] Step 3 (2 pts) - Create `BlazorWebApp/Components/Resources/CivitaiAssetAdapter.cs` (static helper). Single responsibility: project `CivitaiImageDto` (and a list of them) into transient `ImageEntity` instances. Maps Url -> Path, Width/Height, Meta.Prompt -> Prompt, Meta.NegativePrompt -> NegativePrompt, Meta.Seed -> Seed, Meta.Steps -> Steps, Meta.CfgScale -> CfgScale, Meta.Sampler (name) -> a transient Sampler entity (or stores the string in `Scheduler` if `SamplerId` resolution is unavailable). NSFW / video detection from URL. Document the field-mapping table inline.

#### Success Criteria

- Existing consumers of `AssetViewer` (Gallery, Danbooru, Img2Vid, generation tabs) render identically; smoke test required.
- `CivitaiAssetAdapter.Project(...)` returns a list of transient `ImageEntity` whose `Path` matches `dto.Url` and whose `Prompt`/`NegativePrompt`/`Seed`/`Steps`/`CfgScale` are populated when `Meta` is non-null.
- New tokens exist and are referenced by no consumer yet (defining them only).

---

### Phase 2: Image Dialog Replacement

**Objective:** Replace `CivitaiImageDialog` usages with `AssetViewer` driven by the adapter. Delete the dialog component.

**Complexity:** 5 points

**Status:** [x] Complete

#### Steps

- [x] Step 1 (2 pts) - In `CivitaiImageCard.razor` and `CivitaiModelImageCard.razor`, replace `DialogService.Show<CivitaiImageDialog>(...)` with toggling an `AssetViewer` over the projected list. Card becomes responsible for projecting _the parent panel's_ image collection (passed as a parameter) and computing `StartIndex`, so navigation between sibling images works.
- [x] Step 2 (1 pt) - In `CivitaiImagesPanel.razor`, manage a single `AssetViewer` instance per panel; cards raise an `OnView(int index)` callback. Same change in `CivitaiModelVersionInfoPanel.razor` for per-version image grids (will be reused in Phase 3).
- [x] Step 3 (1 pt) - Pass `ShowFavorite="false" ShowScore="false" ShowOpenInExplorer="false" ExternalSourceUrl="..."` to suppress local-only controls and surface the CivitAI link. Verify `AssetInfoPanel` correctly hides the "Send image to" buttons (not local) and shows the bookmark CTA copy. Confirm "Send parameters to" buttons render and route through `IImageSendToService`.
- [x] Step 4 (1 pt) - Delete `CivitaiImageDialog.razor` and any orphaned references / styles. Update `_Imports.razor` if needed.

#### Success Criteria

- Clicking a CivitAI image card opens `AssetViewer` instead of the legacy dialog.
- Arrow-key navigation between sibling images works.
- "Send parameters to <workflow>" buttons appear when at least one parameter workflow is registered; clicking sends parameters via `IImageSendToService`.
- "Open on CivitAI" button opens the source URL in a new tab.
- No regressions in Gallery / Danbooru / Img2Vid `AssetViewer` consumers.
- `CivitaiImageDialog.razor` is deleted; project builds clean.

---

### Phase 3: Model Dialog Skeleton (Concept 2)

**Objective:** Rebuild `CivitaiModelInfoDialog` around the new layout: compact toolbar + header band + hero/spec two-column body + collapsed description. No version-switching wiring yet (single active version assumed).

**Complexity:** 8 points

**Status:** [x] Complete

#### Steps

- [x] Step 1 (2 pts) - Replace the dialog's hard-coded inline styles and `pa-5` with the new `--app-dialog-*` tokens. Restructure into header band + body wrapper + footer (close on header). Children flush.
- [x] Step 2 (2 pts) - Build `CivitaiModelHeader.razor` (type chip, name, creator avatar+username, stats strip, NSFW dot, ID link, close). Single row, ellipsis on name, no wrap.
- [x] Step 3 (3 pts) - Build `CivitaiModelSpecCard.razor` (per-version): file selector `MudSelect Variant="Text"`, primary `Download` action (filled), `Save All` (`.send-to-btn`), size / hash / base model / created / updated, trigger-words chip rail, tags chip rail with 2-row cap + "+N more" expander.
- [x] Step 4 (1 pt) - Build `CivitaiModelHero.razor`: prominent preview area (image or video), click opens `AssetViewer` over the active version's images. Skeleton/fallback when no images.
- [x] Step 5 (1 pt) - Add the description as a `MudExpansionPanel`, collapsed by default, internal `max-height: 40vh; overflow-y: auto;` scroll container.

#### Success Criteria

- Opening a model from `CivitaiModelsPanel` shows the new layout for the first version.
- Tags with 50+ entries cap at 2 rows with a working "+N more" expander.
- Descriptions of arbitrary length scroll inside the expansion panel without breaking the dialog.
- Clicking the hero opens `AssetViewer` over the active version's projected images.
- Spec card primary action triggers the existing `Civitai.DownloadResource(...)` flow with the same status snackbar messaging as today.

---

### Phase 4: Version Switching & Image Strip

**Objective:** Wire multiple versions into the new shell with the agreed pill-bar / select hybrid.

**Complexity:** 5 points

**Status:** [x] Complete

#### Steps

- [x] Step 1 (2 pts) - Build `CivitaiVersionSelector.razor`: renders pills when `Versions.Count <= VersionPillThreshold` (constant = 5), otherwise a `MudSelect Variant="Text"`. Active version highlighted; per-version status icon (downloaded / partially saved / missing) reused from `CivitaiModelVersionInfoPanel.GetTabIconColor` logic, hoisted into a small helper.
- [x] Step 2 (1 pt) - Build `CivitaiPreviewStrip.razor`: horizontal scrollable thumb strip beneath the hero; click sets the hero image and opens `AssetViewer` at that index.
- [x] Step 3 (2 pts) - Migrate the per-version save / download flows out of `CivitaiModelVersionInfoPanel` into a thin `CivitaiVersionActionsService` (or a partial code-behind on the new dialog) so the original panel can be retired. Behaviour identical: download file -> save preview -> save all images.

#### Success Criteria

- Switching versions updates hero, spec card, image strip, and description.
- Pill / select hybrid switches at the documented threshold.
- Per-version status indicator matches today's color rules (Default / Success / Info / Warning / Error).
- All existing snackbar messages remain identical.

---

### Phase 5: Cleanup, Design-Doc Update, Smoke Tests

**Objective:** Retire dead code, document the new standards, validate with a build and manual smoke pass.

**Complexity:** 3 points

**Status:** [x] Complete

#### Steps

- [x] Step 1 (1 pt) - Delete `CivitaiModelVersionInfoPanel.razor` once the new shell fully replaces it. Verify no other consumer.
- [x] Step 2 (1 pt) - Update `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`:
  - Add `--app-dialog-*` tokens to the spacing-token table.
  - Add an "App-wide media modal" section declaring `AssetViewer` as the canonical media modal and listing its opt-out / opt-in parameters.
  - Add a "Tag rail overflow" pattern (2-row cap + expander).
  - Add a "Description overflow" pattern (collapsed expansion panel + internal scroll).
- [x] Step 3 (1 pt) - Build, run, smoke test: Gallery / Danbooru / Img2Vid `AssetViewer` parity, CivitAI image dialog replacement, CivitAI model dialog with low-tag / high-tag / long-description / many-versions models.

#### Success Criteria

- `dotnet build` clean.
- All `AssetViewer` consumers visually identical to pre-change.
- CivitAI flows function end-to-end (browse -> open model -> switch versions -> download -> view image -> send parameters to workflow).
- Design language doc reflects the new patterns and tokens.

---

## Stress Points & Risks

| Risk                                                                                                                                                        | Mitigation                                                                                                                  | Complexity |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ---------- |
| `AssetViewer` parameter additions silently regress Gallery / Danbooru / Img2Vid consumers                                                                   | Defaults must be byte-identical to today's behaviour; smoke test all four consumers before closing Phase 1.                 | 2          |
| Adapter loses metadata fidelity (sampler name vs id, no scheduler, no model FK)                                                                             | Document the lossy fields in the adapter; rely on the receiving workflow to resolve sampler-by-name (same as Danbooru).     | 2          |
| `AssetInfoPanel.LoadWorkflowData()` attempts metadata reads on `https://...` URLs                                                                           | Already short-circuited by `SendTo.IsLocal(...)` per the Danbooru integration. Verify the same gate fires for CivitAI URLs. | 1          |
| `CivitaiModelImageCard` "Save image" action conflicts with the new viewer launch when both bind to the card click                                           | Reserve card click for "view in AssetViewer"; keep "Save" as an explicit overlay button.                                    | 2          |
| Tag overflow expander state leaks across rerenders when switching versions / models                                                                         | Local component state + key on model id; reset on `OnParametersSetAsync`.                                                   | 1          |
| Version-pill -> select threshold (= 5) might feel wrong with very long version names                                                                        | Threshold lives in one constant; user validates and we tune in Phase 4.                                                     | 1          |
| Hero preview for video-type CivitAI images needs autoplay/mute matching `CivitaiImageCard` behaviour                                                        | Reuse `CivitaiImageCard`'s video-detection helper; centralise into adapter or a small `IsVideoUrl` utility.                 | 2          |
| Removing `CivitaiImageDialog` and `CivitaiModelVersionInfoPanel` may break in-flight references in unrelated docs / plans                                   | Grep for both names before deletion; update any stale references.                                                           | 1          |
| `CivitaiAssetAdapter` building transient `ImageEntity` may surface DB navigation properties (e.g., Sampler) that EF tries to track if accidentally attached | Adapter must never attach to a `DbContext`; document this and keep instances local to the dialog scope.                     | 2          |
| Dialog max-width token may clash with existing `MudDialog` defaults / themes                                                                                | Apply token via a `Class` on the dialog wrapper rather than overriding theme variables; verify in dark/light.               | 1          |

---

## Changelog

| Phase    | Changes                                                                                                                                                                                                                                                                                                                                              |
| -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Planning | Initial plan created; Concept 2 confirmed; pill-threshold=5 tentative; adapter feature-local; AssetViewer parameter set agreed (`ShowFavorite`, `ShowScore`, `ShowOpenInExplorer`, `ExternalSourceUrl`); dialog token cluster (`--app-dialog-*`) introduced.                                                                                         |
| Phase 1  | Step 1 complete - dialog tokens (`--app-dialog-max-width=1400px`, `--app-dialog-padding=16px`, `--app-dialog-radius=6px`) added to `site.css` and documented in `04-UI-DESIGN-LANGUAGE.md` Spacing tokens table.                                                                                                                                     |
| Phase 1  | Steps 2 & 3 complete - `AssetViewer` gained `ShowFavorite` / `ShowScore` / `ShowOpenInExplorer` / `ExternalSourceUrl` (defaults preserve existing behaviour); `CivitaiAssetAdapter` static helper landed under `Components/Resources/`. Build clean.                                                                                                 |
| Phase 2  | All steps complete - `CivitaiImageCard` / `CivitaiModelImageCard` reduced to `OnView` emitters; `CivitaiImagesPanel` / `CivitaiModelVersionInfoPanel` host a single `AssetViewer` and project DTO lists via `CivitaiAssetAdapter`; legacy `CivitaiImageDialog.razor` deleted. Build clean.                                                           |
| Phase 3  | All steps complete - `CivitaiModelInfoDialog` rewritten around `--app-dialog-*` tokens; new `CivitaiModelHeader`, `CivitaiModelHero`, `CivitaiModelSpecCard` components shipped; download / save-all flows migrated from `CivitaiModelVersionInfoPanel`; description rendered in collapsed `MudExpansionPanels` with 40vh inner scroll. Build clean. |
| Phase 4  | All steps complete - `CivitaiVersionSelector` (pill/select hybrid) + `CivitaiVersionStatusHelper` shipped; `CivitaiPreviewStrip` extracted from hero; multi-version switching wired through the dialog. Build clean.                                                                                                                                 |
| Phase 5  | All steps complete - `CivitaiModelVersionInfoPanel` and `CivitaiFileButton` retired; `04-UI-DESIGN-LANGUAGE.md` updated with `AssetViewer` canonical section, `--app-dialog-*` token guidance, long-content overflow patterns, and version-selector pattern. Final build clean.                                                                      |

---

## References

- `BlazorWebApp/Components/Shared/AssetViewer.razor` - canonical media modal.
- `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - send-to / parameter selection panel.
- `BlazorWebApp/Components/Resources/CivitaiModelInfoDialog.razor` - legacy model dialog (to be rebuilt).
- `BlazorWebApp/Components/Resources/CivitaiImageDialog.razor` - legacy image dialog (to be deleted).
- `BlazorWebApp/Components/Resources/CivitaiModelVersionInfoPanel.razor` - legacy per-version panel (to be retired).
- `BlazorWebApp/Components/Resources/CivitaiImageCard.razor` / `CivitaiModelImageCard.razor` - card consumers, must launch `AssetViewer` after the change.
- `BlazorWebApp/Components/Resources/CivitaiResourceCard.razor` / `CivitaiModelsPanel.razor` - entry points to the model dialog.
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - design language; receives the new media-modal section, dialog tokens, tag/description overflow patterns.
- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md` - prior precedent for adopting `AssetInfoPanel` from a remote source.
