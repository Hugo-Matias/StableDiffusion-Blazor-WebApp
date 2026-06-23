# Phase 5: Round Panel UI (6 Options, Undo/More/Skip, Layer Collection)

**Objective:** Build the main game loop UI — one question with 6 clickable options per round, action buttons (Undo, More..., Skip), and a visible layer collection display.
**Complexity:** 8 points
**Status:** [x] Complete

## Steps

- [x] Create `OdditariumRoundPanel` component — Displays LLM-generated question in persona voice + 6 option buttons
- [x] Implement option selection flow — Clicking an option adds it to `CollectedLayers[]`, triggers next LLM call for next round
- [x] Action buttons: Undo (revert last pick), More... (regenerate options for current question), Skip this axis (jump past uninteresting questions)
- [x] Layer collection display — Visual list of collected layers shown alongside the round panel, growing as rounds progress
- [x] Loading states — Skeleton/spinner while waiting for LLM response between rounds
- [x] Commit button — Always available, no minimum layer count enforced (disabled when no layers collected)
- [x] Wire `OdditariumView` to game loop via `IOdditariumService.StartGameAsync` after persona selection

## Changes Made

### Files Created

| File                                                                       | Purpose                                                                                                                                                            |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumRoundPanel.razor`     | Main round panel component — question display, 6-option grid, action bar (Undo/More/Skip/Commit), layer collection sidebar, loading states, committed draft editor |
| `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumRoundPanel.razor.css` | Scoped CSS for round panel — option buttons with hover effects, layer chip animations, vibe badge, empty state styling                                             |

### Files Modified

| File                                                             | Change                                                                                                                                                          |
| ---------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Components/Prompts/LLM/Views/OdditariumView.razor` | Added `_showRoundPanel` state, `_session` field, `IOdditariumService` injection; wired persona selection → `StartGameAsync`; added round panel rendering branch |

## Component Architecture

### OdditariumRoundPanel

- **Input:** `Session` (OdditariumSession?), `SelectedModel` (string), `DraftCommitted` (EventCallback\<string\>)
- **States:** Loading spinner with contextual message, empty state (not started / waiting / committed)
- **Actions:** Option selection → `NextRoundAsync`, Undo → `UndoAsync`, More → `RequestMoreAsync`, Skip → `SkipAxisAsync`, Commit → `CommitAsync`
- **Layer Collection:** Sidebar showing collected layers as animated chips with count, vibe badge, and editable draft area post-commit

### OdditariumView Flow

```
Hero Card (Start Game) → Persona Selection → Round Panel (Game Loop)
                                    ↑              ↓
                              Back Button    Draft Committed
```

## Success Criteria

- [x] Round panel renders question + 6 options per round ✓
- [x] Clicking option adds layer and triggers next round ✓
- [x] Undo reverts last pick ✓
- [x] More... regenerates options for current question ✓
- [x] Skip advances past uninteresting questions ✓
- [x] Layer collection grows visibly as rounds progress ✓
- [x] Commit available when at least one layer collected ✓

## Build Status

Build: **Green** — 0 errors (pre-existing warnings only)
