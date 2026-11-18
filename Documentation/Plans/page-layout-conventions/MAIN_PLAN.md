# Page Layout Conventions - Implementation Plan

## Status
**Current Phase:** Complete

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits for a step

### Progress Tracking Symbols
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)
- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic

### Key Rules
- Each step = commitable checkpoint
- No time/date references - complexity points only
- Detours acceptable after discussion - append as new phase or split existing
- Phase documents must contain enough context to resume in new sessions
- Minimal, focused changes - avoid over-engineering
- User permission required before moving to next phase

---

## Problem Statement

The tabbed pages in the app (`Prompts`, `Resources`, `CivitAI`, `Scheduler`, plus the future-tabbed `Danbooru`) each implement their own spacing, container padding, and column structure. Visible consequences:

- **Prompts page** (current baseline): gap between sidebar and content is larger than the gap between tabs and sidebar/content. `MudTabs PanelClass="px-5 py-2"` creates an outer padding that is inconsistent with the page's outer gutter.
- **Resources / CivitAI**: no 2-column layout; search/filter forms live inline at the top of content, inflating the content area and forcing the user to scroll past controls they rarely change once configured.
- **Scheduler**: uses `MudContainer MaxWidth=ExtraLarge` + `PanelClass="pa-4"` - different paddings/max-width from other tabbed pages, no shared gutter.
- **Danbooru**: not tabbed today, but planned to grow (Browse + future Library). Search is a `MudGrid` at the top of the page.
- **Form variants**: `Variant.Outlined` is in use on multiple tabbed pages where `Variant.Text` is the documented convention (`Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`).

There is no single source of truth for gutter/spacing sizes, sidebar proportion, or the allowed set of tabbed-page layouts.

---

## Proposed Solution

Introduce a small set of **shared layout components** that all tabbed pages consume, backed by **global CSS spacing tokens**. Allow a limited, documented set of layout variants so pages that don't naturally fit a sidebar still share the same gutter language.

### Layout variants (allowed set)

| Variant | When to use | Structure |
|---|---|---|
| **Two-column** | Settings / search / browse tree on the left, main content on the right. Default for data-heavy pages. | `[Sidebar] [Content]` horizontal split, gap = `--app-gutter-inner` |
| **Topbar** | Small, infrequently-changed filter/search cluster above a single large content surface (e.g. gallery). | `[Topbar card]` stacked above `[Content card]`, gap = `--app-gutter-inner` |
| **Content-only** | Page is self-contained (e.g. Scheduler Runs list). | `[Content]` filling the shell, gutters still driven by tokens |

All three share the same **outer gutter** (page <-> window edges) and the same **inner gutter** (between shell elements). Tabs stay centered at the top across all variants.

### Collapsible sidebar (Two-column only)

- A small toggle affordance on the sidebar <-> content boundary (chevron button).
- Collapses to a narrow **rail** (just wide enough to show the expand chevron vertically) rather than to 0, so users always have a visible way back.
- CSS-grid driven (`grid-template-columns` transition on `--app-sidebar-width` vs `--app-sidebar-rail-width`).
- Collapsed state persisted **per page** via `IStateService` (the existing `State.Prompts`, `State.Resources` etc. blocks already host per-page UI flags like `ActiveTabIndex` - we add a `SidebarCollapsed` sibling).
- Emits no event contract - purely visual. Content uses the remaining grid column automatically.

### Tab header rules (applies to all tabbed pages)

- `MudTabs` with `Centered=true`, `Rounded=true`, `Elevation=4` (align on Prompts baseline; Scheduler will move up from `Elevation=1`).
- Remove per-page `PanelClass` padding (`px-5 py-2`, `pa-4`). All padding comes from the layout component, driven by tokens.
- Secondary tab clusters inside a panel (e.g. Process / System Prompts / History inside LLM Tools) remain centered too.

### Key Decisions

| Decision | Rationale |
|---|---|
| Sidebar width uses `%` with a clamp (min/max px) | User preference; prevents over-stretch on wide displays and under-squish on small ones |
| Three layout variants only | Keeps the system constrained; any new page must justify a fourth variant |
| Collapsible state lives on `IStateService` per-page block | Mirrors the existing convention used for `ActiveTabIndex`; survives navigation without extra persistence infrastructure |
| CSS tokens defined in `wwwroot/site.css` (global) | Matches existing global-overrides pattern in `site.css`; no build/theme-service coupling |
| New components live under `BlazorWebApp/Components/Layouts/` | Follows your preference; dedicated folder for structural layout primitives |
| Variant.Text sweep is a final pass after structural refactor | Decouples visual variant changes from layout changes; easier to review/rollback independently |
| Tab elevation exposed as a shared C# constant (`LayoutDefaults.TabsElevation`) | `MudTabs.Elevation` is `int`, cannot be a CSS var; centralizing in a constant keeps the "tweak in one place" goal |
| Collapsible sidebar limited to Resources / CivitAI | Pages where content benefits from added width (galleries); Prompts sidebar is used too frequently to justify a toggle |
| `SidebarCollapsed` persisted via `IStateService` (DB-backed) | Parity with `ActiveTabIndex`; survives reload |
| Danbooru gets a stub `Library` tab now | User-approved scaffolding; placeholder content until feature is designed |

### Conventions

- All tabbed pages consume a layout component - no page renders `MudTabs` directly anymore (tabs become an internal detail of `TabbedPageShell`).
- Pages provide `Tabs` (a list of tab definitions) and, per tab, either a `Content` fragment (single slot) or `Sidebar` + `Content` / `Topbar` + `Content` fragments, depending on the chosen variant.
- Spacing (`padding`, `gap`, `margin` between shell elements) must reference the CSS tokens - no hard-coded `px-5`, `pa-4`, etc. on the shell or top-level panel paper.
- Card/paper backgrounds on Sidebar, Topbar, and Content surfaces are uniform (`Elevation=1`, same border-radius).

---

## Proposed CSS Tokens (baseline values)

Defined in `:root` in `wwwroot/site.css`:

```css
:root {
    --app-gutter-outer: 12px;     /* page <-> window/navbar edges */
    --app-gutter-inner: 8px;      /* between shell elements (tabs<->panels, sidebar<->content) */
    --app-sidebar-width: 22%;     /* proportional sidebar width */
    --app-sidebar-min: 240px;
    --app-sidebar-max: 340px;
    --app-sidebar-rail-width: 40px; /* collapsed rail */
    --app-shell-max-width: 1600px; /* overall page max-width clamp */
    --app-surface-radius: 4px;    /* shared radius for sidebar/topbar/content surfaces */
}
```

Alongside, a shared C# constants class at `BlazorWebApp/Components/Layouts/LayoutDefaults.cs`:

```csharp
public static class LayoutDefaults
{
    public const int TabsElevation = 4;
    public const int SurfaceElevation = 1; // sidebar/topbar/content paper
}
```

Values are baselines; tuning happens in Phase 1 against the Prompts screenshot.

---

## Proposed Component Surface

Under `BlazorWebApp/Components/Layouts/`:

- `TabbedPageShell.razor` - outer shell. Renders centered `MudTabs`, applies outer gutter, clamps max-width. Accepts `ActivePanelIndex` binding and a list of `TabbedPageTab` definitions.
- `TwoColumnLayout.razor` - `[Sidebar][Content]` grid. Parameters: `Sidebar` + `Content` `RenderFragment`s, `Collapsible` bool, `@bind-Collapsed` bool, `SidebarTitle` string (optional).
- `TopbarLayout.razor` - `[Topbar]` above `[Content]` stacked. Parameters: `Topbar` + `Content` `RenderFragment`s.
- `ContentOnlyLayout.razor` - single `Content` slot; exists so all pages go through a layout and gutter tokens apply uniformly.

Pages compose: `TabbedPageShell` -> per-tab one of the three layouts.

---

## Implementation Phases

### Phase 1: Foundation (tokens + design doc)
**Objective:** Establish the single source of truth for spacing and document the layout convention before any page is touched.
**Complexity:** 2 points
**Status:** [x] Complete

#### Steps
- [x] Add CSS tokens to `wwwroot/site.css` under `:root`
- [x] Extend `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` with a new **Layout** section covering tabbed-page shell, three variants, collapsible sidebar behavior, and token reference
- [x] Visually verify tokens match the current Prompts baseline (tune values if needed)

#### Success Criteria
- [x] `:root` exposes the agreed tokens
- [x] Design-language doc describes the three variants and when to use each
- [x] No visual change yet (pages still use their own spacing)

---

### Phase 2: Layout components
**Objective:** Build the reusable shell + three layout variants, including the collapsible sidebar.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Create `Components/Layouts/TabbedPageShell.razor` (+ scoped CSS)
- [ ] Create `Components/Layouts/TwoColumnLayout.razor` with `Collapsible` + `@bind-Collapsed` and rail-mode CSS
- [ ] Create `Components/Layouts/TopbarLayout.razor`
- [ ] Create `Components/Layouts/ContentOnlyLayout.razor`
- [ ] Add `SidebarCollapsed` fields to the relevant state blocks on `IStateService` (per page that uses Two-column)
- [ ] Manual smoke test in a throwaway route before migrating real pages

#### Success Criteria
- Components compile, render, and respond to token changes
- Collapsible sidebar toggles smoothly and persists across tab switches
- No regressions on unrelated pages

---

### Phase 3: Prompts page (baseline validation)
**Objective:** Migrate the current baseline page first to validate the components against the reference design.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Refactor `Pages/Prompts.razor` to use `TabbedPageShell`
- [x] Split `PromptsPanel`, `WildcardsTab`, `LLMToolsTab` into explicit `Sidebar` / `Content` fragments consumed by `TwoColumnLayout`
- [x] Remove the `PanelClass="px-5 py-2"` in favor of shell-driven gutters
- [x] Verify gutter uniformity matches the reference screenshot

#### Success Criteria
- [x] Gap tabs<->sidebar == gap sidebar<->content == gap tabs<->content (all = inner gutter)
- [x] Outer gutter uniform around the page
- [x] No functional regression in Prompts tabs

---

### Phase 4: Resources page
**Objective:** Adopt Two-column, moving the filter/search UI from content into the sidebar.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Refactor `Pages/Resources.razor` through `TabbedPageShell`
- [x] Extract the filter/search cluster currently inside `ResourcePanel` into a `Sidebar` fragment
- [x] Add `State.Resources.SidebarCollapsed`
- [x] Verify resource grid has full width of content column and does not reflow oddly when sidebar collapses

#### Success Criteria
- [x] Search/filter controls live in the sidebar
- [x] Resource grid is the sole content
- [x] Collapsible toggle works

---

### Phase 5: CivitAI page
**Objective:** Same Two-column migration as Resources.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps
- [x] Refactor `Pages/CivitAI.razor` through `TabbedPageShell` + `TwoColumnLayout`
- [x] Move search form into sidebar
- [x] Add `State.CivitAI.SidebarCollapsed` (or existing equivalent state block)

#### Success Criteria
- Visual parity with Resources' structure
- Collapsible sidebar functional

---

### Phase 6: Scheduler page
**Objective:** Apply the three variants in one page - each tab picks the right layout.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Refactor `Pages/Scheduler.razor` through `TabbedPageShell`; drop `MudContainer` + `PanelClass="pa-4"`; raise elevation to match other pages
- [x] Jobs (Editor) tab -> `TwoColumnLayout` (existing editor/browse split)
- [x] Runs tab -> `ContentOnlyLayout`
- [x] Results tab -> `TopbarLayout` (filter/search topbar + images content card)

#### Success Criteria
- All three Scheduler tabs share the same outer gutter and tab styling as Prompts
- Results tab filter UI rendered in the topbar card
- Jobs tab's sidebar is collapsible if useful (defer decision to implementation)

---

### Phase 7: Danbooru page (tabbed shell + Topbar variant)
**Objective:** Promote Danbooru to a tabbed page with a single `Browse` tab today (Topbar variant) and a scaffold for `Library` later.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps
- [x] Refactor `Pages/Danbooru.razor` through `TabbedPageShell` with a `Search` tab
- [x] Move the current top `MudGrid` search cluster into a `TopbarLayout` Topbar fragment
- [x] `Library` tab scaffolded as a stub (`ContentOnlyLayout` + "Coming soon" placeholder)

#### Success Criteria
- Danbooru uses the shared shell and tokens
- No regression in search/results behavior

---

### Phase 8: Variant.Outlined -> Variant.Text sweep
**Objective:** Enforce the existing design-language rule across all tabbed pages touched above, plus any stragglers discovered during the sweep.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps
- [x] Grep all files under `Pages/` and `Components/` (Prompts, Resources, CivitAI, Scheduler, Danbooru) for `Variant.Outlined`
- [x] Replace with `Variant.Text` unless the element is a documented emphasis case (primary dialog actions, warnings)
- [x] Note any ambiguous cases for user review rather than silently changing them

#### Success Criteria
- Form controls on tabbed pages consistently use `Variant.Text`
- Emphasis-variant exceptions are documented

---

### Phase 9: Final documentation pass
**Objective:** Close out the docs and capture lessons.
**Complexity:** 1 point
**Status:** [x] Complete

#### Steps
- [x] Update `04-UI-DESIGN-LANGUAGE.md` with any corrections discovered during implementation
- [x] Add a short "Layout components" reference (component names, slots, when to use)
- [x] Mark `MAIN_PLAN.md` status = complete, add changelog entry

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|---|---|---|
| Existing per-page CSS (`PromptsPanel.razor.css`, `Danbooru.razor.css`) contains spacing that fights the new tokens | Audit each `.razor.css` as we migrate that page; delete or rewrite conflicting rules | 3 |
| Collapsible sidebar breaks existing content that assumes a fixed width | Content column is fluid (grid `1fr`); any child assuming fixed width gets fixed during its page's phase | 3 |
| `IStateService` state-block expansion requires care (these blocks are persisted in DB as JSON via the `State` entity) | Add properties with safe defaults; confirm no migration needed since existing rows deserialize with defaults for new fields | 2 |
| Scheduler currently uses `MudContainer MaxWidth=ExtraLarge`; switching may reflow the Results masonry | Validate Results tab visually after Phase 6 step; adjust `--app-shell-max-width` if needed | 2 |
| `Variant.Text` sweep may catch intentional emphasis usages | Flag ambiguous cases in Phase 8 rather than auto-replacing | 2 |
| Danbooru `Library` tab not yet designed | Scaffold only if user approves in Phase 7; otherwise keep single-tab shell | 1 |

---

## Changelog

| Phase | Changes |
|---|---|
| Planning | Initial plan created |
| Planning | Open questions resolved; advanced to Phase 1 |
| Phase 1 | Completed - CSS tokens, `LayoutDefaults` constants, design-doc Layout section, `IMPLEMENTATION_GUIDE.md` cross-reference |
| Phase 2 | Completed - 4 layout primitives (`TabbedPageShell`, `TwoColumnLayout` with collapsible rail, `TopbarLayout`, `ContentOnlyLayout`); `::deep` scoped-CSS pattern documented |
| Phase 3 | Completed - Prompts page + 3 sub-tabs migrated; 3 child components flattened; `--app-surface-padding` token added; "children flush" convention promoted to both design docs |
| Phase 4 | Completed - Resources page migrated with collapsible sidebar; scrollable content pattern + tri-state icon-cycler pattern introduced; card-size control added |
| Phase 5 | Completed - CivitAI page migrated; `CivitaiPanel` wrapper retired; `CivitaiImageCard` made parent-sizable via CSS var; card-size cycler ported from Resources |
| Phase 6 | Completed - Scheduler page migrated; all three layout variants demonstrated (Two-column / Content-only / Topbar); `MudContainer` anti-pattern removed; inner papers flattened on Jobs / Edit Job / Runs |
| Phase 7 | Completed - Danbooru page migrated; `TabbedPageShell` + `TopbarLayout` + `ContentOnlyLayout` combined; Library stub scaffolded |
| Phase 8 | Completed - 29 form-control `Variant.Outlined` -> `Variant.Text` replacements across 12 files; intentional emphasis usages (chips, alerts, toggle-state buttons, `MudButtonGroup`) preserved |
| Phase 9 | Completed - `04-UI-DESIGN-LANGUAGE.md` gets Layout-components reference and scrollable-content pattern sections; plan closed |

---

## Resolved Decisions (pre-Phase 1)

1. **Baseline tokens** - accepted as-is (`outer=12px / inner=8px / sidebar=22% (240-340px) / rail=40px / shell-max=1600px`).
2. **Elevation baseline** - aligned on `TabsElevation=4`, `SurfaceElevation=1`, exposed via `LayoutDefaults` constants class.
3. **Danbooru `Library` tab** - stub included in Phase 7.
4. **Collapsible sidebar scope** - Resources and CivitAI only. Prompts keeps a static sidebar.
5. **`SidebarCollapsed` persistence** - DB-backed via `IStateService` per-page state blocks.

---

## References
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - existing design language (Variant.Text rule lives here)
- `BlazorWebApp/Pages/Prompts.razor` - current visual baseline
- `BlazorWebApp/Pages/Scheduler.razor` - divergent spacing example
- `BlazorWebApp/Pages/Danbooru.razor` - future-tabbed page
- `BlazorWebApp/wwwroot/site.css` - host for global tokens
