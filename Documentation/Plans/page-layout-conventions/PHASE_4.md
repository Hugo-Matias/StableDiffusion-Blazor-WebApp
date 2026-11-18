# Phase 4 - Resources page

## Status
**Phase:** 4 - Complete
**Build Status:** Passing | **Tests:** user-verified across all 7 resource types

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

Migrate the Resources page to the layout system and make this the first page to use the **collapsible sidebar**. The current filter/search grid (`MudGrid` with Title/Tag/Subtype/BaseModel/OrderBy/Limit/Enabled/Search button/Update BaseModels) moves into the `Sidebar` slot; the paginated `ResourceCard` grid stays in `Content`.

---

## Context

### Current state
- `Pages/Resources.razor` renders `MudTabs` directly with `PanelClass="px-5 py-2"`, one panel per `ResourceType`, each wrapping `ResourcePanel`.
- `ResourcePanel` renders a big `MudGrid` (5/1/4/2 column split) with all filters inline above the resource cards, then pagination + card grid + pagination.

### Target state
- `Pages/Resources.razor` uses `TabbedPageShell`.
- Each `ResourcePanel` becomes a `TwoColumnLayout Collapsible="true" @bind-Collapsed="State.State.Resources.SidebarCollapsed"`:
  - `Sidebar`: filters (Title, Tag, Subtype, BaseModel, OrderBy + Asc/Desc, Limit slider, Enabled tri-state, Search button, Update BaseModels button, `ResourceTemplatesBar`).
  - `Content`: pagination header + resource card grid + pagination footer.
- `SidebarTitle="Filters"` for the rail label.
- Per the "children flush" convention established in Phase 3: no `pa-*` / root `MudPaper` in the slot content. Stack the filter fields vertically with `MudStack Spacing="2"`.

### State persistence
- Add a `bool SidebarCollapsed` property to the `Resources` state block on `IStateService` (parity with `ActiveTabIndex`). Default: `false`.
- The existing `State` entity persists these via JSON - new field deserializes with default for existing rows (no migration needed per `03-PERSISTENCE-AND-MIGRATIONS.md`).

### Key considerations
- The filter grid is shared across **every** resource tab (Checkpoint / Diffusion / LORA / etc.). `Resources.razor` iterates tabs and passes the resource list to `ResourcePanel`. Migration happens at the `ResourcePanel` level so all tabs benefit uniformly.
- The `SidebarCollapsed` state is page-wide, not per-tab - one toggle applies to all resource types (consistent with how the existing `State.Resources.*` fields work).
- The filter layout currently uses horizontal `MudGrid` rows; in the sidebar, switch to a vertical `MudStack Spacing="2"`. All fields stretch to full width.
- Preserve the `ResourceTemplatesBar` placement inside the sidebar (below the filter fields, above the action buttons).
- `Variant.Outlined` on any filter controls stays untouched in this phase (sweep is Phase 8).

---

## Execution Checklist

### Step 1: Extend `IStateService` / `Resources` state block with `SidebarCollapsed`
**Complexity:** 1
**Status:** [x] Complete

#### Changes Made
- `Models/AppState.cs` - added `SidebarCollapsed` (bool, default false) and `CardSize` (ResourceCardSize, default Medium) to `AppStateResources`; new enum `ResourceCardSize { Small, Medium, Large }`.

---

### Step 2: `Pages/Resources.razor` - adopt `TabbedPageShell`
**Complexity:** 1
**Status:** [x] Complete

#### Changes Made
- `Pages/Resources.razor` - `MudTabs` -> `TabbedPageShell` binding the existing `State.State.Resources.ActiveTabIndex`.

---

### Step 3: `ResourcePanel` - consume `TwoColumnLayout Collapsible=true`
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- `Components/Resources/ResourcePanel.razor` - full rewrite of markup section:
  - Wrapped in `TwoColumnLayout Collapsible="true" @bind-Collapsed="State.State.Resources.SidebarCollapsed" SidebarTitle="Filters"`.
  - Filters moved to Sidebar as vertical `MudStack Spacing="2"`.
  - Pagination + card grid moved to Content.
  - Dropped `CivitaiService` inject and `UpdateBaseModels` method (button removed per user).

---

### Step 4: Visual + functional verification
**Complexity:** 1
**Status:** [x] Complete

#### Outcome
User approved after a detour applying 7 UX tweaks (see Issue 1).

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 1 | `SidebarCollapsed` + `CardSize` on Resources state |
| 2 | [x] | 1 | `Resources.razor` -> `TabbedPageShell` |
| 3 | [x] | 5 | `ResourcePanel` -> `TwoColumnLayout` collapsible + 7-tweak detour |
| 4 | [x] | 1 | Verification - approved |

---

## Issues & Resolutions

### Issue 1: UX tweak detour after initial migration landed
**Impact:** Initial sidebar layout was functional but had several rough edges once seen in the narrow column.
**Tweaks applied (bundled as a single detour round):**
1. Asc/Desc toggle moved inline with the `Order By` select (icon-only `MudIconButton` with tooltip), replacing the separate labeled checkbox.
2. Quantity slider constraints hardcoded (`Min=4`, `Max=100`, `Step=4`) - overrides the settings values that were poorly tuned for the new width.
3. `Active` tri-state label made reactive (`All` / `Active` / `Inactive` via `GetEnabledFilterLabel()`).
4. `ResourceTemplatesBar` refactored from `MudGrid` into a two-row `MudStack`: `Templates` select on row 1, three action `MudIconButton`s centered on row 2 - fixes horizontal overflow in the clamped sidebar.
5. `Update BaseModels` button removed entirely (one-time feature no longer needed); `CivitaiService` inject + `UpdateBaseModels` handler + `_isUpdating` field all dropped from `ResourcePanel`.
6. Scrollable cards container - new `Components/Resources/ResourcePanel.razor.css` with `.resources-scroll-container { max-height: calc(100vh - var(--resources-scroll-offset, 280px)); overflow-y: auto; }`. Both pagination bars stay outside the scroll so they remain visible. User tuned the default offset from 260 -> 280 for better buffer.
7. Card size control - new `ResourceCardSize` enum (`Small` / `Medium` / `Large`), `CardSize` property on `AppStateResources`. First pass was a 3-button `MudButtonGroup`; final form is a single `MudIconButton` that cycles through sizes with a reactive icon, placed inline with the Active tri-state and moved below the `ResourceTemplatesBar` action row. Card widths: `11rem` / `16rem` / `22rem` applied via the existing `ResourceCard.Width` parameter.
**Convention touched:** None - all tweaks stayed within the established "children render flush" rule and the token-based padding model.

---

## Commit Checkpoints

- [x] After Step 1 (state field)
- [x] After Steps 2-3 (layout migration)
- [x] After Step 4 (user visual approval + 7-tweak detour)

---

## Phase Summary

Resources page fully migrated to `TabbedPageShell` + `TwoColumnLayout Collapsible`. First page to consume the collapsible rail, first page to add a scrollable content area via `--resources-scroll-offset`, and first page to introduce a reactive tri-state icon control (the card-size cycler).

### Accomplishments
1. All 7 resource tabs use the shared layout system.
2. `SidebarCollapsed` + `CardSize` persisted via existing `State` JSON entity (no migration).
3. Scrollable cards container pattern established (token-driven offset).
4. Tri-state icon-cycler pattern introduced as a reusable idea for compact sidebars.
5. `ResourceTemplatesBar` made narrow-column friendly (two-row layout).
6. Legacy `Update BaseModels` button retired.

### Metrics
- Modified razor files: 4 (`Pages/Resources.razor`, `ResourcePanel.razor`, `ResourceTemplatesBar.razor`, `Models/AppState.cs`).
- New files: 1 (`ResourcePanel.razor.css`).
- New enums: 1 (`ResourceCardSize`).
- New state fields: 2 (`SidebarCollapsed`, `CardSize`).
- New CSS tokens: 1 (`--resources-scroll-offset`, scoped).

### Deferred Items
- `HandleCardSizeChanged(ResourceCardSize)` method kept unused in case a future caller wants explicit set; can be removed if it proves unnecessary.

**Phase Status:** Complete [x]
