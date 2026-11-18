# Phase 3 - In-Page Model Detail View

## Status

**Phase State:** Complete and tested
**Complexity:** 13 points
**Parent Plan:** `Documentation/Plans/civitai-page-refactor-and-api-resilience/MAIN_PLAN.md`

---

## Objective

Replace the CivitAI model detail modal with an in-page Models-tab detail view that preserves search results, uses the wider page surface for model images/descriptions/version notes, and keeps metadata/download actions in a narrower sidebar.

---

## Scope

This phase is limited to the CivitAI Models tab detail browsing experience:

- Switch `CivitaiModelsPanel` between search-results and selected-model detail modes.
- Preserve loaded search results while viewing a selected model.
- Add explicit Back behavior that restores the prior results area and scroll/anchor where practical.
- Reuse existing CivitAI detail components where possible, extracting from the current dialog instead of rebuilding behavior from scratch.
- Replace version selection with scrollable tabs.
- Give model/version images, descriptions, and notes more space in the content column.

Out of scope for this phase:

- Broader CivitAI API fail-safe work.
- The optional automatic resource-image download toolbar toggle.
- New persistence or EF migrations.
- Browser history/deep-link integration unless needed for the in-page flow to work.

---

## Steps

- [x] Step 1 (2 pts) - Introduce a Models-tab view mode in `CivitaiModelsPanel`: search results vs selected model detail. Remove `IDialogService` usage for model details once the page view is ready.
- [x] Step 2 (2 pts) - Add search-result preservation and back behavior. Keep `_models` in memory while viewing a model, capture selected card id and scroll position before entering detail, and restore anchor/scroll after returning.
- [x] Step 3 (2 pts) - Build or extract a `CivitaiModelDetailPanel` from the current dialog parts. Use `TwoColumnLayout` with metadata/download actions in the sidebar and images/description/version content in the main area.
- [x] Step 4 (2 pts) - Replace `CivitaiVersionSelector` with MudBlazor scrollable version tabs. Keep version status affordances from `CivitaiVersionStatusHelper` and preserve selected version id while navigating.
- [x] Step 5 (2 pts) - Replace the hard-to-navigate thumbnail strip with a higher-signal image area in the content column: larger thumbnail grid, previous/next controls, or image count plus direct `AssetViewer` open flow. Choose after testing models with many images.
- [x] Step 6 (1 pt) - Move descriptions and version notes into the content column with roomier scroll behavior, avoiding cramped expansion panels where long descriptions dominate.
- [x] Step 7 (1 pt) - Add visual and interaction checks at desktop and mobile widths for long version names, sparse metadata, many images, long descriptions, no-image versions, back navigation, and scroll restoration.
- [x] Step 8 (1 pt) - Decide whether `CivitaiModelInfoDialog` should be deleted, left as an unused fallback, or retained temporarily until regression confidence is high.

---

## Implementation Notes

- Follow `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` plus the Razor/CSS instruction files before editing UI components.
- Keep `CivitaiModelsPanel` as the owner of loaded search results so the first in-page detail pass does not require a route-level cache service.
- The first implementation checkpoint should focus on view-mode plumbing only. Full detail layout extraction belongs to Step 3.
- Preserve existing download, save-all-images, metadata enrichment, and viewer behavior while moving the host surface.
- Use `TabbedPageShell` and documented layout variants; do not introduce nested page cards or ad hoc shell spacing.

---

## Validation Plan

- Run file-level diagnostics for touched Razor/C# files after each step.
- Run the `build` task after completing all file edits for a step.
- Manually verify Models tab search still loads, selecting a model enters detail mode, and Back returns without re-running search for Step 1/2.
- For later steps, verify long version names, many images, no-image versions, long descriptions, download actions, save-all-images, and viewer send-to behavior.

---

## Running Notes

- Phase began after Phase 2 saved-image/send-to behavior and the encoded `/image/...` follow-up were confirmed by the user.
- Implemented the in-page detail mode directly in `CivitaiModelsPanel`, preserving loaded `_models` results while a selected model is viewed.
- Added `CivitaiModelDetailPanel` to host the existing model header, version selector, hero/media viewer, and spec/download card in a page-scale `TwoColumnLayout`.
- Replaced the version selector with MudBlazor tabs and replaced the preview strip in `CivitaiModelHero` with previous/next controls plus a scrollable thumbnail grid.
- Retained `CivitaiModelInfoDialog` temporarily as an unused fallback until there is more regression confidence around the new in-page path.
- Runtime smoke used a live CivitAI model with many versions and images. The detail view rendered, version tabs were scrollable, thumbnail navigation updated the active image, and Back returned to the preserved result list. Some remote preview image requests aborted in the browser log, which is expected to be addressed by Phase 4 resilience work.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                                                                                          |
| ---- | ------ | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 2          | `CivitaiModelsPanel` now switches between search results, loading state, and selected model detail. Dialog service usage was removed from the model-open path. |
| 2    | [x]    | 2          | Results stay in memory. Selected card id and scroll position are captured before opening detail, then restored through `civitaiModelNavigation` on Back.       |
| 3    | [x]    | 2          | Added `CivitaiModelDetailPanel` using `TwoColumnLayout`; spec/download actions live in the sidebar while media/descriptions live in content.                   |
| 4    | [x]    | 2          | `CivitaiVersionSelector` now uses MudBlazor tabs with status icons and scroll buttons for large version lists.                                                 |
| 5    | [x]    | 2          | `CivitaiModelHero` now has previous/next image controls, image count, and a scrollable thumbnail grid while preserving `AssetViewer` behavior.                 |
| 6    | [x]    | 1          | Model description and version notes render in the content column with roomier scroll limits.                                                                   |
| 7    | [x]    | 1          | Build plus live browser smoke covered search, detail entry, many versions/images, thumbnail navigation, long description rendering, and Back to results.       |
| 8    | [x]    | 1          | Retained the legacy dialog temporarily as a fallback; no active entry path uses it.                                                                            |

---

## Issues & Resolutions

- Fixed a Razor syntax issue introduced while moving the base-model grouping code inside the new render branch.
- `dotnet watch` reported the repo's existing package vulnerability warnings, but the app started and served `/civitai` successfully.
- Live CivitAI media requests still showed intermittent aborted remote image loads; Phase 4 is scoped to harden those fetch/download paths.

---

## Commit Checkpoints

- [x] After Step 1 complete
- [x] After Step 2 complete
- [x] After Step 3 complete
- [x] After Step 4 complete
- [x] After Step 5 complete
- [x] After Step 6 complete
- [x] After Step 7 complete
- [x] After Step 8 complete

---

## Phase Summary

Phase 3 is complete. Model results now open into the Models tab itself instead of a modal, with a narrower metadata/download sidebar and a roomier content column for version tabs, images, descriptions, and notes. Back returns to the preserved result list, and the previous modal component remains in the codebase only as a temporary fallback while the new path gathers regression confidence.

---

**Phase Status:** Complete [x]
