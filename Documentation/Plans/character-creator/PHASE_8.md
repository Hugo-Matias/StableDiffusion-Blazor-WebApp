# Phase 8 - Prompt Application And Workflow Integration

## Status

Complete. User approved proceeding after Phase 7 completion, and implementation/validation finished in this session.

## Objective

Apply saved character prompts to prompt fields for text-to-image and image/edit workflows.

## Scope

Phase 8 adds prompt application controls and workflow send-to actions from the Character Creator tab. It does not add a Generate-page character picker fragment; the `/characters` flow remains the source of truth for this phase.

## Step Checklist

- [x] Step 1: Add send-to actions using `PromptSendToService` for eligible workflows. `[2 pts]`
- [x] Step 2: Add apply modes: append, prepend, replace, and copy. `[2 pts]`
- [x] Step 3: Add application context controls for pose, expression override, wardrobe preset, and edit intent. `[3 pts]`
- [x] Step 4: Add optional negative guard output and routing decision. `[2 pts]`
- [x] Step 5: Defer Generate-page fragment/picker until the `/characters` flow is stable. `[1 pt]`

## Implementation Notes

- Prompt application uses the existing deterministic compiler with `CharacterPromptApplicationContext`.
- Workflow send-to uses `PromptSendToService`, including saved-state composition for inactive workflows.
- Copy mode stays local to the Character Creator page via browser clipboard.
- `AppStateCharacter` stores prompt application mode, negative-guard routing, and temporary application context so page reloads preserve the working prompt surface without mutating saved character traits.
- `PromptSendToService.SendPromptToWorkflowAsync` composes positive and optional negative prompts using replace/prepend/append against active or saved target workflow state, then navigates to Generate.
- The Character Creator prompt panel now exposes mode, negative guard, pose, expression, camera, scene, style, edit intent, and negative additions alongside eligible workflow buttons.

## Validation Plan

- File diagnostics for touched service, model, page, component, CSS, and test files: no errors.
- Focused `runTests` for `PromptSendToServiceTests.cs` and `CharacterPromptCompilerServiceTests.cs`: 7 passed.
- `process: build`: passed with known NuGet warning noise (`NU1603`, `NU1901`/`NU1902`/`NU1903`).

## Open Issues / Blockers

- Runtime workflow availability still depends on backend availability, matching existing send-to behavior.
- Clipboard copy uses browser clipboard APIs and therefore follows normal browser permission/context rules.

## Change Log

- Created Phase 8 document and began implementation after user approval.
- Completed prompt application models, service routing, creator UI controls, tests, and documentation.
