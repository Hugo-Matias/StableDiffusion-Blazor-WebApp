# Phase 1 - Cleanup Domain And Index Schema

## Status

[x] Implementation checkpoint complete; focused tests blocked by unrelated test-project compile errors

## Scope

Add the persistence foundation for cleanup indexing and review groups without running ONNX inference or file hashing yet.

## Decisions

- Phase 1 creates relational tables for index rows, embeddings, cleanup runs, groups, and group members.
- Embedding rows include model/provider metadata placeholders, but no ONNX package is added in this phase.
- CUDA support is a Phase 6 implementation requirement, and the schema must be ready to track model identity independently from provider choice.
- Cleanup candidates must remain review-first and flow into existing gallery selection/deletion behavior in later phases.

## Checklist

- [x] Update main plan for CPU/CUDA runtime selector direction.
- [x] Add cleanup entity and enum models.
- [x] Add EF Core mappings, DbSets, and migration.
- [x] Add repository interface and implementation.
- [x] Register repository with DI.
- [x] Add focused repository tests.
- [!] Run targeted validation.

## Validation Plan

- Run focused tests for the cleanup repository.
- Check file diagnostics for touched persistence files if the test runner cannot isolate the new tests.
- Use a narrow app build only after repository tests are green or if diagnostics suggest build-level issues.

## Notes

- The implementation should not enqueue scans or touch the gallery UI yet.
- The first useful queries are upsert by image id, stale rows, missing-file rows, and persisted group run retrieval.

## Implementation Notes

- Added cleanup persistence models for image indexes, embeddings, group runs, groups, and group members.
- Embedding rows include model key, model hash, runtime provider, dimensions, and float-vector BLOB storage for the future ONNX/CUDA phase.
- Added `ICleanupRepository` / `CleanupRepository` with upsert, stale/missing index queries, group run creation, group persistence, and lazy member retrieval.
- Registered cleanup tables in `AppDbContext`, including enum string conversions and cleanup-specific indexes.
- Added manual migration `20260508120000_Add_Cleanup_Tables` with both required EF migration attributes.
- Updated `AppDbContextModelSnapshot.cs` with cleanup entities and relationships.
- Registered `ICleanupRepository` in DI.

## Validation Result

- File diagnostics found no errors in the new cleanup entities, repository, migration, snapshot, tests, or DI registration.
- `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~BlazorWebApp.Tests.Cleanup.CleanupRepositoryTests --no-restore` did not reach the new tests because the existing test project currently fails to compile in `BlazorWebApp.Tests/Services/ImageSendToServiceTests.cs` due missing `ImageSendToService` type references.
- `dotnet build .\BlazorWebApp\BlazorWebApp.csproj /p:UseAppHost=false /p:OutputPath=..\Temp\cleanup-build\` succeeded. Existing warnings remain, mostly package vulnerability/RID warnings and repo-wide nullability warnings.
