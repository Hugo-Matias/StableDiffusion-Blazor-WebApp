# Resources Page Navigation and LoRA Fixes - Implementation Plan

## Status

**Current Phase:** Implementation complete; runtime QA pending

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to the next step until testing is complete
   - User must explicitly approve before updating phase documents
   - Build runs only after user requests or after completing all file edits for a step

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)

- **1**: Trivial (simple property change, config update)
- **2**: Simple (straightforward refactor, single file change)
- **3**: Moderate (multi-file change, simple logic)
- **5**: Medium (service extraction, interface creation)
- **8**: Complex (component migration, breaking changes)
- **13**: Very complex (architecture change, wide impact)
- **21+**: Epic (should be split into smaller phases)

### Key Rules

- Each step is a commitable checkpoint
- Keep changes focused on Resources, CivitAI preview handling, and LoRA resource integration
- Use the existing pub/sub pattern via `EventService.cs` for resource/cache/model refreshes
- Use `TabbedPageShell`, `TwoColumnLayout`, and existing surface tokens; do not add ad hoc page shells
- Use `.send-to-btn` for simple flat action rows
- Reuse `AssetViewer` for full-screen image/video inspection
- Preserve ComfyUI file values passed to generation; user-facing titles may differ from payload values
- Remove superseded Resource dialogs and their call paths during implementation; do not keep obsolete compatibility shims

---

## Problem Statement

The Resources page still relies on modal dialogs for resource loading and details. This hides important metadata behind tabs and diverges from the newer CivitAI in-page detail navigation pattern. Separately, the existing CivitAI in-page navigation detail view renders its preview hero too tall, making the media hard to inspect because it is rarely visible in full.

There are also related functional bugs and inconsistencies:

1. Resource details should open in-page, with metadata/actions in a sidebar and the main content focused on title, external IDs, preview media, and description.
2. The existing CivitAI navigation detail view and the new Resources detail implementation should show two shorter hero media items instead of one tall preview that is rarely visible in full.
3. CivitAI videos display remotely but fail when saved/downloaded as resource previews or regular saved media because the current save paths are image/PNG-centric.
4. Local Resource images should open in `AssetViewer` like CivitAI images, with send-to support for both prompt parameters and media/source targets.
5. The LoRA loader surfaces Comfy filenames where the user should see resource titles, while still preserving the exact filename/path values used by generation.
6. Newly downloaded LoRAs can appear as untracked in the LoRA loader because resource cache invalidation does not follow the CivitAI download/create path consistently.
7. Send-to actions from the current Resource dialog create LoRA values differently from the LoRA loader panel, producing path formats Comfy does not recognize.

---

## Proposed Solution

Replace the Resource load dialog flow with an in-page detail view modeled after CivitAI model navigation. The Resources tab keeps the existing filter/results state, but selecting a resource switches the content area to a detail panel with a Back action and a metadata/action sidebar.

Move resource media into a reusable detail pattern that can handle local images and videos, use two compact hero media slots where available, and delegate full-screen viewing to `AssetViewer`. Resource assets are local, so the `AssetViewer` info panel should expose both parameter send-to and media/source send-to without requiring a local-save step.

For CivitAI downloads and saves, introduce media-aware persistence instead of forcing every asset through PNG conversion. Store videos with their original extension where possible, update preview path discovery to recognize video extensions, and render video previews wherever a resource preview can appear.

For LoRAs, add a small shared identity/display layer that maps Comfy model values to resource metadata. The loader and Resource send-to actions should agree on the exact Comfy value (`Path`, `HighPath`, `LowPath`) while displaying the resource title when available.

Remove the old Resource load/info/version/image dialogs as the new in-page detail and shared viewer replace their responsibilities. The implementation should leave no dead dialog call paths or compatibility wrappers behind.

### Key Decisions

| Decision                                                                        | Rationale                                                                                                          |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Use CivitAI-style in-page detail navigation for Resources                       | Keeps search/results state alive and avoids cramped modal content                                                  |
| Keep Resources inside documented `TabbedPageShell` + `TwoColumnLayout` patterns | Matches repo UI rules and avoids new shell variants                                                                |
| Use `AssetViewer` for resource image/video inspection and send-to actions       | Avoids parallel media viewers; local Resource assets can use existing parameter and media send-to support directly |
| Preserve Comfy file/path values separately from display titles                  | Prevents visual polish from breaking generation payloads                                                           |
| Fix resource refresh through events at the creation source                      | Resource cache, filters, and LoRA panels should see newly created DB records without manual reloads                |
| Prefer tests around services/path helpers over broad UI snapshot tests          | The highest-risk bugs are path normalization, cache invalidation, and media persistence                            |
| Delete superseded Resource dialogs instead of retaining compatibility logic     | The in-page detail and `AssetViewer` are the replacement surfaces, so stale code should not remain                 |

### Current Code Anchors

- `BlazorWebApp/Pages/Resources.razor` owns tab setup, dialog orchestration, and current LoRA send-to queuing.
- `BlazorWebApp/Components/Resources/ResourcePanel.razor` owns filter/results layout.
- `BlazorWebApp/Components/Resources/LoadResourceDialog.razor` is the main dialog to replace with in-page detail UI.
- `BlazorWebApp/Components/Resources/ResourceInfoDialog.razor`, `ResourceVersionsDialog.razor`, `ResourceImageDialog.razor`, and `ResourceImageCard.razor` are current dialog/media surfaces to remove or absorb into the new in-page/detail-view flow.
- `BlazorWebApp/Components/Resources/CivitaiModelDetailPanel.razor` and `CivitaiModelHero.razor` are the model for in-page navigation and hero media.
- `BlazorWebApp/Components/Shared/AssetViewer.razor` and `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` already provide parameter send-to, media/source send-to, image-to-prompt, and local-media handling.
- `BlazorWebApp/Services/CivitaiService.cs` currently downloads resource cover previews through `TryDownloadPreviewImageAsync`, which writes `.png` and calls `DownloadImageAsPng`.
- `BlazorWebApp/Services/CivitaiResourceImageService.cs` currently saves CivitAI images to `.png` and persists `ResourceImage` rows.
- `BlazorWebApp/Components/Shared/Generation/LoraSelectorPanel.razor` already uses Comfy-provided LoRA values as the authoritative `Lora.Path` values.
- `BlazorWebApp/Components/Shared/Generation/LoraCard.razor` and `LoraSelectorPanel.razor` are the display surfaces that should show resource titles.
- `BlazorWebApp/Services/ResourceCacheService.cs` invalidates on `ResourcesChangedEventArgs`, while CivitAI downloads currently publish `DownloadCompletedEventArgs` after success.

---

## Implementation Phases

### Phase 1: Resources In-Page Detail Navigation

**Objective:** Replace the modal resource load entry point with CivitAI-style in-page navigation while preserving the current filter/results workflow and removing superseded dialog call paths.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Add selected-resource state to the Resources tab flow and route `ResourceCard` selection into an in-page detail state instead of `LoadResourceDialog`.
- [x] Step 2 - Preserve search/page/sidebar state and add a Back action that returns to the previous results without forcing a fresh search.
- [x] Step 3 - Handle multi-file resources in-page with a compact version/file selector instead of `ResourceVersionsDialog`.
- [x] Step 4 - Move not-found/delete handling into the in-page flow, then remove the old dialog orchestration methods that only served modal loading.
- [x] Step 5 - Remove obsolete references to `LoadResourceDialog`, `ResourceVersionsDialog`, and other replaced Resource dialogs from the page flow.

#### Success Criteria

- Selecting a resource opens an in-page detail view, not `LoadResourceDialog`.
- Multi-file resources can switch/select files without opening `ResourceVersionsDialog`.
- Returning to results preserves current filters, card size, page, and scroll position where practical.
- Existing tabs and Import tab still work.
- No obsolete Resource load/version dialog call paths remain.

---

### Phase 2: Resource Detail Sidebar, Actions, and Content

**Objective:** Build the actual Resources detail experience with metadata/actions in the sidebar and resource content in the main panel.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Create a Resource detail component using `TwoColumnLayout` slot rules: sidebar metadata/actions, content panel for title/media/description.
- [x] Step 2 - Sidebar details: type, subtype, base model, enabled state, trigger words, tags, file info, resource weight.
- [x] Step 3 - Sidebar actions: enable/disable toggle, inline edit/update/delete actions, send-to workflow actions, add LoRA to loader, prompt/negative prompt actions where applicable.
- [x] Step 4 - Content panel: title, CivitAI model/version/file IDs as hyperlinks, preview media, description/version notes, and local image/video gallery.
- [x] Step 5 - Move trigger word selection behavior from the dialog into the detail page for LoRA send-to flows.
- [x] Step 6 - Project local `ResourceImage` records and cover previews into `AssetViewer` assets so Resource images/videos open with parameter send-to, media/source send-to, and image-to-prompt support.
- [x] Step 7 - Remove replaced Resource image/info dialog components once their responsibilities are covered by the detail page and `AssetViewer`.

#### Success Criteria

- The old load/info/image dialog behavior is functionally represented in-page or through `AssetViewer`.
- Action buttons are available without switching dialog tabs.
- Local Resource media opened in `AssetViewer` supports both parameter send-to and media/source send-to.
- Checkpoint, LoRA/LoCon, TextualInversion, Hypernetwork, and VAE-disabled behavior remain coherent.
- Resource descriptions no longer force the page into awkward vertical overflow.
- `ResourceInfoDialog`, `ResourceImageDialog`, and obsolete image-card dialog wrappers are removed or no longer referenced.

---

### Phase 3: Two-Hero Media UX for Resources and CivitAI

**Objective:** Fix the oversized preview hero in the existing CivitAI navigation detail view and apply the same two-short-hero pattern to the new Resources detail page.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Retune the existing CivitAI navigation `CivitaiModelHero` to render up to two primary media items in a shorter responsive row/grid.
- [x] Step 2 - Add the same compact hero treatment to the new Resource detail content panel.
- [x] Step 3 - Keep media click-through wired to the panel-owned `AssetViewer`.
- [x] Step 4 - Ensure videos render with sane preload/autoplay/muted behavior in hero slots and thumbnails.
- [x] Step 5 - Adjust CSS with page/component variables instead of hard-coded repeated dimensions.
- [x] Step 6 - Add CivitAI-style Resource thumbnail navigation below the hero media, avoid duplicating cover previews in the saved media list, and render hero/thumb media with contain-fit sizing.

#### Success Criteria

- CivitAI detail pages show two useful hero previews when at least two media assets exist.
- Resources detail pages show the same compact media pattern for local resource images/videos.
- Hero media is visible without requiring excessive vertical scrolling.
- Full-screen viewing still works through `AssetViewer`.
- CivitAI model navigation no longer uses the tall single-preview hero as its primary layout.
- Resource and CivitAI media previews fit inside their containers without cropping.
- Resource cover previews are only used as a fallback when no saved media list exists, avoiding duplicated cover/first-image entries.

---

### Phase 4: CivitAI Video Preview Persistence

**Objective:** Make CivitAI video previews downloadable/saveable for both cover previews and regular saved resource media.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Add media-aware download logic that detects CivitAI video assets by type/URL and streams them to disk with a video extension instead of converting to PNG.
- [x] Step 2 - Update resource cover preview saving so video cover previews can be stored under `ResourcePreviewsPath` and found later.
- [x] Step 3 - Replace image-only preview lookup with a media-preview lookup that returns image or video preview paths, including `.mp4`/`.webm` where present, and migrate call sites.
- [x] Step 4 - Update resource cards/detail media to render video previews when the preview path is a video.
- [x] Step 5 - Update `CivitaiResourceImageService` so regular saved CivitAI videos persist as video files and still create usable `ResourceImage` metadata rows where appropriate.

#### Success Criteria

- Downloading a CivitAI resource whose preview is a video leaves a usable local preview file.
- Saved CivitAI videos open in `AssetViewer` as videos, not failed images.
- Resource cards no longer fall back to no preview solely because the CivitAI preview was a video.
- Image preview behavior remains unchanged for normal images.

---

### Phase 5: LoRA Resource Titles in Loader UI

**Objective:** Show resource titles in LoRA picker cards and applied LoRA cards while preserving Comfy filename/path values.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Add a helper/service method that resolves LoRA display metadata from a Comfy filename/path using `ResourceCacheService`.
- [x] Step 2 - Update `LoraSelectorPanel` cards and untracked list rows to display `Resource.Title` when available, with filename/path retained as tooltip or secondary text.
- [x] Step 3 - Update `LoraCard` applied list display to prefer resource title while retaining enough path/high-low information to disambiguate duplicates.
- [x] Step 4 - Update search filtering so users can find tracked LoRAs by resource title as well as filename.

#### Success Criteria

- LoRA picker cards show human-readable resource titles for tracked LoRAs.
- Applied enabled/disabled LoRA lists show titles where available.
- `Lora.Name`, `Lora.Path`, `Lora.HighPath`, and `Lora.LowPath` values used by generation are not changed for visual-only display.
- High/Low dual-model LoRAs remain distinguishable during review.

---

### Phase 6: LoRA Tracking Refresh and Send-To Path Parity

**Objective:** Fix newly downloaded LoRAs appearing as untracked and make Resource send-to LoRA actions use the same Comfy path format as the LoRA loader.
**Complexity:** 8 points
**Status:** [~] Implementation complete; tests pending

#### Steps

- [x] Step 1 - Publish the appropriate `ResourcesChangedEventArgs` when CivitAI downloads create/update resource DB rows so `ResourceCacheService` invalidates immediately.
- [~] Step 2 - Confirm whether a `ModelsChangedEventArgs` or existing Comfy model refresh path is also required after file download; no additional model-refresh event was added in this pass.
- [x] Step 3 - Extract or reuse a single LoRA value factory that mirrors `LoraSelectorPanel.TryAdd`: `Path` is the exact Comfy-recognized LoRA value, `Name` is `Path.GetFileNameWithoutExtension(Path)`, dual-model paths remain exact.
- [x] Step 4 - Replace `Resources.razor` `QueueLoraForGenerate` path construction with the shared path normalization/factory.
- [ ] Step 5 - Add focused tests for root LoRA files, nested LoRA files, LoCon/subtype paths, Windows separators, and duplicate title cases.

#### Success Criteria

- A newly downloaded LoRA appears as a tracked resource in the LoRA loader after the relevant UI refresh.
- Resource send-to LoRA actions produce the same path format as selecting that LoRA directly from the loader.
- Generation payloads still use Comfy-recognized filenames/relative paths.
- Trigger words and selected weight continue to apply when sending from Resources.

---

### Phase 7: Validation and Documentation

**Objective:** Verify the full workflow with targeted checks and document any updated resource/media conventions.
**Complexity:** 3 points
**Status:** [~] Build validated; focused tests pending

#### Steps

- [ ] Step 1 - Run targeted tests for resource cache/filtering, CivitAI media save paths, and LoRA path factory logic.
- [x] Step 2 - Run a focused build after all file edits for the selected phase are complete.
- [x] Step 3 - Update relevant documentation if a shared media preview convention or LoRA display helper becomes part of the architecture.
- [x] Step 4 - Record any deferred UI refinements or duplicate-title observations in the phase document.

#### Success Criteria

- Targeted validation passes or unrelated failures are clearly isolated.
- The implementation can be resumed from phase docs without rediscovery.
- Media preview and LoRA path/display conventions are documented where useful.

---

## Stress Points & Risks

| Risk                                                                  | Mitigation                                                                                                                   | Complexity |
| --------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | ---------- |
| LoRA display titles accidentally change generation values             | Keep display metadata outside `Lora.Name`/`Path`; add tests around factory output                                            | 5          |
| CivitAI video URLs do not have reliable extensions                    | Detect by DTO type first, fallback to URL/content type, and choose a stable extension                                        | 5          |
| Resource preview path method name says image but now may return video | Introduce a clearer media-preview method, migrate call sites, and remove obsolete image-only callers where they are replaced | 3          |
| In-page detail view duplicates dialog logic and drifts                | Move reusable action/path logic into small helpers/services instead of copying component code                                | 5          |
| Resource cache remains stale after CivitAI download                   | Publish resource change events at DB creation/update points and verify cache invalidation in tests                           | 3          |
| Two-hero layout behaves poorly on narrow screens                      | Use responsive CSS grid/flex with stable aspect ratios and `AssetViewer` for full inspection                                 | 3          |
| Duplicate resource titles hide High/Low distinctions                  | Show title first, path/model assignment as secondary text or compact badge                                                   | 3          |

---

## Open Clarifications

1. For the two hero media slots, should videos count as hero media alongside images, or should the hero prefer still images when available and keep videos in the gallery?
2. For video cover previews on resource cards, should hover autoplay be enabled like CivitAI cards, or should cards show a static video tile with a play indicator until opened?

---

## Changelog

| Phase    | Changes                                                                                                                                                                              |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Planning | Initial phased plan created from Resources/CivitAI/LoRA investigation                                                                                                                |
| Planning | Corrected scope: CivitAI navigation hero is the existing oversized preview, Resource media uses `AssetViewer` with local send-to support, and old Resource dialogs should be removed |

---

## References

- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
- `Documentation/Plans/context-aware-resource-loading/MAIN_PLAN.md`
- `BlazorWebApp/Pages/Resources.razor`
- `BlazorWebApp/Components/Resources/ResourcePanel.razor`
- `BlazorWebApp/Components/Resources/LoadResourceDialog.razor`
- `BlazorWebApp/Components/Resources/ResourceInfoDialog.razor`
- `BlazorWebApp/Components/Resources/ResourceVersionsDialog.razor`
- `BlazorWebApp/Components/Resources/ResourceImageDialog.razor`
- `BlazorWebApp/Components/Resources/CivitaiModelDetailPanel.razor`
- `BlazorWebApp/Components/Resources/CivitaiModelHero.razor`
- `BlazorWebApp/Components/Shared/AssetViewer.razor`
- `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor`
- `BlazorWebApp/Services/CivitaiService.cs`
- `BlazorWebApp/Services/CivitaiResourceImageService.cs`
- `BlazorWebApp/Components/Shared/Generation/LoraSelectorPanel.razor`
- `BlazorWebApp/Components/Shared/Generation/LoraCard.razor`
- `BlazorWebApp/Services/ResourceCacheService.cs`
