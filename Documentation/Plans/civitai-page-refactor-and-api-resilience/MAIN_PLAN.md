# CivitAI Page Refactor & API Resilience - Implementation Plan

## Status

**Current Phase:** Phase 4 complete - API fetch and download fail-safes implemented and tested

This plan follows `Documentation/Plans/IMPLEMENTATION_GUIDE.md` and supersedes no existing plan. It builds on the completed `Documentation/Plans/civitai-modals-redesign/MAIN_PLAN.md` work, but tracks a new pass over the CivitAI page: in-page model detail browsing, CivitAI image metadata/send-to, optional resource image downloads, and API/download resilience.

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to the next step until testing is complete.
   - User must explicitly approve before updating the phase document.
   - Build runs only after user requests or after completing all file edits for a step.

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked / needs discussion

### Complexity Estimation (Fibonacci Points)

- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic

### Repository Rules

- UI work must follow `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.
- Razor and component CSS changes must follow `.github/instructions/design-language.instructions.md` and `.github/instructions/tokens.instructions.md`.
- CivitAI resource/browser work must reuse `CivitaiService`, `AssetViewer`, `IImageSendToService`, and existing resource save/download services instead of embedding new API/download logic in components.
- All cross-component notifications must use `EventService.cs` pub/sub.
- Use `.send-to-btn` for flat utility actions; keep one filled primary action per panel when needed.
- Avoid new persistence unless proven necessary. No EF migration is expected in this plan.

---

## Problem Statement

The current CivitAI page is better than the legacy modal flow, but three areas still feel brittle or awkward in daily use.

### 1. Model Detail Browsing Ergonomics

- The current model detail experience is too information-dense for a modal. CivitAI models often include many versions, many images, long descriptions, hashes, files, trigger words, and download actions.
- The detail experience should move into the CivitAI Models tab itself, using a two-column page layout for maximum screen estate.
- Version selection is clearer as scrollable tabs than as a dropdown/pill hybrid, especially when comparing versions of one model.
- The narrower metadata/download section belongs in the sidebar, while images, descriptions, and version notes should own the main content area.
- The detail view needs a back button that returns to search results without losing filters, loaded pages, or scroll position.

### 2. CivitAI Image Metadata And Send-To

- CivitAI image previews correctly use the global `AssetViewer` style, but generation metadata is not reliably read into `AssetInfoPanel`.
- The most important missing fields are prompt, negative prompt, seed, sampler, scheduler, CFG, steps, dimensions, and resources/model names when available.
- The current external-image hint in `AssetInfoPanel` says: "Bookmark this image to enable workflow send...". That is Danbooru-specific language. CivitAI does not currently have an image library/bookmark flow, so the hint is misleading.
- CivitAI already has resource-image saving through model/version flows. We can likely leverage that same save-to-disk path to make remote CivitAI images usable for workflow image send and LLM Generate Prompt actions.

### 3. CivitAI API And Download Reliability

- CivitAI API responses are inconsistent across endpoints and change over time. Some endpoints used here were discovered through website requests rather than formal docs.
- Image fetching and enrichment can fail with missing image IDs, missing metadata, null wrappers, video items, unavailable image URLs, or endpoint errors.
- Some failures currently throw through UI flows or leave the app in a half-broken state instead of degrading to partial cards, unavailable media placeholders, or actionable snackbars.
- Resource downloads and preview image downloads should be fail-safe: one bad preview image must not abort a resource download, and metadata/download errors should be logged with enough context to debug later.
- Resource preview image downloading is currently enforced after a model/resource download. Users should be able to opt out of downloading CivitAI resource images from a context-aware `/civitai` toolbar toggle.

---

## Current Code Observations

### Model Detail Flow

- `CivitAI.razor` renders a thin `TabbedPageShell` with Models, Images, and Creators tabs.
- `CivitaiModelsPanel.razor` owns model search state, currently renders a `TwoColumnLayout`, and opens `CivitaiModelInfoDialog` through `IDialogService`.
- Search filter state is already persisted in `State.State.Civitai.Models`, but the loaded `_models` collection and scroll position are component-local and not explicitly restored when drilling into a model.
- `CivitaiModelInfoDialog.razor` currently uses a two-column grid: `minmax(0, 5fr)` hero and `minmax(0, 7fr)` spec.
- `CivitaiModelHero.razor` hosts a large 4:5 preview and a `CivitaiPreviewStrip` of 64px thumbnails.
- `CivitaiVersionSelector.razor` renders pills for up to 5 versions, then falls back to `MudSelect`.
- `CivitaiModelSpecCard.razor` owns download, save-all-images, hashes, trigger words, tags, and metadata.
- `CivitaiModelSpecCard.SaveImage(...)` already downloads an image, writes JSON metadata, and creates a `ResourceImage` row.
- `TopToolbar.razor` already renders context-aware CivitAI controls through `ToolbarCivitaiSettings` when `CurrentPage == "/civitai"`.

### AssetViewer And Metadata

- `CivitaiImagesPanel.razor` and `CivitaiModelHero.razor` project CivitAI DTOs into transient `ImageEntity` objects through `CivitaiAssetAdapter.Project(...)`.
- `CivitaiAssetAdapter.Project(...)` only sees populated metadata when `dto.Meta` is populated.
- `AssetInfoPanel.razor` hides image-send and LLM image-to-prompt for remote URLs because `IImageSendToService.IsLocal(...)` returns false.
- `AssetInfoPanel.razor` always uses the Danbooru-oriented external hint text when an image is remote.

### API Service

- `CivitaiService.GetImages(...)` deserializes `CivitaiImagesDto`, then parses `image.MetaObject` by calling `new CivitaiImageMetaDto(image.MetaObject)`.
- `CivitaiImageMetaDto(JsonElement meta)` expects keys like `seed`, `prompt`, `sampler`, `cfgScale`, and `negativePrompt` directly under `meta`.
- Live probes showed `v1/images` currently returns `meta` as a wrapper: `{"id": <imageId>, "meta": {...}}` or `{"id": <imageId>, "meta": null}`. This explains why prompts and generation fields are missed.
- `v1/models/{id}` returns model-version image stubs with fields such as `url`, `nsfwLevel`, `width`, `height`, `hash`, `type`, `hasMeta`, and `hasPositivePrompt`; full generation metadata must come from image enrichment.
- `CivitaiService.GetImageByModelVersionId(...)` walks pages by cursor until it finds a matching image id. It assumes metadata and image lists are present, and it can add null image collections.
- `CivitaiService.GetImageType(...)` creates a new raw `HttpClient`, range-requests bytes, logs failures, and returns null. It is only used to infer video/image behavior.
- `DownloadResource(...)` attempts to download preview images in a loop after resource download setup, but assumes `version.Images[index]` is safe while evaluating the loop body and does not expose a user preference to skip this step.

---

## Proposed Solution

Refactor in four execution phases, ordered from investigation to send-to support to page-flow polish to resilience.

1. **API contract probe and DTO hardening**: build a small repeatable live-probe harness, capture current response shapes, and make DTO parsing tolerate wrapper/null/different-key metadata.
2. **CivitAI image send-to bridge**: make remote CivitAI images actionable by saving them as `ResourceImage` records, then enabling send-to actions from the local saved copy.
3. **In-page model detail view**: replace the modal with a Models-tab view mode that switches between search results and model detail while preserving results, loaded pages, and scroll position.
4. **Robust download and fetch fail-safes**: wrap critical API, image enrichment, image type, resource preview download, and optional resource-image download flows so failures degrade without crashing.

This plan intentionally does not rely on CivitAI docs as the final truth. Docs can seed tests, but live endpoint probes must define the schema we code against.

---

## Key Decisions

| Decision                                                         | Rationale                                                                                                                                                                                            |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Create a new plan instead of extending `civitai-modals-redesign` | The previous plan is complete historical work; this pass includes API resilience and send-to behavior beyond modal design.                                                                           |
| Treat live CivitAI probes as an execution artifact               | Current API shape matters more than formal documentation because some endpoints were discovered from website traffic.                                                                                |
| Keep CivitAI metadata parsing tolerant and lossy-safe            | Missing prompt should mean "not available", not a thrown exception or broken viewer.                                                                                                                 |
| Replace the model modal with an in-page model detail mode        | CivitAI model detail is information-dense and needs more screen estate than a modal can comfortably provide.                                                                                         |
| Version selection should use MudBlazor scrollable tabs           | The user explicitly prefers tabs, and MudBlazor already supports scrollable tab behavior for large version counts.                                                                                   |
| Save CivitAI viewer images as `ResourceImage` records            | This mirrors the Danbooru library pattern closely enough for send-to workflows and lets saved CivitAI images appear in Random panel/resource-image flows even when not linked to a tracked resource. |
| Reuse `AssetViewer` and `AssetInfoPanel`                         | They are the documented global media surface; extend with source-aware hooks rather than creating a CivitAI-specific viewer.                                                                         |
| Reuse or extract existing resource-image save logic              | `CivitaiModelSpecCard.SaveImage(...)` already downloads image plus metadata and creates `ResourceImage`; duplicating that flow in a viewer would drift quickly.                                      |
| Add a `/civitai` toolbar toggle for resource image downloads     | Resource image downloading is currently enforced after resource downloads; users should be able to skip that optional network/file work.                                                             |
| Failures become typed/logged outcomes where practical            | UI should distinguish not found, unavailable, rate/HTTP failure, parse failure, and download failure when that helps the user or logs.                                                               |

---

## Model Detail Navigation Strategy

The preferred approach is a single `/civitai` route and a Models-tab internal view mode:

- **Search Results view**: existing filters/sidebar plus model cards, paging, and load-more results.
- **Model Detail view**: same Models tab, but content switches to a detail layout for the selected model.
- **Back button**: returns to Search Results view and restores the previous scroll/anchor.

This avoids creating a second route that would naturally recreate the CivitAI page and lose component-local result lists. It also keeps the active CivitAI tab and toolbar behavior stable.

Implementation notes:

- Keep loaded search results in `CivitaiModelsPanel` while the user is inside detail view. Do not re-run search on back unless filters changed.
- Before entering detail view, capture both the selected model id and the window scroll position through JS interop. Give each model card a stable DOM id such as `civitai-model-card-{model.Id}`.
- On back, render the search results first, then restore by anchor if the card still exists; otherwise fall back to the captured scroll Y.
- Persist lightweight navigation fields in `State.State.Civitai.Models` if useful for resilience: selected model id, view mode, last result scroll Y, and last selected version id. Do not persist the full `_models` DTO collection in app state.
- Browser refresh can restore filters/page and optionally selected model id, but exact loaded extra pages and scroll restoration are best-effort unless we introduce a heavier cache service later.
- Browser back/forward integration can be deferred. The first pass can use an explicit in-page Back button; a later pass can push a query string like `?modelId=...` if deep links become important.

---

## Implementation Phases

### Phase 1: Live API Probe And Metadata Parser Hardening

**Objective:** Establish the current CivitAI response shapes and fix generation metadata extraction at the service/DTO layer.

**Complexity:** 8 points

**Status:** [x] Complete

#### Steps

- [x] Step 1 (2 pts) - Create a repeatable CivitAI probe artifact under this plan folder (for example, `API_PROBE_NOTES.md`) that records sampled endpoints, response shapes, edge cases, and sanitized examples. Include `v1/models/{id}`, `v1/images?modelId=...`, `v1/images?imageId=...`, `v1/images?modelVersionId=...`, and cursor paging.
- [x] Step 2 (3 pts) - Refactor `CivitaiImageMetaDto` parsing so it handles both legacy flat metadata and current wrapper metadata: `meta.meta`, `meta.id`, `meta: null`, missing keys, numeric/string variants, and current fields such as `scheduler`, `models`, `vaes`, `comfy`, and resource arrays.
- [x] Step 3 (1 pt) - Update `CivitaiAssetAdapter.Project(...)` to map newly recovered fields, especially scheduler vs sampler, prompt, negative prompt, dimensions, seed, steps, CFG, denoise, and model display name where the receiving `ImageEntity` supports it.
- [x] Step 4 (2 pts) - Add focused tests for representative metadata shapes. If existing test infrastructure makes DTO tests noisy, add narrow parser/unit tests and document any pre-existing test blockers.

#### Success Criteria

- CivitAI images with `meta.meta.prompt` populate `AssetInfoPanel` prompt blocks.
- Images with `meta.meta == null` or missing fields still render and open in `AssetViewer` without exceptions.
- Parser tests cover wrapper metadata, flat metadata, null metadata, numeric/string denoise, and resource arrays.

---

### Phase 2: CivitAI-Aware Image Save And Send-To Flow

**Objective:** Make CivitAI remote images useful from `AssetInfoPanel`: save individual images as `ResourceImage` records, then enable workflow image-send and LLM Generate Prompt using the saved local file.

**Complexity:** 8 points

**Status:** [x] Complete and tested

#### Steps

- [x] Step 1 (2 pts) - Extract the save-one-resource-image logic from `CivitaiModelSpecCard.SaveImage(...)` into a reusable service or helper owned by the CivitAI resource feature. Preserve metadata JSON writing and `ResourceImage` creation behavior, and allow saved CivitAI images to exist without a tracked resource link when the source image comes from browsing.
- [x] Step 2 (2 pts) - Extend `AssetViewer`/`AssetInfoPanel` with source-aware external-action parameters, such as custom hint text, source label, and an `OnRequestSaveLocal`/`OnResolveLocalAsset` callback. Defaults must preserve Gallery and Danbooru behavior.
- [x] Step 3 (2 pts) - Wire CivitAI image viewers to use a two-step hint/action pattern: remote CivitAI image first shows a save/download-to-send hint, then after save the active transient asset path updates to the local saved path and workflow image-send plus Generate Prompt buttons become available in place.
- [x] Step 4 (1 pt) - Add CivitAI-specific source labels for LLM Image-to-Prompt handoff so the prompt tool receives a meaningful source label instead of the generic "Asset Info".
- [x] Step 5 (1 pt) - Verify parameter send remains available even when the image itself is remote, and image-send/LLM-send only unlock after a local file exists.

#### Success Criteria

- CivitAI remote images no longer display Danbooru bookmark language.
- A CivitAI image with metadata can send parameters to compatible workflows without saving locally.
- A CivitAI image can be saved locally from the viewer as a `ResourceImage`, then sent as an image to workflows and LLM Generate Prompt.
- Saved standalone CivitAI `ResourceImage` records are available to existing resource-image consumers where supported, including Random panel/result generation flows.
- Danbooru external-image behavior remains unchanged.

---

### Phase 3: In-Page Model Detail View

**Objective:** Replace the CivitAI model modal with an in-page Models-tab detail view: narrower metadata/download sidebar, scrollable version tabs, richer image/description content area, and reliable return to search results.

**Complexity:** 13 points

**Status:** [x] Complete and tested

#### Steps

- [x] Step 1 (2 pts) - Introduce a Models-tab view mode in `CivitaiModelsPanel`: search results vs selected model detail. Remove `IDialogService` usage for model details once the page view is ready.
- [x] Step 2 (2 pts) - Add search-result preservation and back behavior. Keep `_models` in memory while viewing a model, capture selected card id and scroll position before entering detail, and restore anchor/scroll after returning.
- [x] Step 3 (2 pts) - Build or extract a `CivitaiModelDetailPanel` from the current dialog parts. Use `TwoColumnLayout` with metadata/download actions in the sidebar and images/description/version content in the main area.
- [x] Step 4 (2 pts) - Replace `CivitaiVersionSelector` with MudBlazor scrollable version tabs. Keep version status affordances from `CivitaiVersionStatusHelper` and preserve selected version id while navigating.
- [x] Step 5 (2 pts) - Replace the hard-to-navigate thumbnail strip with a higher-signal image area in the content column: larger thumbnail grid, previous/next controls, or image count plus direct `AssetViewer` open flow. Choose after testing models with many images.
- [x] Step 6 (1 pt) - Move descriptions and version notes into the content column with roomier scroll behavior, avoiding cramped expansion panels where long descriptions dominate.
- [x] Step 7 (1 pt) - Add visual and interaction checks at desktop and mobile widths for long version names, sparse metadata, many images, long descriptions, no-image versions, back navigation, and scroll restoration.
- [x] Step 8 (1 pt) - Decide whether `CivitaiModelInfoDialog` should be deleted, left as an unused fallback, or retained temporarily until regression confidence is high.

#### Success Criteria

- Opening a model from search results changes the Models tab into an in-page detail view instead of a modal.
- The detail view has a clear Back button that returns to the prior search results without re-searching.
- Returning from detail restores scroll position or selected-card anchor closely enough that the user does not lose their place.
- Version switching uses scrollable tabs and remains usable for many-version models.
- Metadata/download actions fit naturally in the sidebar, while images, descriptions, and notes use the wider content column.
- Existing download/save-all workflows still work from the detail view.

---

### Phase 4: API Fetch And Download Fail-Safes

**Objective:** Make CivitAI browsing, metadata enrichment, image type detection, image save, optional resource-image download, and resource download robust against partial API failures.

**Complexity:** 13 points

**Status:** [x] Complete and tested

#### Steps

- [x] Step 1 (3 pts) - Introduce small result/guard helpers in `CivitaiService` for HTTP responses, JSON parse failures, empty payloads, and null lists. Avoid throwing from normal missing/unavailable API outcomes.
- [x] Step 2 (2 pts) - Harden `GetImages`, `GetImageById`, and `GetImageByModelVersionId` against null `items`, null `metadata`, cursor loops, rate/HTTP failures, and item-level metadata parse failures. Log item-level failures and keep the remaining images.
- [x] Step 3 (2 pts) - Replace `GetImageType` behavior with safer media detection. Prefer DTO `type` when available, URL extension as fallback, and range probes only when needed with timeout/retry and shared `HttpClient` handling.
- [x] Step 4 (2 pts) - Harden model-version image enrichment. Do not assume image IDs can always be parsed from URLs; if enrichment is impossible, keep stub image data and display "metadata unavailable" rather than hiding the image.
- [x] Step 5 (2 pts) - Add a context-aware `/civitai` toolbar toggle in `ToolbarCivitaiSettings` for downloading resource images after resource downloads. Store it in settings because the preference controls future network/file work and should persist across sessions; default preserves current behavior unless user explicitly disables it.
- [x] Step 6 (2 pts) - Harden preview/resource image downloads. If the toggle is disabled, skip resource image download entirely. If enabled, a failed preview should not abort the model file download; failed individual images in Save All should continue with a final partial-success snackbar.
- [x] Step 7 (2 pts) - Add focused tests or service-level fakes for null responses, wrapper metadata, failed image download, cursor exhaustion, optional resource-image download disabled, and partial save-all results.

#### Success Criteria

- CivitAI image browsing does not crash when some items have null metadata, missing images, videos, or failed media probes.
- Model dialogs open even if some preview images cannot be enriched.
- Resource downloads can succeed even if preview image download fails.
- Users can disable CivitAI resource image downloading from the `/civitai` toolbar and still download the model/resource file.
- Save All reports partial success/failure instead of stopping on the first bad image.
- Logs contain endpoint, model/version/image id where available, and failure category.

---

## Stress Points & Risks

| Risk                                                                                                   | Mitigation                                                                                                                                                               | Complexity |
| ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------- |
| CivitAI changes response shape again after parser work                                                 | Keep live probe notes as an execution artifact; parser should be tolerant and tests should cover wrapper and flat shapes.                                                | 3          |
| Current API examples include sensitive or NSFW prompt content                                          | Store sanitized shape-focused examples only; never commit raw explicit prompts into docs/tests.                                                                          | 2          |
| Version tabs become unwieldy for models with many versions                                             | Use scrollable tabs or hybrid overflow after testing high-version models; keep a single component boundary for tuning.                                                   | 2          |
| Search-result scroll restoration is fragile when the DOM is destroyed                                  | Capture both card anchor id and window scroll Y; restore anchor first after render and fall back to scroll Y. Keep loaded results in memory while detail view is active. | 3          |
| Extracting save logic from `CivitaiModelSpecCard` could accidentally change existing download behavior | Extract behind a method with identical call sites first, then add viewer use. Validate modal download/save-all after extraction.                                         | 3          |
| Unlocking image-send from remote CivitAI images may require async local download before navigation     | Make save explicit or show progress before enabling send; do not silently navigate with an unavailable file.                                                             | 3          |
| `AssetInfoPanel` source-specific copy could regress Danbooru                                           | Add parameters with defaults matching current copy and wire CivitAI explicitly.                                                                                          | 2          |
| Optional resource image download toggle could diverge from Save All behavior                           | Scope the toggle to automatic post-resource-download images only; keep explicit Save All as a user-commanded action.                                                     | 2          |
| Service hardening may hide serious parse errors                                                        | Log structured warnings for dropped fields/items; only swallow expected remote-data failures.                                                                            | 2          |
| Full build/test suite has known unrelated noise                                                        | Use targeted tests and file-level errors first; mention any full-build blockers separately.                                                                              | 1          |

---

## Open Clarifications

- Resolved: the automatic resource-image download toggle is persisted in `Settings.Settings.Resources.Civitai` because it controls future network/file work and should survive page reloads/sessions.
- Should the CivitAI image viewer expose the first-step "save/download to send" action in the main viewer controls, the info panel, or both?
- Should the in-page detail view update the browser URL/query string for deep-linking to a selected model, or should that wait until the internal view mode feels solid?

---

## Validation Strategy

- Run live API probes during Phase 1 and record sanitized response-shape findings in plan artifacts.
- Add focused parser/service tests for DTO and CivitAI service behavior.
- Use targeted validation for touched files because this repo has known unrelated build/test noise.
- For UI phases, run the app and smoke test:
  - CivitAI Models: search, scroll, open model detail in page, switch versions, view many-image versions, back to results, confirm scroll restoration, download resource, toggle automatic resource image downloads, save all images.
  - CivitAI Images: search by model id, open viewer, inspect metadata, send parameters, save image, send image to workflow, Generate Prompt.
  - Regression: Danbooru external-image bookmark hint, Gallery local images, and existing `AssetViewer` controls.

---

## References

- `Documentation/Plans/IMPLEMENTATION_GUIDE.md` - planning and execution contract.
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - UI shell, AssetViewer, send-to, modal, and token conventions.
- `Documentation/Plans/civitai-modals-redesign/MAIN_PLAN.md` - completed predecessor plan for current CivitAI modal architecture.
- `BlazorWebApp/Components/Resources/CivitaiModelInfoDialog.razor` - model dialog host.
- `BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor` - current model search/results host and future in-page model detail state owner.
- `BlazorWebApp/Components/Resources/CivitaiModelHero.razor` - hero image and model-image viewer host.
- `BlazorWebApp/Components/Resources/CivitaiPreviewStrip.razor` - current thumbnail strip.
- `BlazorWebApp/Components/Resources/CivitaiModelSpecCard.razor` - download and save image logic.
- `BlazorWebApp/Components/Resources/CivitaiImagesPanel.razor` - CivitAI image search and viewer host.
- `BlazorWebApp/Components/Resources/CivitaiAssetAdapter.cs` - DTO-to-AssetViewer projection.
- `BlazorWebApp/Components/Shared/AssetViewer.razor` - canonical app media viewer.
- `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - metadata and send-to surface.
- `BlazorWebApp/Services/CivitaiService.cs` - CivitAI API and download service.
- `BlazorWebApp/Components/Shared/Toolbar/ToolbarCivitaiSettings.razor` - context-aware `/civitai` toolbar settings.
- `BlazorWebApp/Models/AppState.cs` and `BlazorWebApp/Models/AppSettings.cs` - candidate homes for view state and persistent resource-image download preference.
- `BlazorWebApp/Data/Dtos/CivitaiImageDto.cs` and `BlazorWebApp/Data/Dtos/CivitaiImageMeta.cs` - current CivitAI image DTOs.

---

## Changelog

| Phase    | Changes                                                                                                                                                                                                                                                                                                                                   |
| -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Planning | Initial plan created from user request; inspected current CivitAI modal/viewer/service code; live-probed current CivitAI model/images API shape and identified wrapped `meta.meta` as the likely metadata-loss root cause.                                                                                                                |
| Planning | Updated plan after user pivot: model detail moves from modal to in-page Models-tab view; version tabs are scrollable; CivitAI viewer saves use `ResourceImage`; send-to is a two-step save-then-send flow; automatic resource image downloads get a `/civitai` toolbar toggle; back navigation must preserve results and scroll position. |
| Phase 1  | Added repeatable CivitAI API probe notes, hardened metadata parsing for wrapper/flat/null metadata, mapped recovered scheduler/dimensions into `CivitaiAssetAdapter`, and validated focused parser/adapter tests.                                                                                                                         |
| Phase 2  | Added a reusable CivitAI resource-image save service, wired CivitAI viewers to save remote images locally before image-send/LLM send, preserved Danbooru defaults in shared viewer components, and validated with build plus focused metadata tests.                                                                                      |
| Phase 3  | Replaced the model detail modal entry path with an in-page Models-tab detail view, preserved search results and Back restoration, added a `CivitaiModelDetailPanel`, moved versions to scrollable tabs, upgraded image navigation to a thumbnail grid with previous/next controls, and validated with build plus live browser smoke.       |
| Phase 4  | Hardened CivitAI image fetch/enrichment/media detection and resource download paths, added the persistent automatic resource-image download toggle, made preview downloads and Save All tolerate per-image failures, switched model/resource downloads to the injected client with streamed progress, and validated with focused tests plus build. |

---

## Approval Gate

Implementation must not begin until the user approves this plan or requests revisions. Phase documents (`PHASE_1.md`, etc.) should be created when entering each phase.
