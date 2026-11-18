# HuggingFace Resource Download - Implementation Plan

## Status

**Current Phase:** Planning

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do not proceed to the next step until testing is complete.
   - User must explicitly approve before moving to the next phase.
   - Build runs only after user requests or after completing all file edits for a step.

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation

- **1**: Trivial
- **2**: Simple
- **3**: Moderate
- **5**: Medium
- **8**: Complex
- **13**: Very complex
- **21+**: Epic, should be split

### Repository Conventions

- Use the existing resource integration pattern: remote API service, DTOs, focused UI components, `IEventService` pub/sub, and resource cache invalidation through `ResourcesChangedEventArgs` / `DownloadCompletedEventArgs`.
- Use `TabbedPageShell` for tabbed pages and the documented `TwoColumnLayout`, `TopbarLayout`, or `ContentOnlyLayout` variants.
- Use `Variant.Text` for standard form controls and tokenized spacing/surfaces from the design language.
- Hand-authored EF migrations must include `[DbContext(typeof(AppDbContext))]` and `[Migration("<timestamp>_<name>")]`, and must update `AppDbContextModelSnapshot.cs`.
- Validate with focused service/component tests before falling back to a full project build.

---

## Problem Statement

The app currently has CivitAI discovery and download support, but HuggingFace is a major model host with a very different model-page and repository structure. HuggingFace repositories are file trees rather than model/version/file records, so we need a new service and UX that can search HF, inspect repository metadata, let users select files and folders, and persist the downloaded resources with correct local `Resource` mapping.

The key challenge is not basic downloading; it is mapping unstructured or semi-structured HuggingFace metadata into the app's local resource system without making unsafe assumptions about type, subtype, base model, preview image, or destination path.

---

## Defined Goals

These goals are already agreed and must shape every implementation phase:

1. **HuggingFace search should feel similar to the main HF website.**
   - Provide free-text search.
   - Support HF-like filters such as pipeline/task, library, tags, author/namespace, and sort where API support allows.
   - Prefer website-like discovery over a narrow CivitAI clone.

2. **File and folder selection is a core feature.**
   - HuggingFace repositories can contain nested folders and many companion files.
   - The UI must expose a browsable tree or folder-aware list.
   - Users must be able to select individual files and, where useful, whole folders.

3. **Download destination must support folder preservation.**
   - Add a `Keep folder structure` toggle.
   - When enabled, selected HF paths are written under the selected resource destination while preserving their repository-relative paths.
   - When disabled, selected files are downloaded flat into the selected resource path.
   - Flattened downloads must handle duplicate filenames from different folders deliberately, not accidentally overwrite them.

4. **Manual resource mapping is the preferred path.**
   - Type and subtype can be inferred as a quality-of-life hint, but the user must confirm or set them.
   - Models cannot be downloaded into local resources with empty Type/Subtype.
   - Base model can be inferred as a hint, but should be treated as user-confirmed ground truth because it drives resource organization and downstream behavior.

5. **Preview images should be useful but modest.**
   - Parse the model card README and use the first image as the downloaded resource preview when available.
   - Use a HuggingFace logo or source placeholder for resources without a parseable preview.
   - Do not try to recreate CivitAI's gallery behavior from unstructured README content in the first implementation.

6. **Persistence should make HuggingFace provenance explicit.**
   - Add a migration rather than overloading CivitAI columns.
   - Add HuggingFace-specific fields to `Resource` so we can identify source, repo, revision, and file path later.
   - Use those fields to support future display or source-specific logic without introducing a generic source enum prematurely.

---

## HuggingFace API Surface

Official Hub API base URL: `https://huggingface.co`.

Relevant endpoints:

| Need                      | Endpoint                                                   | Notes                                                                                                                                                    |
| ------------------------- | ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Website-like quick search | `GET/POST /api/quicksearch`                                | Supports model/dataset/space search, `q`, type/repo filters, pipeline filters, namespace, and exclusions. Best candidate for HF-like search affordances. |
| Model browse / metadata   | `GET /api/models` and `GET /api/models/{namespace}/{repo}` | Use for richer model results, tags, downloads, likes, pipeline tag, card data, and siblings where returned.                                              |
| Repository tree           | `GET /api/models/{namespace}/{repo}/tree/{rev}/{path}`     | Supports folders, recursive listing, pagination, and optional expanded metadata. Core endpoint for file/folder UX.                                       |
| Path metadata             | `POST /api/models/{namespace}/{repo}/paths-info/{rev}`     | Useful for selected path validation, sizes, and scanner metadata before download.                                                                        |
| Security status           | `GET /api/models/{namespace}/{repo}/scan`                  | Optional but useful for warning states around files.                                                                                                     |
| File download             | `GET /{namespace}/{repo}/resolve/{rev}/{path}`             | Requires following redirects. Supports byte ranges. Works for public files and token-authorized gated/private files.                                     |
| Auth check                | `GET /api/whoami-v2`                                       | Optional token validation for `HuggingFaceApiToken`.                                                                                                     |

Authentication:

- Public repositories can be searched and downloaded without a token.
- Gated/private repositories require a HuggingFace token.
- Add `HuggingFaceApiToken` configuration and send `Authorization: Bearer <token>` when present.

---

## Proposed Solution

Add a dedicated HuggingFace integration beside CivitAI rather than forcing HF into CivitAI-specific concepts.

Core pieces:

- `HuggingFaceService` as the HTTP API/download orchestration service.
- DTOs for search results, model details, tree entries, path info, and download requests.
- `HuggingFaceModelMapper` / helper logic to infer Type/SubType/BaseModel hints without treating them as authoritative.
- `HuggingFaceModelsPanel` and related components for search, result cards, model detail, repository file tree, mapping controls, and selected-file download actions.
- EF migration adding HuggingFace provenance fields to `Resource`.
- Tests around mapping, path safety, duplicate handling, and service URL construction.

### Key Decisions

| Decision                                                   | Rationale                                                                            |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Create a separate `HuggingFaceService`                     | HF API shape is repository/tree-based and should not bloat `CivitaiService`.         |
| Use HF repo id as source identity                          | `{namespace}/{repo}` is HF's durable model identifier.                               |
| Add migration with HF-specific columns                     | Keeps CivitAI data intact and makes source detection explicit.                       |
| Preserve folder structure as a first-class download option | HF repositories often need nested files, configs, encoders, or quantization folders. |
| Require confirmed Type/SubType before download             | Prevents files landing in undefined local resource folders.                          |
| Treat BaseModel as user-confirmed ground truth             | Tags can suggest Flux/SDXL/etc., but user selection should drive final persistence.  |
| Parse only the first README image initially                | Gives local preview value without creating fragile gallery scraping.                 |
| Reuse resource events and cache behavior                   | Keeps local library refresh behavior consistent with CivitAI downloads.              |

---

## Resource Mapping Contract

### HuggingFace Metadata To Local Resource

| HuggingFace field                      | Local target                                                         | Confidence                                      |
| -------------------------------------- | -------------------------------------------------------------------- | ----------------------------------------------- |
| `id` / `{namespace}/{repo}`            | `HuggingFaceModelId`                                                 | High                                            |
| selected revision / branch             | `HuggingFaceRevision`                                                | High                                            |
| selected file path                     | `HuggingFaceFilePath`                                                | High                                            |
| filename from selected path            | `Filename`                                                           | High, but flattening can require disambiguation |
| `author` / namespace                   | `Author`                                                             | Medium                                          |
| `modelId` / repo name                  | `Title` default                                                      | Medium                                          |
| `tags[]`                               | `Tags`                                                               | High as raw tags, low as semantic mapping       |
| `pipeline_tag` / tags / file extension | Type/SubType hint only                                               | Low to medium                                   |
| README/card data                       | `Description`, preview candidate                                     | Medium                                          |
| `lastModified`                         | display metadata only, not `CreatedDate` replacement unless approved | Medium                                          |
| file size                              | `SizeKb`                                                             | High when returned by tree/path metadata        |
| README image                           | local preview                                                        | Medium                                          |

### Proposed `Resource` Additions

Add nullable fields:

- `string? HuggingFaceModelId`
- `string? HuggingFaceRevision`
- `string? HuggingFaceFilePath`
- `string? HuggingFaceSha`

Potential later fields, not required for first implementation:

- `string? HuggingFaceCommitSha`
- `string? HuggingFaceLicense`
- `bool? HuggingFaceGated`

Uniqueness policy to decide during implementation:

- Existing `Filename` unique index remains a constraint today.
- For HF folder support, flat downloads may collide by filename even when source paths differ.
- Phase 2 must decide whether to keep `Filename` unique and rename flattened collisions, or migrate uniqueness to a source/path-aware rule. Conservative first pass: keep existing uniqueness and make conflicts visible in the UI.

---

## UX Shape

### Search/Browse Page

Add a new HuggingFace browsing surface. Preferred shape:

- Route: `/huggingface`
- Nav label: `HuggingFace` or `HF`
- Layout: `TabbedPageShell` with an initial `Models` tab.
- Panel layout: `TwoColumnLayout Collapsible="true"` like CivitAI, because filters are useful but results need width.

Initial filters:

- Search text
- Pipeline/task, defaulting toward image-generation-relevant options but not hiding everything else
- Library, especially `diffusers`, `safetensors`, `transformers`, `gguf`-related options where API support allows
- Author/namespace
- Sort (`downloads`, `likes`, `lastModified` or HF-supported equivalents)
- Limit/page or cursor controls

### Model Detail / Download Panel

After selecting a result:

- Show title, namespace, tags, pipeline, downloads/likes if available, and source link.
- Show README description in a collapsed long-content area.
- Show first README image or HF placeholder.
- Show repository tree with folder rows, file rows, extension/type badges, size, selected state, and warnings.
- Provide quick filters for file extensions: `.safetensors`, `.gguf`, `.bin`, `.pt`, `.ckpt`, `.json`, `.yaml`, `.txt`.
- Provide selection actions: select file, select folder, clear selection, select likely model files.
- Provide mapping controls: Type, SubType, BaseModel, destination preview, and `Keep folder structure`.
- Disable download until required mapping is valid and at least one downloadable file is selected.

### Folder Structure Behavior

Destination root:
`{ResourcesPath}/{Type}/{SubType}` unless subtype is explicitly `none`, matching CivitAI conventions.

When `Keep folder structure = true`:

- Source `unet/diffusion_pytorch_model.safetensors` downloads to `{root}/unet/diffusion_pytorch_model.safetensors`.
- DB `Filename` should reflect the local relative path or a safe display filename decision made in Phase 2.
- DB `HuggingFaceFilePath` always stores the original repo-relative path.

When `Keep folder structure = false`:

- Source `unet/model.safetensors` downloads to `{root}/model.safetensors`.
- If multiple selected files would resolve to the same filename, the UI must block and ask the user to rename/deselect, or auto-disambiguate with a clear suffix policy.
- No overwrites without explicit confirmation.

---

## Implementation Phases

### Phase 1: API Spike And DTO Foundation

**Objective:** Establish the HF API client, request/response DTOs, auth behavior, and focused tests without UI.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add `HuggingFaceService` with `HttpClient` registration and optional bearer token configuration.
- [ ] Add DTOs for quick search, model details, tree entries, path info, and file download metadata.
- [ ] Implement methods for quick search, model detail, recursive tree listing, path info, README fetch, and resolve URL construction.
- [ ] Add service tests for URL construction, token header behavior, pagination handling, and invalid repo IDs.

#### Success Criteria

- HF search and model metadata can be fetched through a typed service.
- Repository tree entries preserve nested paths.
- Downloads are not implemented yet, but resolve URLs can be constructed safely.

---

### Phase 2: Persistence And Resource Mapping

**Objective:** Add clean HF provenance to local resources and define mapping rules for Type/SubType/BaseModel, file paths, and collisions.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add nullable HF fields to `Resource`.
- [ ] Add migration and update `AppDbContextModelSnapshot.cs`.
- [ ] Add mapper/helper logic that produces suggested Type/SubType/BaseModel from tags, pipeline, repo name, and selected file path.
- [ ] Ensure final `Resource` creation requires confirmed Type/SubType and captures user-confirmed BaseModel.
- [ ] Decide and implement filename/path collision policy compatible with the existing `Filename` unique index.
- [ ] Add database/service tests for creating HF-backed resources and duplicate detection.

#### Success Criteria

- Existing CivitAI resources remain compatible.
- HF resources can be identified by nullable HF columns.
- Empty Type/SubType cannot reach persistence.
- Path collisions are predictable and visible.

---

### Phase 3: Download Engine With Folder Support

**Objective:** Implement robust multi-file downloads from HF with progress, folder preservation, previews, and event publishing.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add `HuggingFaceDownloadRequest` containing repo id, revision, selected files, mapping, destination mode, and preview preference.
- [ ] Implement streaming downloads with `HttpCompletionOption.ResponseHeadersRead`, redirect support, progress updates, and cancellation-safe file writes.
- [ ] Implement `Keep folder structure` path resolution with traversal protection and Windows-safe path handling.
- [ ] Implement flat download collision detection before any file write.
- [ ] Create resource DB records for selected resource files while avoiding DB rows for non-resource companion files unless explicitly selected as resources.
- [ ] Parse README markdown for the first image and save a preview; fallback to an HF placeholder/logo.
- [ ] Publish `ResourcesChangedEventArgs` and `DownloadCompletedEventArgs` on successful resource downloads.

#### Success Criteria

- Multi-file HF downloads work for public repos.
- Folder-preserving and flat modes both produce predictable local paths.
- No selected file can escape the intended resource root.
- Local resource list refreshes after download.

---

### Phase 4: HuggingFace Search UI

**Objective:** Build the HF discovery page with website-like search, filters, result cards, and preserved panel state.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add `/huggingface` page using `TabbedPageShell` and `TwoColumnLayout`.
- [ ] Add `HuggingFaceModelsPanel` with search filters and result cards.
- [ ] Add app state for HF active tab, sidebar collapse, card size, search filters, and selected result restoration.
- [ ] Add nav entry.
- [ ] Reuse existing card/grid visual patterns and avoid new top-level layout variants.

#### Success Criteria

- Users can search HF from inside the app with a familiar HF-like filter set.
- Search results are inspectable and visually consistent with CivitAI/resource pages.
- Page state survives normal navigation patterns.

---

### Phase 5: Repository File Tree And Mapping UI

**Objective:** Build the model detail/download UX where users select files/folders, confirm mapping, preview destination paths, and start downloads.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add model detail panel with metadata, source link, README summary, and preview image.
- [ ] Add repository tree/list component with folder rows, file rows, extension filters, selection states, and file-size display.
- [ ] Add `Keep folder structure` toggle and destination preview.
- [ ] Add manual Type/SubType/BaseModel controls with inferred suggestions applied only as hints.
- [ ] Add validation messages for missing mapping, gated/private access, duplicate filenames, path conflicts, and unsupported files.
- [ ] Wire download action to `HuggingFaceService`.

#### Success Criteria

- Users can select files and folders confidently before downloading.
- Download button is disabled until required mapping and selected files are valid.
- Destination preview reflects flat vs preserved folder mode.
- Manual mapping is clearly the source of truth.

---

### Phase 6: Integration Polish And Validation

**Objective:** Finish source-aware display details, settings, tests, and documentation updates.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add settings for HF token, default limit, and default file-extension filters if needed.
- [ ] Add local resource detail source display for HF-backed resources.
- [ ] Add focused tests for mapper, path resolver, collision detection, and service methods.
- [ ] Run targeted validation: service tests first, then focused project build if needed.
- [ ] Update architecture/resource documentation only after behavior is implemented and approved.

#### Success Criteria

- HF-backed resources are distinguishable in local resource details.
- Tests cover the risky parts: mapping, paths, duplicates, and service calls.
- Documentation reflects final behavior.

---

## Stress Points & Risks

| Risk                                                                 | Mitigation                                                                                         | Complexity |
| -------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- | ---------- |
| HF metadata is less structured than CivitAI                          | Treat API-derived Type/SubType/BaseModel as suggestions only; require user confirmation            | 5          |
| Nested repository paths conflict with existing `Filename` uniqueness | Decide collision strategy before writing downloads; surface conflicts in UI                        | 8          |
| Folder download can create unsafe paths                              | Normalize repo-relative paths, reject rooted paths and `..`, test Windows separators               | 5          |
| Gated/private repos fail without token or accepted access            | Add optional token, clear status messages, and do not retry blindly                                | 3          |
| Large repos can have many files                                      | Use paginated tree loading and extension filters; avoid rendering huge trees without filtering     | 5          |
| README image parsing is fragile                                      | Parse first standard markdown/HTML image only; fallback to source placeholder                      | 3          |
| Multiple selected files may not all be resources                     | Separate resource files from companion files, and decide whether companion files create DB records | 5          |
| CivitAI assumptions leak into HF implementation                      | Keep DTOs, service, mapper, and UI separate; only reuse shared events/resource persistence         | 3          |

---

## Open Clarifications

These are not blockers for the plan, but they should be resolved before implementation reaches the relevant phase:

1. Should HuggingFace get a dedicated `/huggingface` nav item, or should it become another tab under a broader remote resources page later?
2. For multi-file downloads, should every selected model-like file create a `Resource` DB row, while companion files are file-only, or should all selected files become resources?
3. For flat downloads with duplicate filenames, do we prefer blocking the download until the user resolves collisions, or auto-renaming with source-folder suffixes?
4. Should BaseModel be strictly required for all HF resource downloads, or only for resource types where downstream UI/workflows rely on it?
5. Should the first implementation include gated-repo access diagnostics, or only token-authenticated download attempts with clear failures?

---

## Suggested First Execution Order

1. Phase 1 first because it proves API shape and prevents UI from being built on guesses.
2. Phase 2 before downloads because persistence and collision policy affect folder behavior.
3. Phase 3 before UI polish because the file path engine is the riskiest part.
4. Phases 4 and 5 can be developed together if desired, but should remain separate checkpoints.
5. Phase 6 last for settings, source display polish, and documentation.

---

## References

- [Documentation/Plans/IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md)
- [Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md)
- [Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md](../../Architecture/03-PERSISTENCE-AND-MIGRATIONS.md)
- [BlazorWebApp/Services/CivitaiService.cs](../../../BlazorWebApp/Services/CivitaiService.cs)
- [BlazorWebApp/Data/Entities/Resource.cs](../../../BlazorWebApp/Data/Entities/Resource.cs)
- [BlazorWebApp/Data/AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs)
- [BlazorWebApp/Pages/CivitAI.razor](../../../BlazorWebApp/Pages/CivitAI.razor)
- HuggingFace Hub API: `https://huggingface.co/.well-known/openapi.md`

---

## Changelog

| Phase    | Changes                                                                                                            |
| -------- | ------------------------------------------------------------------------------------------------------------------ |
| Planning | Initial plan created with explicit HF search, folder preservation, manual mapping, preview, and persistence goals. |
