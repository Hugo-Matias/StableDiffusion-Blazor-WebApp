# Phase 2 - Character Persistence And Catalog Loader

## Status

Complete and validated.

## Objective

Add persistent global characters with embedded reference sheets and load user-editable catalog JSON files.

## Scope

Phase 2 introduces the durable data foundation only. It should not build the new `/characters` first-tab UI yet, and it should not migrate the existing Reference Sheets UI to selected characters yet. Those changes belong to Phase 3.

## Step Checklist

- [x] Step 1: Add `CharacterEntity`, `CharacterBody`, and reference-sheet body model types. `[3 pts]`
- [x] Step 2: Register JSON conversion in `AppDbContext`. `[2 pts]`
- [x] Step 3: Add repository/service using `IDbContextFactory<AppDbContext>`. `[3 pts]`
- [x] Step 4: Hand-author EF migration and update model snapshot. `[3 pts]`
- [x] Step 5: Add one-sheet-per-source enforcement in the repository. `[3 pts]`
- [x] Step 6: Add catalog loader service with validation warnings and fallback defaults. `[5 pts]`
- [x] Step 7: Add focused repository and catalog-loader tests. `[5 pts]`

## Implementation Summary

- Added a global `CharacterEntity` EF root with JSON-backed `CharacterBody`, timestamps, thumbnail id, name, and description.
- Added `CharacterBody`, embedded `CharacterReferenceSheetBody`, source-image fingerprint helpers, prompt profile defaults, trait/region/wardrobe models, and catalog DTOs.
- Added `ICharacterRepository` / `CharacterRepository` for create, list, get, update, duplicate, delete, reference-sheet add-or-get, update, select, and delete operations.
- Enforced one reference sheet per source fingerprint within each character.
- Added `ICharacterCreatorCatalogService` / `CharacterCreatorCatalogService` for user-editable JSON catalog loading with fallback defaults and recoverable warnings.
- Registered repository and catalog services in DI.
- Added manual migration `20260514000000_Add_CharacterEntity` with required EF attributes and updated `AppDbContextModelSnapshot`.
- Added focused tests for model defaults, reference-sheet source fingerprints, repository behavior, duplicate prevention, JSON body mutation persistence, and catalog loading validation.

## Step 1 Plan

Step 1 should add model and entity types only, plus focused model tests. It should avoid EF context registration, migrations, repository code, service registration, and UI changes.

### Files To Create

- `BlazorWebApp/Data/Entities/CharacterEntity.cs`
- `BlazorWebApp/Models/CharacterCreator/CharacterBody.cs`
- `BlazorWebApp/Models/CharacterCreator/CharacterReferenceSheetBody.cs`
- `BlazorWebApp/Models/CharacterCreator/CharacterCreatorCatalogModels.cs`
- `BlazorWebApp.Tests/Models/CharacterCreatorModelsTests.cs`

### Files To Modify

- None for Step 1. Existing `AppStateCharacter` / `CharacterState` migration remains Phase 3 work.

### Intended Changes

- Add `CharacterEntity` as the future EF root with `Id`, `Name`, `Description`, `ThumbnailImageId`, `Body`, `CreatedAt`, and `UpdatedAt`.
- Add `CharacterBody` with schema version, identity fields, region states, wardrobe presets, prompt profiles, embedded reference sheets, active reference sheet id, and notes.
- Add `CharacterReferenceSheetBody` that mirrors the durable subset of the existing `AppStateCharacter` reference-generation fields.
- Add `CharacterReferenceSourceImage` with image id/path/label/fingerprint/original filename fields.
- Add source fingerprint helper/factory methods using the Phase 1 precedence: saved image id, content hash, normalized path fallback.
- Add minimal catalog DTOs that match the Phase 1 schema draft, without implementing file loading yet.
- Add tests for default character creation, default reference sheet creation, and source fingerprint uniqueness behavior.
- Keep existing `AppStateCharacter` behavior intact so the current `/characters` Reference Sheet page continues working until Phase 3.

## Conventions

- Persistence follows the JSON-backed aggregate pattern from the persistence playbook.
- Do not register `CharacterEntity` in `AppDbContext` until Step 2.
- Do not hand-author a migration until Step 4.
- Avoid UI changes in Phase 2.
- Avoid moving existing Reference Sheet state out of `AppStateCharacter` until Phase 3.

## Validation Plan

- Step 1: run focused model tests for the new character models.
- Step 2-5: run focused repository/persistence tests once repository and migration work exists.
- Step 6-7: run catalog loader tests with valid, invalid, duplicate, and partial catalog JSON samples.
- Full build is reserved for phase completion or when narrow tests are insufficient.

## Validation Results

- Focused tests: `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CharacterCreatorModelsTests|FullyQualifiedName~CharacterRepositoryTests|FullyQualifiedName~CharacterCreatorCatalogServiceTests" --no-restore /p:OutputPath=..\Temp\character-creator-test-output\ /p:NuGetAudit=false /p:TreatWarningsAsErrors=false /p:WarningLevel=0 /p:NoWarn=NU1603`
  - Result: Passed, 15 total, 0 failed.
- Build task: `process: build`
  - Result: Passed. Existing NuGet warning noise remains, including `Microsoft.Bcl.AsyncInterfaces` version resolution and `Magick.NET-Q16-AnyCPU` audit warnings.

## Open Issues / Blockers

- No Phase 2 implementation blockers remain.
- Phase 3 must migrate `/characters` UI selection and the existing Reference Sheets working state from global `AppState.Character` to selected character/sheet context.

## Change Log

- Created Phase 2 document from the completed Phase 1 schema decisions.
- Implemented and validated Phase 2 persistence, repository, catalog loader, migration, and tests.
