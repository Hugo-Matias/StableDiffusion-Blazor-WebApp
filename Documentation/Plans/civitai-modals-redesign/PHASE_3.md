# Phase 3 - Model Dialog Skeleton (Concept 2)

## Status

**Phase:** 3
**Build Status:** Passing (0 errors, pre-existing warnings only)

---

## Objective

Replace the legacy `CivitaiModelInfoDialog` shell (hard-coded inline styles, `pa-5`, single-column `MudGrid`, tabs for Files/Description) with the new Concept 2 layout: token-driven dialog shell with a header band, hero/spec two-column body, and a collapsed description expansion panel. No version-switching wiring yet (single active version assumed; Phase 4 wires the selector).

---

## Context

- `--app-dialog-*` tokens introduced in Phase 1 (`max-width: 1400px`, `padding: 16px`, `radius: 6px`) are now consumed by the dialog shell.
- `AssetViewer`'s opt-out parameters from Phase 1 (`ShowFavorite/ShowScore/ShowOpenInExplorer`) plus `ExternalSourceUrl` are reused inside the new `CivitaiModelHero` component.
- Per-version download / save-all / save-image flows previously embedded in `CivitaiModelVersionInfoPanel` are migrated to the new `CivitaiModelSpecCard`. The legacy panel remains in the codebase (Phase 5 deletes it) but is no longer referenced by the dialog.

---

## Execution Checklist

### Step 1: Dialog shell tokens + structure

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Replace `CivitaiModelInfoDialog`'s `min-width: 90vw; min-height: 90vh; pa-5` with token-driven CSS (`--app-dialog-max-width`, `--app-dialog-padding`, `--app-dialog-radius`).
- [x] Restructure into header band (`<TitleContent>` rendering `CivitaiModelHeader`) + body wrapper (`<DialogContent>`) + description expansion panel.
- [x] Strip default MudDialog title padding/borders so the header band fills the dialog width and owns its own padding/border.
- [x] Open the dialog with `MaxWidth=ExtraLarge` + `FullWidth=true` so the token max-width caps the resolved width naturally.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiModelInfoDialog.razor.css`: rewritten around `.civitai-model-dialog`, with `::deep .mud-dialog` consuming `--app-dialog-*` tokens, `body` wrapper at `padding: var(--app-dialog-padding)`, two-column grid (5fr / 7fr) collapsing to single column under 960px.
- `BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor`: dialog options now use `MaxWidth=ExtraLarge`, `FullWidth=true` so the dialog respects the token cap.

---

### Step 2: CivitaiModelHeader

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Build `CivitaiModelHeader.razor` (type chip, name, NSFW dot, ID link, creator avatar+username, POI chip, stats strip with downloads / favorites / rating, close button).
- [x] Single row, name ellipsis on overflow, no wrap.
- [x] Type chip background uses the existing `Parser.ParseCivitaiResourceColorAsString(...)` token mapping with `--mud-palette-white` foreground.
- [x] Close button raises `OnClose` so the dialog can close itself via `MudDialog.Close()`.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiModelHeader.razor` + `.razor.css`: new component with BEM-style class names. Stats are tooltipped, the rating shows the star widget alongside count, and the NSFW dot is gated on `Model.Nsfw`.

#### Notes

- Initially included `@using BlazorWebApp.Data.Enums`; no such namespace exists (`CivitaiModelType` lives in `BlazorWebApp.Data.Dtos`). Removed; `Enum.TryParse<CivitaiModelType>` resolves from the existing dtos using.

---

### Step 3: CivitaiModelSpecCard

**Complexity:** 3 pts
**Status:** [x] Complete

#### Tasks

- [x] Build `CivitaiModelSpecCard.razor` per-version: file selector (`MudSelect Variant="Text"`), primary `Download` (filled), `Save All` (`.send-to-btn`), size / hash / base model / NSFW level / created / updated meta grid, hashes table with toggle, trigger-words chip rail, tags chip rail with 2-row cap + "+N more" expander.
- [x] Migrate `DownloadFile`, `SaveAllImages`, `SaveImage` flows from `CivitaiModelVersionInfoPanel` so the spec card is self-sufficient.
- [x] Include the existing Resource Type / Sub-Type selectors that previously lived in `CivitaiFileButton` so download metadata is captured before the action runs.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiModelSpecCard.razor` + `.razor.css`: full implementation. Tags clamp uses a `--clamped` modifier with `max-height: 64px; overflow: hidden;` (~2 chip rows) flipped via the +N more / Show less button. Hashes start collapsed showing AutoV2; "More hashes" toggles to AutoV1 + SHA256.

#### Notes

- File selector renders only when more than one file exists; otherwise the primary file's metadata still drives the download button. This mirrors the legacy `CivitaiFileButton` ergonomics while replacing its visual structure.
- The 2-row tag clamp is approximated via `max-height` rather than a JS-measured boundary (kept simple per workspace's "avoid over-engineering" guidance; tag chips are uniform-height so the visual cap is reliable).

---

### Step 4: CivitaiModelHero + AssetViewer

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Build `CivitaiModelHero.razor`: large preview tile of the active version's first image (or video), click opens `AssetViewer` over the active version's projected images. Skeleton/fallback when no images.
- [x] Render a small thumbnail strip (up to 5 thumbs + "+N" tile) below the hero so users can jump straight to a sibling preview.
- [x] Auto-detect video vs image via `CivitaiAssetAdapter.IsVideoUrl(...)`; videos autoplay muted / looped.
- [x] Apply `ShowFavorite="false" ShowScore="false" ShowOpenInExplorer="false"` plus `ExternalSourceUrl` synced through `OnAssetChanged` (same pattern as Phase 2 panels).

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiModelHero.razor` + `.razor.css`: new component with hero tile, thumb strip, AssetViewer host, and per-image external URL tracking.

---

### Step 5: Description expansion panel

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Render the model description inside a `MudExpansionPanel` (collapsed by default).
- [x] Inner scroll container caps height at `40vh` with `overflow-y: auto` so arbitrarily long descriptions don't break the dialog.
- [x] Add a second expansion panel for the active version's release notes (when present).

#### Changes Made

- `CivitaiModelInfoDialog.razor`: `<MudExpansionPanels Class="civitai-model-dialog__description" MultiExpansion="false" Elevation="0">` with two collapsed panels (model description + version notes) sharing the same scroll-clamped body.
- CSS: `.civitai-model-dialog__description-body { max-height: 40vh; overflow-y: auto; }`.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                                 |
| ---- | ------ | ---------- | ----------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 2          | Dialog shell tokenised; header / body / description structure introduced.                             |
| 2    | [x]    | 2          | `CivitaiModelHeader` renders single-row title band with stats / NSFW / ID link / close.               |
| 3    | [x]    | 3          | `CivitaiModelSpecCard` owns download / save-all flows, file selector, tags clamp + expander.          |
| 4    | [x]    | 1          | `CivitaiModelHero` opens `AssetViewer` over the active version's images; supports video previews.     |
| 5    | [x]    | 1          | Description in collapsed `MudExpansionPanel` with 40vh internal scroll; version notes panel optional. |

---

## Issues & Resolutions

### Issue 1: Wrong namespace import in header

**Impact:** Build error CS0138 (`'Enums' is a type not a namespace`).
**Resolution:** Dropped the `@using BlazorWebApp.Data.Enums` directive; `CivitaiModelType` lives in `BlazorWebApp.Data.Dtos` which is already imported. Build clean afterwards.

### Issue 2: File already existed when re-creating dialog

**Impact:** `create_file` failed because the legacy `CivitaiModelInfoDialog.razor` was still on disk; couldn't apply the rewrite as a single replacement (markup structure changed wholesale).
**Resolution:** Removed the legacy file via PowerShell, then re-created with the new shell (in-place edit was not safe for this scope of restructuring). Same approach for the legacy `.razor.css`.

---

## Commit Checkpoints

- [x] After Step 2 complete (`CivitaiModelHeader` lands).
- [x] After Step 3 complete (`CivitaiModelSpecCard` lands; download/save flows migrated).
- [x] After Step 4 complete (`CivitaiModelHero` + AssetViewer).
- [x] After Step 5 complete (dialog rewired, description expansion panel, build clean).

---

## Phase Summary

The legacy `CivitaiModelInfoDialog` is now a thin shell composed of three new feature-local components - `CivitaiModelHeader`, `CivitaiModelHero`, `CivitaiModelSpecCard` - plus a `MudExpansionPanels` description block. All shell sizing/padding/radius is driven by the `--app-dialog-*` tokens; no inline `min-width`/`min-height`/`pa-*` magic numbers remain in the dialog.

### Accomplishments

1. Three new components shipped under `BlazorWebApp/Components/Resources/`.
2. Dialog shell tokenised end-to-end; layout collapses gracefully under 960px.
3. Download / Save All / Save Image flows migrated from the legacy version panel into the new spec card so the dialog no longer depends on `CivitaiModelVersionInfoPanel`.
4. Hero + thumbnail strip surface the active version's media via the canonical `AssetViewer` (video/image auto-detect, external link to civitai.com/images/{id}).
5. Description content lives in a collapsed expansion panel with a 40vh inner scroll.

### Metrics

- Files added: 6 (3 components + 3 component CSS files).
- Files rewritten: 2 (`CivitaiModelInfoDialog.razor`, `CivitaiModelInfoDialog.razor.css`).
- Files modified: 1 (`CivitaiModelsPanel.razor` - dialog options updated to `MaxWidth=ExtraLarge` + `FullWidth=true`).
- Files deleted: 0 (legacy `CivitaiModelVersionInfoPanel` retained until Phase 5).

### Deferred Items

- Multi-version selector / pill bar / per-version download status icons -> Phase 4.
- Removing `CivitaiModelVersionInfoPanel.razor` and `CivitaiFileButton.razor` (now both unused by the new dialog) -> Phase 5 cleanup.
- Manual smoke pass (open dialog, toggle description, click hero, click thumbnails, download flow, tags expander) -> Phase 5 Step 3.

---

**Phase Status:** Complete [x]
