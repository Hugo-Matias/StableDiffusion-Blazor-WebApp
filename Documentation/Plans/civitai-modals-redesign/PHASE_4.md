# Phase 4 - Version Switching & Image Strip

## Status

**Phase:** 4
**Build Status:** Passing (0 errors, pre-existing warnings only)

---

## Objective

Wire multi-version selection into the new dialog shell and extract the thumbnail strip from `CivitaiModelHero` into its own component so hero / strip / future surfaces share one widget. Status indicators on both the selector and the strip reuse a single source-of-truth helper hoisted from the legacy `CivitaiModelVersionInfoPanel.GetTabIconColor` rules.

---

## Context

- Phase 3 left the dialog assuming a single active version (always index 0). The plan committed Phase 4 to introduce a pill / select hybrid driven by `VersionPillThreshold = 5`.
- Per-version download / save flows already migrated to `CivitaiModelSpecCard` in Phase 3, so the only Phase 4 work is presentation: selector, strip, and hero composition.

---

## Execution Checklist

### Step 1: CivitaiVersionSelector

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Build `CivitaiVersionSelector.razor`. Renders pills when `Versions.Count <= VersionPillThreshold` (constant = 5), otherwise a `MudSelect Variant="Text"`.
- [x] Active version highlighted (filled vs outlined pill, primary color).
- [x] Per-version status icon (downloaded / partially saved / missing) reuses the legacy `CivitaiModelVersionInfoPanel.GetTabIconColor` rules - hoisted into `CivitaiVersionStatusHelper`.
- [x] Selector caches per-version status across renders (recomputed only when a new version id appears).
- [x] Wire into `CivitaiModelInfoDialog` so changing the active version swaps the hero + spec card content. Selector renders only when `ModelVersions.Count > 1`.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiVersionStatusHelper.cs`: new static helper exposing `VersionStatus` enum, `ResolveAsync(IDatabaseService, version)`, `ToColor(...)`, `ToTooltip(...)`. Replaces the inline async logic that previously lived inside `CivitaiModelVersionInfoPanel`.
- `BlazorWebApp/Components/Resources/CivitaiVersionSelector.razor` + `.razor.css`: pill / select hybrid. Pills render with `StartIcon="fa-solid fa-server"` colored by status. The `MudSelect` fallback embeds the same icon inside `MudSelectItem` content for parity.
- `BlazorWebApp/Components/Resources/CivitaiModelInfoDialog.razor`: hosts the selector above the hero/spec layout when more than one version exists. Active version state is now mutable and updated via `HandleVersionChanged`.

#### Notes

- Status helper introduces an extra `Unknown` state for the brief window before async resolution completes; the UI defaults to `Color.Default` so it visually matches the "Missing" baseline until the cache resolves.

---

### Step 2: CivitaiPreviewStrip + Hero composition

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Extract the thumbnail strip out of `CivitaiModelHero` into a standalone `CivitaiPreviewStrip.razor` component.
- [x] Strip is horizontally scrollable (no row cap), shows all images, highlights the active index, supports image + video thumbs.
- [x] Click on a thumb sets the hero index (in-place hero swap) without opening the viewer; clicking the hero opens the viewer at the current index.
- [x] Strip + hero share `_heroIndex`, and the hero index follows `OnAssetChanged` from `AssetViewer` so closing the viewer leaves the hero at the last viewed image.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiPreviewStrip.razor` + `.razor.css`: new presentational component, single `OnSelect` callback emitting the chosen index.
- `BlazorWebApp/Components/Resources/CivitaiModelHero.razor`: rebuilt to compose `CivitaiPreviewStrip`, track `_heroIndex` + `_trackedVersion`, and synchronise the active image through `HandleViewerAssetChanged`. Hero CSS reduced to media tile + placeholder rules (strip styles moved to the new component).

---

### Step 3: Migration verification

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Confirm download / save-all / save-image flows are owned exclusively by `CivitaiModelSpecCard` (migrated in Phase 3). No new code required - the new dialog shell never references `CivitaiModelVersionInfoPanel`.
- [x] Confirm the legacy panel can be retired in Phase 5 (only consumer remaining is the legacy panel itself, which Phase 5 deletes).
- [x] Confirm snackbar messages in the migrated flows match the legacy text exactly (`"... downloaded successfully!"`, `"All images saved!"`, etc.).

#### Notes

- The plan originally scoped Step 3 to "introduce a thin `CivitaiVersionActionsService`", but the simpler migration into `CivitaiModelSpecCard` already satisfies the success criterion (per-version flows decoupled from the old panel) without adding a service. Documented here rather than spawning a new abstraction for one consumer.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                                           |
| ---- | ------ | ---------- | --------------------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 2          | `CivitaiVersionSelector` + `CivitaiVersionStatusHelper` shipped; pills/select hybrid wired to the dialog.       |
| 2    | [x]    | 1          | `CivitaiPreviewStrip` extracted; hero composes the strip and shares `_heroIndex` with the AssetViewer.          |
| 3    | [x]    | 2          | Verified the per-version flows already live on `CivitaiModelSpecCard`; legacy panel ready for Phase 5 deletion. |

---

## Phase Summary

The model dialog now supports the full multi-version workflow: a pill bar (or `MudSelect` once the threshold is exceeded) drives the active version, the hero updates in place, an extracted thumbnail strip lets users skim previews without opening the modal, and `AssetViewer` navigation still opens at the chosen index.

### Accomplishments

1. `CivitaiVersionSelector.razor` + `CivitaiVersionStatusHelper.cs` shipped with the agreed pill/select hybrid and a single source-of-truth status resolver.
2. `CivitaiPreviewStrip.razor` extracted and reused by `CivitaiModelHero.razor`.
3. Hero internalised an active-image index that survives strip clicks and AssetViewer navigation.
4. Dialog now mutates `_activeVersion` via `HandleVersionChanged`, swapping hero + spec card atomically.

### Metrics

- Files added: 4 (`CivitaiVersionSelector.razor` + `.razor.css`, `CivitaiPreviewStrip.razor` + `.razor.css`).
- Files added (logic): 1 (`CivitaiVersionStatusHelper.cs`).
- Files modified: 2 (`CivitaiModelInfoDialog.razor`, `CivitaiModelHero.razor` + its `.razor.css`).
- Files deleted: 0 (legacy panel deleted in Phase 5).

### Deferred Items

- Delete `CivitaiModelVersionInfoPanel.razor` and `CivitaiFileButton.razor` -> Phase 5 Step 1.
- Update `04-UI-DESIGN-LANGUAGE.md` with the new media-modal section, version-selector pattern, and tag/description overflow rules -> Phase 5 Step 2.
- Manual smoke pass (open dialog with 1, 3, 6, 12 versions; click a non-active pill; switch via `MudSelect`; click strip thumbnails; open viewer; verify status icon colors after a download) -> Phase 5 Step 3.

---

**Phase Status:** Complete [x]
