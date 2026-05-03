# Phase 3: Entity/Service Rename (WorkshopWizard -> Odditarium)

**Objective:** Complete the pivot by renaming all WorkshopWizard infrastructure to Odditarium equivalents, dropping the intro-catalog dependency, and extending the session body with persona/layer fields.
**Complexity:** 8 points
**Status:** [x] Complete

## Steps

- [x] Rename `WorkshopWizardSession` entity to `OdditariumSession` — Extend JSON body schema with: `PersonaId`, `Vibe`, `CollectedLayers[]`, `RoundHistory[]`
- [x] Rename `WorkshopWizardService` to `OdditariumService` — Remove intro-catalog dependency entirely; new flow is persona + vibe + LLM rounds
- [x] Rename Wizard events (`WizardTurnAdvancedEventArgs`, etc.) to Odditarium equivalents — Pub/sub plumbing reused as-is
- [x] Keep `WizardResponseValidator` — Renamed to `OdditariumResponseValidator`; JSON response shape (question + options array) unchanged
- [x] Create EF Core migration for entity rename and body schema extension
- [x] Update all references across the codebase — No traces of "Wizard" naming remain in Odditarium-related code

## Changes Made

### Files Created

| File                                                                                          | Purpose                                                                                                                                                      |
| --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `BlazorWebApp/Models/OdditariumModels.cs`                                                     | Models: OdditariumOption, OdditariumTurn, OdditariumBody, OdditariumLLMResponse, OdditariumJsonOptions, OdditariumResponseValidator                          |
| `BlazorWebApp/Data/Entities/OdditariumSession.cs`                                             | Entity replacing WorkshopWizardSession with new body schema                                                                                                  |
| `BlazorWebApp/Events/OdditariumEventArgs.cs`                                                  | Events: OdditariumTurnAdvancedEventArgs, OdditariumCommittedEventArgs, OdditariumResetEventArgs                                                              |
| `BlazorWebApp/Services/OdditariumService.cs`                                                  | Service implementing IOdditariumService — StartGameAsync, LockVibeAsync, NextRoundAsync, RequestMoreAsync, SkipAxisAsync, UndoAsync, CommitAsync, ResetAsync |
| `BlazorWebApp/Migrations/20260502214900_Rename_WorkshopWizardSession_to_OdditariumSession.cs` | EF Core migration renaming table WorkshopWizardSessions -> OdditariumSessions                                                                                |

### Files Modified

| File                                                   | Change                                                                                           |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| `BlazorWebApp/Data/AppDbContext.cs`                    | Replaced WizardBody converter with OdditariumBody converter; DbSet changed to OdditariumSessions |
| `BlazorWebApp/Program.cs`                              | Removed WizardIntroCatalog + IWorkshopWizardService; added IOdditariumService registration       |
| `BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs` | Updated entity references from WorkshopWizardSession to OdditariumSession                        |

### Files Deleted

- `WorkshopWizardSession.cs`, `WizardModels.cs`, `WizardEventArgs.cs`, `WorkshopWizardService.cs`, `WizardIntroCatalog.cs`
- `WorkshopWizardPanel.razor` + `.css`
- `WorkshopWizardServiceTests.cs`, `WizardModelsTests.cs`

## Success Criteria

- [x] All Wizard-named entities/services/events renamed to Odditarium equivalents
- [x] Session body supports persona fields (PersonaId, Vibe, CollectedLayers)
- [x] EF Core migration applies cleanly with no data loss
- [x] Build passes with 0 errors
- [x] No remaining references to "Wizard" in Odditarium-related code

## Commit Checkpoint

- Infrastructure rename complete. Build green. Ready for Phase 4 (System Prompt v1).
