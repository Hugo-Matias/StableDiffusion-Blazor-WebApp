# Phase 3 - Deterministic Group Generation

## Status

[x] Complete and cleanup-test validated

## Scope

Generate persisted cleanup group runs from deterministic indexed signals without embeddings and without UI. This phase turns indexed cleanup rows into stable review groups that can later be shown by the Phase 4 review surface.

## Decisions

- Group generation reads lightweight `CleanupImageIndex` rows only; it does not materialize full `Image` entities.
- Supported strategies in this phase are exact duplicate, near duplicate, prompt fingerprint, and prompt fuzzy.
- Exact duplicate groups use identical SHA-256 file hashes.
- Near duplicate groups use perceptual hash Hamming distance with a configurable max distance.
- Prompt fingerprint groups use exact prompt fingerprint matches.
- Prompt fuzzy groups use token-signature Jaccard similarity with a configurable minimum similarity.
- Group representatives are stable: the lowest image id in the group.
- Non-representative members are marked as `CleanupCandidate` with `Review`, not `Delete`, so later UI remains review-first.

## Checklist

- [x] Implement exact duplicate groups.
- [x] Implement perceptual-near-duplicate groups.
- [x] Implement prompt fingerprint groups.
- [x] Implement prompt fuzzy groups.
- [x] Persist group runs, groups, and members.
- [x] Add tests for grouping strategies.
- [x] Run targeted validation.

## Implementation Notes

- Added `CleanupGroupingOptions` / `CleanupGroupingResult` service models.
- Added `ICleanupGroupingService` and `CleanupGroupingService` under `BlazorWebApp.Services.Cleanup`.
- Exact and prompt fingerprint strategies use keyed grouping over indexed rows.
- Near duplicate grouping uses a BK-tree over 64-bit perceptual hashes to avoid comparing every indexed row to every other row.
- Prompt fuzzy grouping uses an inverted token index and union-find grouping for rows that meet the Jaccard threshold.
- Persisted groups include representative image id, member count, estimated bytes, similarity/confidence ranges, and ordered members.
- Cleanup grouping services are registered in `Program.cs`.

## Validation Result

- File diagnostics found no errors in the new grouping service, models, interface, tests, or DI registration.
- `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~BlazorWebApp.Tests.Cleanup --no-restore` passed all 12 cleanup tests.
- `dotnet build .\BlazorWebApp\BlazorWebApp.csproj /p:UseAppHost=false /p:OutputPath=..\Temp\cleanup-build\` succeeded. Existing package vulnerability/RID warnings remain unrelated to this slice.

## Deferred Work

- Cleanup review UI belongs to Phase 4.
- Generation save enqueueing belongs to Phase 5.
- Visual embeddings and embedding-based grouping belong to Phases 6 and 7.
