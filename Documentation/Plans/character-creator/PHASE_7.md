# Phase 7 - Create From Image

## Status

Complete. User approved proceeding after Phase 6 completion, and Phase 7 implementation plus validation finished in this pass.

## Objective

Reuse vision-language model support to create an editable draft character identity from an image.

## Scope

Phase 7 adds image interrogation for character identity drafts, parser normalization against the Character Creator catalog, editable review, and accepted source-image metadata on the character's reference sheets. It does not route compiled prompts to Generate workflows; that remains Phase 8.

## Step Checklist

- [x] Step 1: Add character-identity interrogation prompt templates for a wrapper service. `[2 pts]`
- [x] Step 2: Add image input UI based on the existing Image-to-Prompt pattern. `[3 pts]`
- [x] Step 3: Parse VL JSON into normalized regions with source/confidence metadata. `[3 pts]`
- [x] Step 4: Add review UI for accepting, editing, and discarding inferred traits. `[3 pts]`
- [x] Step 5: Support source image thumbnail/reference metadata on saved sheets. `[2 pts]`
- [x] Step 6: Add tests for parser normalization and invalid output handling. `[3 pts]`

## Implementation Notes

- The image draft service uses Ollama multimodal chat with JSON mode and the existing shared LLM options.
- Draft parsing accepts only region and trait ids present in the loaded Character Creator catalog.
- Accepted image traits are marked with `CharacterTraitSource.Image` and confidence is recorded per trait.
- Accepted drafts add or select a reference sheet for the source image fingerprint and keep its source image metadata in the character aggregate.
- The Character Creator workspace exposes model selection, image input, editable draft fields, accept/discard actions, and raw response inspection.

## Validation Plan

Completed:

- File diagnostics for touched service, model, page, component, CSS, and test files: no errors.
- `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterImageDraftServiceTests --no-restore`: 4 passed.
- `process: build`: passed.

Known unrelated warning noise:

- `NU1603` for `Microsoft.Bcl.AsyncInterfaces` version resolution.
- `NU1901`/`NU1902`/`NU1903` advisories for `Magick.NET-Q16-AnyCPU`.
- Existing nullable/analyzer warnings in unrelated UI/service/test files during the focused test build.

## Open Issues / Blockers

- Live image extraction quality depends on the selected local multimodal Ollama model.

## Change Log

- Created Phase 7 document and began implementation after user approval.
- Added `CharacterImageDraftService` and `ICharacterImageDraftService` for multimodal JSON character drafts.
- Added `CharacterImageDraftModels` for draft requests, editable draft regions/traits, and apply results.
- Wired the Character Creator workspace to run image drafts, edit inferred fields, accept/discard drafts, and inspect raw VL JSON.
- Added focused tests for parser normalization, malformed fallback, safe apply behavior, and Ollama multimodal request options.
