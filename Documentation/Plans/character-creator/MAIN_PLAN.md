# Character Creator - Implementation Plan

## Status

**Current Phase:** Phase 10 complete; Character Creator plan implementation complete
**Total Complexity:** ~110 points across 10 implementation phases
**Primary Reference:** `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
**Inspiration Reference:** `Temp/comfyui-character-composer/`

---

## Implementation Guidelines

See `Documentation/Plans/IMPLEMENTATION_GUIDE.md` for the canonical planning and execution contract. Summary for this plan:

### Execution Workflow Per Step

1. Initial Code Writing -> 2. Test and Debug -> 3. Discuss Improvements -> 4. Update Phase Document
   - Do not proceed to the next step until testing is complete.
   - User must explicitly approve before updating phase documents or moving to a new step.
   - Build runs only after user request or after completing all file edits for a step.

### Progress Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked / needs discussion

### Complexity Points

1 Trivial | 2 Simple | 3 Moderate | 5 Medium | 8 Complex | 13 Very Complex | 21+ Epic

### Repository Conventions

- Keep this plan and all phase docs synchronized with the actual implementation.
- UI work follows `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` and the Blazor design-language skill.
- New tabbed surfaces use `TabbedPageShell`; `/characters` tabs use the documented layout variants.
- Form controls default to `Variant.Text`, compact grids use `Dense="true"`, and send-to actions reuse the shared `.send-to-btn` style.
- Cross-component notifications use `EventService` pub/sub.
- Character persistence should follow the JSON-backed aggregate pattern from `OdditariumSession`, `JobEntity`, and scheduler draft storage.
- Manual EF migrations must include both `[DbContext(typeof(AppDbContext))]` and `[Migration("<timestamp>_<name>")]`, and update `AppDbContextModelSnapshot.cs`.
- ComfyUI node or workflow work must probe live `object_info` schemas before implementation.

---

## Problem Statement

The app has strong prompt, workflow, image-to-prompt, and LLM tooling, but it does not yet have a dedicated way to define reusable character identity. Users currently have to manually repeat character traits in prompts, which makes iteration fragile across text-to-image, image-to-image, and edit workflows.

The requested feature is an LLM-powered Character Creator that helps users build, edit, save, and reuse characters. A character should capture persistent traits such as body and figure, face, hair, clothing, expression, marks, and other identity details. The character identity should then be transformed into a fluent, descriptive prompt segment rather than a flat tag dump.

The feature should also support image-derived initialization: users can supply a reference image, use existing vision-language model support to infer a draft character identity, then correct or enrich it through the editor.

---

## Proposed Solution

Build Character Creator as the first tab of the existing `/characters` page, backed by a global JSON-backed `CharacterEntity` aggregate. The already implemented Reference Sheet page becomes a second tab that works against the selected character and selected reference sheet instead of one global `AppState.Character` working state. The Temp custom node remains useful inspiration for rule concepts, especially JSON-driven trait lists, prompt budgets, conflict handling, preset biasing, and text cleanup. The app should not begin by porting the ComfyUI node directly, because the key value here is reusable app state, richer UI, prompt send-to integration, editable character assets, and durable reference-sheet history.

### Core Concept

A character stores structured identity traits and owns one reference sheet per source input image. A compiler service turns those traits into one or more prompt fragments:

- **Identity fragment:** stable core description used in most prompts.
- **Wardrobe fragment:** clothing/accessory choices, possibly switchable per sheet.
- **Expression fragment:** current expression, often situational.
- **Pose/application fragment:** external context applied when sending to Generate, not necessarily part of the permanent sheet.
- **Negative guard fragment:** optional anatomy or consistency guard terms, separate from positive prompt text.

The UI lets users select or create a character, edit traits through a clickable diagram and structured controls, then use the linked Reference Sheets tab to generate or review multi-view sheets for that character. The LLM helps with expansion, rewriting, summarization, conflict resolution, and image-to-sheet extraction.

### Key Decisions

| Decision                                                    | Rationale                                                                                                             |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Build inside `/characters` first                            | The existing Character Reference Sheet page already owns the right route, page shell, and future-tab surface.         |
| Add Character Creator as the first tab                      | Character selection and identity editing should anchor the page before users work on reference sheets.                |
| Store characters as global JSON-backed aggregates           | Characters are reusable across projects and have nested, evolving identity and reference-sheet state.                 |
| Keep `AppState.Character` lightweight                       | App state should track active tab, selected character, selected sheet, and pending source image, not durable sheets.  |
| A character owns reference sheets                           | Multi-view outputs belong to a selected character instead of a single global page state.                              |
| Use one reference sheet per input image per character       | The source image is the sheet's base identifier; sending the same input should load/update that sheet, not duplicate. |
| Keep option catalogs in JSON files                          | Users can expand regions, traits, and presets without rebuilding the app.                                             |
| Separate sheet identity from application context            | Pose, camera, scene, and edit intent change per generation; permanent identity should stay reusable.                  |
| Use a prompt compiler before LLM rewriting                  | Deterministic composition gives testable output and lets the LLM improve prose without owning all behavior.           |
| Treat the Temp node as inspiration, not source architecture | Its rule engine ideas are useful, but Blazor state, EF persistence, and Generate integration are app-native concerns. |
| Make create-from-image an assisted draft, not final truth   | VL models hallucinate and overfit; users need a clear review/edit pass before saving.                                 |
| Ask before routing image send-to without context            | If no character is selected, `Characters / Source Image` should prompt the user to choose or create one.              |
| Do not write image metadata links yet                       | Character/reference-sheet associations can live in the Character aggregate for now.                                   |

---

## Reference Findings

### Existing App Anchors

- `BlazorWebApp/Pages/Characters.razor` already renders the top-level `/characters` page through `TabbedPageShell`.
- `BlazorWebApp/Components/Character/CharacterReferenceSettings.razor` and `CharacterShotGrid.razor` already provide the multi-view reference-sheet experience.
- `BlazorWebApp/Models/CharacterState.cs` currently stores one global reference-sheet working state and needs to be split between durable sheet state and lightweight page selection state.
- `BlazorWebApp/Services/CharacterReferenceRunService.cs` already runs selected reference slots and persists output images; it should eventually run against a selected character/sheet context.
- `BlazorWebApp/Components/Prompts/LLM/Views/ImageToPromptView.razor` already handles image input, VL model selection, streaming, state persistence, and send-to actions.
- `BlazorWebApp/Services/VLModelService.cs` already wraps multimodal Ollama interrogation and system-prompt templates.
- `BlazorWebApp/Services/PromptSendToService.cs` queues positive prompt overrides and navigates to target workflows.
- `BlazorWebApp/Services/MediaSendToService.cs` already routes `Characters / Source Image` into `/characters`; it needs a character-selection gate.
- `BlazorWebApp/Data/AppDbContext.cs` contains the established JSON conversion pattern.
- `BlazorWebApp/Data/Entities/OdditariumSession.cs` is a close entity pattern for a JSON-backed body with timestamps.
- `BlazorWebApp/Models/AppState.cs` has nested LLM view state suitable for lightweight view preferences.

### Temp Baseline Findings

`Temp/comfyui-character-composer/` includes a custom ComfyUI node and AIO Qwen workflow. Important concepts worth carrying forward:

- JSON-driven trait catalogs similar to `tags.json`.
- UI grouping around core creative fields, character look, camera/framing, style/scene, and optional extras.
- Smart presets that bias or block trait categories.
- Conflict maps for mutually exclusive traits.
- Detail budgets that keep prompt output from becoming bloated.
- Prompt cleanup, deduplication, and bad-term filtering.
- Separate modes for text-to-image, image-to-image, character preservation, and scene preservation.

Concepts to avoid copying directly:

- Hardcoded Python UI layout as the source of truth.
- Prompt output centered around dropdown concatenation rather than editable app state.
- Workflow-specific assumptions before probing live ComfyUI schemas.

---

## Architecture Direction

### Domain Model Draft

Initial data model names are tentative and should be refined during Phase 1.

```text
CharacterEntity
  Id
  Name
  Description
  ThumbnailImageId?
  Body: CharacterBody JSON
  CreatedAt
  UpdatedAt

CharacterBody
  SchemaVersion
  Identity
  Regions: Dictionary<string, CharacterRegionState>
  Wardrobes: List<CharacterWardrobePreset>
  PromptProfiles: List<CharacterPromptProfile>
  ReferenceSheets: List<CharacterReferenceSheetBody>
  ActiveReferenceSheetId?
  Notes

CharacterReferenceSheetBody
  Id
  Label
  SourceImage: CharacterReferenceSourceImage
  Engine
  LoaderMode
  Assets
  Loras
  GlobalPositivePromptExtension
  GlobalNegativePrompt
  GlobalSeed
  SamplerOverrides
  Slots: List<CharacterReferenceSlotState>
  UseRtxUpscale
  UseCleanGpu
  FaceReplacement
  CreatedAt
  UpdatedAt

CharacterReferenceSourceImage
  ImageId?
  ImagePath?
  SourceLabel?
  SourceFingerprint
  OriginalFilename?

CharacterRegionState
  RegionId
  SelectedTraits: Dictionary<string, CharacterTraitValue>
  FreeformNotes
  ConfidenceByTrait?

CharacterTraitValue
  TraitId
  Value
  Weight?
  Source: Manual | Llm | Image | Imported
  Locked

CharacterCreatorCatalog
  SchemaVersion
  Diagrams
  Regions
  TraitDefinitions
  Presets
  ConflictRules
  PromptRules

AppState.Character
  ActiveTabIndex
  SelectedCharacterId?
  SelectedReferenceSheetId?
  PendingSourceImage?
  Sidebar/display toggles
```

`CharacterReferenceSheetBody` intentionally mirrors the existing reference-sheet state closely enough for the current page and run service to migrate incrementally. The repository should enforce one sheet per `SourceFingerprint` within each character because that uniqueness lives inside JSON rather than a normal relational column.

### Catalog Files

Suggested file locations:

- `BlazorWebApp/Data/CharacterCreator/character_regions.json`
- `BlazorWebApp/Data/CharacterCreator/character_traits.json`
- `BlazorWebApp/Data/CharacterCreator/character_prompt_rules.json`
- `BlazorWebApp/Data/CharacterCreator/character_presets.json`

Catalogs should support user expansion through external editing. The loader should validate shape and expose recoverable warnings in the UI rather than failing the whole page for one bad option.

### UI Model

The first usable UI should live on the existing `/characters` page:

- First tab: `Character`.
- Second tab: `Reference Sheets`, reusing the existing multi-view generation UI.
- Possible supporting components under `BlazorWebApp/Components/Character/Creator/`.

Recommended first-tab layout:

- Sidebar: character selector, create/rename/delete/duplicate actions, sheet list for the selected character, and create-reference-sheet entry points.
- Main content: character identity editor, graphical character diagram surface with clickable regions, region editor, prompt preview, and save state.
- Reference-sheet entry: selecting a sheet switches or enables the Reference Sheets tab and loads that sheet into the existing generator surface.
- Send-to section: Process, Workshop, and eligible workflows via `PromptSendToService` after the deterministic compiler exists.

`Characters / Source Image` send-to should prompt the user to choose or create a character when no character is selected. When a character is selected, the app should create or load the one reference sheet matching the source image fingerprint.

The diagram should start pragmatic. A generic SVG or CSS-layered anatomical map is acceptable for MVP if it is accessible, keyboard-navigable, and maps cleanly to region IDs. Advanced zoom diagrams can be added incrementally.

### Prompt Compilation Model

The compiler should produce deterministic draft text first:

1. Normalize selected traits by region and semantic category.
2. Apply conflict rules and locked-trait rules.
3. Apply detail budget by target mode: concise, balanced, rich.
4. Build a structured internal prompt model.
5. Render fluent prose fragments through templates.
6. Optionally send the deterministic draft to the selected LLM for rewrite, expansion, or style adaptation.

This avoids relying on the LLM for every run and makes service tests straightforward.

### Create From Image Model

The image flow should reuse `VLModelService` but with a character-sheet-specific system prompt:

1. User selects or drops an image.
2. User chooses a VL model.
3. Service asks for structured JSON matching the character identity schema.
4. Parser validates and normalizes the response.
5. UI displays a review state with confidence/source markers.
6. User edits and saves the sheet.

The raw VL output should be inspectable for debugging, but the saved sheet should store normalized structured fields.

### Generate Integration

Applying a character prompt should be a deliberate action, not a hidden global mutation:

- Send compiled prompt to current workflow positive prompt.
- Send compiled prompt to a selected T2I/I2I workflow through `PromptSendToService`.
- Optionally append, prepend, or replace the current prompt.
- Optionally include pose/application context at send time.
- Future integration may add a Generate-page fragment picker for selecting a saved character directly inside prompt controls.

---

## Resolved Clarifications

| Decision                         | Resolution                                                                                                   |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Page placement                   | Character Creator lives on `/characters`, as the first tab before Reference Sheets.                          |
| Character scope                  | Characters are global and are not linked to the current project.                                             |
| Reference sheet ownership        | A character can have several reference sheets. Each sheet belongs to one character.                          |
| Reference sheet uniqueness       | Each character has a single reference sheet per input image/source fingerprint.                              |
| Image send-to without selection  | Ask the user to choose or create a character before accepting the image as a Character source.               |
| Image metadata mapping           | No image metadata mapping is required for now; character/sheet associations live in the Character aggregate. |
| Existing Reference Sheet feature | Keep it as the second tab and migrate it to load/save the selected character's selected reference sheet.     |
| First region schema              | Use a human/anime-character-first MVP with catalog-defined extension points for fantasy or non-human bodies. |
| Head zoom hierarchy              | Model head zoom as catalog-defined subregions under `head`; treat hair as a high-priority identity anchor.   |
| Prompt output default            | Default to deterministic natural prose; defer tag-style output as an optional future profile.                |
| Import/export timing             | Defer import/export to the catalog expansion phase after the editor is usable.                               |

## Later Phase Clarifications

These are discussion items for later phases, not blockers for Phase 2 persistence:

1. Should pose be stored as reusable pose presets separate from character sheets?
2. Should create-from-image use only local Ollama/VL models, or can later phases integrate ComfyUI/Qwen image-edit workflows too?
3. How much negative prompt assistance should be bundled with a character sheet versus left to workflow templates?

---

## Stress Points

| Risk                                           | Impact                                 | Mitigation                                                                                              |
| ---------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| UI diagram becomes too ambitious too early     | Delays the whole feature               | Ship an accessible MVP map first; add zoom diagrams by region in a later phase.                         |
| Catalog schema changes frequently              | Persistence churn and broken user data | Version catalogs and sheet bodies from Phase 1. Add migration/normalization service.                    |
| Prompt output becomes a tag dump               | Misses the main feature goal           | Use deterministic compiler plus prose templates; LLM rewrite is optional and reviewable.                |
| VL image extraction hallucinates traits        | Bad saved sheets                       | Treat image extraction as draft with source/confidence markers and required review.                     |
| Character identity mixes with scene/pose       | Sheets become hard to reuse            | Separate identity, wardrobe, expression, pose, and application context in the model.                    |
| JSON aggregate grows too opaque                | Hard debugging                         | Add import/export, schema validation, and repository-level tests.                                       |
| Workflow integration becomes workflow-specific | Fragile across T2I/I2I                 | Use `PromptSendToService` for prompt text first; add workflow fragments only after MVP.                 |
| User-editable JSON catalogs can break load     | Page failures                          | Validate catalogs, log warnings, and fall back to built-in minimal defaults.                            |
| Manual EF migration mistakes                   | Missing tables at runtime              | Follow persistence playbook exactly, including attributes and snapshot update.                          |
| Existing reference state is global             | Wrong sheet can be overwritten         | Move durable sheet state into `CharacterEntity.Body.ReferenceSheets`; keep app state as selection only. |
| Sheet uniqueness lives inside JSON             | Duplicate sheets for one source image  | Repository enforces one sheet per source fingerprint when adding or accepting source images.            |

---

## Implementation Phases

### Phase 1: Discovery And Schema Design

**Objective:** Define the global character aggregate, reference-sheet ownership model, region taxonomy, catalog JSON shape, prompt output profiles, and `/characters` MVP UI scope.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

1. Review Temp node categories and translate useful concepts into app-native schema terms.
2. Define `CharacterEntity`, `CharacterBody`, and `CharacterReferenceSheetBody` boundaries.
3. Define source-image fingerprint behavior and one-sheet-per-input rules.
4. Define MVP region hierarchy: full body, head, torso, arms, hands, legs, feet, clothing, marks, expression.
5. Define zoom hierarchy for head: face shape, eyes, brows, nose, mouth, ears, hair, makeup, scars/marks.
6. Draft JSON catalog schemas with version fields, labels, option types, presets, conflicts, and prompt rules.
7. Decide first prompt profiles: identity-only, identity-plus-wardrobe, image-edit preservation, rich portrait.
8. Write `PHASE_1.md` with final schema decisions before implementation.

#### Success Criteria

- The schema separates permanent identity from per-generation pose/context.
- Character selection and reference-sheet selection are explicitly modeled.
- Existing Reference Sheet state can be migrated into a selected sheet body.
- Catalogs can describe selectable values, freeform fields, and region relationships.
- Open questions that affect implementation are either resolved or explicitly blocked.

---

### Phase 2: Character Persistence And Catalog Loader

**Objective:** Add persistent global characters with embedded reference sheets and load user-editable catalog JSON files.
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps

1. Add `CharacterEntity`, `CharacterBody`, and reference-sheet body model types.
2. Register JSON conversion in `AppDbContext`.
3. Add repository/service using `IDbContextFactory<AppDbContext>`.
4. Hand-author EF migration and update model snapshot using the persistence checklist.
5. Add one-sheet-per-source enforcement in the repository.
6. Add catalog loader service with validation warnings and fallback defaults.
7. Add focused repository and catalog-loader tests.

#### Success Criteria

- Characters can be created, updated, listed, duplicated, and deleted.
- Reference sheets can be created, updated, selected, and deleted inside a character.
- A character cannot silently create duplicate reference sheets for the same source image.
- In-place JSON body mutations are persisted correctly.
- Bad catalog entries surface warnings without taking down the view.

---

### Phase 3: `/characters` Selection And Reference Sheet Integration

**Objective:** Add the first `Character` tab, selection flow, and migrate the existing Reference Sheet tab to load/save the selected character sheet.
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps

1. [x] Split `AppState.Character` into lightweight page state and selected character/sheet identifiers.
2. [x] Add first `Character` tab before `Reference Sheets` in `Characters.razor`.
3. [x] Build character selector/create/rename/delete UI using documented layout patterns.
4. [x] Add reference-sheet list and create/load behavior for the selected character.
5. [x] Refactor existing `CharacterReferenceSettings`, `CharacterShotGrid`, and run actions to work against the selected sheet state.
6. [x] Update `MediaSendToService` so `Characters / Source Image` asks for a character when none is selected, then creates or loads the matching sheet.
7. [x] Add focused state, repository, send-to, and run-service tests.

#### Success Criteria

- `/characters` opens with a Character tab first and Reference Sheets second.
- The user can select a character and see all sheets available for that character.
- The Reference Sheets tab loads the selected sheet, including source image, settings, slots, and output references.
- Sending an image to Characters never overwrites an unrelated sheet.

---

### Phase 4: Deterministic Prompt Compiler

**Objective:** Convert structured sheets into fluent, reusable prompt fragments without requiring an LLM call.
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps

1. [x] Add prompt compiler models for identity, wardrobe, expression, application context, and negative guards.
2. [x] Implement trait normalization, ordering, deduplication, conflict handling, and detail budgets.
3. [x] Add template-driven prose rendering by output profile.
4. [x] Add append/prepend/replace composition helpers for prompt send-to flows.
5. [x] Add focused tests for compiler output, conflicts, locked traits, and detail budgets.

#### Success Criteria

- A simple sheet becomes a coherent descriptive sentence or paragraph.
- Output is stable and testable without LLM availability.
- Compiler can render different prompt profiles from the same sheet.

---

### Phase 5: Character Creator UI MVP

**Objective:** Build the first usable character editor with generic body diagram, region editor, and live prompt preview.
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps

1. [x] Create first-tab Character Creator components under `BlazorWebApp/Components/Character/Creator/`.
2. [x] Implement dirty-state handling and save/reload behavior against `CharacterEntity`.
3. [x] Implement clickable MVP body diagram with region selection.
4. [x] Implement region editor from catalog definitions.
5. [x] Show live compiled prompt preview with profile selector.
6. [x] Connect selected reference sheets as visual/source context without making them mandatory.
7. [x] Persist lightweight view state in `AppState.Character`.

#### Success Criteria

- User can create and edit a character without leaving `/characters`.
- Clicking a diagram region updates the editor panel.
- Prompt preview updates predictably as traits change.
- UI follows the documented design language and does not add ad hoc shell spacing.

---

### Phase 6: LLM Assisted Authoring

**Objective:** Add LLM helpers that turn terse selections into polished descriptions and suggest missing traits.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

1. [x] Add character-specific system prompt templates.
2. [x] Add service methods for expand, summarize, rewrite, and fill-missing operations.
3. [x] Ensure LLM suggestions return structured changes, not only prose.
4. [x] Let users preview/accept/reject proposed trait updates.
5. [x] Add tests around response parsing and fallback behavior.

#### Success Criteria

- LLM can turn simple selections into richer sheet descriptions.
- Suggestions never silently overwrite locked/manual values.
- Failed or malformed LLM responses leave the sheet unchanged.

#### Validation

- `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterLlmAuthoringServiceTests --no-restore`: 4 passed.
- `process: build`: passed with known NuGet warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`).

---

### Phase 7: Create From Image

**Objective:** Reuse VL model support to create an editable draft character identity from an image.
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps

1. [x] Add character-identity interrogation prompt templates for a small wrapper service.
2. [x] Add image input UI based on the existing `ImageToPromptView` pattern.
3. [x] Parse VL JSON into normalized sheet regions with source/confidence metadata.
4. [x] Add review UI for accepting, editing, and discarding inferred traits.
5. [x] Support source image thumbnail/reference metadata on saved sheets.
6. [x] Add tests for parser normalization and invalid output handling.

#### Success Criteria

- A reference image can produce a draft sheet.
- User can review inferred traits before saving.
- Raw VL response remains inspectable during the session.

#### Validation

- File diagnostics for touched service, model, page, component, CSS, and test files: no errors.
- `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterImageDraftServiceTests --no-restore`: 4 passed.
- `process: build`: passed with known NuGet warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`).

---

### Phase 8: Prompt Application And Workflow Integration

**Objective:** Apply saved character prompts to prompt fields for T2I and I2I/edit workflows.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

1. [x] Add send-to actions using `PromptSendToService` for eligible workflows.
2. [x] Add apply modes: append, prepend, replace, and copy.
3. [x] Add application context controls for pose, expression override, wardrobe preset, and edit intent.
4. [x] Add optional negative guard output and routing decision.
5. [x] Consider a Generate-page fragment or picker only after the `/characters` flow is stable.

#### Success Criteria

- User can send compiled character prompt to current or selected workflows.
- T2I and I2I use cases are explicit in the UI.
- Pose/context can be applied without permanently changing the saved sheet.

#### Validation

- File diagnostics for touched service, model, page, component, CSS, and test files: no errors.
- Focused `runTests` for `PromptSendToServiceTests.cs` and `CharacterPromptCompilerServiceTests.cs`: 7 passed.
- `process: build`: passed with known NuGet warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`).

---

### Phase 9: Catalog Expansion And Import/Export

**Objective:** Make the system easy to extend outside the app.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

1. [x] Add import/export for characters and reference sheets as JSON.
2. [x] Add validation report UI for user-edited catalogs.
3. [x] Add catalog reload action.
4. [x] Add sample catalog documentation and examples.
5. [x] Defer optional LLM tool for generating new trait options until after catalog editing is exercised.

#### Success Criteria

- Users can back up and share characters and reference sheets.
- Users can edit catalog JSON and understand validation issues.
- Adding new trait options does not require rebuilding the app.

#### Validation

- File diagnostics for touched service, model, page, CSS, test, and project files: no new errors; known package warning noise remains on the project file.
- Focused `runTests` for `CharacterImportExportServiceTests.cs` and `CharacterCreatorCatalogServiceTests.cs`: 4 passed from the test runner.
- `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterImportExportServiceTests --no-restore`: 3 passed.
- `process: build`: passed with known NuGet warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`).

---

### Phase 10: Advanced Diagram Zoom And Polishing

**Objective:** Expand the Sims-like editor experience with region zoom diagrams and richer interaction patterns.
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps

1. [x] Add head-specific zoom diagram and navigation back to full body.
2. [x] Add additional zoom diagrams for hands, torso/clothing, and marks if warranted.
3. [x] Add keyboard navigation and focus affordances for diagram regions.
4. [x] Add comparison view for before/after compiled prompt changes.
5. [x] Add final documentation and update architecture notes if new UI patterns are introduced.

#### Success Criteria

- Region zoom improves editing without blocking core workflows.
- Diagram remains accessible and responsive.
- Documentation is sufficient to resume or extend the feature later.

#### Validation

- File diagnostics for touched page/component/CSS files: no errors.
- `process: build`: passed with known repository warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`, and unrelated nullable/analyzer warnings).

---

## Validation Strategy

Use the narrowest useful validation for each phase:

- Catalog and compiler phases: focused service/model tests under `BlazorWebApp.Tests/Services/` or a new nearby test folder.
- Persistence phase: repository tests using the existing test fixtures and SQLite/EF patterns, including one-sheet-per-source behavior.
- UI phase: file diagnostics plus focused project build when no component test harness exists.
- Reference-sheet integration phase: focused tests for character/sheet selection, media send-to routing, and run-service persistence back into the selected sheet.
- Workflow integration: tests around send-to composition where practical; otherwise focused build and manual runtime checklist.
- Create-from-image: parser tests using sample VL responses; runtime VL calls remain environment-dependent and should be called out as residual risk.

Full build is reserved for phase completion or when narrow checks are insufficient.

---

## Success Criteria For The Whole Feature

- Users can save reusable global characters with structured traits.
- Users can edit characters through a graphical region-based interface.
- Users can select a character and load all reference sheets available for that character.
- Each character has at most one reference sheet per input image.
- Prompt output is descriptive prose, not a raw dump of tags.
- Users can apply a character prompt to T2I and I2I/edit prompt fields.
- Users can initialize a draft sheet from an image using existing VL model support.
- Trait options and region definitions can be expanded through JSON files.
- The implementation follows existing `/characters`, persistence, event, and UI conventions.

---

## Deferred Ideas

- Multiple characters in a single prompt with relationship/interaction modeling.
- Character consistency scoring across generated images.
- ComfyUI custom node integration or workflow fragment after native app flow is stable.
- Pose library with reference images or control images.
- Resource-library integration for clothing, LoRA, embedding, or reference packs.
- Per-project character roster and campaign/world organization.
