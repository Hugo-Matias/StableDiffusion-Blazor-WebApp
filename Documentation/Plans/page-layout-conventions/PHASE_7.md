# Phase 7 - Danbooru page

## Status
**Phase:** 7 - Complete
**Build Status:** Passing | **Tests:** user-verified (Search + Library tabs)

---

## Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

---

## Objective

Migrate the Danbooru page to the layout system. Introduce the tabbed shell with two tabs:

1. **Search** (existing functionality) -> `TopbarLayout` (search + blacklist form in Topbar, masonry gallery in Content).
2. **Library** (stub placeholder) -> `ContentOnlyLayout` with a "Coming soon" card. Per resolved decision #3 in the plan, the stub lands now so the navigation shape is final.

---

## Context

### Current state
- `Pages/Danbooru.razor` is not tabbed. It renders an inline `MudGrid` with Search text-field + Search button + Blacklist tags text-field, followed by an infinite-scroll masonry.
- `AppStateDanbooru` has a single field `SearchString`.

### Target state
- `TabbedPageShell` wraps two `MudTabPanel`s (Search + Library) with active-index bound to new `AppStateDanbooru.ActiveTabIndex`.
- Search content wrapped in `TopbarLayout`; Library content wrapped in `ContentOnlyLayout` (stub).
- Masonry + infinite-scroll wiring (`OnAfterRenderAsync`, JS module, DotNetObjectReference) preserved unchanged.

### Key considerations
- Infinite scroll logic is tied to `Danbooru` component lifecycle; only the markup wrapping changes.
- `DanbooruSearchesDrawer` is a drawer triggered by the magnifying-glass adornment on the Search text-field; keep it rendered outside the layout (as a sibling) so it floats above everything.
- `ImageViewer` (external-image viewer) similarly stays at page level.

---

## Execution Checklist

### Step 1: `AppStateDanbooru.ActiveTabIndex`
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Add `public int ActiveTabIndex { get; set; } = 0;`

---

### Step 2: `Pages/Danbooru.razor` - adopt `TabbedPageShell` with two tabs
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Wrap existing search form + masonry markup in `<MudTabPanel Text="Search" Icon="@Icons.Material.Filled.Search">` within `<TabbedPageShell @bind-ActivePanelIndex="State.State.Danbooru.ActiveTabIndex">`
- [ ] Inside the Search panel, apply `TopbarLayout` with the form cluster in Topbar and masonry in Content
- [ ] Add a second `<MudTabPanel Text="Library" Icon="@Icons.Material.Filled.CollectionsBookmark">` with `ContentOnlyLayout` showing a simple placeholder (`MudAlert Severity=Info` or centered stack with icon + message)
- [ ] Keep `DanbooruSearchesDrawer` and `ImageViewer` as siblings of `TabbedPageShell` at page level

---

### Step 3: Visual + functional verification
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Search tab: form in topbar, masonry in content, infinite scroll still works
- [ ] `DanbooruSearchesDrawer` opens from the search-field adornment and overlays correctly
- [ ] `ImageViewer` still opens when clicking an image
- [ ] Library tab: stub renders
- [ ] Tab state persists across reload
- [ ] Gutters match baseline

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 1 | `ActiveTabIndex` on Danbooru state |
| 2 | [x] | 2 | Tabbed shell + Search topbar + Library stub |
| 3 | [x] | 1 | Verification - approved |

---

## Issues & Resolutions

_None yet._

---

## Phase Summary

Danbooru migrated to the tabbed layout system. Library tab scaffolded as a stub so the final navigation shape is now in place. Search form reorganized vertically (Search row + Blacklist row) to fit the `TopbarLayout` better than the old `MudGrid xs=10/2/12`.

### Accomplishments
1. `TabbedPageShell` + `TopbarLayout` + `ContentOnlyLayout` all exercised on a single page.
2. Library tab placeholder ready for future expansion (saved searches / saved images).
3. Infinite-scroll lifecycle untouched - only markup wrapping changed.

### Metrics
- Modified razor files: 2 (`Pages/Danbooru.razor`, `Models/AppState.cs`).

**Phase Status:** Complete [x]
