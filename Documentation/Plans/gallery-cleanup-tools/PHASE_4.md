# Phase 4 - Cleanup Review UI

## Status

[x] Complete and validated

## Scope

Add a review-first UI for persisted cleanup runs and groups. This phase makes the deterministic index/grouping work usable from the app without adding a separate destructive delete path.

## Decisions

- The cleanup review surface lives at `/cleanup` as a focused page using `TopbarLayout` and token-based component CSS.
- Gallery exposes the page through a cleanup action in the expanded project toolbar and the compact project extras menu.
- The UI can index the selected scope before grouping so users can bootstrap cleanup from the page.
- Cleanup indexing reports a determinate total and progress percentage for the selected scope instead of an indeterminate-only progress bar.
- Deterministic group generation controls expose scope, strategy, minimum group size, max groups, perceptual-hash distance, and fuzzy-prompt similarity.
- Recent cleanup runs are loaded through `ICleanupRepository.GetGroupRunsAsync`; groups are paged, and members are lazy-loaded only when a group expands.
- Group actions update `IGalleryService.SelectedImageIds`; deletion remains in the existing Gallery selected-image flow.
- Member inspection reuses the shared `AssetViewer`.
- Selected Gallery tiles show a persistent visual badge and ring in addition to the hover-only checkbox.

## Checklist

- [x] Add cleanup entry point from Gallery.
- [x] Add cleanup review page.
- [x] Add indexing and deterministic grouping controls.
- [x] Add determinate indexing progress with total scope count.
- [x] Add recent run loading.
- [x] Add paged top-level groups.
- [x] Add expandable lazy-loaded group members.
- [x] Wire suggested/all group selections into `IGalleryService`.
- [x] Add persistent selected-image tile cue.
- [x] Move compact cleanup access into the extras menu and place expanded cleanup access before filters.
- [x] Reuse `AssetViewer` for member inspection.
- [x] Run targeted validation.

## Implementation Notes

- Added `BlazorWebApp/Pages/Cleanup.razor` and `Cleanup.razor.css`.
- Added `ICleanupRepository.GetGroupRunsAsync` and the EF implementation in `CleanupRepository`.
- The review page keeps top-level group loading paged at 25 groups per request.
- Expanded groups load `CleanupGroupMember` rows and then fetch only those images through `IDatabaseService.GetImages`.
- Suggested selection excludes the representative and selects members with `CleanupSuggestedAction.Review`; all selection remains available for manual review cases.
- Saving a group creates a normal gallery `Selection`, so existing selection management remains the authority.
- Direct delete from the embedded tile is intercepted with review-first guidance instead of deleting.
- `CleanupIndexingService` now counts the selected scope before scanning and emits an initial progress report, so the page can show `processed / total` and a percentage while indexing.
- Compact Gallery mode keeps cleanup access in the overflow menu; move-left/right folder actions stay available in expanded mode only.

## Validation Result

- File diagnostics found no errors in the cleanup page, page CSS, Gallery entry edit, or cleanup repository contract changes.
- `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~BlazorWebApp.Tests.Cleanup --no-restore --logger "console;verbosity=minimal"` passed all 12 cleanup tests.
- `dotnet build .\BlazorWebApp\BlazorWebApp.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary /p:UseAppHost=false /p:OutputPath=..\Temp\cleanup-build\` succeeded. Existing package vulnerability, RID, nullability, and MudBlazor warnings remain unrelated to this slice.
- Refinement validation found no file diagnostics in the touched cleanup, Gallery, and test files. The VS Code test adapter still discovered zero tests for this project, so the focused `dotnet test` filter was rerun with `/p:UseAppHost=false /p:OutputPath=..\Temp\cleanup-test-build\` to avoid the running-app binary lock; all 12 cleanup tests passed.

## Deferred Work

- Generation save enqueueing and incremental cleanup index updates belong to Phase 5.
- CPU/CUDA ONNX provider setup belongs to Phase 6.
- Embedding-backed visual grouping belongs to Phase 7.
- Additional storage-safety actions and cleanup run maintenance belong to later phases.