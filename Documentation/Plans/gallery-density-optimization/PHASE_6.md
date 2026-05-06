# Phase 6 - Infinite Masonry Revisit And AssetViewer Alignment

## Status

**Phase:** 6  
**Build Status:** Passed  
**Tests:** Focused diagnostics + browser validation

---

## Objective

Revisit infinite masonry after the primary paginated experience is fixed, addressing known loading/detection issues and aligning viewer behavior.

---

## Context

Phases 1 through 5 made paginated Gallery the validated image-first path. Infinite masonry was intentionally deferred because it uses a separate absolute-positioned JavaScript layout path and still hosts separate `ImageViewer` / `VideoViewer` components instead of the app-wide `AssetViewer`.

Initial inspection found two concrete issues to address first:

- `Index.razor` and `InfiniteScrollMasonry.razor` can both attach `InfiniteMasonry.js`, but the script keeps module-global scroll/layout state. This can cause competing handlers and stale disposal behavior.
- `InfiniteMasonry.js` observes appended items but does not always schedule a layout when parent-side preload appends items outside the scroll callback path.

Relevant files:

- `BlazorWebApp/Pages/Index.razor`
- `BlazorWebApp/Components/Gallery/InfiniteScrollMasonry.razor`
- `BlazorWebApp/Components/Gallery/InfiniteScrollMasonry.razor.css`
- `BlazorWebApp/wwwroot/js/InfiniteMasonry.js`
- `BlazorWebApp/Components/Shared/AssetViewer.razor`
- `Documentation/Plans/gallery-density-optimization/MAIN_PLAN.md`

---

## Execution Checklist

### Step 1: Consolidate Scroll Ownership

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Remove duplicate infinite-scroll script attachment from `Index.razor`.
- [x] Let `InfiniteScrollMasonry` own scroll observer lifecycle.
- [x] Keep parent-side preload behavior through `EnsureEnoughContent` without competing JS handlers.

---

### Step 2: Align Infinite Viewer With AssetViewer

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Replace separate `ImageViewer` / `VideoViewer` hosting with `AssetViewer`.
- [x] Open both image and video items through the same viewer index.
- [x] Preserve favorite and score updates through shared `Image` entities.

---

### Step 3: Stabilize Masonry Relayout

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Schedule relayout when items are appended or removed.
- [x] Keep layout recalculation debounced enough to avoid thrashing.
- [x] Preserve existing absolute masonry behavior for this pass instead of switching layout engines mid-phase.
- [x] Relayout when presentation mode or tile size changes.
- [x] Reuse `GalleryImageTile` for pure-mode masonry images.

---

### Step 4: Validate Phase 6 Slice

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Run focused diagnostics on touched Razor/CSS/JS files.
- [x] Run an alternate-output build after edits.
- [x] Browser-check infinite scroll mode, appended page layout, and unified viewer opening.
- [x] Document known limitations that remain after this slice.

---

## Current Decision

Keep the existing JavaScript absolute masonry layout for this phase slice and stabilize it. A full conversion to CSS columns, IntersectionObserver sentinel loading, or ResizeObserver-driven layout remains a possible follow-up if this smaller fix does not make infinite masonry reliable enough.

`AssetViewer` is used over the currently loaded infinite-scroll batch. Page-boundary viewer loading is intentionally disabled in masonry by passing a single viewer page; scrolling remains the mechanism that appends more assets.

---

## Validation Results

- Focused diagnostics passed for touched Razor/CSS/JS/docs files.
- Alternate-output builds passed with existing broad warning noise.
- Browser validation on the running app confirmed infinite scroll appends from 29 to 58 items and all items become visible after media settles.
- Browser validation on a temporary development server confirmed pure masonry renders `GalleryImageTile`, tile-size changes relayout columns (`small` width about 306px, `medium` width about 380px in the checked viewport), appended pure tiles remain visible, and fullscreen opens `AssetViewer`.
- Temporary validation server and alternate output were removed after checks.

---

## Phase Summary

Phase 6 removed duplicate scroll ownership, aligned masonry viewing with `AssetViewer`, stabilized relayout on appended/removed items, and made pure Gallery density apply to infinite masonry.

**Phase Status:** Complete [x]
