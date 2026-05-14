# Phase 9 - Catalog Expansion And Import/Export

## Status

Complete. User approved proceeding after Phase 8 completion, and implementation/validation finished in this session.

## Objective

Make Character Creator easier to extend outside the app and safer to back up or share.

## Scope

Phase 9 adds JSON import/export for characters and reference sheets, catalog reload/report UI on `/characters`, editable runtime catalog JSON files, and catalog authoring documentation. The optional LLM trait-idea generator is deferred until catalog editing patterns settle; the no-rebuild extension path is covered by JSON catalogs plus reload.

## Step Checklist

- [x] Step 1: Add import/export for characters and reference sheets as JSON. `[3 pts]`
- [x] Step 2: Add validation report UI for user-edited catalogs. `[2 pts]`
- [x] Step 3: Add catalog reload action. `[1 pt]`
- [x] Step 4: Add sample catalog documentation and examples. `[2 pts]`
- [x] Step 5: Defer optional LLM tool for generating new trait options until after catalog editing is exercised. `[0 pts]`

## Implementation Notes

- Added `CharacterImportExportService` and document DTOs for character and reference sheet JSON.
- Character imports create a new character with a unique name so existing rows are not overwritten.
- Reference sheet imports preserve one-sheet-per-source-image by updating an existing sheet when the source fingerprint matches; otherwise they add a new sheet to the selected character.
- `/characters` now exposes Library import/export actions, reference-sheet import/export actions, and a Catalog panel with reload plus loader warnings.
- Added editable catalog files under `BlazorWebApp/Data/CharacterCreator/` and configured them to copy to output/publish.
- Added catalog authoring docs at `Documentation/CharacterCreator/CATALOGS.md`.

## Validation

- File diagnostics for touched service, model, page, CSS, test, and project files: no new errors; known package warning noise remains on the project file.
- Focused `runTests` for `CharacterImportExportServiceTests.cs` and `CharacterCreatorCatalogServiceTests.cs`: 4 passed from the test runner.
- `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterImportExportServiceTests --no-restore`: 3 passed.
- `process: build`: passed with known NuGet warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`).

## Open Issues / Blockers

- The optional LLM trait-idea helper remains deferred. The current supported extension path is editing JSON catalog files and using Catalog reload.
- Imported reference sheets can contain local image ids/paths from another machine; the sheet still imports, but those assets may need relinking if the original media is unavailable.

## Change Log

- Added character/reference-sheet JSON import-export service and tests.
- Added `/characters` Library and Catalog controls.
- Added runtime catalog JSON samples, project copy rules, and catalog authoring docs.
