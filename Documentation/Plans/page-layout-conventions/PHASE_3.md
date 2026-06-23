# Phase 3 - Prompts page (baseline validation)

## Status
**Phase:** 3 - Complete
**Build Status:** Passing | **Tests:** user-verified across all three tabs

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits for a step

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

---

## Objective

Migrate the Prompts page (the reference design) to the new layout primitives. Each of the three sub-tabs (`PromptsPanel`, `WildcardsTab`, `LLMToolsTab`) consumes `TwoColumnLayout` internally. The top-level `Prompts.razor` uses `TabbedPageShell`. Gutters and spacing stop being hard-coded and start coming from tokens.

---

## Context

### Current state
- `Pages/Prompts.razor` renders `MudTabs` directly with `PanelClass="px-5 py-2"`, `Centered`, `Rounded`, `Elevation=4`.
- Each tab's sub-component has its own 2-column split via `MudGrid Spacing=2` + `MudItem xs=12 md=X lg=Y`:
  - `PromptsPanel` - `CategoryBrowser` (md=3) + `PromptStyleTable` (md=9).
  - `WildcardsTab` - `CollectionBrowser` inside a `MudPaper Elevation=1 Class="pa-4" Style="height: calc(100vh - 200px); overflow-y: auto;"` (md=4 lg=3) + `EntryManager` in a similarly-wrapped paper (md=8 lg=9).
  - `LLMToolsTab` - `LLMSettingsPanel` (md=4 lg=3) + `LLMMainPanel` (md=8 lg=9).
- Prompts sidebar is **not** collapsible per the resolved design decision.

### Migration approach
- Each sub-tab consumes `TwoColumnLayout` itself (Sidebar + Content fragments). Keeps each tab self-contained; `Prompts.razor` stays thin.
- Remove `MudGrid` + `MudItem` wrappers from all three sub-tabs.
- Remove `WildcardsTab`'s explicit `MudPaper ... height: calc(100vh - 200px)` wrappers - the layout surfaces already provide the paper. Scroll behavior preserved via `overflow-y: auto` on the child content, not on the layout surface.
- Preserve `@ref="_containerRef"` keyboard-shortcut wiring on `PromptsPanel` and `WildcardsTab`. The ref-bearing div becomes the outer wrapper above `TwoColumnLayout` (not inside a layout slot).
- `Pages/Prompts.razor`: swap `MudTabs` for `TabbedPageShell`; drop `PanelClass`; drop `Elevation=4` (shell provides it).

### Key design constraints
- No `Variant.Outlined` cleanup in this phase. That's Phase 8.
- No `.razor.css` audits beyond the WildcardsTab height fix. Other scoped CSS is untouched.
- Sidebar surface gets a top padding (~8px) so the `CategoryBrowser` / `CollectionBrowser` / `LLMSettingsPanel` first element isn't flush against the surface edge. Same for Content surface. Handled per-tab.

---

## Execution Checklist

### Step 1: `Pages/Prompts.razor` - adopt `TabbedPageShell`
**Complexity:** 1
**Status:** [x] Complete

#### Changes Made
- `Pages/Prompts.razor` - `MudTabs` -> `TabbedPageShell`; removed `Elevation`, `PanelClass`, `Rounded`, `Centered` (shell defaults).

---

### Step 2: `PromptsPanel` - consume `TwoColumnLayout`
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- `Components/Prompts/PromptsPanel.razor` - `MudGrid`/`MudItem` -> `TwoColumnLayout`; `@ref="_containerRef"` kept on outer div; child padding removed (layout surface owns it).
- `Components/Prompts/Styles/CategoryBrowser.razor` - flattened root: `MudPaper Elevation=1 pa-4` -> plain `<div class="category-browser">`.

---

### Step 3: `WildcardsTab` - consume `TwoColumnLayout`, drop hardcoded MudPaper wrappers
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- `Components/Prompts/Wildcards/WildcardsTab.razor` - `MudGrid`/`MudItem` -> `TwoColumnLayout`; removed the two `MudPaper Elevation=1 pa-4 height:calc(100vh-200px)` wrappers; scroll preserved on inner `.wildcards-*-body` divs.
- `Components/Prompts/Wildcards/WildcardsTab.razor.css` - new, `max-height: calc(100vh - 180px); overflow-y: auto;` on the body divs.

---

### Step 4: `LLMToolsTab` - consume `TwoColumnLayout`
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- `Components/Prompts/LLM/LLMToolsTab.razor` - `MudGrid`/`MudItem` -> `TwoColumnLayout`.
- `Components/Prompts/LLM/LLMSettingsPanel.razor` - flattened root: `MudPaper Elevation=1 pa-4` -> plain `<div>` keeping inline scroll style.
- `Components/Prompts/LLM/LLMMainPanel.razor` - flattened root same way; added `Centered="true"` to inner `MudTabs` (Process/System Prompts/History).

---

### Step 5: Visual verification against reference screenshot
**Complexity:** 1
**Status:** [x] Complete

#### Outcome
User approved after two correction rounds (see Issues & Resolutions).

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 1 | `Prompts.razor` -> `TabbedPageShell` |
| 2 | [x] | 2 | `PromptsPanel` -> `TwoColumnLayout` + `CategoryBrowser` flattened |
| 3 | [x] | 3 | `WildcardsTab` -> `TwoColumnLayout` + wrapper cleanup |
| 4 | [x] | 2 | `LLMToolsTab` -> `TwoColumnLayout` + two panels flattened + inner tabs centered |
| 5 | [x] | 1 | Visual verification (2 correction rounds) |

---

## Issues & Resolutions

### Issue 1: Empty dark region below the sidebar; double inset shadow on child cards; inner LLM tabs not centered
**Impact:** After the initial migration the Prompts page had a large dark band below the Categories card, every child component (CategoryBrowser, LLMSettingsPanel, LLMMainPanel) showed a subtle inset shadow inside the layout surface, and the nested Process/System Prompts/History tabs were left-aligned.
**Root causes:**
- `ApplyEffectsToContainer="true"` on `TabbedPageShell`'s `MudTabs` painted the whole panel area with the header's elevation.
- Layout surfaces rendered their own `MudPaper Elevation=1` **and** the children rendered their own `MudPaper Elevation=1` at the root. Nested papers = double elevation artifact.
- The inner `MudTabs` in `LLMMainPanel` was missing `Centered=true`.
**First attempt (reverted):** Made layout surfaces transparent `<div>`s to avoid nesting. This eliminated the artifact but lost all page-wide consistency: children placed at their natural positions, gutters felt random, no unified card look.
**Final resolution:**
- `ApplyEffectsToContainer` removed from `TabbedPageShell`.
- Layouts keep ownership of the surface: one `MudPaper Elevation=LayoutDefaults.SurfaceElevation` per slot with `border-radius: var(--app-surface-radius)` and `padding: var(--app-surface-padding)`.
- Children flattened at the root (`CategoryBrowser`, `LLMSettingsPanel`, `LLMMainPanel` -> plain `<div>`).
- `Centered=true` added to `LLMMainPanel`'s inner `MudTabs`.
- New token `--app-surface-padding: 16px` added to `site.css`.
**Convention promoted:** "Children of a layout slot render flush - no root `MudPaper` / `pa-*` / `Elevation`" is now a first-class rule in both `IMPLEMENTATION_GUIDE.md` (UI / Layout References) and `04-UI-DESIGN-LANGUAGE.md` (Layout anti-patterns). Focus surfaces (Elevation >= 2) are documented as the explicit exception.

---

## Commit Checkpoints

- [x] After Step 1 (`Prompts.razor` migration)
- [x] After Steps 2-4 (all three sub-tabs migrated)
- [x] After Step 5 (user visual approval)

---

## Phase Summary

Prompts page fully migrated to the layout system and serves as the validated baseline for Phases 4+.

### Accomplishments
1. `Prompts.razor` + 3 sub-tabs consume `TabbedPageShell` + `TwoColumnLayout`.
2. Three child components flattened (`CategoryBrowser`, `LLMSettingsPanel`, `LLMMainPanel`).
3. Double-paper artifact diagnosed and resolved by establishing the "layouts own surfaces, children render flush" convention.
4. New spacing token `--app-surface-padding` added.
5. Anti-patterns section of the design-language doc expanded with the nested-paper and `ApplyEffectsToContainer` warnings.

### Metrics
- Modified files: 8 razor, 3 CSS, 2 docs.
- New files: 1 (`WildcardsTab.razor.css`).
- New token: 1 (`--app-surface-padding`).

### Deferred Items
- None.

**Phase Status:** Complete [x]
