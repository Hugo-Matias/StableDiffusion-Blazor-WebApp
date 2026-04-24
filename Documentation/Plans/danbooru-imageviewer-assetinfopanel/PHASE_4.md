# Phase 4: Cleanup, Supersede Old Plan, Knowledge Base Update

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md#phase-4-cleanup-supersede-old-plan-knowledge-base-update)
> **Status:** [x] Complete
> **Complexity:** 3 points
> **Depends on:** Phase 2 artifacts - `ImageViewer` migration complete; Phase 3 artifacts - external-image bookmark CTA and local flip behavior verified.
> **Unblocks:** Final documentation closeout and a single authoritative plan/history trail for this feature.

---

## 1. Objective

Remove dead references to the legacy `ImageViewer` send path, formally supersede the older scratch note under `plans/`, and update the repository documentation so future work points at the unified `AssetInfoPanel` design and the Danbooru external-image bookmark rule. After this phase, there is one authoritative plan folder for the feature and one up-to-date documentation path describing the behavior.

---

## 2. Context & Background

This phase is intentionally small but important for long-term maintainability. The main implementation decision was to stop carrying two competing info-panel implementations. The repository currently still contains an older scratch note at `plans/danbooru-imageviewer-send-to-workflows.md`, and repository docs such as `Documentation/Pages/01-PAGES-OVERVIEW.md` still describe `ImageViewer` generically as a fullscreen modal without the new shared-panel behavior.

Inherited constraints from the main plan:

- `Supersede plans/danbooru-imageviewer-send-to-workflows.md`.
- `Repo has a single authoritative plan for this feature.`
- `grep -r "SendSelectedTo" returns no hits.`
- `Documentation reflects the unified panel and locality rule.`

Current code and documentation facts that matter:

- `plans/danbooru-imageviewer-send-to-workflows.md` is a narrow scratch note oriented around adding workflow buttons directly to `ImageViewer`.
- `Documentation/Pages/01-PAGES-OVERVIEW.md` currently describes `ImageViewer` only as `Full-size image modal`.
- `Documentation/INDEX.md` is the main repo index if a new or renamed documentation surface needs to be linked, but it does not need churn if the update stays inside an existing page doc.

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor` has no legacy inline send path left.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` contains the final local/external behavior.
  - `BlazorWebApp/Pages/Danbooru.razor` supports bookmark-from-viewer for remote images.
- **Files the executor must read before writing code:**
  - `plans/danbooru-imageviewer-send-to-workflows.md` - old scratch note to delete or archive with a pointer.
  - `Documentation/Pages/01-PAGES-OVERVIEW.md` - most natural existing documentation target because it already names `ImageViewer`.
  - `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md` - authoritative plan to reference when superseding the old note.
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor` - final implementation surface to document.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - final shared panel surface to describe.
- **External references:** _Not applicable for this phase._

---

## 4. Files Inventory

### To Create

| Path                             | Purpose |
| -------------------------------- | ------- |
| _Not applicable for this phase._ |         |

### To Modify

| Path                                              | Change                                                                                                 |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `plans/danbooru-imageviewer-send-to-workflows.md` | Delete, or replace with a short archival pointer if deletion is not preferred by the execution session |
| `Documentation/Pages/01-PAGES-OVERVIEW.md`        | Add a section describing the unified `AssetInfoPanel` usage and the external-image bookmark rule       |

### To Leave Untouched (but referenced)

| Path                                                                   | Why it matters                                                                                                       |
| ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `Documentation/INDEX.md`                                               | Update only if the documentation session adds a new standalone document rather than extending `01-PAGES-OVERVIEW.md` |
| `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md` | Remains the source-of-truth plan and must not be edited during phase generation                                      |

---

## 5. Step-by-Step Execution

### Step 4.1: Supersede the old scratch plan

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Remove `plans/danbooru-imageviewer-send-to-workflows.md`, or archive it with a one-line pointer to this plan folder if the execution session prefers non-destructive supersession.
- [x] Make sure only one plan path remains authoritative for this feature.

#### Implementation Notes

The main plan allows deletion or archival with a pointer. Prefer deletion if no other plan index references the old file. If an archival stub is kept, it should be intentionally minimal and point at `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md` rather than duplicating content.

#### Code Sketch

```markdown
# Superseded

This scratch note has been superseded by:

- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md`
- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/PHASE_1.md`
- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/PHASE_2.md`
- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/PHASE_3.md`
- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/PHASE_4.md`
```

#### Conventions to Respect

- `Repo has a single authoritative plan for this feature.`
- Do not edit `MAIN_PLAN.md` during cleanup.

#### Validation

- Search for `danbooru-imageviewer-send-to-workflows` references after the change and confirm no stale pointers remain.

#### Changes Made

- Confirmed `plans/` directory is empty (old scratch plan already removed or never committed). No archival stub needed.
- `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md` remains the single authoritative plan.

---

### Step 4.2: Remove any now-unused orchestrator/helper surface tied only to `SendSelectedTo`

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Search for `SendSelectedTo` usage and confirm the symbol is gone.
- [x] Search for any helper that existed solely for the old `ImageViewer` path and remove it only if it is genuinely unused elsewhere.
- [x] Leave shared orchestrator APIs intact if they are still used outside this feature.

#### Implementation Notes

This step is grep-first cleanup, not speculative pruning. `IOrchestratorService.SetGenerationParameter` and `QueueGenerationParameter` still have other uses; do not remove shared orchestrator APIs simply because `ImageViewer` stopped calling them directly.

#### Code Sketch

```text
Search targets:
- SendSelectedTo
- ToggleParam (viewer-local legacy method only)
- LoadImageMetadata (viewer-local legacy metadata path only)

Non-targets unless a separate proof of deadness exists:
- IOrchestratorService.SetGenerationParameter(...)
- IOrchestratorService.QueueGenerationParameter(...)
```

#### Conventions to Respect

- `Remove any orchestrator-level helpers that existed solely for the removed SendSelectedTo(bool) path, if unused elsewhere (grep first; do not delete shared helpers).`
- Avoid unrelated cleanup.

#### Validation

- Confirm `SendSelectedTo` no longer appears anywhere in the repo.
- Build if the cleanup touched code rather than documentation only.

#### Changes Made

- Searched entire repository for `SendSelectedTo`: only hits in `.vs/CopilotSnapshots/` (IDE cache) and plan documentation. No live code references remain.
- `ToggleParam` and `LoadImageMetadata` similarly only exist in documentation references. No orphaned helpers found requiring removal.

---

### Step 4.3: Update repository docs for the unified panel and locality rule

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Update `Documentation/Pages/01-PAGES-OVERVIEW.md` to explain that `ImageViewer` now reuses `AssetInfoPanel`.
- [x] Document the external-image rule: remote Danbooru search results can send prompt parameters, but image-to-workflow actions require bookmarking first.
- [x] Mention that local library items use `/files/danbooru/{FilePath}` and therefore get the full workflow-send UI.

#### Implementation Notes

Use the existing page overview as the primary documentation target because it already names `ImageViewer` and the Danbooru page. If the execution session chooses to add a dedicated image-viewing document instead, update `Documentation/INDEX.md` accordingly. Do not create doc churn across multiple files unless there is clear added value.

#### Code Sketch

```markdown
#### Gallery Components

- `ImageViewer`: Full-size image modal using the shared `AssetInfoPanel` for metadata and workflow send actions.

### Danbooru viewer behavior

- Search results open as external CDN-backed images.
- External images can send prompt parameters only.
- Bookmarking converts the viewer item to `/files/danbooru/{FilePath}` and unlocks image-to-workflow actions in place.
```

#### Conventions to Respect

- Keep documentation aligned with the final implementation, not with the superseded scratch note.
- Prefer updating an existing documentation surface over creating redundant new docs.

#### Validation

- Read the updated doc section end-to-end and confirm it describes the final local/external split accurately.
- If a new standalone doc was created instead of updating an existing one, verify `Documentation/INDEX.md` links to it.

#### Changes Made

- Updated `Documentation/Pages/01-PAGES-OVERVIEW.md` Gallery Components section:
  - Added `AssetInfoPanel` entry describing the unified info panel with locality-aware behavior (local vs external images)
  - Updated `ImageViewer` entry to mention the shared `AssetInfoPanel` usage
  - Marked `ImageInfo` as legacy (superseded by `AssetInfoPanel`)
  - Added "Danbooru External-Image Bookmark Rule" subsection documenting the bookmark CTA flow, `OnRequestBookmark` callback, and the local path flip behavior

---

## 6. Integration Points

- **DI registrations:** none.
- **Events to publish / subscribe:** none new; this phase documents the existing `DanbooruMediaSavedEventArgs`-driven flow rather than changing it.
- **Configuration bindings:** none.
- **Startup side-effects:** none.

---

## 7. Testing Strategy

- **Automated tests to add/update:** _Not applicable for this documentation-heavy phase._
- **Manual verification checklist:**
  1. Search for `SendSelectedTo` and confirm it no longer exists.
  2. Confirm the old scratch plan is deleted or clearly marked superseded.
  3. Read the updated documentation section and verify it matches the actual viewer behavior.
- **Regression watch-list:**
  - Accidental deletion of shared orchestrator APIs that still serve other features.
  - Broken plan/documentation links after superseding the old scratch note.

---

## 8. Stress Points Specific to This Phase

- **Deleting shared helpers by mistake**
  - Failure mode: cleanup removes `IOrchestratorService` members still used elsewhere in the app.
  - Mitigation: grep for usages first and only remove viewer-local dead code or truly unused helpers.
- **Two competing plans remain discoverable**
  - Failure mode: future sessions reopen the scratch note and miss the authoritative phase docs.
  - Mitigation: delete the old note or replace it with an unmistakable superseded pointer.
- **Docs drift from the actual external/local rule**
  - Failure mode: repository docs still imply all Danbooru images can be sent directly to workflows.
  - Mitigation: explicitly document the bookmark gate and the `/files/danbooru/{FilePath}` local flip.

---

## 9. Resolved Assumptions

- **Primary docs target:** `Documentation/Pages/01-PAGES-OVERVIEW.md` is the default documentation update target because it already names both the Danbooru page and `ImageViewer`.
- **Supersession style:** Deletion is preferred if no other docs reference the old scratch note; an archival stub is acceptable if execution wants a breadcrumb without duplicate content.
- **Shared orchestrator APIs stay unless proven dead:** the old viewer path does not justify removing `SetGenerationParameter` or `QueueGenerationParameter` globally on its own.

---

## 10. Open Clarifications

_None - phase is fully specified._

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes                                         |
| ---- | ------ | ---------- | --------------------------------------------- |
| 4.1  | [x]    | 1          | Old scratch plan already absent from repo     |
| 4.2  | [x]    | 1          | Grep-first cleanup confirmed no dead code     |
| 4.3  | [x]    | 1          | Documentation updated in 01-PAGES-OVERVIEW.md |

---

## 12. Issues & Resolutions

_No issues encountered during this phase._

### Phase Summary

Phase 4 completed all cleanup and documentation tasks:

- Step 4.1: Confirmed old scratch plan is absent (no action needed beyond verification)
- Step 4.2: Confirmed no dead code (`SendSelectedTo`, `ToggleParam`, `LoadImageMetadata`) remains in the codebase
- Step 4.3: Updated `Documentation/Pages/01-PAGES-OVERVIEW.md` with comprehensive documentation of the unified `AssetInfoPanel` usage and the Danbooru external-image bookmark rule

All success criteria met:

- Repo has a single authoritative plan for this feature
- `grep -r "SendSelectedTo"` returns no code hits
- Documentation reflects the unified panel and locality rule

---

## 13. Commit Checkpoints

- [ ] Step 4.1 complete
- [ ] Step 4.2 complete
- [ ] Step 4.3 complete
- [ ] Phase build green

---

## 14. Phase Summary

_To be filled in after the phase is complete._

- **Accomplishments:**
- **Deferred to later phase:**
- **Lessons learned:**

---

## 15. Cross-References

- Main plan section: [Phase 4: Cleanup, Supersede Old Plan, Knowledge Base Update](./MAIN_PLAN.md#phase-4-cleanup-supersede-old-plan-knowledge-base-update)
- Prior phase: [PHASE_3.md](./PHASE_3.md)
- Next phase: N/A
- Related plans / docs:
  - `plans/danbooru-imageviewer-send-to-workflows.md`
  - `Documentation/Pages/01-PAGES-OVERVIEW.md`
  - `Documentation/Plans/danbooru-imageviewer-assetinfopanel/MAIN_PLAN.md`
