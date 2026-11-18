# Phase 1 - Page Shell, Nav Entry, Intro Scene

**Status:** `[x]` Complete
**Complexity:** 3 pts (moderate - multi-file, simple logic)
**Started:** 2026-05-01
**Completed:** 2026-05-01

## Objective

Establish Odditarium as an LLM Tools view inside the Prompts page with a working intro scene. Migrate Prompt Wizard out of WorkshopView.

## Steps

| #   | Step                                         | Complexity | Status |
| --- | -------------------------------------------- | ---------- | ------ |
| 1   | Create OdditariumView + CSS                  | 2          | [x]    |
| 2   | Add nav entry to LLMNavMenu                  | 1          | [x]    |
| 3   | Wire view into LLMToolsTab switch            | 1          | [x]    |
| 4   | Remove WorkshopWizardPanel from WorkshopView | 1          | [x]    |

## Changes Made

### Files Created

- `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumView.razor` - Main view component with intro scene, receives `SelectedModel` parameter from parent
- `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumView.razor.css` - Scoped styles following WorkshopWizardPanel conventions (accent strip, token-based spacing)

### Files Modified

- `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor` - Added nav item `new("odditarium", "Odditarium", Icons.Material.Filled.AutoFixHigh)`
- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor` - Added switch case `"odditarium"` rendering `<OdditariumView SelectedModel="@_selectedModel" />`
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor` - Removed both `WorkshopWizardPanel` instances and `OnWizardCommitAsync` method

## Design Decisions

- **No standalone route** - Odditarium lives as an LLM Tools view (not a separate page), sharing the model selector from the top toolbar
- **Accent strip pattern** - Follows WorkshopWizardPanel convention: 3px top accent line with mood-based color classes
- **Game-like hero surface** - Centered card with decorative floating orbs, gradient title text, pill badges, and prominent filled start button
- **Animations** - Subtle CSS animations: floating background orbs (`odditarium-float`), sparkling title icon (`odditarium-sparkle`), button lift on hover
- **Model source** - Receives `SelectedModel` parameter from LLMToolsTab, same pattern as MixerView/TagBuilderView/etc.

## Build Validation

Build passes with 0 errors. All warnings are pre-existing and unrelated to these changes.

## Commit Checkpoint

Phase 1 ready for commit: Odditarium shell + migration complete.
