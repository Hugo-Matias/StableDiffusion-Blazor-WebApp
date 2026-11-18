# Phase 6 - Scheduler page

## Status
**Phase:** 6 - Complete
**Build Status:** Passing | **Tests:** user-verified across Jobs / Runs / Results tabs

---

## Implementation Guidelines

Same conventions as prior phases. Watch out for the two Scheduler-specific gotchas noted in Context.

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

---

## Objective

Scheduler is the showcase page for all three layout variants:

- **Jobs tab** (`SchedulerEditorTab`) -> `TwoColumnLayout` (job list + draft controls on left, editor on right). **Non-collapsible** (editor needs both panes visible while working).
- **Runs tab** (`SchedulerRunsTab`) -> `ContentOnlyLayout` (single list).
- **Results tab** (`SchedulerResultsTab`) -> `TopbarLayout` (job/run selector toolbar + results gallery).

This exercises the full layout API in one page and gives us a reference implementation for future contributors.

---

## Context

### Current state
- `Pages/Scheduler.razor` uses `MudContainer MaxWidth=ExtraLarge` (anti-pattern - should use `--app-shell-max-width`) and renders `MudTabs` directly with `Elevation=1 ApplyEffectsToContainer=true PanelClass="pa-4"` (the exact anti-patterns we documented).
- Route-based tab switching: `/scheduler`, `/scheduler/jobs`, `/scheduler/runs`, `/scheduler/results` drive `_activeIndex` through `ApplyRouteState()` on `OnParametersSet` and `Nav.LocationChanged`. `OnTabChanged` navigates to the matching URL on tab click.
- Each tab has icons (`Edit`, `PlaylistPlay`, `Collections`).
- `SchedulerEditorTab` has its own 4/8 split via `<MudGrid><MudItem xs=12 md=4 lg=3>` + `xs=12 md=8 lg=9`.
- `SchedulerRunsTab` is a single list with empty state + progress.
- `SchedulerResultsTab` has an inline `MudStack Row Class="mb-3"` header toolbar above results.

### Target state
- `Pages/Scheduler.razor`: drop `MudContainer`; shell clamp comes from `TabbedPageShell` via `--app-shell-max-width`. Preserve route-binding handlers exactly.
- `SchedulerEditorTab`: `TwoColumnLayout` (no Collapsible); left pane stays ~25% width, right pane gets the rest.
- `SchedulerRunsTab`: wrap in `ContentOnlyLayout`.
- `SchedulerResultsTab`: wrap in `TopbarLayout`; move the header toolbar into the Topbar slot.
- Apply the flush-children rule: no `pa-*` on slot roots, no extra `MudPaper` wrappers at the root of any tab component.

### Key considerations
- `SchedulerEditorTab` is ~500 lines of nested `MudGrid`/`MudItem` inside the right pane. Only the **outer** split changes - everything else stays intact.
- `SchedulerResultsTab` toolbar might be tall; `TopbarLayout` flexes naturally.
- Tab icons on `MudTabPanel` pass through `TabbedPageShell` unchanged (it just forwards `ChildContent` to `MudTabs`).
- The existing `ActivePanelIndex` + `ActivePanelIndexChanged` callback pattern on Scheduler (non-two-way bind) is supported by `TabbedPageShell`.

---

## Execution Checklist

### Step 1: `Pages/Scheduler.razor` - adopt `TabbedPageShell`
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Remove `<MudContainer MaxWidth=ExtraLarge>` wrapper
- [ ] Replace `<MudTabs ...>` with `<TabbedPageShell ActivePanelIndex="@_activeIndex" ActivePanelIndexChanged="OnTabChanged">`
- [ ] Drop `Elevation`, `ApplyEffectsToContainer`, `PanelClass`, `Rounded`, `Centered`
- [ ] Preserve `MudTabPanel` icons and the three child tab components
- [ ] Preserve route-sync logic (`ApplyRouteState`, `OnLocationChanged`)

---

### Step 2: `SchedulerEditorTab` - consume `TwoColumnLayout`
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Replace outer `<MudGrid>` + 2 `<MudItem>` with `<TwoColumnLayout>`
- [ ] Left pane (job list + restore banner) goes into `Sidebar`
- [ ] Right pane (editor) goes into `Content`
- [ ] No root `pa-*` on slot content; layout surface owns padding

---

### Step 3: `SchedulerRunsTab` - consume `ContentOnlyLayout`
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Wrap existing markup in `<ContentOnlyLayout>`
- [ ] Remove any redundant root `MudPaper`/padding

---

### Step 4: `SchedulerResultsTab` - consume `TopbarLayout`
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Move the `<MudStack Row ... Class="mb-3">` header into the `Topbar` slot (drop the `mb-3`; layout gutter replaces it)
- [ ] Gallery content moves into `Content`

---

### Step 5: Visual + functional verification
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Tab routing still works: `/scheduler`, `/scheduler/jobs`, `/scheduler/runs`, `/scheduler/results` all land on the correct tab
- [ ] Tab clicks update the URL correctly
- [ ] Editor tab: draft restore banner, save/load, variation editing all work
- [ ] Runs tab: list renders; empty state still looks correct
- [ ] Results tab: toolbar (job/run selector) renders in topbar; results gallery below
- [ ] Gutters match Prompts/Resources baseline
- [ ] Tab icons still render

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 2 | `Scheduler.razor` shell (MudContainer dropped, route sync preserved) |
| 2 | [x] | 3 | Editor tab Two-column + Jobs/Edit Job inner papers flattened |
| 3 | [x] | 1 | Runs tab Content-only + MudTable elevation removed |
| 4 | [x] | 2 | Results tab Topbar |
| 5 | [x] | 1 | Verification - approved |

---

## Issues & Resolutions

### Issue 1: Inner paper wrappers caused double-elevation on Jobs / Edit Job / Runs table
**Impact:** After the initial migration landed, the Jobs sidebar card, the Edit Job content card, and the Runs table each showed a visible inset shadow inside the layout surface.
**Root cause:** Legacy `MudPaper Class="pa-*" Elevation="1"` wrappers at the root of each tab's inner content; `MudTable Elevation="1"` on the runs table. All violated the "children render flush" convention established in Phase 3.
**Resolution:**
- `SchedulerEditorTab` - `MudPaper Class="pa-3" Elevation="1"` (Jobs) -> plain `<div>`; `MudPaper Class="pa-4" Elevation="1"` (Edit Job) -> plain `<div>`. Inner `Elevation="0" Outlined` sub-panels preserved as intentional focus surfaces.
- `SchedulerRunsTab` - `MudTable Elevation="1"` -> `Elevation="0"`.

---

## Commit Checkpoints

- [x] After Step 1 (shell)
- [x] After Steps 2-4 (all three tabs migrated)
- [x] After Step 5 (user visual approval + inner-paper flush fixes)

---

## Phase Summary

Scheduler page now showcases all three layout variants: Two-column (Jobs), Content-only (Runs), Topbar (Results). `MudContainer MaxWidth=ExtraLarge` anti-pattern eliminated; the page now respects `--app-shell-max-width`.

### Accomplishments
1. Three layout variants demonstrated in one page - useful as a reference for future contributors.
2. Route-based tab switching (`/scheduler/{Tab}`) preserved via `ActivePanelIndex` + `ActivePanelIndexChanged` passthrough on `TabbedPageShell`.
3. Three inner paper wrappers flattened per the Phase-3 flush convention.
4. Runs table elevation dropped; now sits flush inside its surface.

### Metrics
- Modified razor files: 4 (`Scheduler.razor`, 3 `Scheduler*Tab.razor`).
- New files: 0.
- New tokens: 0.

### Deferred Items
- None.

**Phase Status:** Complete [x]
