# Danbooru ImageViewer - AssetInfoPanel Integration - Implementation Plan

## Status

**Current Phase:** Documentation Complete

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

- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic

### Key Rules

- Each step = commitable checkpoint
- No time/date references - use complexity points only
- Detours acceptable after discussion - append to plan
- Phase docs must contain enough context to resume in new sessions
- Minimal, focused changes - avoid over-engineering
- User permission required before moving to next phase
- All events must use the pub/sub pattern via `EventService.cs`

---

## Problem Statement

The `ImageViewer` component (used by the Danbooru page for both Search and Library tabs) exposes an **inline info panel** whose "Send To" surface is outdated:

- Hard-coded `Txt2Img` / `Img2Img` buttons routing through `OrchestratorService.SetGenerationParameter(image, param, isImg2Img)` to pages (`/txt2img`, `/img2img`) that were **replaced** by the dynamic workflow-driven `/generate/{workflowId}` page.
- No awareness of the current `WorkflowBase`, no multi-source slot support, no "Send image to workflow" buttons.
- No handling for **external vs local** image paths - `ImageSendToService` depends on `IIOService.GetBase64FromFile(asset.Path)` which only resolves local paths. Danbooru Search posts are remote URLs and would fail silently / produce broken base64 payloads.
- Danbooru images carry **no AI generation metadata** - only the `Prompt` field (constructed from tag bundles) is meaningful. The Send-to-parameters UI must gracefully reflect this.

In parallel, `AssetInfoPanel` (already consumed by `AssetViewer` and `AssetInfoDrawer`) implements the correct, up-to-date pattern: `IImageSendToService.GetImageWorkflows()`, multi-source slot enumeration, per-workflow buttons, and parameter send via workflow id. The inline info panel in `ImageViewer` is essentially a stale, partially duplicated fork of `AssetInfoPanel`.

## Proposed Solution

Replace the inline info panel markup in `ImageViewer.razor` with the existing `AssetInfoPanel` component, then layer the locality gating and bookmark call-to-action on top.

### Design Question (requires user decision before execution)

| Approach                                        | Description                                                                                                                                                                                                                                                                 | Pros                                                                                                                                                                                          | Cons                                                                                                                                                                                             | Est. Complexity                      |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------ |
| **A. Integrate `AssetInfoPanel`** (recommended) | Delete the inline info panel markup/code in `ImageViewer` and render `<AssetInfoPanel>` inside the existing `<div class="info-panel">` slide-out. Pass `Asset`, `IsVideo`, `ShowKeyboardShortcuts`, and a close callback. Add a locality-aware wrapper around send buttons. | Single source of truth. Automatic parity with Gallery. Dynamic workflow send + multi-source support for free. Less code in `ImageViewer`. Matches existing component composition conventions. | Requires `AssetInfoPanel` to accept a couple of new opt-in parameters (e.g., `ShowAiMetadata`, `ExternalBookmarkCta`). Small risk of visual regressions in the Gallery consumer until re-tested. | 8                                    |
| **B. Expand `ImageViewer` in place**            | Keep the inline info panel but swap `SendSelectedTo(bool)` for `IImageSendToService` calls, add workflow enumeration, multi-source loops, external gating.                                                                                                                  | Isolated change - Gallery consumers untouched.                                                                                                                                                | Perpetuates duplication. Every future `AssetInfoPanel` enhancement must be re-applied here. `ImageViewer` grows further. Violates DRY; code already drifted once.                                | 8 (same effort, higher ongoing cost) |

**Assistant recommendation: Approach A.** The duplication is already causing drift; absorbing the panel keeps one implementation and aligns with the AssetViewer/AssetInfoDrawer composition pattern. The extra parameters on `AssetInfoPanel` are strictly additive.

### Key Decisions (to be confirmed by the user)

| Decision                                                                                       | Rationale                                                                                                   |
| ---------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Adopt Approach A (integrate `AssetInfoPanel` into `ImageViewer`)                               | Eliminates duplication; aligns with dynamic workflow system; single upgrade path.                           |
| Local-file-only for "Send image to workflow"                                                   | Matches existing `ImageSendToService` assumption; avoids async base64 download pipeline; scope stays small. |
| External images keep parameter-send enabled (prompt only)                                      | `SetGenerationParameter` works on in-memory `Image` with `Prompt` populated - no file IO required.          |
| Show a bookmark CTA on external images                                                         | Users have a direct path to unlock full send-to-workflow behaviour without leaving the viewer.              |
| Locality detection lives in `ImageSendToService` (new helper)                                  | Keeps the rule next to the code that will fail if violated; reusable from any consumer.                     |
| Legacy `SendSelectedTo(bool)` and the `Txt2Img`/`Img2Img` routing in `ImageViewer` are removed | Dead code after migration; keeping them creates two ways to do the same thing.                              |
| Supersede `plans/danbooru-imageviewer-send-to-workflows.md`                                    | Older scratch plan - this document replaces it.                                                             |

### Conventions

- All new event hooks (e.g., bookmark request from external viewer) route through `IEventService` pub/sub (the Danbooru page is already a subscriber of `DanbooruMediaSavedEventArgs`).
- `AssetInfoPanel` parameters are additive - default behaviour unchanged for existing callers (`AssetViewer`, `AssetInfoDrawer`).
- No Powershell scripts expected; if one is needed it must be saved as a `.ps1` and executed per workspace rules.
- Follow the persistence/migration conventions from `.github/copilot-instructions.md` (not expected to apply here - no schema changes).

---

## Stress Points & Risks

| Risk                                                                                                                                                                                                                                                                                                  | Mitigation                                                                                                                                                                                                                       | Complexity |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Visual regression in Gallery's `AssetViewer`/`AssetInfoDrawer` after adding new parameters                                                                                                                                                                                                            | Keep defaults identical to today; smoke-test Gallery before closing Phase 2.                                                                                                                                                     | 2          |
| `IImageSendToService` workflow lookups filter by `WorkflowBase`; if Danbooru is opened while no backend is connected, both workflow lists are empty                                                                                                                                                   | `AssetInfoPanel` already guards with `.Any()`; CTA banner should still render when images are external so users see guidance regardless of backend.                                                                              | 1          |
| Locality check false positives/negatives (e.g., library paths vs gallery paths vs remote URLs)                                                                                                                                                                                                        | Centralise in `ImageSendToService.IsLocal(string path)`; write a dedicated unit test covering Gallery (`/image/...`), Danbooru library (`/files/danbooru/...`), Danbooru search (`https://...`), empty/null.                     | 2          |
| `SendParametersToWorkflow` currently issues `QueueGenerationParameter` with `isImg2Img` derived from `workflow.Mode` - for Danbooru (prompt only) this is fine, but selected params UI still offers `Seed`, `CfgScale`, etc. when `ShowAiMetadata=false` because selection lives in `_selectedParams` | With Approach A, `AssetInfoPanel` already only renders meta items that exist on the asset (`Asset.Seed > 0`, etc.). Verify during Phase 2 test that Danbooru-sourced `Image` entities never populate AI fields.                  | 2          |
| `AssetInfoPanel` attempts `IO.ReadMetadata(Asset.Path)` on every load - for external URLs this will hit the backend and may 404                                                                                                                                                                       | Short-circuit `LoadWorkflowData()` when path is external/non-local.                                                                                                                                                              | 2          |
| Removing legacy `Txt2Img`/`Img2Img` buttons might surprise Gallery users who still click them                                                                                                                                                                                                         | They were already superseded; mention in Phase 4 documentation update. Dynamic workflow buttons cover every current mode.                                                                                                        | 1          |
| `ImageViewer.IsExternal` currently gates favorite/rating UI too - make sure locality introduction does not regress that                                                                                                                                                                               | New `IsLocal` helper is purely additive; keep `IsExternal` parameter semantics untouched.                                                                                                                                        | 1          |
| Session flags (`Img2ImgInputImage`, `Img2VidInputImage`, `PendingSourceImages`) rely on navigation to `/generate/{id}` - confirm Danbooru viewer's overlay dismisses cleanly after navigation                                                                                                         | `ImageSendToService` already calls `NavigationManager.NavigateTo` which unmounts the overlay; verify scroll lock released via existing `SetBodyScrollLock(false)` in `Close()`. May require calling `Close()` before navigation. | 3          |

---

## Implementation Phases

### Phase 1: Locality Helper & AssetInfoPanel Parameter Extension

**Objective:** Add the `IsLocal` helper to `IImageSendToService` and extend `AssetInfoPanel` with opt-in parameters required by `ImageViewer` - without changing any caller yet.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [ ] Step 1 (2 pts) - Add `bool IsLocal(string? path)` to `IImageSendToService` + implementation in `ImageSendToService`. Cover: null/empty -> false; starts with `http://`/`https://` -> false; otherwise true. Add `BlazorWebApp.Tests` unit coverage for the four path shapes identified in Stress Points.
- [ ] Step 2 (2 pts) - Extend `AssetInfoPanel` with three additive parameters: `bool ShowAiMetadata = true`, `bool ShowKeyboardShortcuts = false`, `RenderFragment? HeaderTrailing = null` (reserved for the external-asset banner / close button coming from `ImageViewer`). Default render path stays byte-identical for `AssetViewer`/`AssetInfoDrawer`.
- [ ] Step 3 (1 pt) - Short-circuit `AssetInfoPanel.LoadWorkflowData()` when `!SendTo.IsLocal(Asset.Path)` to avoid metadata reads for external URLs.

#### Success Criteria

- Unit tests for `IsLocal` pass.
- Gallery viewer (`AssetViewer`) and drawer (`AssetInfoDrawer`) render unchanged.
- `dotnet build` succeeds.

---

### Phase 2: ImageViewer -> AssetInfoPanel Migration

**Objective:** Replace the inline info panel markup and code in `ImageViewer` with `AssetInfoPanel`; remove the legacy `SendSelectedTo(bool)` path.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [ ] Step 1 (3 pts) - Rewrite the `<div class="info-panel">` section of `ImageViewer.razor`: host `<AssetInfoPanel>` passing `Asset=CurrentImage`, `IsVideo=IsCurrentVideo`, `ShowAiMetadata=ShowAiMetadata`, `ShowKeyboardShortcuts=true`, `SelectedParams` / `SelectedParamsChanged` bound to the existing `_selectedParams` field, and `OnClose=ToggleInfo`.
- [ ] Step 2 (2 pts) - Delete the now-unused markup (meta grid, prompt blocks, old send-to section) from `ImageViewer.razor` and the corresponding code-behind (`SendSelectedTo`, `ToggleParam`, `LoadImageMetadata` model/sampler loading that `AssetInfoPanel` already performs). Keep pan/zoom, navigation, slideshow, favorite/rating logic untouched.
- [ ] Step 3 (2 pts) - Ensure scroll lock is released when `AssetInfoPanel` triggers workflow navigation: if the panel navigates via `NavigationManager`, `ImageViewer` must observe that and call `SetBodyScrollLock(false)`. Approach: subscribe to `NavigationManager.LocationChanged` while visible, or expose a new `OnBeforeSendTo` callback from `AssetInfoPanel`. Decision to be made during planning discussion for this step.
- [ ] Step 4 (1 pt) - Verify CSS - `AssetInfoPanel.razor.css` and the `ImageViewer.razor.css` info-panel styles must not conflict. Adjust selectors if needed (prefer extending `AssetInfoPanel.razor.css`).

#### Success Criteria

- `ImageViewer` renders the info panel indistinguishable from the Gallery's `AssetViewer` on local images.
- Gallery flow still works end-to-end (select image -> info panel -> send to workflow -> navigate).
- Build succeeds; no warnings introduced.

---

### Phase 3: External Image Gating & Bookmark CTA

**Objective:** Implement the UX for external (non-bookmarked) Danbooru images: hide/disable "Send image to workflow" buttons, show a bookmark CTA, keep parameter-send enabled for prompt.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [ ] Step 1 (2 pts) - In `AssetInfoPanel`, when `!SendTo.IsLocal(Asset.Path)`, hide the "Send image to:" section entirely and render an informational `MudAlert` (info severity) with text: "Bookmark this image to enable workflow send. Only prompt parameters are available for external images." Add an optional `EventCallback OnRequestBookmark` parameter; when set, render a button inside the alert that invokes it.
- [ ] Step 2 (1 pt) - In `ImageViewer`, accept an optional `EventCallback<Image> OnRequestBookmark` parameter and forward it to `AssetInfoPanel`. Wire through to the Danbooru page.
- [ ] Step 3 (2 pts) - In `Pages/Danbooru.razor`, implement `HandleBookmarkFromViewer(Image img)`: locate the corresponding `DanbooruPost` (via a `Dictionary<string,DanbooruPost>` keyed by the thumbnail URL that `ShowImageViewerDialog` already has access to), call `LibraryService.SaveAsync(post)`, show the existing snackbar messaging, and rely on the existing `DanbooruMediaSavedEventArgs` subscription to refresh the list. After save, update the in-memory `Image.Path` to the local `/files/danbooru/{FilePath}` so the viewer switches to the local-available UI without re-opening.

#### Success Criteria

- External (Search-tab) images show the bookmark CTA; parameter send still works for the prompt.
- After clicking "Bookmark", the viewer flips to the local-file UI in place (no dialog reopen required).
- Library images (already local) show the full workflow send UI.

---

### Phase 4: Cleanup, Supersede Old Plan, Knowledge Base Update

**Objective:** Remove dead code paths, supersede the older scratch plan, update documentation.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps

- [ ] Step 1 (1 pt) - Delete the legacy `plans/danbooru-imageviewer-send-to-workflows.md` (or archive with a pointer to this plan). Cross-reference from relevant `/Documentation` entries.
- [ ] Step 2 (1 pt) - Remove any orchestrator-level helpers that existed solely for the removed `SendSelectedTo(bool)` path, if unused elsewhere (grep first; do not delete shared helpers).
- [ ] Step 3 (1 pt) - Update `/Documentation` feature docs: add a section under the image-viewing / gallery docs describing the unified `AssetInfoPanel` usage and the Danbooru external-image bookmark rule.

#### Success Criteria

- Repo has a single authoritative plan for this feature.
- `grep -r "SendSelectedTo"` returns no hits.
- Documentation reflects the unified panel and locality rule.

---

## Changelog

| Phase    | Changes                                                                                                                |
| -------- | ---------------------------------------------------------------------------------------------------------------------- |
| Planning | Initial plan created; pending user approval on Approach A vs B and on the plan structure.                              |
| Phase 1  | Added `IsLocal` helper, extended `AssetInfoPanel` with additive parameters, short-circuited metadata for external URLs |
| Phase 2  | Replaced inline info panel in `ImageViewer` with `<AssetInfoPanel>`, removed legacy code, verified scroll lock and CSS |
| Phase 3  | Added locality gating for external images, forwarded `OnRequestBookmark`, implemented `HandleBookmarkFromViewer`       |
| Phase 4  | Superseded old scratch plan, confirmed no dead code, updated documentation                                             |

---

## References

- `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - target component to reuse.
- `BlazorWebApp/Components/Shared/Image/ImageViewer.razor` - component being refactored.
- `BlazorWebApp/Services/ImageSendToService.cs` / `IImageSendToService.cs` - send orchestration; locality helper lands here.
- `BlazorWebApp/Pages/Danbooru.razor` - consumer of `ImageViewer`; owner of the bookmark CTA hook.
- `BlazorWebApp/Components/Shared/AssetViewer.razor`, `Components/Shared/Image/AssetInfoDrawer.razor` - existing `AssetInfoPanel` consumers (regression surfaces).
- `plans/danbooru-imageviewer-send-to-workflows.md` - older, narrower scratch plan; to be superseded in Phase 4.
- `.github/copilot-instructions.md` - workspace conventions (pub/sub event rule, Powershell script rule).
