# Phase 4 - API Fetch And Download Fail-Safes

## Status

**Phase State:** Complete and tested
**Complexity:** 13 points
**Parent Plan:** `Documentation/Plans/civitai-page-refactor-and-api-resilience/MAIN_PLAN.md`

---

## Objective

Make CivitAI browsing, metadata enrichment, media detection, resource download, and resource-image download flows tolerate partial API and file/network failures without breaking the page or aborting unrelated work.

---

## Scope

This phase covers CivitAI API and download resilience:

- Guard CivitAI image-list and image-enrichment calls against HTTP failures, null payloads, parse failures, and cursor exhaustion.
- Preserve stub image data when enrichment cannot recover full metadata.
- Avoid range probing when DTO type or URL extension is enough to identify video/image media.
- Keep resource downloads independent from optional preview/resource-image downloads.
- Add a persistent `/civitai` toolbar toggle for automatic resource-image downloads after a model/resource download.
- Report partial Save All success instead of claiming every image saved after failures.

Out of scope for this phase:

- New CivitAI detail layout changes.
- Deep-link routing for selected models.
- Persistence migrations.

---

## Steps

- [x] Step 1 (3 pts) - Introduce small result/guard helpers in `CivitaiService` for HTTP responses, JSON parse failures, empty payloads, and null lists.
- [x] Step 2 (2 pts) - Harden `GetImages`, `GetImageById`, and `GetImageByModelVersionId` against null `items`, null `metadata`, cursor loops, rate/HTTP failures, and item-level metadata parse failures.
- [x] Step 3 (2 pts) - Replace `GetImageType` behavior with safer media detection using DTO type, URL extension, and range probes only when needed.
- [x] Step 4 (2 pts) - Harden model-version image enrichment so stub image data survives when metadata cannot be enriched.
- [x] Step 5 (2 pts) - Add a context-aware `/civitai` toolbar toggle for automatic resource-image downloads after resource downloads.
- [x] Step 6 (2 pts) - Harden preview/resource image downloads and partial Save All reporting.
- [x] Step 7 (2 pts) - Add focused tests or service-level fakes for the new fail-safe behavior.

---

## Implementation Notes

- The automatic resource-image toggle is stored in `Settings.Settings.Resources.Civitai` so it persists across sessions.
- Explicit Save All remains a user-commanded action and should not be disabled by the automatic-download toggle.
- Metadata enrichment failures should log the image id/model version id when available and then keep the original DTO.
- Logs should avoid dumping raw CivitAI response bodies because image metadata can include prompt text.
- `ImageType` is only used as a video sentinel by the current cards; `byte[] { 0 }` remains the compatibility signal for video media.
- `DownloadResource` now uses the injected CivitAI `HttpClient` for model/resource downloads and streams content with progress updates. This keeps the path testable and avoids bypassing the configured client.
- Optional automatic preview/resource-image downloads are skipped when `Settings.Settings.Resources.Civitai.DownloadResourceImages` is disabled. Explicit Save All remains available.

---

## Validation Plan

- Add focused service tests for null image payloads, HTTP failure, cursor exhaustion, DTO video type detection, and disabled automatic preview image download.
- Run the focused CivitAI test filter with isolated artifacts to avoid the existing locked debug output issue.
- Run file-level diagnostics for touched service/model/Razor files.
- Run the build task after focused tests pass.

---

## Running Notes

- Phase began after the user approved proceeding from completed Phase 3.
- Added guarded CivitAI JSON fetch and image-response normalization helpers. Failed HTTP, null payload, parse failure, null `items`, and null `metadata` cases now degrade to empty DTOs instead of throwing through the UI.
- Hardened image enrichment with a cursor visit set and maximum page count so missing image ids, repeated cursors, and unavailable enrichment endpoints preserve the original stub image.
- Replaced eager media range probing with DTO type and URL extension checks first. Range probing now uses the shared client only when the type cannot be inferred.
- Added the persistent `/civitai` toolbar switch for automatic resource-image downloads and wired model/resource download actions to honor it.
- Preview/resource image downloads now fail per image without aborting the model file download. Save All continues after individual failures and reports full, partial, or failed outcomes.
- Added focused CivitAI service tests covering HTTP failure, null image payload normalization, DTO video type detection without range probing, cursor exhaustion fallback, and disabled automatic preview image download.
- Validation note: Razor, settings, DTO, and test-file diagnostics were clean. `CivitaiService` still surfaces existing nullable warning noise in the diagnostics panel, but the final focused tests and build passed after the Phase 4 changes.
- Validation passed: `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CivitaiServiceTests|FullyQualifiedName~CivitaiImageMetaDtoTests" --artifacts-path .\Temp\test-artifacts-phase4 -p:UseAppHost=false -p:NuGetAudit=false -p:NoWarn=NU1603 -p:WarningLevel=0 -v minimal --nologo` completed with 11 tests passed, 0 failed.
- Validation passed: `dotnet build BlazorWebApp/BlazorWebApp.csproj` through the VS Code build task completed successfully with the existing warning noise.