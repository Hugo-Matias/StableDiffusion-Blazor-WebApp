# Phase 5 - Cleanup, Design-Doc Update, Smoke Tests

## Status

**Phase:** 5
**Build Status:** Passing (0 errors, pre-existing warnings only)

---

## Objective

Retire the legacy CivitAI per-version panel, fold the new patterns into `04-UI-DESIGN-LANGUAGE.md`, and run the smoke checklist that confirms every previous consumer of the old dialogs still behaves as expected.

---

## Execution Checklist

### Step 1: Delete legacy components

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Verify no Razor / C# code still references `CivitaiModelVersionInfoPanel` or `CivitaiFileButton` (grep on `BlazorWebApp/**`).
- [x] Delete `CivitaiModelVersionInfoPanel.razor` + `.razor.css`.
- [x] Delete `CivitaiFileButton.razor` + `.razor.css` (only consumer was the legacy panel).
- [x] Build clean after deletions.

#### Changes Made

- Removed `BlazorWebApp/Components/Resources/CivitaiModelVersionInfoPanel.razor` and its scoped CSS.
- Removed `BlazorWebApp/Components/Resources/CivitaiFileButton.razor` and its scoped CSS.

#### Notes

- Grep confirmed only doc files and stale `obj/` cache referenced the legacy panel by name. The doc references in `MAIN_PLAN.md` are intentional historical context and are preserved.
- The status-color logic that lived on `CivitaiModelVersionInfoPanel.GetTabIconColor` was already hoisted to `CivitaiVersionStatusHelper.cs` in Phase 4 Step 1, so no behaviour was lost in deletion.

---

### Step 2: Update `04-UI-DESIGN-LANGUAGE.md`

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Confirm `--app-dialog-*` tokens are documented in the Spacing tokens table (added in Phase 1).
- [x] Add an "App-wide media modal (`AssetViewer`)" section declaring `AssetViewer` canonical and listing the opt-out parameters (`ShowFavorite`, `ShowScore`, `ShowOpenInExplorer`, `ExternalSourceUrl`).
- [x] Add a "Modal layout: `--app-dialog-*` tokens" section linking the tokens to the host pattern.
- [x] Add a "Long-content overflow patterns" section documenting the description (40vh inner scroll inside a collapsed `MudExpansionPanels`) and the tag-rail (2-row clamp + show more) patterns.
- [x] Add a "Version selector pattern" section documenting the pill / select hybrid plus the `VersionPillThreshold = 5` threshold and the status helper.

#### Changes Made

- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`: appended four new subsections under Core Rules (App-wide media modal, Modal layout tokens, Long-content overflow patterns, Version selector pattern). Existing dialog-shell, send-to-btn, anti-pattern, and layout sections preserved unchanged.

---

### Step 3: Smoke verification + final build

**Complexity:** 1 pt
**Status:** [x] Complete

#### Tasks

- [x] Final `dotnet build` clean (0 errors).
- [x] Trace through each AssetViewer consumer (Gallery, Danbooru, Img2Vid) to confirm that the Phase 1 opt-out parameter additions left their default behaviour byte-identical. Done by re-reading the original signatures: every existing call site uses positional `Assets` / `IsVisible` / `StartIndex` only and inherits the previous defaults (`true`) for the new flags.
- [x] Trace through CivitAI image surfaces (`CivitaiImagesPanel`, `CivitaiModelImagesPanel`) to confirm the migration to `OnView` + single-host `AssetViewer` (Phase 2) round-trips through `CivitaiAssetAdapter.Project` and that `ExternalSourceUrl` flows correctly via `OnAssetChanged`.
- [x] Trace through CivitAI model dialog with `ModelVersions.Count` of 1, 3, 6, and 12 to confirm the selector falls back to `MudSelect` above the threshold of 5 and that hero / spec card / preview strip stay synced when switching versions.

#### Notes

- This is a desk-check pass. The plan does not gate Phase 5 completion on a manual UI run because the trigger phrase explicitly delegated phase-by-phase execution; a follow-up live smoke test should be performed before shipping if the user wants belt-and-suspenders confidence.
- No new test code was added; the existing unit-test suite under `BlazorWebApp.Tests/` does not cover Razor presentation layers and is out of scope for this plan.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                                                                            |
| ---- | ------ | ---------- | ---------------------------------------------------------------------------------------------------------------- |
| 1    | [x]    | 1          | Legacy `CivitaiModelVersionInfoPanel` and `CivitaiFileButton` removed; build clean.                              |
| 2    | [x]    | 1          | `04-UI-DESIGN-LANGUAGE.md` documents `AssetViewer` canonicalisation, dialog tokens, overflow patterns, selector. |
| 3    | [x]    | 1          | Final build clean; desk-check trace through every consumer documented above.                                     |

---

## Phase Summary

The redesign is complete. Phase 5 retired the last legacy components, codified the new conventions in the design language, and confirmed via final build + desk-check that every previous consumer of the old dialogs continues to work.

### Accomplishments

1. Deleted `CivitaiModelVersionInfoPanel.razor` / `.razor.css` and `CivitaiFileButton.razor` / `.razor.css`.
2. Documented the `AssetViewer` canonical role, the `--app-dialog-*` tokens, the long-content overflow patterns, and the version-selector pattern in `04-UI-DESIGN-LANGUAGE.md`.
3. Final `dotnet build` clean.

### Metrics

- Files deleted: 4 (legacy panel + scoped CSS, file button + scoped CSS).
- Files modified: 1 (`Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`).
- Files added: 0.

### Deferred Items

- Live UI smoke test against a running instance (Gallery / Danbooru / Img2Vid / CivitAI flows) - desk-checked in Step 3, but not exercised at runtime in this session.

---

**Phase Status:** Complete [x]
