# Phase 1 - Live API Probe And Metadata Parser Hardening

## Status

**Phase State:** Complete
**Complexity:** 8 points
**Parent Plan:** `Documentation/Plans/civitai-page-refactor-and-api-resilience/MAIN_PLAN.md`

---

## Objective

Establish the current CivitAI image/model response shapes and fix generation metadata extraction at the DTO/service boundary so CivitAI images populate prompt and generation parameters reliably in `AssetViewer`.

---

## Scope

This phase is limited to metadata parsing and projection:

- Record sanitized API shape findings for the endpoints used by the CivitAI browser.
- Harden `CivitaiImageMetaDto` for wrapper, flat, null, missing, and mixed-type metadata.
- Map newly recovered fields through `CivitaiAssetAdapter` where `ImageEntity` supports them.
- Add focused parser/adapter tests.

Out of scope for this phase:

- In-page model detail UI.
- Resource image download toggle.
- CivitAI viewer save-to-send flow.
- Broader fetch/download fail-safes beyond metadata parse tolerance.

---

## Steps

- [x] Step 1 (2 pts) - Create a repeatable CivitAI probe artifact under this plan folder that records sampled endpoints, response shapes, edge cases, and sanitized examples.
- [x] Step 2 (3 pts) - Refactor `CivitaiImageMetaDto` parsing so it handles both legacy flat metadata and current wrapper metadata.
- [x] Step 3 (1 pt) - Update `CivitaiAssetAdapter.Project(...)` to map newly recovered fields where `ImageEntity` supports them.
- [x] Step 4 (2 pts) - Add focused tests for representative metadata shapes.

---

## Implementation Notes

- `CivitaiImageDto.MetaObject` is the deserialized raw `meta` field.
- Current live shape can be a wrapper object with a nested `meta` property, or an object where nested `meta` is null.
- Existing app code calls `new CivitaiImageMetaDto(image.MetaObject)` in service code, so constructor tolerance is the safest first correction point.
- Keep parser behavior lossy-safe: missing or malformed values should leave default/null fields instead of throwing.
- Use invariant culture for numeric strings such as denoise and CFG.
- Do not commit raw API prompt text into notes or tests; test strings should be synthetic.

---

## Validation Plan

- Run the sanitized probe script and record endpoint shape findings in `API_PROBE_NOTES.md`.
- Run focused tests for the new CivitAI DTO/adapter test file.
- Use file-level diagnostics for touched C# files after edits.
- Escalate to a focused test project build only if direct tests expose project-wide compile issues.

---

## Running Notes

- Phase began after user approval of the revised main plan.
- Initial code inspection confirmed the parser expects flat keys directly under `meta`, while current API responses can place generation fields under `meta.meta`.
- Added `Probe-CivitaiApi.ps1` and `API_PROBE_NOTES.md` with sanitized findings for `v1/models/{id}`, `v1/images?modelId=...`, `v1/images?imageId=...`, and `v1/images?modelVersionId=...`.
- Hardened `CivitaiImageMetaDto` for wrapper metadata, flat metadata, null nested metadata, malformed scalar/list values, and mixed string/numeric generation fields.
- Updated `CivitaiAssetAdapter.Project(...)` so scheduler and dimensions can be recovered from parsed metadata when the DTO surface is incomplete.
- Added focused tests in `BlazorWebApp.Tests/Data/Dtos/CivitaiImageMetaDtoTests.cs`.
- Validation: touched-file diagnostics are clean. Focused test command passed with isolated artifacts because a running `BlazorWebApp` process locks the normal debug output: `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CivitaiImageMetaDtoTests --artifacts-path .\Temp\test-artifacts -p:UseAppHost=false -p:NuGetAudit=false -p:NoWarn=NU1603 -p:WarningLevel=0 -v minimal --nologo`.
- Focused test result: 6 total, 0 failed, 0 skipped. Remaining warning: existing `NETSDK1206` runtime identifier warning from `SQLitePCLRaw.lib.e_sqlite3`.
