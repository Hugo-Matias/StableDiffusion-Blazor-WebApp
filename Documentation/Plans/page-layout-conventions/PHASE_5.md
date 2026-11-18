# Phase 5 - CivitAI page

## Status
**Phase:** 5 - Complete
**Build Status:** Passing | **Tests:** user-verified across all 3 tabs (Models / Images / Creators)

---

## Implementation Guidelines

Same as prior phases. Use the tools you already trust.

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

---

## Objective

Migrate the CivitAI page to the layout system. Each of the three panels (Models / Images / Creators) becomes a `TwoColumnLayout Collapsible`, with its `EditForm`+`MudGrid` search cluster moved into the sidebar and results into the content column. Applies the **scrollable content** pattern established on Resources so card grids scroll inside the surface while paginations remain visible.

---

## Context

### Current state
- `Pages/CivitAI.razor` = one-liner rendering `<CivitaiPanel />`.
- `CivitaiPanel.razor` uses `<MudTabs Elevation="1" Rounded Centered>` (no `ActiveTabIndex` binding, no persistence).
- Each of the three panels has the same inline pattern:
  - `<EditForm Model=Request>` wrapping a `<MudGrid Class="px-4 mb-* mt-*">` with 8-12 `MudItem` filter fields + a `Search` button.
  - Below the form: results (card `MudGrid` for Models/Images, `MudTable` for Creators) and pagination.
- Form fields are MudBlazor (`MudTextField`, `MudNumericField`, `MudSelect`, `MudSlider`, `MudButton`).

### Target state
- `Pages/CivitAI.razor` consumes `TabbedPageShell` directly (inline the 3 tabs; retire `CivitaiPanel.razor` since it's now a one-liner inside the shell).
- Each panel (`CivitaiModelsPanel`, `CivitaiImagesPanel`, `CivitaiCreatorsPanel`) wraps its form + results in `TwoColumnLayout Collapsible="true" @bind-Collapsed="State.State.Civitai.SidebarCollapsed" SidebarTitle="Filters"`.
- Form fields restacked vertically (`MudStack Spacing="2"`) inside the sidebar.
- Results area wrapped in the `.civitai-scroll-container` class (mirror of `.resources-scroll-container`) with a token-driven offset.
- Both paginations (top + bottom) remain outside the scroll container.

### State additions
- `AppStateCivitai.ActiveTabIndex` (int, default 0) - persist the tab across navigation.
- `AppStateCivitai.SidebarCollapsed` (bool, default false) - one shared collapse flag across all three tabs (same approach as Resources).

### Key considerations
- The `Models` panel search form is large (~11 fields). Vertical stacking will make the sidebar tall. That's expected; collapse affordance mitigates it.
- Each panel has its own `Request` object and `Search()` / `HandlePageSelected` handlers; those stay unchanged.
- No `CardSize` control on CivitAI yet (deferred - current card layout is fine).
- No `Variant.Outlined` cleanup here. That's Phase 8.

---

## Execution Checklist

### Step 1: Extend `AppStateCivitai` with `ActiveTabIndex` + `SidebarCollapsed`
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Add `public int ActiveTabIndex { get; set; } = 0;` to `AppStateCivitai`
- [ ] Add `public bool SidebarCollapsed { get; set; } = false;` to `AppStateCivitai`

---

### Step 2: `Pages/CivitAI.razor` - adopt `TabbedPageShell`
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Inline the 3 `MudTabPanel`s from `CivitaiPanel` directly inside `TabbedPageShell` (retire the thin wrapper)
- [ ] Bind `@bind-ActivePanelIndex` to `State.State.Civitai.ActiveTabIndex`
- [ ] Remove `Components/Resources/CivitaiPanel.razor` if no other reference exists

---

### Step 3: `CivitaiModelsPanel` - `TwoColumnLayout` migration
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Wrap in `TwoColumnLayout Collapsible="true" @bind-Collapsed="State.State.Civitai.SidebarCollapsed" SidebarTitle="Filters"`
- [ ] Move `EditForm` + form fields into `Sidebar` slot as vertical `MudStack Spacing="2"`
- [ ] Move pagination + card grid into `Content` slot
- [ ] Apply `.civitai-scroll-container` to the card grid wrapper

---

### Step 4: `CivitaiImagesPanel` - `TwoColumnLayout` migration
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Same treatment as Step 3 for Images panel

---

### Step 5: `CivitaiCreatorsPanel` - `TwoColumnLayout` migration
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Same treatment as Step 3 for Creators panel (results area is `MudTable`, not a card grid; wrap table in scroll container)

---

### Step 6: Shared scroll CSS
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Add a scoped `CivitaiScrollContainer.razor.css` OR co-locate rules in each panel's `.razor.css`
- [ ] Use token `--civitai-scroll-offset` (default `280px`, matching Resources)

---

### Step 7: Visual + functional verification
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] All 3 tabs switch cleanly; active tab persists across reload
- [ ] Collapse toggle works across all 3 tabs (shared state)
- [ ] Each panel's search form submits correctly and pagination works
- [ ] Card/table grids scroll inside their surface; paginations stay visible
- [ ] No overflow in the narrow sidebar column

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 1 | Civitai state additions (+ post-approval CardSize) |
| 2 | [x] | 2 | `CivitAI.razor` shell; `CivitaiPanel` retired |
| 3 | [x] | 3 | Models panel migration + card-size cycler |
| 4 | [x] | 2 | Images panel migration + card-size cycler |
| 5 | [x] | 2 | Creators panel migration |
| 6 | [x] | 1 | Scroll CSS (Models + Images + Creators) |
| 7 | [x] | 1 | Verification - approved |

---

## Issues & Resolutions

### Issue 1: Card-size parity request with Resources
**Impact:** After the initial migration landed, user requested the card-size tri-state cycler from Phase 4 be added to both Models and Images tabs for consistency.
**Resolution:**
- Added `CardSize` field to `AppStateCivitai` (shared state across Models + Images tabs).
- `CivitaiImageCard.razor.css` rewired: `.civitai-card` width/min-width/max-width now read from `var(--civitai-card-width, 14rem)`. Non-breaking - default preserved.
- Models panel passes `Width` parameter to `CivitaiResourceCard` (which already supported it); widths `12/16/22rem`.
- Images panel sets the CSS variable on the scroll container via inline style; widths `10/14/20rem`.
- Creators panel intentionally excluded (table, no card concept).
**Takeaway:** The tri-state icon cycler pattern from Phase 4 generalizes cleanly. Helper methods duplicated across both panels; extraction to a shared class deferred unless a third page ever needs it.

---

## Commit Checkpoints

- [x] After Steps 1-2 (state + shell)
- [x] After Steps 3-5 (all three panels migrated)
- [x] After Step 6 (scroll CSS)
- [x] After Step 7 (user visual approval + CardSize detour)

---

## Phase Summary

CivitAI page migrated to the layout system; `CivitaiPanel` thin wrapper retired; the scrollable-content pattern and tri-state card-size cycler both reused from Phase 4.

### Accomplishments
1. All 3 CivitAI tabs use `TwoColumnLayout Collapsible`.
2. `CivitAI.razor` now renders `TabbedPageShell` directly (one fewer indirection).
3. `AppStateCivitai` gains `ActiveTabIndex`, `SidebarCollapsed`, `CardSize`.
4. `CivitaiImageCard` made parent-sizable via CSS variable without breaking existing callers.
5. Replaced the old fixed-height `height: 70vh; overflow: scroll` hack on `.images-container` with the unified token-driven scroll container.

### Metrics
- Modified razor files: 5 (`Pages/CivitAI.razor`, 3 `Civitai*Panel.razor`, `AppState.cs`).
- Deleted files: 1 (`CivitaiPanel.razor`).
- New files: 1 (`CivitaiCreatorsPanel.razor.css`).
- Modified CSS files: 3.
- New state fields: 3 (`ActiveTabIndex`, `SidebarCollapsed`, `CardSize`).
- New CSS vars: 2 (`--civitai-scroll-offset`, `--civitai-card-width`).

### Deferred Items
- Shared extraction of card-size helpers (may revisit if a 3rd page adopts the cycler).

**Phase Status:** Complete [x]
