# Phase 2 - Layout components

## Status
**Phase:** 2 - Complete
**Build Status:** Passing | **Tests:** smoke route verified, torn down

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

Build the reusable tabbed-page shell and three layout variants, including the collapsible sidebar for Two-column. After this phase, we have working primitives ready to be consumed by Phases 3+.

---

## Context

### Deliverables (under `BlazorWebApp/Components/Layouts/`)
- `TabbedPageShell.razor` + `.razor.css` - outer shell. Renders centered `MudTabs` with `Elevation=LayoutDefaults.TabsElevation`, applies `--app-gutter-outer`, clamps to `--app-shell-max-width`, **never** sets `PanelClass`. Accepts `@bind-ActivePanelIndex` and a `ChildContent` render fragment (consumer places `MudTabPanel`s inside).
- `TwoColumnLayout.razor` + `.razor.css` - CSS-grid `[Sidebar][Content]` with gap = `--app-gutter-inner`. Parameters: `Sidebar` + `Content` render fragments; `Collapsible` bool; `@bind-Collapsed` bool; `SidebarTitle` string (optional, shown in rail mode as vertical label).
- `TopbarLayout.razor` + `.razor.css` - flex-column `[Topbar][Content]`, gap = `--app-gutter-inner`.
- `ContentOnlyLayout.razor` + `.razor.css` - single `Content` slot honoring the shell gutters. Thin wrapper so all pages go through a layout.

### Key design notes
- Sidebar, topbar, and content surfaces render as `MudPaper Elevation=LayoutDefaults.SurfaceElevation Square=false` with `border-radius: var(--app-surface-radius)`. Padding inside each surface is the consumer's responsibility.
- `TwoColumnLayout` uses `grid-template-columns: clamp(var(--app-sidebar-min), var(--app-sidebar-width), var(--app-sidebar-max)) 1fr;` when expanded; switches to `var(--app-sidebar-rail-width) 1fr;` when collapsed. Transition on `grid-template-columns`.
- Collapse toggle: a small `MudIconButton` with chevron icon, positioned at the top-right of the sidebar paper (expanded) or centered in the rail (collapsed). Icon rotates 180deg via CSS.
- When collapsed, the sidebar `Sidebar` render fragment is **not rendered** (content is replaced by just the toggle + optional vertical `SidebarTitle`) - prevents scroll-traps and keeps DOM light.
- `TabbedPageShell` does not know about layout variants; panels are free-form `ChildContent`.

### Smoke-test route
A throwaway dev route `/layouts-smoke` under `Pages/LayoutsSmoke.razor` exercising all three variants across three tabs. Deleted before Phase 3 commit (tracked as a step below).

---

## Execution Checklist

### Step 1: `TabbedPageShell`
**Complexity:** 2
**Status:** [x] Complete

#### Tasks
- [x] Create `Components/Layouts/TabbedPageShell.razor` + scoped CSS
- [x] `@bind-ActivePanelIndex`, `ChildContent` parameters
- [x] Apply outer gutter + shell max-width via scoped CSS referencing tokens
- [x] Wire `Elevation`, `Centered`, `Rounded` from `LayoutDefaults`

#### Changes Made
- `Components/Layouts/TabbedPageShell.razor` + `.razor.css` - shell wraps `MudTabs` with outer gutter, shell max-width, and `::deep .mud-tabs-panels` top padding = inner gutter.
- `_Imports.razor` - added `@using BlazorWebApp.Components.Layouts`.

---

### Step 2: `ContentOnlyLayout`
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Create `Components/Layouts/ContentOnlyLayout.razor` + scoped CSS
- [x] Single `Content` render fragment wrapped in a `MudPaper` surface with `SurfaceElevation`

#### Changes Made
- `Components/Layouts/ContentOnlyLayout.razor` + `.razor.css`.

---

### Step 3: `TopbarLayout`
**Complexity:** 2
**Status:** [x] Complete

#### Tasks
- [x] Create `Components/Layouts/TopbarLayout.razor` + scoped CSS
- [x] `Topbar` + `Content` render fragments, vertical stack, gap = `--app-gutter-inner`
- [x] Both surfaces use `SurfaceElevation` + `--app-surface-radius`

#### Changes Made
- `Components/Layouts/TopbarLayout.razor` + `.razor.css`.

---

### Step 4: `TwoColumnLayout` (non-collapsible)
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Create `Components/Layouts/TwoColumnLayout.razor` + scoped CSS
- [x] CSS grid with clamped sidebar width
- [x] `Sidebar` + `Content` fragments, both `SurfaceElevation`
- [x] `Collapsible=false` path only for now

#### Changes Made
- `Components/Layouts/TwoColumnLayout.razor` + `.razor.css` - grid with `clamp(min, %, max)` sidebar column.

---

### Step 5: Collapsible sidebar support
**Complexity:** 5
**Status:** [x] Complete

#### Tasks
- [x] Add `Collapsible` bool and `Collapsed` + `CollapsedChanged` two-way binding to `TwoColumnLayout`
- [x] Toggle `MudIconButton` with chevron icon (switches direction)
- [x] Rail mode: `grid-template-columns` switches to `--app-sidebar-rail-width 1fr`; 200ms ease transition
- [x] Optional `SidebarTitle` shown as vertical label in rail
- [x] Sidebar content not rendered while collapsed (kept DOM light)

#### Changes Made
- `Components/Layouts/TwoColumnLayout.razor` - added `Collapsible`, `Collapsed` (bindable), `SidebarTitle` params; chevron toggle repositions between expanded (top-right) and rail (centered).
- `Components/Layouts/TwoColumnLayout.razor.css` - `.collapsed` modifier, vertical rail title styling, toggle positioning, transition. Rewritten to use `::deep` selectors (see Issue 1).
- `Components/Layouts/TopbarLayout.razor.css` - same `::deep` fix preventively applied.
- `Components/Layouts/ContentOnlyLayout.razor` - wrapped `MudPaper` in a native `<div class="app-content-only">` so `::deep` has a parent.
- `Components/Layouts/ContentOnlyLayout.razor.css` - updated for the wrapper.
- `Pages/LayoutsSmoke.razor` - Two-column tab exercises `Collapsible=true` with `@bind-Collapsed` and a `SidebarTitle`.

---

### Step 6: Smoke-test route
**Complexity:** 2
**Status:** [x] Complete (built incrementally with each step)

#### Tasks
- [x] Create `Pages/LayoutsSmoke.razor` at `/layouts-smoke` with three tabs (one per variant)
- [x] Fill each tab with placeholder cards to visually inspect spacing
- [x] Exercise the collapsible toggle in the Two-column tab

#### Changes Made
- `Pages/LayoutsSmoke.razor` - three tab panels, one per variant. Marked as temporary; to be removed in Step 7.

---

### Step 7: Teardown smoke route before Phase 3
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Remove `Pages/LayoutsSmoke.razor` once user approves components

#### Changes Made
- `Pages/LayoutsSmoke.razor` deleted.

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 2 | TabbedPageShell |
| 2 | [x] | 1 | ContentOnlyLayout |
| 3 | [x] | 2 | TopbarLayout |
| 4 | [x] | 3 | TwoColumnLayout (non-collapsible) |
| 5 | [x] | 5 | Collapsible sidebar - scoped-CSS bug found and fixed |
| 6 | [x] | 2 | Smoke route |
| 7 | [x] | 1 | Teardown |

---

## Issues & Resolutions

### Issue 1: Chevron toggle rendered over the content column
**Impact:** The collapse chevron in `TwoColumnLayout` appeared on top of the content panel instead of inside the sidebar's top-right corner.
**Root cause:** Blazor scoped CSS appends a `b-xxxxx` attribute to HTML elements declared in a `.razor`, but MudBlazor components do not reliably forward that attribute to their rendered root. The rule `.app-sidebar-surface { position: relative; }` targeted `<MudPaper>` and never landed; the absolutely-positioned toggle then anchored to a further ancestor and `right: 4px` placed it over the content column.
**Resolution:** Rewrote the scoped rules in `TwoColumnLayout.razor.css`, `TopbarLayout.razor.css`, and `ContentOnlyLayout.razor.css` to target descendants of a native-element parent via `::deep`. `ContentOnlyLayout` additionally gained a `<div class="app-content-only">` wrapper so `::deep` has a parent to hang off.
**Takeaway (captured for future layout work):** Any rule targeting a class applied on a MudBlazor component must be written as `.native-parent ::deep .class` - do not rely on scoped-attribute forwarding.

---

## Commit Checkpoints

- [x] After Steps 1-4 (core layouts, no collapsible yet)
- [x] After Step 5 (collapsible sidebar)
- [x] After Step 6 (smoke route) - user visual approval
- [x] After Step 7 (teardown)

---

## Phase Summary

Four reusable layout primitives under `Components/Layouts/` ready to be consumed by Phases 3+:
- `TabbedPageShell` - centered `MudTabs`, token-driven outer/inner gutters, shell max-width clamp.
- `ContentOnlyLayout`, `TopbarLayout`, `TwoColumnLayout` - the three allowed tab-body variants, all consuming the same tokens.
- `TwoColumnLayout` supports an opt-in collapsible sidebar with rail mode and two-way `@bind-Collapsed`.

### Accomplishments
1. Three layout variants + shell working against a shared token system.
2. Collapsible rail with smooth transition and persistent-ready state binding.
3. Documented Blazor + MudBlazor scoped-CSS pattern (use `::deep` off a native parent) for all downstream pages.

### Metrics
- New files: 8 (4 components + 4 scoped CSS).
- Modified files: 2 (`_Imports.razor`, `site.css` in Phase 1).
- Deleted files: 1 (`LayoutsSmoke.razor`).

### Deferred Items
- None.

**Phase Status:** Complete [x]
