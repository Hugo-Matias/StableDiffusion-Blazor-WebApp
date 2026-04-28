# Phase 2 - Image Dialog Replacement

## Status

**Phase:** 2
**Build Status:** Passing (0 errors, pre-existing warnings only)

---

## Objective

Replace `CivitaiImageDialog` with `AssetViewer` driven by the `CivitaiAssetAdapter` projection added in Phase 1. Cards become "view trigger" emitters; their parent panels host a single `AssetViewer` instance per panel and project the relevant DTO collection on demand.

---

## Context

- Phase 1 already landed the `AssetViewer` opt-out / opt-in parameters and the adapter; this phase only wires consumers.
- The legacy `CivitaiImageDialog.razor` had an unused `result.Data == "Save"` branch (no Save button in its markup) and hard-coded `Txt2Img` / `Img2Img` send-to-fragment writes that bypassed `IImageSendToService`. Both are dropped in favour of `AssetInfoPanel`'s standard send-to / parameter selection UX.

---

## Execution Checklist

### Step 1: Cards raise OnView callbacks

**Complexity:** 2 pts
**Status:** [x] Complete

#### Tasks

- [x] Add an `OnView` `EventCallback<CivitaiImageDto>` parameter to `CivitaiImageCard.razor`. Replace the info-button click handler with a thin `RaiseView()` invoker. Drop the `IDialogService` injection.
- [x] Add the same `OnView` parameter to `CivitaiModelImageCard.razor`. Keep the existing pre-fetch branch (`Civitai.GetImageByModelVersionId`) so the projected DTO has its `Hash`/`Meta` populated, then raise `OnView` instead of opening the legacy dialog. Drop the `IDialogService` injection.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiImageCard.razor`:
  - Removed `@inject IDialogService DialogService`.
  - Added `OnView` `EventCallback<CivitaiImageDto>` and a private `RaiseView()` helper.
  - Markup info button now binds to `RaiseView`.
- `BlazorWebApp/Components/Resources/CivitaiModelImageCard.razor`:
  - Removed `@inject IDialogService DialogService`.
  - Added `OnView` parameter; `ShowImage()` retains the pre-fetch step and now invokes `OnView` instead of `DialogService.ShowAsync<CivitaiImageDialog>`.
  - Removed dead `result.Data == "Save"` branch.

#### Notes

- The "send to Img2Img / Img2Img" hover quick-actions on `CivitaiImageCard` were already commented out in `SendTo(bool)`. Left untouched per minimal-change discipline.

---

### Step 2: Panels host the AssetViewer

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Add a single `AssetViewer` instance to `CivitaiImagesPanel.razor`, project `_images.Images` lazily on first view, track `StartIndex`, and synchronise `ExternalSourceUrl` with the active asset via `OnAssetChanged`.
- [x] Same wiring in `CivitaiModelVersionInfoPanel.razor`, scoped per-version: when a card raises `OnView`, project that version's `Images` and pass them to the viewer.

#### Changes Made

- `BlazorWebApp/Components/Resources/CivitaiImagesPanel.razor`:
  - Added `@using ImageEntity = BlazorWebApp.Data.Entities.Image`.
  - Added a panel-level `AssetViewer` outside the layout shell (so the overlay isn't constrained by the sidebar/content split).
  - Card invocations now pass `OnView="OpenViewer"`.
  - New private state: `_showViewer`, `_viewerStartIndex`, `_viewerAssets`, `_viewerExternalUrl`.
  - `OpenViewer(CivitaiImageDto)` projects the full result list with `CivitaiAssetAdapter.Project(...)`, locates the clicked DTO, sets the start index, and toggles `_showViewer`.
  - `HandleViewerAssetChanged(ImageEntity)` keeps `ExternalSourceUrl` synced with the active asset by mirroring the index back into the source DTO list.
  - `BuildExternalUrl(CivitaiImageDto)` returns `https://civitai.com/images/{id}` when the DTO has a positive id.
- `BlazorWebApp/Components/Resources/CivitaiModelVersionInfoPanel.razor`:
  - Added `@using ImageEntity = BlazorWebApp.Data.Entities.Image`.
  - Added the same panel-level `AssetViewer` plus per-version `OpenViewer(version, image)` handler that projects only the active version's images (matches the plan's "active version's image set" rule).
  - Tracks `_viewerSourceImages` so `OnAssetChanged` can resolve the per-image external URL without duplicating DTOs into the entity.

---

### Step 3: AssetViewer parameter wiring

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Pass `ShowFavorite="false" ShowScore="false" ShowOpenInExplorer="false"` to suppress local-only controls.
- [x] Pass `ExternalSourceUrl="@_viewerExternalUrl"` so `AssetViewer` renders the "Open source" link.
- [x] Confirm `AssetInfoPanel` correctly hides the "Send image to" buttons (its existing `SendTo.IsLocal(...)` gate fires for `https://...` URLs - no extra change required).

#### Changes Made

- Parameters wired on both panel-level `AssetViewer` instances; verified by build only at this stage. Manual smoke validation pending in Phase 5.

---

### Step 4: Delete legacy dialog

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Delete `BlazorWebApp/Components/Resources/CivitaiImageDialog.razor`.
- [x] Verify no other consumer references the dialog (grep).
- [x] `_Imports.razor` not affected (the dialog was referenced only via fully-qualified `DialogService.Show<CivitaiImageDialog>` calls, both replaced).
- [x] Build clean after deletion.

#### Changes Made

- Deleted `BlazorWebApp/Components/Resources/CivitaiImageDialog.razor`.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                              |
| ---- | ------ | ---------- | -------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 2          | Cards now raise `OnView` and own no dialog plumbing.                                               |
| 2    | [x]    | 1          | Both panels host a single `AssetViewer` and project DTOs through `CivitaiAssetAdapter`.            |
| 3    | [x]    | 1          | `ShowFavorite/Score/OpenInExplorer` set false; `ExternalSourceUrl` set to civitai.com/images/{id}. |
| 4    | [x]    | 1          | Legacy `CivitaiImageDialog.razor` deleted; build clean.                                            |

---

## Issues & Resolutions

### Issue 1: Duplicated `ShowImageInfoDialog` block during edit

**Impact:** Three duplicate stubs accumulated in `CivitaiImageCard.razor` from successive replacements.
**Resolution:** Collapsed back to a single canonical `RaiseView()` method (no `ShowImageInfoDialog` shim retained); markup binds to `RaiseView` directly. Build verified clean afterwards.

---

## Commit Checkpoints

- [x] After Step 1 complete (cards refactored)
- [x] After Step 2 complete (panels host viewer)
- [x] After Step 3 complete (parameters wired)
- [x] After Step 4 complete (legacy dialog deleted, build clean)

---

## Phase Summary

CivitAI image viewing now flows through the canonical `AssetViewer` + `AssetInfoPanel` stack instead of a bespoke dialog. Send-to actions automatically route through `IImageSendToService` (via `AssetInfoPanel`'s standard buttons), favourite/score/open-in-explorer controls are suppressed for transient remote assets, and a new "Open source" link surfaces the originating CivitAI image page.

### Accomplishments

1. Two cards (`CivitaiImageCard`, `CivitaiModelImageCard`) reduced to view-emitters with no dialog dependency.
2. Two panels (`CivitaiImagesPanel`, `CivitaiModelVersionInfoPanel`) gained a single `AssetViewer` host with projected DTO lists and per-image external URL tracking.
3. Legacy `CivitaiImageDialog.razor` removed; ~165 LOC of redundant UI deleted.
4. Build remains green (0 errors).

### Metrics

- Files modified: 4 (`CivitaiImageCard.razor`, `CivitaiModelImageCard.razor`, `CivitaiImagesPanel.razor`, `CivitaiModelVersionInfoPanel.razor`).
- Files deleted: 1 (`CivitaiImageDialog.razor`).
- New components: 0.

### Deferred Items

- Manual smoke pass (Gallery / Danbooru / Img2Vid `AssetViewer` parity, CivitAI image dialog replacement, navigation, send-to, external link) - tracked under Phase 5 Step 3.
- Hovering "send to img2img" / "send to upscale" overlay buttons in `CivitaiImageCard` are still no-ops (pre-existing TODO); intentionally left untouched as it is outside Phase 2 scope.

---

**Phase Status:** Complete [x]
