# Phase 2 - Persona Data Model + Hardcoded Roster + CCG Card Selector

**Status:** `[x]` Complete
**Complexity:** 5 pts (medium - service-level model creation)
**Started:** 2026-05-02
**Completed:** 2026-05-02

## Objective

Create the persona data model, hardcoded v1 roster of 5 personas, and a CCG-style card selector UI that transitions from the intro scene.

## Steps

| #   | Step                                                        | Complexity | Status |
| --- | ----------------------------------------------------------- | ---------- | ------ |
| 1   | Create OdditariumPersona.cs data model                      | 2          | [x]    |
| 2   | Create OdditariumPersonaRoster.cs with 5 hardcoded personas | 2          | [x]    |
| 3   | Create OdditariumPersonaView.razor - CCG card grid          | 5          | [x]    |
| 4   | Wire Start Game -> Persona selection transition             | 1          | [x]    |

## Changes Made

### Files Created

- `BlazorWebApp/Models/OdditariumPersona.cs` - Data model with Id, Name, Tagline, Description, ThematicTags, ToneBias, StylePreferences, ImageAsset, AccentColor
- `BlazorWebApp/Models/OdditariumPersonaRoster.cs` - Static roster class returning 5 hand-authored personas (Muse, Iron Mother, Void Walker, Pixel Pixie, The Architect) with `All` and `FindById()` accessors
- `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumPersonaView.razor` - CCG-style persona card selector with native HTML + scoped CSS (no MudBlazor grid)
- `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumPersonaView.razor.css` - Full CCG card styling: corner ornaments, accent glow borders, hover lift animations, staggered entrance animations, selection check mark pop animation

### Files Modified

- `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumView.razor` - Added `_showPersonaSelection` state; Start button transitions to persona selector; `HandlePersonaSelected` captures chosen persona ID; `HandlePersonaBack` returns to intro scene

### Directory Created

- `BlazorWebApp/wwwroot/odditarium/personas/` - Placeholder directory for transparent PNG persona illustrations (assets TBD)

## Design Decisions

- **CCG card style over MudBlazor** - Persona selection is the game entrypoint and needs to feel special. Native HTML + scoped CSS gives full visual control without grid constraints.
- **Card features:** Corner ornaments, accent-colored border glow on hover/select, lift animation on hover, staggered entrance animations, description reveal on hover, check mark pop animation for selected state
- **CSS custom property `--card-accent`** - Each card sets its own accent color from the persona data, enabling per-card theming without generating unique CSS classes
- **Persona images as transparent PNGs** - Placeholder paths point to `/odditarium/personas/{id}.png`. Actual illustrations are TBD (artist assets). Cards render gracefully with missing images.
- **Wizard migration note** - `WizardBody`, `WizardTurn`, `WizardOption` models must be fully migrated/renamed to Odditarium equivalents in Phase 3. No traces of "Wizard" naming should remain after the pivot.

## Build Validation

Build passes with 0 errors and 0 warnings from new code.

## Commit Checkpoint

Phase 2 ready for commit: Persona system + CCG card selector complete.
