# UI Design Test Bed - Implementation Plan

## Status

**Current Phase:** Phase 8 (Audit Report) - deferred

**Completed:**

- Phase 1: Foundation - shell, Tokens tab, Layout tab.
- Phase 2: Form Controls - inventory, deviations, cascading-selector prototype.
- Phase 3: Buttons & Actions - inventory, deviations, `dt-proto-btn` lab.
- Phase 4: Cards & Surfaces - inventory, deviations, `dt-proto-card` lab.
- Phase 5: Dialogs & Modals - dialog token table, MudDialog patterns, custom-overlay inventory, version-selector + scrollable-description prototype.
- Phase 6: Navigation - `TabbedPageShell` vs component-context `MudTabs`, NavBar inventory, `::deep .mud-tab` deviations, live tabs configuration matrix prototype.
- Phase 7: Data Display - card grid + pagination + scrollable container + MudTable inventory, scroll-offset and grid-min-width deviations, token-driven grid prototype with empty state.
- Phase 9: Lean-down + Design Language Sync - extracted `.app-grid` (with `--app-card-min` / `--app-grid-gap`) and `.app-empty-state` primitives into `site.css`; testbed Data Display lab now consumes the primitives directly; `04-UI-DESIGN-LANGUAGE.md` updated with the two new tokens and primitive usage sections.

---

## Problem Statement

The BlazorWebApp has evolved organically. There is documentation (`Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`), tokens (`site.css`), and a few extracted primitives (`TabbedPageShell`, layout variants, `.send-to-btn`), but in practice each page picks its own button class, its own inline `style="..."`, its own MudBlazor variant. The result:

- Multiple custom button classes side-by-side (`.send-to-btn`, `.viewer-btn`, `.action-btn`, `.contextual-nav-action-btn`, `.layer-control-btn`, Bootstrap `.btn-secondary`)
- Inline `Style="flex:1;"`, `Style="position: absolute;"` on MudIconButton in toolbars
- `MudChip` used both for read-only status and for togglable filters with no visual distinction
- Component-scoped `.razor.css` files with `::deep` overrides and `!important` on padding
- No single place to **see** all of these together and decide "these three should converge."

The goal is **not** another reference document — `04-UI-DESIGN-LANGUAGE.md` is the source of truth for documented rules. The goal is a place to **iterate on visuals** that MudBlazor doesn't give us cleanly, propose unified replacements, and converge on a custom design language layered on top of MudBlazor (not replacing it).

---

## Proposed Solution

Create a permanent dev page (`/dev/design-testbed`) that is a **prototyping playground**, not a reference site. For each UI primitive used in production:

1. **Inventory** — show every variant actually used in the codebase with a file-link to a real usage
2. **Deviation callout** — flag inline-styled / one-off variants with file:line so they're targets for unification
3. **Prototype** — propose a unified replacement (custom class, restyled MudBlazor wrapper, or new primitive) sitting next to the current versions
4. **Compare** — only when meaningful ("correct vs avoid" used sparingly, not for every section)

### Core Principles

- **Playground, not docs** — captions are one line, no rule recitations, no decision matrices that duplicate `04-UI-DESIGN-LANGUAGE.md`
- **Inventory-first** — every section starts with "what we currently use, where"
- **Inline CSS / scoped CSS allowed for prototypes** — that's the whole point; once a prototype lands, extract it and update the doc
- **Visible in all environments** — staging review benefits from the page being reachable everywhere
- **Improve MudBlazor globally, don't replace it** — prototypes wrap or restyle MudBlazor components when possible, only fall back to plain HTML buttons when MudBlazor cannot produce the look (as `.send-to-btn` already does)

---

## Key Decisions

### 1. Page Structure

- **Route:** `/dev/design-testbed` (dev environment only)
- **Shell:** Use `TabbedPageShell` to mirror production page structure
- **Tabs:** One tab per design system domain:
  - **Tokens & Variables** - spacing, colors, elevations, radius values
  - **Layout System** - `TwoColumnLayout`, `TopbarLayout`, `ContentOnlyLayout` examples
  - **Form Controls** - inputs, selects, autocomplete, variants, density
  - **Buttons & Actions** - `.send-to-btn`, `MudButton` variants, icon buttons
  - **Cards & Surfaces** - elevation policy, surface padding, focus surfaces
  - **Dialogs & Modals** - `AssetViewer`, dialog tokens, expansion patterns
  - **Navigation** - tabs, nested tabs, sidebar collapse
  - **Data Display** - grids, lists, pagination, scrollable containers
  - **Audit Report** - automated scan of existing pages highlighting pattern violations

### 2. Section anatomy (the playground template)

Each section is a single domain (Buttons, Form Controls, Surfaces, ...). Inside, structure is:

1. **Inventory grid** — one row per variant currently used in production. Columns: live render · short label · `file.razor:line` link.
2. **Deviations** — bullet list of inline `Style="..."`, custom `.razor.css` overrides, Bootstrap-class leakage, etc., each with `file.razor:line`.
3. **Prototype lab** — sandbox area with `dt-proto-*` classes scoped to `DesignTestbed.razor.css` proposing a unified replacement. Toggle controls (size / tone / density) where useful so designs can be iterated visually.
4. **Compare (optional)** — only when there's a clear correct/avoid worth showing.

No rule recitations, no excerpts copied from `04-UI-DESIGN-LANGUAGE.md`. Captions are at most one line; if more is needed, link to the doc.

### 3. Iteration workflow

- Prototypes live as `dt-proto-*` (or `dt-*`) classes in `DesignTestbed.razor.css` and inline styles in the page itself — both are explicitly allowed here.
- When a prototype is approved:
  1. Extract to a shared primitive (token, class in `wwwroot/css/`, or shared `.razor` component)
  2. Update `04-UI-DESIGN-LANGUAGE.md` with the new rule
  3. Replace inline test-bed CSS with the extracted primitive (the page now consumes its own output, proving the extraction works)
  4. Migrate one or two production sites in the same PR; track the rest as a refactor follow-up

### 4. Audit Automation

- Build a lightweight analyzer service (`DesignAuditService`) that:
  - Scans `.razor` files for known anti-patterns (hard-coded spacing, wrong variants, etc.)
  - Reports violations as a list with file/line references
  - Displays results in the "Audit Report" tab
  - _Stretch goal:_ integrate with build warnings (low priority)

---

## Implementation Phases

### Phase 1: Foundation (3 points)

**Deliverable:** Dev page registered, basic shell, first two tabs with static content

1. Create `/Pages/Dev/DesignTestbed.razor` with environment guard
2. Register route in dev environment only
3. Implement `TabbedPageShell` with tab structure
4. Add "Tokens & Variables" tab showing all CSS vars from `:root`
5. Add "Layout System" tab with minimal example of each layout variant

**Success Criteria:**

- Page accessible at `/dev/design-testbed` in dev, returns 404 in production
- Tokens tab displays computed values for all `--app-*` vars
- Layout tab shows three empty layout variants side-by-side

---

### Phase 2: Form Controls Playground (5 points)

**Status:** Implemented but currently doc-style; needs leaning down to match the playground template (Phase 9).

**Deliverable:** Inventory of form controls actually used + prototype lab for cascading selectors and density.

1. Inventory: `MudTextField`, `MudNumericField`, `MudSelect`, `MudAutocomplete`, large-list autocomplete — one row each, file-link to a real usage.
2. Deviations: any form control using `Variant.Outlined`/`Filled` outside dialogs.
3. Prototype lab: cascading Folder → Project selector with downstream clearing.
4. Compare (optional): `Variant.Text` vs `Variant.Outlined` only if the difference is non-obvious.

**Success Criteria:**

- Inventory rows link to real production usages
- Cascading selector clears invalid downstream state on parent change
- No design-language rule excerpts copied into the page

---

### Phase 3: Buttons & Actions Playground (5 points)

**Deliverable:** Inventory of every button-like element used in production, deviations called out, and a prototype lab for converging custom button classes.

1. **Inventory** (one minimal example + file:line link each):
   - `.send-to-btn` plain button (with `mode-*` tints)
   - `MudButton` Filled / Text / Outlined (Primary / Secondary / Success / Error / Warning / Info / Tertiary)
   - `MudIconButton` toolbar small
   - `MudButtonGroup` (segmented control — ImageEditor)
   - `MudToggleIconButton` (GallerySettings)
   - `MudToggleGroup` single + multi (Forge / Inspiration)
   - `MudChipSet` filter mode (GallerySettings T2I/I2I/I2V/Upscale)
   - `MudChip` status (read-only)
   - `MudMenu` dropdown + split-button (ImageEditor / VideoCard)
   - Custom `.viewer-btn`, `.action-btn`, `.contextual-nav-action-btn`, `.layer-control-btn`, Bootstrap `.btn-secondary` (Dropdown.razor)
2. **Deviations:** inline `Style="flex:1;"` (TopToolbar), `Style="position: absolute;"` (ImageViewerDialog), inline flex on button container (ImagesContainer), `!important` padding override (LayerPanel), Bootstrap class mixing (Dropdown).
3. **Prototype lab:** `dt-proto-btn` family with size and tone toggles; aim is a unified replacement for the multiple `.viewer-btn` / `.action-btn` / `.contextual-nav-action-btn` overlap.
4. **Compare:** correct vs avoid only for the one big rule already documented (`.send-to-btn` vs `MudButton Outlined` for simple actions).

**Success Criteria:**

- Every button-like primitive used in production appears at least once with a file-link
- All deviations have file:line callouts
- Prototype lab is interactive (size / tone / density toggles)
- Page captions stay one line; no design-doc excerpts

---

### Phase 4: Cards, Surfaces & Layout Policy (5 points)

**Status:** Implemented. Inventory + deviations + `dt-proto-card` lab + flush/double-wrap compare.

**Deliverable:** Surface elevation policy visualized, flush-child rule demonstrated, custom card-class fragmentation called out.

1. Inventory: `MudPaper` (flush layout slot, focus surface, outlined grouping), `MudCard` (selectable, LLM view container), custom card classes (`.image-card`, `.artist-card`, `.viewer-media`).
2. Deviations: pixel `border-radius` / `box-shadow` / `padding` in scoped `.razor.css` (Image/Artist/Video/Viewer/Editor cards); one-off `Outlined + Elevation` mix; inline rgba background on `PromptDialog`; custom `box-shadow` on `VideoViewer` side panel.
3. Prototype lab: `dt-proto-card` with tone (flush / surface / focus / outlined), density (dense / cozy / comfortable), and interactive toggle. All values come from `--app-surface-*` and `--mud-elevation-*`.
4. Compare: flush slot child vs root `MudPaper pa-*` (the inset-shadow + double padding anti-pattern).

---

### Phase 5: Dialogs & Modal Patterns (3 points)

**Status:** Implemented.

**Deliverable:** Dialog tokens displayed live; MudDialog patterns inventoried; custom overlay components called out; version-selector + scrollable-description prototype.

1. Inventory: `--app-dialog-*` token table; MudDialog patterns (Confirm with parameterized buttons, custom `TitleContent`); custom fullscreen overlays (AssetViewer, ImageEditorModal).
2. Deviations: inline `Style="max-width: 98vw..."` on `MudDialog` (ImageViewerDialog), inline `overflow: scroll` (ResourceVersionsDialog), `calc(100vh - 120px)` chrome offset (InfoDrawer), repeated z-index stack and close-button positioning across overlays.
3. Prototype lab: `dt-dialog-frame` mock using `--app-dialog-*` tokens with version-count slider, threshold numeric, expand-description switch. Below threshold renders pills; above renders `MudSelect`. Description clamps to 60px collapsed, `max-height: 40vh; overflow-y: auto` expanded.
4. Compare: omitted (the prototype lab covers the documented patterns directly).

---

### Phase 6: Navigation & Tab Patterns (3 points)

**Status:** Implemented.

**Deliverable:** Page-level vs component-context tab usage made explicit; NavBar inventoried; deviation overrides on internal MudTabs DOM called out.

1. Inventory: `TabbedPageShell` rule + list of pages using it; component-context `MudTabs` (GeneratedImageTabs); nested tabs (SourcesPanel); horizontal `MudNavMenu` NavBar.
2. Deviations: `::deep .mud-tabs-tabbar { background: transparent !important }` and `::deep .mud-tab` sizing (DetailerForm); `tabs-header-container ::deep .mud-tabs` background override (GallerySettings); nested `ApplyEffectsToContainer="true"` while page shell forbids it.
3. Prototype lab: live `MudTabs` configuration matrix (Centered, Rounded, ApplyEffectsToContainer, Elevation) for component-context use.
4. Compare: `TabbedPageShell` Razor vs raw `MudTabs` on a page (anti-pattern).

---

### Phase 7: Data Display Patterns (3 points)

**Status:** Implemented.

**Deliverable:** Card grids, pagination, scrollable containers, MudTable, and empty states with deviations called out.

1. Inventory: card grid (auto-fill minmax), MudPagination (Topbar `Middle=3` vs Full `Middle=5` styles), scrollable container with viewport-relative max-height, MudTable (no MudDataGrid in app).
2. Deviations: hard-coded `minmax(120px, 1fr)` (no token); four different scroll offsets (`100vh - 180px / 200px / 280px / 300px` across `WildcardsTab`, `CategoryBrowser`, `ResourcePanel`, `CollectionBrowser`); inline `Style="opacity: 0.6;"` empty-state markup variation; no `Virtualize` on any large gallery.
3. Prototype lab: token-driven grid with `--dt-card-min` and `--dt-grid-gap` sliders + items numeric. Includes empty-state rendering when count is 0, demonstrating a unified empty-state primitive.
4. Compare: omitted (the prototype itself contrasts with the per-component hard-coded grid rules).

---

### Phase 8: Audit Report & Pattern Scanner (8 points)

**Deliverable:** Automated design audit, violation report, extraction tracker

1. Create "Audit Report" tab
2. Build `DesignAuditService` that scans `.razor` files for:
   - Hard-coded spacing on tabbed pages (`px-*`, `pa-*` on shells)
   - Wrong form variants (`Variant.Outlined` on `MudTextField` outside dialogs)
   - Direct `MudTabs` rendering (not wrapped in `TabbedPageShell`)
   - Double-wrapped surfaces (root `MudPaper` inside layout slots)
   - `.send-to-*` rules re-declared in component `.razor.css`
   - Missing token usage (hard-coded `max-width`, `border-radius`, `padding`)
3. Display violations grouped by file with line numbers
4. Add "Extraction Tracker" sub-section listing:
   - Test bed patterns ready for extraction
   - Patterns already extracted (links to shared primitives)
   - Open design questions / unresolved deviations
5. Generate markdown summary exportable for planning sessions

**Success Criteria:**

- Audit scan completes in < 5 seconds for all pages
- Violations link to relevant design doc sections
- Extraction tracker updates manually (admin panel, not automated)

---

### Phase 9: Lean-down + Design Language Sync (5 points)

**Status:** Implemented (Phase 8 audit deferred until needed).

**Deliverable:** All tabs follow the playground template; design doc updated with anything validated here.

1. Phases 1-7 already follow the inventory -> deviations -> prototype -> compare structure (see Phase 1-7 entries above; doc-style verbosity stripped during their original implementation).
2. Cross-reference: `04-UI-DESIGN-LANGUAGE.md` keeps a single "See also: UI Design Test Bed" link at the top, no per-section excerpt duplication.
3. Promoted prototypes:
   - `.app-grid` primitive (and `--app-card-min` / `--app-grid-gap` tokens) extracted from the Phase 7 grid prototype into `BlazorWebApp/wwwroot/site.css`.
   - `.app-empty-state` primitive extracted from the Phase 7 empty-state lab into `site.css`.
4. Replaced testbed inline declarations: the Data Display tab's grid + empty-state now render through `.app-grid` / `.app-empty-state` directly (proves extraction works without regressing the demo).
5. `04-UI-DESIGN-LANGUAGE.md` updated with new token rows in the spacing table and dedicated `Card grid primitive` + `Empty-state primitive` subsections under Layout. Future `dt-proto-*` graduations follow the same pattern.

**Success Criteria:**

- No tab contains rule excerpts copied from `04-UI-DESIGN-LANGUAGE.md` (met).
- Every implemented tab has the four-section structure or omits sections that don't apply (met).
- At least one prototype extracted to a shared primitive (met: two - `.app-grid`, `.app-empty-state`).

---

## Stress Points & Mitigation

### 1. Maintenance Burden

**Risk:** Test bed becomes stale as design language evolves
**Mitigation:**

- Make test bed the **required first stop** for design changes (enforce via PR template)
- Keep examples minimal (single component per pattern, not full page replicas)
- Use computed CSS var display (auto-syncs with `:root` changes)

### 2. Environment Guard Bypass

**Risk:** Dev page accidentally ships to production
**Mitigation:**

- Double-guard: route registration + `OnInitialized` check
- Add build-time warning if page compiled in Release config
- Keep page in `Pages/Dev/` folder (clear "not production" signal)

### 3. Audit False Positives

**Risk:** Pattern scanner flags valid edge cases as violations
**Mitigation:**

- Start with high-confidence patterns only (hard-coded spacing, wrong variants)
- Make violations manually reviewable (don't fail builds)
- Add inline suppression comments when edge case is intentional

### 4. Inline CSS Creep

**Risk:** Test bed becomes a dumping ground for unextracted experiments
**Mitigation:**

- Enforce "Extraction Tracker" discipline: log all inline patterns
- Set TTL rule: inline patterns older than 2 sprints must extract or delete
- Require extraction plan in PR when adding new inline pattern

### 5. Scope Creep

**Risk:** Test bed expands into a full design system documentation site
**Mitigation:**

- Limit scope to **BlazorWebApp-specific patterns only** (not generic MudBlazor docs)
- Focus on **rules that have violations in the wild** (not exhaustive pattern library)
- Keep it a **developer tool** (not user-facing design portfolio)

---

## Success Criteria

### Must Have

- [ ] Dev page accessible in development, 404 in production
- [ ] All documented UI patterns from `04-UI-DESIGN-LANGUAGE.md` represented visually
- [ ] Each pattern shows correct + incorrect side-by-side
- [ ] Token values displayed with live computed values
- [ ] Audit report identifies at least 5 real violations in existing pages

### Should Have

- [ ] Extraction tracker maintained (even if manually)
- [ ] Design doc cross-referenced with test bed tabs
- [ ] Cascading selectors, overflow patterns, version selector working interactively
- [ ] Code snippets copyable for each pattern

### Nice to Have

- [ ] Audit violations exportable as markdown
- [ ] Visual regression screenshots capturable (manual)
- [ ] "What's changed" diff view when tokens are tuned
- [ ] Theme switcher to preview patterns in light/dark modes

---

## Complexity Estimate

| Phase                                         | Points | Cumulative |
| --------------------------------------------- | ------ | ---------- |
| Phase 1: Foundation                           | 3      | 3          |
| Phase 2: Form Controls Catalog                | 5      | 8          |
| Phase 3: Buttons & Actions Library            | 5      | 13         |
| Phase 4: Cards, Surfaces & Layout Policy      | 5      | 18         |
| Phase 5: Dialogs & Modal Patterns             | 3      | 21         |
| Phase 6: Navigation & Tab Patterns            | 3      | 24         |
| Phase 7: Data Display Patterns                | 3      | 27         |
| Phase 8: Audit Report & Pattern Scanner       | 8      | 35         |
| Phase 9: Design Language Sync & Documentation | 5      | 40         |

**Total: 40 Fibonacci points**

This is a medium-large effort equivalent to a complex feature implementation. The high point count reflects the broad surface area (9 pattern categories) and the novel audit tooling (Phase 8), but each phase delivers incremental value and can be validated independently.

---

## Dependencies

- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` must be treated as source of truth
- Existing layout components (`TabbedPageShell`, `TwoColumnLayout`, etc.) must not be modified during test bed creation
- Audit service should read `.razor` files but not parse C# code (simple regex/text search acceptable)
- Design language updates discovered during audit must be recorded in both the design doc and the relevant plan (e.g., `civitai-modals-redesign`)

---

## Open Questions

1. **Audit tool implementation:** Roslyn analyzer vs simple text search?
   - **Recommendation:** Start with text search (regex), add Roslyn later if false positive rate is high
2. **Public visibility:** Should the page be accessible in staging/production for design review?
   - **Recommendation:** Keep dev-only initially, add `?dev=true` query param guard for staging if needed
3. **Theme mode support:** Should test bed show light + dark simultaneously?
   - **Recommendation:** Defer to "Nice to Have", use app-wide theme switcher for now
4. **Version control:** Should test bed patterns be snapshotted (e.g., "pre-refactor" vs "post")?
   - **Recommendation:** Not initially; rely on git history for this

---

## Notes

- This plan assumes the design language doc is already well-established (it is)
- The test bed is a **stewardship tool**, not a greenfield design system creation
- Priority is **consistency audit** and **extraction facilitation**, not comprehensive pattern documentation
- Phases 1-7 are pattern display (low risk), Phase 8 is tooling (higher risk, can be simplified if needed)
- The "Audit Report" tab is the most valuable long-term asset (prevents regression)

---

## Related Plans

- `Documentation/Plans/civitai-modals-redesign/` - may discover inconsistencies addressed by this test bed
- Future page implementations should reference test bed patterns before coding

---

## Revision History

- **Planning session** - Initial plan created
