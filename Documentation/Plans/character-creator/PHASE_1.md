# Phase 1 - Discovery And Schema Design

## Status

Complete.

## Objective

Define the global character aggregate, reference-sheet ownership model, region taxonomy, catalog JSON shape, prompt output profiles, and `/characters` MVP UI scope before implementation begins.

## Approved Direction

- Character Creator belongs on the existing `/characters` page, not inside LLM Tools.
- The first tab will be `Character`; the existing multi-view generator becomes a later `Reference Sheets` tab.
- Characters are global and are not linked to projects.
- A character owns multiple reference sheets.
- Each character has only one reference sheet per input image/source fingerprint.
- If an image is sent to `Characters / Source Image` without a selected character, the UI must ask the user to choose or create one.
- No image metadata mapping is required for now; character/sheet associations live in the character aggregate.

## Step Checklist

- [x] Step 1: Resolve page placement and ownership direction. `[2 pts]`
- [x] Step 2: Resolve character scope, sheet uniqueness, and media send-to behavior. `[2 pts]`
- [x] Step 3: Define `CharacterEntity`, `CharacterBody`, and `CharacterReferenceSheetBody` boundaries. `[3 pts]`
- [x] Step 4: Define MVP region hierarchy. `[3 pts]`
- [x] Step 5: Define head zoom hierarchy. `[2 pts]`
- [x] Step 6: Draft catalog JSON schema. `[5 pts]`
- [x] Step 7: Decide first prompt output profiles. `[2 pts]`
- [x] Step 8: Finalize schema decisions and prepare Phase 2 execution plan. `[3 pts]`

## Draft Domain Model

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

AppState.Character
  ActiveTabIndex
  SelectedCharacterId?
  SelectedReferenceSheetId?
  PendingSourceImage?
  Sidebar/display toggles
```

## Reference Sheet Integration Notes

The current reference-sheet implementation stores one global working state in `AppState.Character`. Phase 2 and Phase 3 should move durable reference-sheet state into `CharacterEntity.Body.ReferenceSheets` while keeping app state focused on the current page selection.

`CharacterReferenceSheetBody` should initially mirror the current `AppStateCharacter` reference-generation fields closely enough to keep the existing workflow composers and run service migration incremental.

The repository should enforce one sheet per `SourceFingerprint` inside each character. Because the sheet list lives in JSON, this is application-level enforcement rather than a database unique index.

## Aggregate Boundaries

### `CharacterEntity`

`CharacterEntity` is the persistence root. It owns the character name, description, thumbnail pointer, timestamps, and serialized `CharacterBody`. Characters are global app assets and should not include `ProjectId`, folder ownership, or gallery-scoped fields.

Phase 2 should store only denormalized fields needed for listing outside JSON:

- `Id`
- `Name`
- `Description`
- `ThumbnailImageId?`
- `CreatedAt`
- `UpdatedAt`
- `Body`

All evolving identity, reference-sheet, and editor details belong inside `Body`.

### `CharacterBody`

`CharacterBody` owns durable character identity. It should not store transient page UI state such as active tab, expanded panels, unsaved pending source image data, or currently running slot status except where the status is part of a saved reference-sheet output history.

`CharacterBody` owns:

- Schema version and future normalization markers.
- Structured identity fields and region states.
- Wardrobe and prompt-profile collections.
- Embedded `ReferenceSheets` list.
- `ActiveReferenceSheetId?` as a convenience for restoring the last selected sheet for that character.
- Notes and user-authored freeform identity context.

### `CharacterReferenceSheetBody`

`CharacterReferenceSheetBody` owns one multi-view generation setup for one source image. It is not a separate EF entity in the first implementation; it is embedded in `CharacterBody.ReferenceSheets`.

It owns the durable subset of the existing reference-sheet state:

- Source image identity and label.
- Engine and loader configuration.
- Asset selections and LoRAs.
- Global positive/negative guidance.
- Seed and sampler settings.
- Slot list, including custom slots, prompt extensions, generated output ids/paths, and dimensions.
- Qwen-specific toggles such as RTX upscale, clean GPU, and face replacement settings.
- Created/updated timestamps.

It should not own global page state, character selection state, modal state, transient base64 source data, or workflow run progress that has not been saved back after a run.

### `AppState.Character`

After persistence exists, `AppState.Character` should become page/session state only:

- `ActiveTabIndex`
- `SelectedCharacterId?`
- `SelectedReferenceSheetId?`
- `PendingSourceImage?`
- Sidebar/display toggles that do not belong to a durable character

This split prevents one global reference-sheet draft from accidentally overwriting a different character's sheet.

## Existing State Mapping

The current `AppStateCharacter` fields should map as follows during Phase 2 and Phase 3 planning:

| Current field                   | Target owner                                  | Notes                                                                                 |
| ------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------------------- |
| `ActiveTabIndex`                | `AppState.Character`                          | Page-level state only.                                                                |
| `SidebarCollapsed`              | `AppState.Character`                          | Page-level display preference.                                                        |
| `Engine`                        | `CharacterReferenceSheetBody`                 | Sheet-specific because different source sheets can use different engines.             |
| `LoaderMode`                    | `CharacterReferenceSheetBody`                 | Sheet-specific Qwen configuration.                                                    |
| `Assets`                        | `CharacterReferenceSheetBody`                 | Sheet-specific asset snapshot.                                                        |
| `Loras`                         | `CharacterReferenceSheetBody`                 | Sheet-specific generation setup.                                                      |
| `SourceImage`                   | `CharacterReferenceSheetBody.SourceImage`     | Persist durable ids/paths/fingerprint; keep transient `ImageDataUri` out of the body. |
| `GlobalPositivePromptExtension` | `CharacterReferenceSheetBody`                 | Sheet-specific generation guidance.                                                   |
| `GlobalNegativePrompt`          | `CharacterReferenceSheetBody`                 | Sheet-specific generation guidance.                                                   |
| `GlobalSeed`                    | `CharacterReferenceSheetBody`                 | Sheet-specific reproducibility setting.                                               |
| `SamplerOverrides`              | `CharacterReferenceSheetBody`                 | Sheet-specific run tuning.                                                            |
| `Slots`                         | `CharacterReferenceSheetBody`                 | Includes generated output references and custom slot definitions.                     |
| `ShowEngineSettings`            | `AppState.Character` or component-local state | Display preference, not durable sheet data.                                           |
| `ShowAdvancedSettings`          | `AppState.Character` or component-local state | Display preference, not durable sheet data.                                           |
| `UseRtxUpscale`                 | `CharacterReferenceSheetBody`                 | Sheet-specific Qwen run behavior.                                                     |
| `UseCleanGpu`                   | `CharacterReferenceSheetBody`                 | Sheet-specific advanced run behavior.                                                 |
| `FaceReplacement`               | `CharacterReferenceSheetBody`                 | Sheet-specific Qwen enhancement behavior.                                             |
| `ReusableDependencyImagePaths`  | transient run state                           | Never persisted; rebuilt per run.                                                     |

## Repository Contract Draft

Phase 2 should introduce a character repository/service with operations shaped around the aggregate:

- List summaries for selector UI.
- Get full character by id.
- Create character with default `CharacterBody`.
- Update character metadata and body.
- Delete character.
- Add or get reference sheet by source fingerprint.
- Update reference sheet in place and force JSON body reserialization.
- Delete reference sheet from a character.
- Set active reference sheet for a character.

Write paths that mutate nested body state must mark the converted `Body` property as modified before saving.

## Source Fingerprint Decision

The preferred source fingerprint order for implementation is:

1. Saved image id when the source came from an existing app image record.
2. Content hash when image bytes are available from upload/send-to.
3. Normalized resolved file path as fallback for local files without an image id or bytes.

The fingerprint should include a prefix describing the source type, such as `image:123`, `sha256:<hash>`, or `path:<normalized-path>`, so unrelated identifier spaces cannot collide.

## MVP Region Hierarchy

The MVP region schema should be human/anime-character first because the existing reference-sheet workflow, prompt language, and UI expectations are centered on character portrait/body generation. The schema should still avoid hardcoding gendered assumptions and should allow later catalog extensions for fantasy or non-human anatomy.

### Root Regions

| Region id    | Label      | Purpose                                                                          | MVP input style                                     |
| ------------ | ---------- | -------------------------------------------------------------------------------- | --------------------------------------------------- |
| `body`       | Body       | Overall body type, proportions, posture baseline, silhouette, and anatomy notes. | Selectable traits plus freeform notes.              |
| `head`       | Head       | Face, hair, ears, makeup, scars, and facial identity details.                    | Zoom region with nested subregions.                 |
| `torso`      | Torso      | Chest, waist, shoulders, back, and torso-specific marks or body traits.          | Selectable traits plus freeform notes.              |
| `arms`       | Arms       | Arm build, length impression, shoulders-to-wrist details, sleeves/armwear.       | Selectable traits plus left/right notes if needed.  |
| `hands`      | Hands      | Hand shape, nails, gloves, jewelry, and distinctive hand details.                | Selectable traits plus freeform notes.              |
| `legs`       | Legs       | Leg build, stance impression, hips/thighs/calves, stockings/legwear.             | Selectable traits plus freeform notes.              |
| `feet`       | Feet       | Footwear, barefoot details, socks, and visible foot/ankle details.               | Selectable traits plus freeform notes.              |
| `clothing`   | Clothing   | Default outfit, accessories, material, color palette, and style notes.           | Wardrobe-aware traits plus freeform notes.          |
| `marks`      | Marks      | Tattoos, scars, freckles, moles, birthmarks, piercings, and recurring symbols.   | Location-aware entries that can reference regions.  |
| `expression` | Expression | Default facial expression, emotional baseline, gaze, and expression presets.     | Selectable traits plus reusable expression presets. |

### Cross-Region Categories

Some traits apply across multiple anatomical regions and should be represented as categories rather than forced into one region:

| Category id      | Purpose                                                                                 |
| ---------------- | --------------------------------------------------------------------------------------- |
| `identity_core`  | Stable one-line identity: apparent age range, archetype, species/person type, presence. |
| `style_surface`  | Art-relevant visible finish: skin texture, complexion, material cues, detail density.   |
| `color_palette`  | Hair, eye, clothing, accessory, and accent colors that should stay consistent.          |
| `asymmetry`      | Left/right differences such as one covered eye, mismatched gloves, or scars.            |
| `accessories`    | Glasses, jewelry, hats, bags, props worn on the body.                                   |
| `negative_guard` | Optional consistency guards that should compile separately from the positive prompt.    |

### Region State Rules

- Every region supports `SelectedTraits`, `FreeformNotes`, and optional source/confidence metadata.
- Region ids must be stable across catalog revisions so persisted characters survive catalog edits.
- Traits can declare whether they are identity-stable, wardrobe-specific, expression-specific, or application/context-specific.
- Wardrobe traits belong to the character when they describe the default outfit, but alternate outfits should live in wardrobe presets.
- Pose, camera, scene, and generation intent do not belong to permanent identity regions.
- Marks should support optional `RegionId` references so a scar can be attached to `head`, `torso`, `arms`, or a nested head subregion later.
- The first implementation can render all root regions in a single editor list even before the clickable diagram is finished.

### Future Extension Points

The catalog schema should be able to add non-human or fantasy regions later without changing the entity shape. Examples include `wings`, `tail`, `horns`, `extra_arms`, `ears_animal`, `fur_pattern`, or `mechanical_parts`. These should be catalog-defined regions rather than nullable properties on `CharacterBody`.

## Head Zoom Hierarchy

The `head` region should support a zoom/edit mode because facial identity has the highest impact on character reuse and reference-sheet quality. The MVP can render this as a focused region list before a graphical zoom diagram exists.

### Head Subregions

| Region id    | Label      | Purpose                                                                                   | MVP input style                                      |
| ------------ | ---------- | ----------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| `face_shape` | Face Shape | Overall face silhouette, jaw, cheeks, chin, forehead, and age impression.                 | Selectable traits plus freeform identity notes.      |
| `eyes`       | Eyes       | Eye shape, size, color, iris/pupil notes, eyelashes, eyelids, and gaze default.           | Selectable traits plus left/right asymmetry support. |
| `brows`      | Brows      | Brow shape, thickness, color, angle, and expressive baseline.                             | Selectable traits plus expression influence notes.   |
| `nose`       | Nose       | Nose size, bridge, tip, nostrils, and stylization level.                                  | Selectable traits plus freeform notes.               |
| `mouth`      | Mouth      | Lip shape, mouth size, teeth/fangs visibility, smile baseline, and lipstick.              | Selectable traits plus expression link.              |
| `ears`       | Ears       | Ear shape, size, visible piercings, elf/animal hints when catalog extensions exist.       | Selectable traits plus accessory links.              |
| `hair`       | Hair       | Hairstyle, bangs, length, volume, texture, color, roots/highlights, and hair accessories. | Structured traits plus freeform notes.               |
| `makeup`     | Makeup     | Eyeliner, eyeshadow, blush, lipstick, face paint, and cosmetic styling.                   | Optional traits; hidden/collapsed by default.        |
| `head_marks` | Marks      | Face/head scars, freckles, moles, tattoos, birthmarks, and recurring symbols.             | Location-aware entries tied to head subregions.      |

### Head State Rules

- `head` remains the parent region; subregions use ids nested by catalog relationship rather than hardcoded object properties.
- `hair` should be identity-stable by default because reference sheets depend heavily on hair silhouette and color.
- `expression` remains a root region, but `eyes`, `brows`, and `mouth` can declare expression-sensitive traits.
- Asymmetry must be supported for eyes, brows, ears, marks, and hair details such as one covered eye.
- Makeup and head marks should be optional so the default editor does not feel overloaded for simple characters.
- Head subregions can contribute to both deterministic prompt output and Qwen reference-sheet preservation prompts.

### Head Prompt Ordering

When compiling identity prose, head traits should render in this stable order:

1. Face shape and overall facial impression.
2. Eyes and gaze.
3. Brows and expression baseline.
4. Nose and mouth.
5. Ears when visually important.
6. Hair as a high-priority identity anchor.
7. Makeup and marks.

Hair may be promoted earlier for image-edit preservation profiles because hairstyle is often the strongest visual anchor in generated references.

## Catalog JSON Schema Draft

Catalogs should be user-editable JSON files with schema versions, stable ids, and recoverable validation warnings. The loader should merge built-in fallback definitions with user files and report invalid entries without taking down the `/characters` page.

### File Split

| File                                                             | Purpose                                                                       |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `BlazorWebApp/Data/CharacterCreator/character_regions.json`      | Region tree, labels, UI grouping, diagram metadata, and input modes.          |
| `BlazorWebApp/Data/CharacterCreator/character_traits.json`       | Trait definitions, option lists, value types, source rules, and prompt terms. |
| `BlazorWebApp/Data/CharacterCreator/character_prompt_rules.json` | Prompt profiles, render ordering, templates, budgets, and negative guards.    |
| `BlazorWebApp/Data/CharacterCreator/character_presets.json`      | Character archetype, wardrobe, expression, and starter preset definitions.    |

### Shared Catalog Envelope

Every catalog file should use the same outer envelope:

```json
{
  "schemaVersion": 1,
  "catalogId": "character-regions",
  "displayName": "Character Regions",
  "items": []
}
```

Loader rules:

- `schemaVersion` is required.
- `catalogId` is required and must match the expected file role.
- Unknown top-level properties are ignored with no warning.
- Invalid items are skipped with a warning that includes file name and item id when available.
- Duplicate ids in one file keep the first valid item and warn for later duplicates.

### Region Definition

```json
{
  "id": "head",
  "label": "Head",
  "parentId": null,
  "group": "identity",
  "sortOrder": 20,
  "inputMode": "zoom",
  "supportsFreeformNotes": true,
  "supportsAsymmetry": false,
  "diagram": {
    "diagramId": "body-front",
    "target": "head"
  },
  "promptPriority": 20,
  "tags": ["identity", "face"]
}
```

Important fields:

| Field                   | Required | Notes                                                                              |
| ----------------------- | -------- | ---------------------------------------------------------------------------------- |
| `id`                    | Yes      | Stable persisted id.                                                               |
| `label`                 | Yes      | User-facing label.                                                                 |
| `parentId`              | No       | Creates nested regions such as head subregions.                                    |
| `group`                 | No       | Suggested UI group such as `identity`, `wardrobe`, `expression`, or `guards`.      |
| `sortOrder`             | No       | Stable display ordering.                                                           |
| `inputMode`             | No       | `single`, `multi`, `freeform`, `composite`, or `zoom`.                             |
| `supportsFreeformNotes` | No       | Defaults to true for MVP.                                                          |
| `supportsAsymmetry`     | No       | Allows left/right or side-specific details.                                        |
| `diagram`               | No       | Optional diagram binding; not required for the first list-based editor.            |
| `promptPriority`        | No       | Used by deterministic compiler ordering.                                           |
| `tags`                  | No       | Loader-visible classification; does not need to be persisted on character regions. |

### Trait Definition

```json
{
  "id": "hair.length",
  "regionId": "hair",
  "label": "Hair Length",
  "valueType": "option",
  "scope": "identity",
  "allowCustomValue": true,
  "lockedByDefault": false,
  "options": [
    { "id": "short", "label": "Short", "prompt": "short hair" },
    {
      "id": "shoulder_length",
      "label": "Shoulder Length",
      "prompt": "shoulder-length hair"
    },
    { "id": "long", "label": "Long", "prompt": "long hair" }
  ],
  "promptTerms": ["hair length"],
  "conflictsWith": []
}
```

Important fields:

| Field              | Required | Notes                                                                       |
| ------------------ | -------- | --------------------------------------------------------------------------- |
| `id`               | Yes      | Stable trait id, usually namespaced by region or category.                  |
| `regionId`         | Yes      | Region that owns the trait.                                                 |
| `label`            | Yes      | User-facing label.                                                          |
| `valueType`        | Yes      | `option`, `multiOption`, `text`, `number`, `weight`, `color`, or `boolean`. |
| `scope`            | No       | `identity`, `wardrobe`, `expression`, `application`, or `negativeGuard`.    |
| `allowCustomValue` | No       | Allows user-entered values outside predefined options.                      |
| `lockedByDefault`  | No       | Default lock behavior for assisted edits.                                   |
| `options`          | No       | Required for option-based value types.                                      |
| `promptTerms`      | No       | Fallback render tokens when no option prompt exists.                        |
| `conflictsWith`    | No       | Trait or option ids that should not coexist.                                |

### Preset Definition

```json
{
  "id": "portrait.neutral_human",
  "label": "Neutral Human Portrait",
  "presetType": "starter",
  "description": "Basic human/anime portrait starting point.",
  "traitValues": [
    {
      "regionId": "expression",
      "traitId": "expression.default",
      "value": "neutral",
      "locked": false
    }
  ]
}
```

Preset types should initially include:

- `starter`
- `wardrobe`
- `expression`
- `negativeGuard`
- `promptProfile`

### Prompt Rule Definition

```json
{
  "profileId": "identity_plus_wardrobe",
  "label": "Identity + Wardrobe",
  "detailBudget": "balanced",
  "regionOrder": ["identity_core", "body", "head", "hair", "clothing", "marks"],
  "template": "{identity}. {body}. {head}. {wardrobe}. {marks}",
  "negativeTemplate": "{negative_guard}",
  "rewriteAllowed": true
}
```

Prompt rule requirements:

- Profiles must have stable `profileId` values.
- Region order must reference known region ids or cross-region category ids.
- Unknown region ids should warn but not block catalog loading.
- Detail budget values should start with `concise`, `balanced`, and `rich`.
- `rewriteAllowed` controls whether the selected LLM can polish the deterministic draft.

### Conflict Rule Definition

Conflict rules can live in `character_prompt_rules.json` or inside trait definitions. The loader should normalize both into one in-memory list.

```json
{
  "id": "hair.length.exclusive",
  "severity": "warning",
  "mode": "mutuallyExclusive",
  "items": ["hair.length.short", "hair.length.long"]
}
```

Conflict fields:

| Field      | Required | Notes                                                         |
| ---------- | -------- | ------------------------------------------------------------- |
| `id`       | Yes      | Stable warning id.                                            |
| `severity` | No       | `info`, `warning`, or `error`; MVP should not block saves.    |
| `mode`     | Yes      | `mutuallyExclusive`, `preferLocked`, `dedupe`, or `suppress`. |
| `items`    | Yes      | Trait ids, option ids, or prompt terms involved.              |

### Loader Output Contract

The catalog loader should return:

```text
CharacterCreatorCatalog
  SchemaVersion
  Regions
  TraitDefinitions
  Presets
  PromptRules
  ConflictRules
  Warnings
```

Warnings should be visible in the Character tab, likely as a compact validation report or warning chip, but should not prevent users from opening existing characters.

## Initial Prompt Profiles

Prompt output should default to natural prose because the main feature goal is reusable descriptive character identity, not a raw tag dump. Tag-style output can be added later as a selectable profile if specific workflows need it.

### MVP Profiles

| Profile id                | Label               | Detail budget | Includes                                                                               | Primary use case                                      |
| ------------------------- | ------------------- | ------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| `identity_only`           | Identity Only       | Concise       | Identity core, body, head, hair, marks.                                                | Reusable prompt prefix for general T2I/I2I workflows. |
| `identity_plus_wardrobe`  | Identity + Wardrobe | Balanced      | Identity, body, head, default wardrobe, marks.                                         | Default send-to prompt and preview profile.           |
| `image_edit_preservation` | Image Edit Preserve | Balanced      | High-priority identity anchors, hair, face, outfit, negative guards.                   | I2I/edit workflows where consistency matters.         |
| `rich_portrait`           | Rich Portrait       | Rich          | Full identity, facial detail, hair, outfit, expression baseline, visual style surface. | Portrait or character showcase prompting.             |
| `negative_guard`          | Negative Guard      | Concise       | Anatomy, identity drift, outfit drift, and artifact guard terms.                       | Separate negative prompt routing when supported.      |

### Profile Rules

- `identity_plus_wardrobe` is the default positive prompt preview profile.
- Profiles render deterministic prose first; LLM rewrite is optional and never required for prompt generation.
- `image_edit_preservation` should prioritize face, hair, outfit, and unique marks before broader body prose.
- `negative_guard` compiles separately and should not be appended to positive prompt text.
- Prompt profiles should support append/prepend/replace composition later, but composition is not part of Phase 1.
- Tag-style output is deferred until a workflow or user flow clearly benefits from it.

### Default Render Order

For positive natural-prose profiles, use this initial order unless a profile overrides it:

1. Identity core.
2. Body and proportions.
3. Face and head details.
4. Hair.
5. Wardrobe and accessories.
6. Marks, asymmetry, and distinctive details.
7. Expression baseline.
8. Style surface notes.

The compiler can omit empty sections and should collapse repeated terms before rendering the final text.

## MVP UI Flow

1. User opens `/characters`.
2. The first `Character` tab is active.
3. User selects an existing character or creates a new one.
4. The tab shows character identity fields and the list of reference sheets for that character.
5. Selecting a reference sheet enables/loads the `Reference Sheets` tab.
6. Creating a reference sheet requires a source image. If the source image already matches a sheet for that character, load that sheet instead of creating a duplicate.
7. The existing multi-view controls operate on the selected reference sheet.

## Media Send-To Flow

1. User sends a local image to `Characters / Source Image`.
2. If a character is selected, the app creates or loads the matching reference sheet for that character.
3. If no character is selected, the app asks the user to choose or create a character.
4. After selection, the app stores the source image reference on the sheet and navigates to `/characters`.

## Resolved Assumptions

- Characters are global assets, independent of the selected gallery project.
- Reference-sheet uniqueness is scoped per character, not app-wide.
- Image metadata does not need to know about Character ownership in the first implementation.
- The current output image persistence path remains valid; saved output ids/paths are referenced by the sheet body.
- A reference sheet is embedded in the character aggregate for the first implementation rather than stored as a separate EF table.
- Source fingerprints use saved image id first, content hash second, and normalized path as fallback.
- The first region schema is human/anime-character first, with catalog-defined extension points for fantasy or non-human bodies.
- Head zoom uses catalog-defined subregions under `head`, with hair treated as a high-priority identity anchor.
- Catalog files use versioned envelopes and recoverable item-level validation warnings.
- Natural prose is the default prompt output style; tag-style output is deferred as an optional future profile.

## Open Clarifications

- None for Phase 1. Import/export remains deferred to the catalog expansion phase after the editor is usable.

## Final Schema Decisions

- Characters are global persisted aggregates with no project relationship.
- `CharacterEntity` is the only new EF root needed for the first implementation.
- `CharacterBody` owns identity, regions, wardrobes, prompt profiles, notes, and embedded reference sheets.
- `CharacterReferenceSheetBody` is embedded in the character body and owns one multi-view reference setup for one source image.
- One reference sheet per source image is enforced per character through source fingerprints.
- `AppState.Character` becomes page/session state only and should not own durable reference-sheet data after migration.
- The MVP character region set is human/anime-first and catalog-extensible.
- Head zoom is represented through catalog-defined subregions under `head`.
- Catalog JSON files use versioned envelopes with item-level validation warnings and built-in fallbacks.
- Prompt output defaults to natural prose, with deterministic rendering before optional LLM rewriting.

## Phase 2 Execution Entry Plan

Phase 2 should begin with persistence and catalog-loader implementation only. It should not build the new UI yet.

Recommended Step 1 implementation scope:

1. Add `CharacterEntity` under `BlazorWebApp/Data/Entities/`.
2. Add model types for `CharacterBody`, `CharacterReferenceSheetBody`, source image metadata, region state, trait value, wardrobe preset, prompt profile, and catalog DTOs.
3. Keep reference-sheet body fields close to the existing `AppStateCharacter` shape so Phase 3 can migrate the current Reference Sheets UI incrementally.
4. Add default factory helpers for new characters and new reference sheets.
5. Add focused model tests for default character body creation and source fingerprint uniqueness helpers.

Phase 2 persistence follow-up steps should then add `AppDbContext` conversion, repository methods, migration, model snapshot update, catalog loading, and repository/catalog tests.

## Phase Summary

Phase 1 resolved the high-level architecture for integrating Character Creator with the existing `/characters` page. The resulting design makes `Character` the first tab, keeps the existing multi-view Reference Sheet feature as a linked second tab, and introduces a global JSON-backed character aggregate that owns embedded reference sheets. Durable character and sheet state is separated from lightweight app/page state, and the schema now has concrete region, head zoom, catalog, source fingerprint, and prompt profile decisions ready for implementation planning.

## Validation Plan

- Phase 1 is documentation-only and does not require a build.
- Phase 2 should add repository tests for character CRUD, reference-sheet CRUD, in-place JSON persistence, and duplicate source prevention.
- Phase 3 should add focused tests for app-state selection, media send-to routing, and run-service persistence against the selected reference sheet.

## Change Log

- Created Phase 1 document with approved `/characters` integration decisions and draft schema boundaries.
- Updated the main plan to make Character Creator the first `/characters` tab and to introduce Character-owned reference sheets.
- Completed Step 3 by defining aggregate boundaries, existing state mapping, repository expectations, and source fingerprint precedence.
- Completed Step 4 by defining the MVP root regions, cross-region categories, state rules, and future extension points.
- Completed Step 5 by defining head subregions, head state rules, and stable head prompt ordering.
- Completed Step 6 by drafting catalog file roles, JSON shapes, validation rules, and loader output contract.
- Completed Step 7 by defining MVP prompt profiles, natural-prose default behavior, and render ordering.
- Completed Step 8 by finalizing schema decisions and preparing the Phase 2 persistence entry plan.
