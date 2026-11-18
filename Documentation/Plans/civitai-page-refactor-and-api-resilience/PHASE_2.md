# Phase 2 - CivitAI-Aware Image Save And Send-To Flow

## Status

**Phase State:** Complete
**Complexity:** 8 points
**Parent Plan:** `Documentation/Plans/civitai-page-refactor-and-api-resilience/MAIN_PLAN.md`

---

## Objective

Make remote CivitAI images actionable from the shared `AssetViewer`: save an individual CivitAI image as a local `ResourceImage`, then enable workflow image-send and LLM Generate Prompt from the saved local file.

---

## Scope

This phase is limited to the image viewer send-to bridge:

- Extract the current one-image save behavior from `CivitaiModelSpecCard` into a reusable CivitAI resource-image service.
- Preserve existing model/version Save All behavior while making the same save path available to CivitAI image browsing and model preview viewers.
- Extend shared viewer/info-panel parameters with source-specific remote-image copy and a save-local callback.
- Preserve Gallery and Danbooru defaults.

Out of scope for this phase:

- In-page model detail refactor.
- Optional automatic resource-image download toggle.
- Broader CivitAI API fail-safe work beyond the save bridge.

---

## Steps

- [x] Step 1 (2 pts) - Extract the save-one-resource-image logic from `CivitaiModelSpecCard.SaveImage(...)` into a reusable service or helper.
- [x] Step 2 (2 pts) - Extend `AssetViewer`/`AssetInfoPanel` with source-aware external-action parameters and save-local callback.
- [x] Step 3 (2 pts) - Wire CivitAI image viewers to use the two-step save-then-send pattern.
- [x] Step 4 (1 pt) - Add CivitAI-specific source labels for LLM Image-to-Prompt handoff.
- [x] Step 5 (1 pt) - Verify parameter send remains available while remote, and image-send/LLM-send unlock after local save.

---

## Implementation Notes

- Keep the original `CivitaiModelSpecCard` save path layout for model/version images so existing saved files stay in the same folders.
- For standalone CivitAI image browsing, use a generic `Saved/CivitAI/Images` folder and allow `CivitaiModelId` / `CivitaiModelVersionID` to remain `0` when not known.
- The shared viewer should not know CivitAI DTOs. It should only request a local asset from its host and replace the current transient asset if the host returns one.
- `AssetInfoPanel` keeps Danbooru bookmark wording as its default remote-image hint; CivitAI hosts override the hint and action label.

---

## Validation Plan

- Run file-level diagnostics for touched Razor/C# files.
- Run a focused build if Razor diagnostics are insufficient.
- Manually verify in the running app when available: remote CivitAI image shows CivitAI save text, parameter send remains visible, local save changes the active asset path, image-send buttons and Generate Prompt appear after save.

---

## Running Notes

- Phase began after Phase 1 metadata fixes were confirmed by the user.
- Added `ICivitaiResourceImageService` / `CivitaiResourceImageService` to centralize CivitAI image download, metadata JSON writing, and `ResourceImage` creation.
- Updated `CivitaiModelSpecCard` to call the shared save service while preserving the existing model/version path layout.
- Extended `AssetViewer` and `AssetInfoPanel` with source-specific remote hint text, save-local callback, action label/icon, and LLM source label while preserving default Danbooru bookmark behavior.
- Wired `CivitaiImagesPanel` and `CivitaiModelHero` so a remote CivitAI image saves locally, replaces the active transient viewer asset with the `/image/...` path, and then unlocks image-send plus Generate Prompt.
- Validation: `dotnet build` task succeeded with existing warning noise; focused `CivitaiImageMetaDtoTests` passed with 6/6 tests.
- Follow-up: added saved-image hydration when opening CivitAI viewers. The viewer now checks existing `ResourceImage` records by CivitAI image hash/id and uses the saved `/image/...` path when the local file still exists, so returning to CivitAI does not show the save-local action again for already saved images.
- Follow-up: encoded CivitAI saved `/image/...` path segments and decoded them in filesystem resolution so special characters in CivitAI hashes, especially `#`, do not break viewer image `src` URLs while send-to resolution keeps working.
