# UI Design Language

Living document. The Generate page is the current baseline for all UI integrations.
When in doubt about spacing, input variants, selector patterns or cascading behaviors,
mirror what `PromptsForm`, `LoraForm` and the Generate-page toolbars already do.

> **See also:** [UI Design Test Bed](/dev/design-testbed) - Visual reference page demonstrating all patterns below (development environment only). See [Usage Guide](../Plans/ui-design-testbed/USAGE_GUIDE.md) for iteration workflow.

> Status: seed rules. Will be revisited after a broader design review.

---

## Core Rules

### Input variant

- Use `Variant.Text` on form controls (MudSelect, MudTextField, MudNumericField, MudAutocomplete).
  This matches the Generate page and keeps density/visual-weight consistent across the app.
- `Variant.Outlined` is reserved for emphasis (dialog primary actions, empty-state chips, warnings).
- `Variant.Filled` is not used in forms.

### Density

- Prefer `Dense="true"` on selects and text fields inside multi-field grids.
- Keep `MudStack Spacing="2"` for vertical field groups and `MudGrid Spacing="2"` for horizontal layouts.
- Use `MudDivider` between logical groups inside a dialog.

### Cascading / data-bound selectors

- Never require users to type a value by hand when a data source is available.
- Use `MudAutocomplete` with a `SearchFunc` when the list can be large (LoRAs, models, wildcards).
- Use `MudSelect` with a pre-loaded list when it is small and bounded (asset keys, project/folder).
- When a selection narrows a downstream list (e.g., Folder -> Project, AssetKey -> Asset value),
  refresh the dependent list and clear an invalid current selection.

### Target / filter semantics

- A selector that filters a larger list (e.g., Folder over Projects) must offer an "all / any" option
  and must not be used as a storage field when only the primary target (e.g., Project) is needed.
  Persist the primary target only.

### Dialog shell

- Header row: type / kind selector first, then label, then booleans (Enabled, etc.).
- Body: per-subtype form separated by `MudDivider`.
- Footer: Cancel (left), primary action (right, `Variant.Filled`, `Color.Primary`).

### App-wide media modal (`AssetViewer`)

`BlazorWebApp/Components/Shared/AssetViewer.razor` is the **canonical full-screen media modal** for the app. Use it everywhere the user needs to inspect a single image / video and optionally page through a list (Gallery, Danbooru, Img2Vid, CivitAI, etc.). Do not introduce parallel viewers - extend `AssetViewer` with opt-out parameters instead.

Opt-out parameters (all default to enabled to preserve legacy behavior):

- `ShowFavorite` - hide the heart toggle for surfaces without a local favorites store (e.g. CivitAI).
- `ShowScore` - hide the inline rating control.
- `ShowOpenInExplorer` - hide the local-explorer button for non-local assets.
- `ExternalSourceUrl` - when set, renders an "Open on source" anchor (used by CivitAI to deep-link `civitai.com/images/{id}`).

Hosting pattern: a parent panel keeps a single `AssetViewer` instance, projects its DTO list once (e.g. via `CivitaiAssetAdapter.Project`), and passes `StartIndex` / `Assets`. Cards inside the panel raise an `OnView` `EventCallback<T>` rather than instantiating their own viewer.

### Modal layout: `--app-dialog-*` tokens

Modal dialogs that act as content surfaces (not the small confirm / form dialogs) consume the shared dialog tokens declared in `BlazorWebApp/wwwroot/site.css`:

- `--app-dialog-max-width` - clamps the dialog width on large displays.
- `--app-dialog-padding` - applied to the body container; children render flush inside it.
- `--app-dialog-radius` - corner radius.

Strip the default `MudDialog` title padding / border via `::deep .mud-dialog-title` when the header band is meant to fill the dialog edge-to-edge (see `CivitaiModelInfoDialog.razor.css`).

### Long-content overflow patterns

These two patterns are the agreed-upon answers for content that would otherwise blow up the dialog height:

- **Description / long prose** - render inside a collapsed `MudExpansionPanels MultiExpansion="false" Elevation="0"` panel; the panel body sets `max-height: 40vh; overflow-y: auto;` so the dialog itself never scrolls vertically.
- **Tag rail / chip overflow** - clamp the rail to two rows (`max-height: 64px; overflow: hidden;` on the `--clamped` modifier) and provide a "Show more / Show less" toggle that swaps to a `--expanded` modifier removing the clamp. Reference: `CivitaiModelSpecCard.razor`.

### Version selector pattern

When a surface offers more than one variant of the same logical resource (e.g., model versions), use a **pill bar / select hybrid**:

- Pills (`MudButton Variant="Variant.Filled"` for active, `Variant.Outlined` for inactive, primary color) when `Versions.Count <= 5`.
- `MudSelect Variant="Text"` fallback above that threshold.
- Per-version status icon driven by a single helper that returns a MudBlazor `Color` (Default / Success / Warning / Info / Error) plus a tooltip string. Reference: `CivitaiVersionSelector.razor` + `CivitaiVersionStatusHelper.cs`.

### Simple action buttons (`.send-to-btn`)

The shared `.send-to-btn` style in [send-to.css](../../BlazorWebApp/wwwroot/css/send-to.css) is the canonical look for **simple, non-primary action buttons** that sit in flat rows (e.g. "Send to ...", "Chat Edit", "Spawn Variations", "Copy", per-item utility actions). Prefer it over `MudButton Variant="Variant.Outlined"` for these cases. Use `MudButton Variant="Variant.Filled"` only for the single primary action of a panel/dialog.

Conventions:

- Markup: a plain `<button class="send-to-btn ...">` carrying `MudIcon` + `<span>` label, OR an icon-only `MudIconButton Size="Size.Small" Variant="Variant.Text"` when the label would be redundant. Both shapes coexist in one row.
- Container: wrap a related cluster in `<div class="send-to-section">` (optional `<span class="send-to-label">` heading) and `<div class="send-to-buttons">` for the row itself. The buttons grow with `flex: 1 1 calc(50% - 4px)` and reflow on narrow widths.
- Mode tinting: use `PromptSendToService.GetWorkflowModeClass(mode)` to apply `mode-txt2img` / `mode-img2img` / `mode-img2vid` / `mode-extras` for the colored hover state. For non-workflow buttons, omit the mode class (defaults to primary hover).
- Stylesheet: rules live in [send-to.css](../../BlazorWebApp/wwwroot/css/send-to.css) and are linked globally from `_Layout.cshtml`. Do not redefine the selectors in component-scoped CSS.

When this style is the wrong choice:

- Form-submission primary actions (use `MudButton Variant="Variant.Filled" Color="Color.Primary"`).
- Destructive actions that need explicit visual weight (use `MudButton Color="Color.Error"`).
- Toolbar / topbar buttons that should be flat icon-only (use `MudIconButton Variant="Variant.Text"` directly without a `.send-to-section` wrapper).

---

## Anti-patterns

- Free-text fields for values that exist in a DB table or backend list.
- Free-text fields for paths that can be discovered (LoRAs, checkpoints, VAEs, CLIPs).
- Two independent selectors that should be cascading (e.g., Folder and Project wired separately).
- `Variant.Outlined` on large grids of fields (creates visual noise vs Generate page baseline).
- `MudButton Variant="Variant.Outlined"` for simple non-primary actions in a flat row - use `.send-to-btn` instead so spacing, hover and mode tinting stay consistent.
- Re-declaring `.send-to-*` rules inside a `*.razor.css` - the shared stylesheet is the single source.

---

## Layout

All tabbed pages (`Prompts`, `Resources`, `CivitAI`, `Scheduler`, `Danbooru`) share a single
layout system built on the components under `BlazorWebApp/Components/Layouts/` and the
global spacing tokens declared in `BlazorWebApp/wwwroot/site.css` (`:root`).

### Spacing tokens (single source of truth)

| Token                                     | Purpose                                                                                    |
| ----------------------------------------- | ------------------------------------------------------------------------------------------ |
| `--app-gutter-outer`                      | Gap between the page and the window / navbar edges                                         |
| `--app-gutter-inner`                      | Gap between shell elements (tabs <-> panels, sidebar <-> content, topbar <-> content)      |
| `--app-sidebar-width`                     | Sidebar width as a percentage of the shell                                                 |
| `--app-sidebar-min` / `--app-sidebar-max` | Clamp for the sidebar width                                                                |
| `--app-sidebar-rail-width`                | Width of a collapsed sidebar rail (always keeps an expand affordance visible)              |
| `--app-shell-max-width`                   | Page max-width clamp; prevents ultra-wide stretching                                       |
| `--app-surface-radius`                    | Shared corner radius for sidebar / topbar / content surfaces                               |
| `--app-surface-padding`                   | Internal padding of sidebar / topbar / content surfaces; children render flush inside this |
| `--app-dialog-max-width`                  | Max width clamp for app-wide modal dialogs (e.g., `AssetViewer`, CivitAI model dialog)     |
| `--app-dialog-padding`                    | Internal padding of app-wide modal dialogs; children render flush inside this              |
| `--app-dialog-radius`                     | Corner radius for app-wide modal dialogs                                                   |
| `--app-card-min`                          | Default minimum card width for the `.app-grid` primitive; override per-element to retune   |
| `--app-grid-gap`                          | Default gap for the `.app-grid` primitive (defaults to `--app-gutter-inner`)               |

**Never hard-code spacing on a tabbed page** (`px-5`, `pa-4`, etc. on the shell or top-level
panel paper). If a gap needs tuning, tune the token.

Outer gutter may be slightly larger than inner gutters, but both must be uniform around
the page.

### Elevation constants

`MudBlazor.Elevation` is an `int` and cannot be a CSS var, so elevations live in
`BlazorWebApp/Components/Layouts/LayoutDefaults.cs`:

- `LayoutDefaults.TabsElevation` - applied to `MudTabs` on every tabbed page.
- `LayoutDefaults.SurfaceElevation` - recommended elevation for **child components**
  that render their own `MudPaper` inside a layout slot (e.g. a sidebar browser panel).
  The layout components themselves are transparent; children decide whether to show a surface.

### Surface policy

Layout components (`TwoColumnLayout`, `TopbarLayout`, `ContentOnlyLayout`) are the
**canonical card surfaces** of a tabbed page. Each slot (sidebar / topbar / content)
is a single `MudPaper` with:

- `Elevation = LayoutDefaults.SurfaceElevation`
- `border-radius = var(--app-surface-radius)`
- `padding = var(--app-surface-padding)` (internal)

Child components render **flush** inside this padding. They must NOT:

- wrap themselves in their own `MudPaper` / `MudCard` at the root (causes double
  elevation and an inset-shadow artifact),
- add `pa-*` classes at the root (the surface already owns the padding),
- set `Elevation` on a root wrapper.

Exception: a child may intentionally add its own `MudPaper` for a **focus surface**
when that element needs to stand out inside the slot (e.g. the `EntryManager` header
card on the Wildcards tab). Focus surfaces use `Elevation = 2` or higher and are
explicit visual emphasis, not structural padding.

### Tabbed-page shell

- Use `TabbedPageShell` for every tabbed page - never render `MudTabs` directly in a page.
- `MudTabs` is `Centered=true`, `Rounded=true`, `Elevation=LayoutDefaults.TabsElevation`.
- Do **not** set `PanelClass` on `MudTabs`. Panel padding comes from the layout variant.
- Do **not** set `ApplyEffectsToContainer` - it paints the whole panel area with the
  header elevation and creates a dark background that extends past the actual content.
- Tab headers for major content (Process, System Prompts, History, etc.) are centered too.
  Nested `MudTabs` inside a panel must also use `Centered=true`.

### Layout variants (allowed set)

Exactly three layout variants are allowed inside a tab panel. Any new variant must be
discussed and documented here.

| Variant          | When to use                                                                                            | Structure                                                                  |
| ---------------- | ------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------- |
| **Two-column**   | Settings / search / browse tree on the left, main content on the right. Default for data-heavy pages.  | `[Sidebar] [Content]` horizontal split, gap = `--app-gutter-inner`         |
| **Topbar**       | Small, infrequently-changed filter/search cluster above a single large content surface (e.g. gallery). | `[Topbar card]` stacked above `[Content card]`, gap = `--app-gutter-inner` |
| **Content-only** | Self-contained page (e.g. Scheduler Runs).                                                             | `[Content]` filling the shell, gutters still driven by tokens              |

### Collapsible sidebar (Two-column only)

- Reserved for pages where the main content benefits from added width once the sidebar
  is "configured once" (galleries, grids). Current scope: **Resources**, **CivitAI**,
  and the **Prompts > LLM Tools** nav rail.
- Do **not** make the main Prompts preset-editing sidebar collapsible. The LLM Tools tab is
  the exception because its rail keeps view navigation available while hiding model settings.
- Collapses to a **rail** (`--app-sidebar-rail-width`), never to `0`, so users always
  have a visible expand affordance (chevron).
- Collapsed state is persisted per page via `IStateService` (parity with `ActiveTabIndex`).

### Anti-patterns (layout)

- Rendering `MudTabs` directly in a page instead of through `TabbedPageShell`.
- Setting `PanelClass` / `px-*` / `pa-*` on the shell or top-level panel paper.
- Using a `MudContainer MaxWidth=...` to control page width; use `--app-shell-max-width` instead.
- Mixing elevations across tabbed pages; always use the `LayoutDefaults` constants.
- Placing filter/search forms inline at the top of content when a sidebar or topbar surface is available.
- Wrapping a slot-child's root in its own `MudPaper` / `MudCard` with `pa-*` padding. The
  layout slot is the card. Children render flush. Use a nested focus surface (Elevation >= 2)
  only for deliberate emphasis on a specific element inside the slot.
- Setting `ApplyEffectsToContainer="true"` on `MudTabs` - it paints the panel area with
  the header's elevation and creates a dark background that extends past the actual content.

### Layout components (reference)

Located in `BlazorWebApp/Components/Layouts/`.

| Component           | Slots                                    | When to use                                                                                                                                                                                                              |
| ------------------- | ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `TabbedPageShell`   | `ChildContent` (MudTabPanels)            | Any page with 2+ top-level tabs. Wraps `MudTabs` with the standard elevation / gutters / shell clamp. Forward `@bind-ActivePanelIndex` or the `ActivePanelIndex` + `ActivePanelIndexChanged` pair for route-driven tabs. |
| `TwoColumnLayout`   | `Sidebar`, `CollapsedSidebar`, `Content` | Page has a persistent filter/nav column beside the main content. Supports `Collapsible="true"` + `@bind-Collapsed` to reduce the sidebar to a rail, with optional custom rail content such as icon-only nav.             |
| `TopbarLayout`      | `Topbar`, `Content`                      | Page has a full-width toolbar (filters / selectors / search) above results, no left column needed.                                                                                                                       |
| `ContentOnlyLayout` | `ChildContent`                           | Page is a single column with no sidebar/topbar. Exists so every tab still renders inside a standardized surface.                                                                                                         |

Every slot in these components is a `MudPaper` at `LayoutDefaults.SurfaceElevation` with `--app-surface-radius` and `--app-surface-padding`. Child content is expected to render **flush** (no root `MudPaper` / `pa-*`) - see the anti-patterns above.

### Scrollable content pattern

When the content column renders a long card grid or table and the pagination / header should stay visible, wrap the scrollable region in a container with a viewport-relative max-height:

```css
.<page > -scroll-container {
  max-height: calc(100vh - var(--<page>-scroll-offset, 280px));
  overflow-y: auto;
  overflow-x: hidden;
  padding-right: 4px;
}
```

Pattern used by `ResourcePanel` (`--resources-scroll-offset`) and the CivitAI panels (`--civitai-scroll-offset`). The offset default (280px) accounts for NavBar + tabs + gutters + surface padding + pagination.

If a child card component has a fixed width set in its own `.razor.css`, expose that width through a CSS variable (e.g. `--civitai-card-width`) so the parent can drive size tokens without editing the card's CSS. See `CivitaiImageCard.razor.css`.

### Card grid primitive (`.app-grid`)

Use the global `.app-grid` utility (declared in [`site.css`](../../BlazorWebApp/wwwroot/site.css)) instead of redeclaring `grid-template-columns: repeat(auto-fill, minmax(...))` in component-scoped CSS. Tune density per surface by overriding `--app-card-min` (and optionally `--app-grid-gap`) on the element:

```html
<div class="app-grid" style="--app-card-min: 180px;">@* cards *@</div>
```

Defaults: `--app-card-min: 140px`, `--app-grid-gap: var(--app-gutter-inner)`. Live demo on the [Design Test Bed](/dev/design-testbed) Data Display tab.

### Empty-state primitive (`.app-empty-state`)

Use `.app-empty-state` (in [`site.css`](../../BlazorWebApp/wwwroot/site.css)) for "no results / nothing here yet" messaging. It centers content, applies the standard secondary-text color and opacity, and replaces inline `Style="opacity: 0.4; margin-top: 10rem;"` one-offs.

```html
<div class="app-empty-state">
  <MudIcon Icon="@Icons.Material.Outlined.Inbox" Size="Size.Large" />
  <MudText Typo="Typo.body2">No items</MudText>
  <MudText Typo="Typo.caption">Hint text...</MudText>
</div>
```
