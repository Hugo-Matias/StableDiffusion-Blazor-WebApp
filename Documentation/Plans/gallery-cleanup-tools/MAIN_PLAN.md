# Gallery Cleanup Tools - Implementation Plan

## Status

**Current Phase:** Phase 4 cleanup review UI checkpoint complete and validated

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow Per Step

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do not proceed to the next step until testing is complete.
   - User must explicitly approve before updating phase documents.
   - Build runs only after user requests or after completing all file edits for a step.

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked or needs discussion

### Complexity Estimation

- **1**: Trivial
- **2**: Simple
- **3**: Moderate
- **5**: Medium
- **8**: Complex
- **13**: Very complex
- **21+**: Epic and should be split

### Key Rules

- Each step should be a commitable checkpoint.
- Keep cleanup destructive actions review-first and confirmation-gated.
- Existing gallery selection state remains the shared selection mechanism.
- Cleanup scans must be resumable, cancellable, and safe to rerun.
- Generation saving should enqueue indexing work, not block image generation on expensive embedding inference.
- All cross-service notifications must use the existing pub/sub pattern implemented by `EventService.cs`.
- Persistence changes must follow `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`.
- UI work must follow `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.

---

## Problem Statement

The app can already select and batch delete gallery images, but it does not help users discover which images should be reviewed or cleaned. Large AI-generation libraries can accumulate many repeated prompts, small variants, failed outputs, old non-favorites, upscales, videos, orphaned records, and near-duplicate images. With around 113k image records, manual review through normal gallery paging is not practical.

The required feature is a cleanup system that discovers redundant or low-value candidates, presents them as reviewable groups, lets the user inspect differences, and feeds existing selection and deletion workflows without making irreversible decisions automatically.

---

## Proposed Solution

Build a staged cleanup system around a persistent cleanup index, deterministic grouping, visual embeddings, and a review-first UI.

The first implementation slices should avoid ONNX inference until the app has a reliable cleanup index and grouped review surface. Once those are in place, add ONNX Runtime image embeddings as an optional indexing provider and use the resulting vectors for visual similarity groups.

### Core Flow

1. Index image metadata in batches.
2. Compute cheap cleanup signals: file existence, file size, exact hash, perceptual hash, prompt fingerprint, prompt tokens, resource/model/workflow hints.
3. Generate cleanup group runs from one selected strategy.
4. Review groups at top level.
5. Expand a group to inspect individual images.
6. Select candidates into the existing `IGalleryService.SelectedImageIds` flow.
7. Save selected candidates as a `Selection` or delete them through existing confirmation dialogs.
8. Add ONNX embeddings for visual grouping once the review flow is stable.

---

## ONNX Runtime Requirements

### Required Pieces

| Area                     | Requirement                                                                           | Notes                                                                                                                                           |
| ------------------------ | ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Runtime package          | Provider-selected ONNX Runtime package                                                | The embedding service must isolate provider selection behind configuration so CPU/GPU/native package concerns do not leak into indexing logic.  |
| CPU provider             | `Microsoft.ML.OnnxRuntime`                                                            | Portable fallback and useful for tests, recovery, and machines without matching GPU runtime dependencies.                                       |
| CUDA GPU                 | `Microsoft.ML.OnnxRuntime.Gpu`                                                        | First-class target from the start. User has an RTX 4090 and wants CUDA support available through a simple runtime selector.                     |
| Optional Windows GPU     | `Microsoft.ML.OnnxRuntime.DirectML`                                                   | Useful future Windows fallback when CUDA is unavailable or native CUDA/cuDNN setup is not desired.                                              |
| Optional remote provider | ComfyUI-backed embedding provider                                                     | Comfy can be considered if it already owns a working ONNX/CUDA environment, but integration concerns should be discussed before implementation. |
| Image preprocessing      | Existing `Magick.NET-Q16-AnyCPU`                                                      | Already installed; can decode, resize, crop, and normalize source images.                                                                       |
| Model artifact           | CLIP/SigLIP/MobileCLIP image encoder exported to ONNX                                 | Prefer image encoder only, not full text/image model unless needed later.                                                                       |
| Model metadata           | Config for input name, input size, layout, mean/std, embedding dimension, output name | Do not hardcode these into inference logic.                                                                                                     |
| Vector normalization     | L2-normalize embeddings before storing                                                | Makes cosine similarity equivalent to dot product.                                                                                              |
| Storage                  | Float32 vector BLOB plus model key/hash/dimension                                     | Recompute when model config or model file changes.                                                                                              |
| Processing               | Background queue with cancellation and progress                                       | Required for 113k records.                                                                                                                      |

### Recommended Runtime Decision

Design ONNX embedding support with runtime selection from the start. The first supported selector should include `CPU` and `CUDA`, with `CUDA` treated as the preferred high-throughput path for the user's RTX 4090 environment and `CPU` kept as the portable fallback. DirectML and ComfyUI-backed embedding can be added behind the same provider boundary once their integration behavior is clear.

Phase 1 does not add ONNX packages yet. It creates the schema and repository boundary that can safely store provider/model identity later.

### Model Choice Direction

Use a compact vision embedding model before a large VL model:

- Preferred: SigLIP or CLIP ViT-B/32 style image encoder ONNX.
- Alternative: MobileCLIP if a small and reliable ONNX export is available.
- Avoid Qwen 3 VL for bulk embedding. It is better for captions or explanations on representative images, not for indexing 113k images.

### Inference Service Shape

- `IImageEmbeddingService`
  - Loads configured ONNX model lazily.
  - Preprocesses one image into tensor input.
  - Returns normalized `float[]` embedding.
  - Exposes model key/hash/dimension for persistence checks.
- `ICleanupIndexingQueue`
  - Enqueues image IDs from generation save and manual scan.
  - Coalesces duplicate IDs.
  - Supports batch progress reporting.
- `CleanupIndexingWorker`
  - Runs background indexing work with limited concurrency.
  - Stores per-image status and errors.
  - Can resume incomplete work.

### Runtime Selector Shape

The runtime selector should stay deliberately simple in the first embedding phase:

- `CPU`: always available fallback.
- `CUDA`: NVIDIA/CUDA provider, preferred for the RTX 4090 setup.
- `DirectML`: optional Windows GPU fallback after CUDA support is stable.
- `ComfyUI`: deferred provider option if the app delegates embeddings to a Comfy workflow or custom node.

Provider selection must be persisted with embedding model metadata so users can understand which runtime produced an index, but similarity comparisons must key on model identity/dimensions rather than provider alone.

---

## Key Decisions

| Decision                                                                | Rationale                                                                                                                |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Review groups are persisted or reproducible from persisted run data     | Users need stable batches while making deletion decisions.                                                               |
| Existing `GalleryService` remains the selected-ID authority             | Existing delete, deselect, selected-only filter, and selection manager already depend on it.                             |
| Cleanup candidates become selected image IDs before deletion            | Keeps destructive action inside known confirmation paths.                                                                |
| Embeddings are indexed in the background                                | Generation and gallery browsing should not block on model inference.                                                     |
| New generated images enqueue indexing after save                        | New work stays fresh without requiring full rescans.                                                                     |
| ONNX model config is data-driven                                        | Different CLIP/SigLIP exports have different input/output names and preprocessing.                                       |
| CPU and CUDA are first-class runtime choices                            | The app should support the user's RTX 4090 from the first embedding implementation while preserving a portable fallback. |
| Provider selection is isolated behind an embedding runtime boundary     | CUDA, DirectML, CPU, and possible Comfy-backed embedding should not change cleanup indexing or grouping code.            |
| Qwen 3 VL is optional for representative captions only                  | It is too expensive and variable for bulk grouping at this scale.                                                        |
| Brute-force cosine is acceptable for the first embedding implementation | 113k vectors can be scanned in batches if embeddings are cached; vector DB can come later.                               |

---

## Proposed Persistence Model

Names are provisional and should be refined during implementation.

| Entity                  | Purpose                                                                                                   |
| ----------------------- | --------------------------------------------------------------------------------------------------------- |
| `CleanupImageIndex`     | One row per gallery image with file facts, scan state, hashes, normalized prompt data, and index version. |
| `CleanupImageEmbedding` | One row per image per embedding model with vector BLOB, dimensions, model key/hash, and status.           |
| `CleanupGroupRun`       | A generated grouping pass with strategy, thresholds, scope, progress, and summary metrics.                |
| `CleanupGroup`          | Top-level review group with representative image, strategy, count, estimated bytes, and reason.           |
| `CleanupGroupMember`    | Images inside a group with distance/similarity, role, and suggested action.                               |

### Persistence Notes

- Use normal relational tables for image index, embeddings, groups, and group members.
- Consider a JSON-backed body only for run configuration and summary metrics that may evolve quickly.
- Add indexes for `ImageId`, `ProjectId`, `Strategy`, `RunId`, `PromptFingerprint`, `ExactHash`, and perceptual hash lookups.
- Store vectors as little-endian float32 BLOBs to avoid huge JSON payloads.
- Keep model identity with every embedding so changing models does not mix vector spaces.
- Use manual migration rules from `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` if the EF CLI is not used.

---

## Cleanup Strategies

| Strategy                  | Uses Embeddings | Description                                                                      | First-Class Value                            |
| ------------------------- | --------------: | -------------------------------------------------------------------------------- | -------------------------------------------- |
| Exact duplicate           |              No | Same file hash.                                                                  | Safest deletion candidates.                  |
| Near duplicate            |              No | Perceptual hash distance.                                                        | Catches resized/compressed duplicates.       |
| Prompt fingerprint        |              No | Normalized same prompt after tag/weight cleanup.                                 | Groups repeated generation runs.             |
| Prompt fuzzy              |              No | Token-set and ordered-token similarity.                                          | Catches reordered or lightly edited prompts. |
| Same experiment           |              No | Project + workflow + model + date bucket + prompt fingerprint.                   | Useful for seed sweeps and parameter tweaks. |
| Visual similarity         |             Yes | Embedding cosine similarity.                                                     | Groups images by what they depict.           |
| Low-value candidates      |        Optional | Non-favorite, score 0, older variants where group has favorites or higher score. | Practical cleanup assist.                    |
| Orphans and missing files |              No | DB row missing file, file missing DB row if folder scan is added.                | Keeps storage and DB honest.                 |
| Large storage offenders   |              No | Videos, upscales, huge files, old non-favorites.                                 | Fast resource recovery.                      |

---

## UI Design Direction

The cleanup UI should be a review surface, not a filter bolted onto the existing masonry grid.

### Top-Level Review Model

- A cleanup run produces top-level groups.
- The main cleanup screen lists groups first.
- Each group has a representative thumbnail, strategy badge, member count, selected count, estimated disk usage, shared prompt/workflow/model hints, and confidence/similarity range.
- Expanding a group reveals its member images in a compact gallery strip/grid.
- Group expansion should lazy-load members to avoid rendering thousands of images at once.

### Group Actions

- Select all in group.
- Select all except favorite.
- Select all except highest score.
- Select all except representative.
- Add group members to current selection.
- Replace current selection with group members.
- Save group as a named `Selection`.
- Open representative in `AssetViewer`.

### Layout Direction

- Prefer a dedicated Cleanup view reachable from the Gallery settings/actions area.
- Use the existing visual language: dense controls, `Variant.Text` form fields, icon buttons with tooltips, token-based spacing.
- Reuse `GalleryImageTile`, `AssetViewer`, and `GalleryService` selection events where practical.
- Do not build a separate delete path unless a recycle/staging feature is explicitly planned.

---

## Implementation Phases

### Phase 1: Cleanup Domain And Index Schema

**Objective:** Add persistent cleanup index entities, repositories, and migration without ONNX inference.
**Complexity:** 8 points
**Status:** [x] Complete and cleanup-test validated

#### Steps

- [x] Define cleanup index entities and repository interfaces.
- [x] Add EF Core mappings and migration.
- [x] Add scan status and error models.
- [x] Add tests for repository create/update/query flows.

#### Success Criteria

- Cleanup index tables are created by startup migration.
- Repository can upsert index rows for existing images.
- Repository can query stale, missing, and indexed rows by scope.

---

### Phase 2: Metadata, Hash, And Prompt Fingerprint Indexing

**Objective:** Build the first resumable scanner with cheap deterministic signals.
**Complexity:** 13 points
**Status:** [x] Complete and cleanup-test validated

#### Steps

- [x] Implement file resolution, existence, file size, and modified-state indexing.
- [x] Implement SHA-256 exact hash.
- [x] Implement perceptual hash with Magick.NET preprocessing.
- [x] Implement prompt normalization and fingerprinting.
- [x] Add batch indexing service with progress, cancellation, and error handling.
- [x] Add tests for prompt normalization and perceptual hash behavior.

#### Success Criteria

- A manual scan can index a large image table incrementally.
- Re-running the scan skips unchanged images.
- Failed images are recorded without aborting the whole scan.

---

### Phase 3: Deterministic Group Generation

**Objective:** Generate reviewable cleanup groups without embeddings.
**Complexity:** 8 points
**Status:** [x] Complete and cleanup-test validated

#### Steps

- [x] Implement exact duplicate groups.
- [x] Implement perceptual-near-duplicate groups.
- [x] Implement prompt fingerprint groups.
- [x] Implement prompt fuzzy groups.
- [x] Persist group runs, groups, and members.
- [x] Add tests for grouping strategies.

#### Success Criteria

- The app can create stable cleanup runs from indexed data.
- Groups contain representative image IDs and member similarity facts.
- The grouping service avoids loading all full image entities into memory at once.

---

### Phase 4: Cleanup Review UI

**Objective:** Add a review-first UI for top-level groups and expandable group members.
**Complexity:** 13 points
**Status:** [x] Complete and validated

#### Steps

- [x] Add cleanup entry point from Gallery.
- [x] Add cleanup run controls: scope, strategy, thresholds, minimum group size.
- [x] Add virtualized or paged top-level group list.
- [x] Add expandable group member grid with lazy loading.
- [x] Wire group actions into `IGalleryService.SelectedImageIds`.
- [x] Reuse `AssetViewer` for inspection.
- [x] Add confirmation language for selected deletion through existing flows.

#### Success Criteria

- User can distinguish groups before inspecting individual members.
- Expanding a group loads only that group's members.
- User can select cleanup candidates and use existing selected-image deletion.
- UI remains responsive on large group runs.

---

### Phase 5: Generation Save Hook And Incremental Indexing

**Objective:** Keep cleanup index current as new images are generated.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Publish an event when an image record is saved or updated.
- [ ] Subscribe cleanup indexing queue to the event.
- [ ] Enqueue cheap metadata/hash indexing by default.
- [ ] Add settings to control whether embeddings run automatically.
- [ ] Add tests for enqueue behavior.

#### Success Criteria

- New generated images are queued for cleanup indexing after save.
- Generation success does not depend on cleanup indexing success.
- Queue failures are logged and visible but non-fatal.

---

### Phase 6: ONNX Embedding Infrastructure

**Objective:** Add configurable ONNX image embeddings and persist vectors.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add ONNX Runtime package strategy and isolate provider selection.
- [ ] Add simple runtime selector with CPU and CUDA choices from the start.
- [ ] Add embedding model configuration and model hash detection.
- [ ] Implement Magick.NET preprocessing to tensor input.
- [ ] Implement embedding inference and L2 normalization.
- [ ] Persist embeddings as float32 BLOBs.
- [ ] Add CPU-focused tests around vector serialization and model config validation, plus CUDA availability probing around provider initialization.

#### Success Criteria

- A configured ONNX image encoder can generate embeddings for local image files.
- Embeddings are tied to model identity and dimensions.
- Re-indexing detects stale embeddings after model changes.

---

### Phase 7: Visual Similarity Grouping

**Objective:** Build cleanup groups from image embeddings.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Implement batch cosine similarity search over stored embeddings.
- [ ] Add visual grouping thresholds and minimum group size.
- [ ] Add representative selection for visual groups.
- [ ] Add low-value candidate rules inside visual groups.
- [ ] Add tests for vector distance/grouping behavior.

#### Success Criteria

- User can create visually similar cleanup groups.
- Similarity thresholds are adjustable and explainable in the UI.
- Favorites and high-score images are protected from automatic cleanup suggestions.

---

### Phase 8: Optional VL Captions And Explanations

**Objective:** Use Qwen 3 VL or other Ollama vision models only where they add review value.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Caption group representatives on demand.
- [ ] Add optional group explanation text.
- [ ] Cache generated captions to avoid repeated VL calls.
- [ ] Keep VL features disabled for bulk scans by default.

#### Success Criteria

- VL output helps explain groups without slowing core indexing.
- Cleanup still works fully without Ollama available.

---

### Phase 9: Safety, Maintenance, And Advanced Storage Cleanup

**Objective:** Add safety features and storage maintenance tools after core grouping works.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add stale index cleanup when images are deleted.
- [ ] Add orphan/missing-file reports.
- [ ] Add optional move-to-staging/trash workflow if desired.
- [ ] Add storage summary metrics by project, workflow, and cleanup run.
- [ ] Add documentation for cleanup operations and recovery expectations.

#### Success Criteria

- Cleanup data does not accumulate stale rows after image deletion.
- Users can identify storage-heavy projects and groups.
- Destructive cleanup remains reversible if staging/trash is enabled.

---

## Stress Points And Risks

| Risk                                                 | Mitigation                                                                                                                          | Complexity |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ---------: |
| 113k records overwhelm memory or UI                  | Batch DB queries, lazy-load group members, virtualize top-level lists, avoid full entity materialization.                           |         13 |
| ONNX native package conflicts                        | Keep provider selection isolated; support CPU fallback and CUDA probing so native failures are surfaced before a large scan starts. |          8 |
| Embedding model swaps corrupt similarity comparisons | Store model key, model hash, output dimensions, and embedding version with every vector.                                            |          5 |
| Generation save becomes slow                         | Enqueue indexing after save and process in background. Never block generation on embeddings by default.                             |          5 |
| Accidental mass deletion                             | Use review groups, existing selected image flow, confirmation dialogs, and optional saved selections/staging.                       |          8 |
| Prompt grouping is too strict or too loose           | Provide separate exact, fingerprint, and fuzzy strategies with thresholds.                                                          |          5 |
| Perceptual hash misses semantic similarity           | Treat perceptual hash as a cheap duplicate signal, not semantic grouping. Embeddings handle semantic similarity.                    |          3 |
| SQLite vector search becomes slow                    | Start brute force over cached vectors; add approximate vector index only after measuring.                                           |          8 |
| File paths may be stale                              | Index existence and errors separately so missing files become a cleanup category instead of scan blockers.                          |          5 |

---

## Open Clarifications

- Which ONNX provider package split is safest for local development: one GPU build with CPU fallback, separate CPU/GPU package profiles, or a runtime plugin boundary?
- Should ComfyUI embedding be treated as a first-class provider or kept as an escape hatch after local CUDA works?
- Should cleanup deletes permanently delete files as today, or should this plan include a move-to-staging/trash phase before permanent deletion?
- Should cleanup groups be scoped to the active project by default, or should the first screen support all projects/folders immediately?
- Which image encoder model should be bundled/recommended, and where should model files live in the app configuration?
- Should videos receive embeddings from poster frames in the same index, or should video cleanup stay metadata/hash-only at first?

---

## Initial Recommendation

Use this execution order first:

1. Phase 1 - schema and repositories.
2. Phase 2 - metadata/hash/prompt indexing.
3. Phase 3 - deterministic grouping.
4. Phase 4 - review UI.
5. Phase 5 - save hook.
6. Phase 6 and 7 - ONNX embeddings with CPU/CUDA selector and visual grouping.

This order gives the user a useful cleanup tool before the hardest ML integration lands, and it gives ONNX embeddings a safe UI destination when they are ready.

---

## Validation Strategy

- Repository tests for cleanup index CRUD and grouping persistence.
- Unit tests for prompt normalization, hash distance thresholds, vector serialization, and cosine similarity.
- Focused service tests for batch indexing cancellation, skip-unchanged behavior, and error recording.
- UI build/diagnostics for cleanup Razor components.
- Full `dotnet build BlazorWebApp/BlazorWebApp.csproj /p:UseAppHost=false` after phase edits are complete.

---

## References

- `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
- `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- `BlazorWebApp/Data/Entities/Image.cs`
- `BlazorWebApp/Services/GalleryService.cs`
- `BlazorWebApp/Components/Gallery/InfiniteScrollMasonry.razor`
- `BlazorWebApp/Components/Gallery/SelectionsDialog.razor`
- `BlazorWebApp/Services/VLModelService.cs`

---

## Changelog

| Phase           | Changes                                                                                                                                                  |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Planning        | Initial cleanup tools plan created with ONNX, grouping, persistence, and review UI direction.                                                            |
| Planning update | Runtime direction updated for first-class CUDA support, simple CPU/CUDA selector, RTX 4090 target environment, and possible ComfyUI provider discussion. |
| Phase 1         | Added cleanup persistence entities, EF mappings, manual migration, repository, DI registration, and focused repository tests.                            |
| Phase 2         | Added deterministic cleanup indexing services for file facts, SHA-256 hash, perceptual hash, prompt fingerprints, batching, progress, and skip reruns.   |
| Phase 3         | Added deterministic grouping service for exact duplicates, perceptual-near duplicates, prompt fingerprints, and prompt fuzzy groups.                     |
| Phase 4         | Added cleanup review UI with scope indexing, deterministic group generation controls, paged groups, lazy members, selection wiring, AssetViewer reuse, determinate indexing progress, selected-tile cues, and Gallery action placement refinements. |
