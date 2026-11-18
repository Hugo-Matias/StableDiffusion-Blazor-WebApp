# Phase 3 - Characters Selection And Reference Sheet Integration

## Status

Complete. Implemented and validated after user approval.

## Objective

Add the first `Character` tab, selection flow, and migrate the existing Reference Sheets tab to load/save the selected character sheet.

## Scope

Phase 3 connects persisted characters to the existing `/characters` Reference Sheets feature. It should keep the current reference-generation controls usable while introducing character selection and sheet ownership. Full identity editing, region diagrams, prompt compilation, and LLM-assisted authoring remain later phases.

## Step Checklist

- [x] Step 1: Split `AppState.Character` into lightweight selected character/sheet state plus existing working reference-sheet state. `[2 pts]`
- [x] Step 2: Add first `Character` tab before `Reference Sheets` in `Characters.razor`. `[3 pts]`
- [x] Step 3: Build character selector/create/rename/delete/duplicate UI using documented layout patterns. `[3 pts]`
- [x] Step 4: Add reference-sheet list and create/load behavior for the selected character. `[3 pts]`
- [x] Step 5: Refactor Reference Sheets save/load actions to persist the selected sheet body. `[5 pts]`
- [x] Step 6: Update `MediaSendToService` so `Characters / Source Image` asks for a character when none is selected, then creates or loads the matching sheet. `[3 pts]`
- [x] Step 7: Add focused state, repository, send-to, and run-service tests. `[5 pts]`

## Implementation Notes

- `AppState.Character` remains the working reference-sheet state for the current page session, but now also carries selected character id, selected reference sheet id, and a pending source image.
- `CharacterReferenceSheetBody` provides mapping helpers between persisted sheet bodies and the existing `AppStateCharacter` working state.
- `/characters` now starts with a `Character` tab and keeps the existing generation controls in the second `Reference Sheets` tab.
- When a saved image is sent to `Characters / Source Image` and no character is selected, the source is stored as pending state and the page prompts the user to attach it after choosing or creating a character.

## Validation Plan

- Focused model tests for new app-state selection and pending-source behavior.
- Focused media send-to tests for no-selected-character and selected-character flows.
- Focused repository tests from Phase 2 should continue passing.
- Build task after implementation.

## Validation Results

- `CharacterStateTests`, `CharacterRepositoryTests`, and `MediaSendToServiceTests`: 35 passed, 0 failed.
- `CharacterReferenceRunServiceTests`: 8 passed, 0 failed.
- VS Code build task `process: build`: passed. Existing NuGet warning noise remains for `Microsoft.Bcl.AsyncInterfaces` version resolution and `Magick.NET-Q16-AnyCPU` audit advisories.

## Open Issues / Blockers

- No blockers remain for this phase.

## Change Log

- Created Phase 3 document and began implementation after user approval.
- Added selected character/sheet state, pending source image flow, sheet/app-state mapping helpers, async character-aware media send-to routing, two-tab `/characters` UI, and focused validation.
