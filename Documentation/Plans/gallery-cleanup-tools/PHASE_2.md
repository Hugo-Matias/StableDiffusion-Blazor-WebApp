# Phase 2 - Metadata, Hash, And Prompt Fingerprint Indexing

## Status

[x] Complete and cleanup-test validated

## Scope

Build the first deterministic cleanup scanner over existing gallery `Image` rows. This phase stays backend-only: no UI entry point, no generation save hook, and no ONNX embeddings.

## Decisions

- Batch indexing reads `Image` rows by increasing image id so a caller can resume from the last processed id.
- Reruns skip unchanged images before computing hashes by comparing file path, file size, last-write UTC, prompt normalization, key image metadata, and index version.
- Missing files are written as `CleanupIndexStatus.MissingFile` instead of throwing.
- Per-image failures are written as `CleanupIndexStatus.Error` with the error message, so one corrupt image does not abort a large scan.
- Prompt fingerprints are SHA-256 hashes over normalized positive prompts. Token signatures are sorted unique normalized prompt tokens for later fuzzy grouping.
- Perceptual hashing uses a compact 8x8 average-hash implementation through Magick.NET preprocessing.

## Checklist

- [x] Implement file resolution, existence, file size, and modified-state indexing.
- [x] Implement SHA-256 exact hash.
- [x] Implement perceptual hash with Magick.NET preprocessing.
- [x] Implement prompt normalization and fingerprinting.
- [x] Add batch indexing service with progress, cancellation, skip unchanged reruns, and error handling.
- [x] Register cleanup indexing services with DI.
- [x] Add focused tests for prompt normalization, hash behavior, missing-file handling, and unchanged reruns.
- [x] Run targeted validation.

## Implementation Notes

- Added `BlazorWebApp.Services.Cleanup` service models and interfaces for cleanup indexing.
- Added `CleanupPromptIndexService` for normalized prompts, fingerprints, and token signatures.
- Added `CleanupImageHashService` for exact SHA-256 and deterministic perceptual hashes.
- Added `CleanupIndexingService` for batch scanning over `AppDbContext.Images`, with project scope, hidden-image filtering, max image limits, progress callbacks, cancellation, and per-image error isolation.
- Registered `ICleanupPromptIndexService`, `ICleanupImageHashService`, and `ICleanupIndexingService` in `Program.cs`.
- Added focused cleanup tests under `BlazorWebApp.Tests/Cleanup`.

## Validation Result

- File diagnostics found no errors in the new cleanup services, interfaces, models, tests, or DI registration.
- `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~BlazorWebApp.Tests.Cleanup --no-restore` passed all 12 cleanup tests.
- `dotnet build .\BlazorWebApp\BlazorWebApp.csproj /p:UseAppHost=false /p:OutputPath=..\Temp\cleanup-build\` succeeded. Existing package vulnerability/RID warnings remain unrelated to this slice.

## Deferred Work

- Manual UI controls for running scans belong to Phase 4.
- Generation save enqueueing belongs to Phase 5.
- ONNX, CUDA provider selection, and embedding vector writes belong to Phase 6.
