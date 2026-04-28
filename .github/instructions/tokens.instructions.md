---
description: "Spacing, sidebar, surface, and elevation token rules for BlazorWebApp CSS. No magic numbers - use or add a token."
applyTo: "BlazorWebApp/**/*.css,BlazorWebApp/**/*.razor.css,BlazorWebApp/Components/Layouts/LayoutDefaults.cs"
---

# Design Tokens - No Magic Numbers

The single source of truth for spacing, sidebar widths, and surface chrome is `BlazorWebApp/wwwroot/site.css` (`:root`). Elevations live in `BlazorWebApp/Components/Layouts/LayoutDefaults.cs` because `MudBlazor.Elevation` is an `int`.

## Available Tokens (`:root` in site.css)

| Token | Purpose |
| --- | --- |
| `--app-gutter-outer` | Gap between page and window / navbar edges |
| `--app-gutter-inner` | Gap between shell elements (tabs <-> panels, sidebar <-> content, topbar <-> content) |
| `--app-sidebar-width` | Sidebar width as a percentage of the shell |
| `--app-sidebar-min` / `--app-sidebar-max` | Clamp for the sidebar width |
| `--app-sidebar-rail-width` | Width of a collapsed sidebar rail |
| `--app-shell-max-width` | Page max-width clamp |
| `--app-surface-radius` | Shared corner radius for sidebar / topbar / content surfaces |
| `--app-surface-padding` | Internal padding of sidebar / topbar / content surfaces |

## Available Elevations (`LayoutDefaults.cs`)

| Constant | Use |
| --- | --- |
| `LayoutDefaults.TabsElevation` | Applied to `MudTabs` on every tabbed page |
| `LayoutDefaults.SurfaceElevation` | Default for layout-slot `MudPaper`s and child focus surfaces |

## Rules

1. **Never hard-code shell spacing.** `px-5`, `pa-4`, `padding: 16px`, `gap: 12px` on a tabbed page shell or a top-level panel paper are forbidden. Tune the relevant token instead.
2. **Tune the token, not the consumer.** If a value feels wrong on one page, it is wrong on all pages. Update the `:root` token. If only one consumer truly needs a different value, that consumer may declare a local CSS variable that defaults to the global token.
3. **Page-local CSS variables follow the `--<page>-<concern>` pattern.** Examples: `--resources-scroll-offset`, `--civitai-scroll-offset`, `--civitai-card-width`. Always provide a default via `var(--name, <fallback>)`.
4. **No raw px for repeated values.** If the same number appears in 2+ places, promote it to a token (global or page-local).
5. **Card widths are CSS variables.** A card component with a fixed width must expose it as a variable so parents can drive sizing without editing the card.
6. **Elevations come from constants.** Use `LayoutDefaults.TabsElevation` and `LayoutDefaults.SurfaceElevation`. Hard-coded `Elevation="2"` is acceptable only for ad-hoc focus surfaces and should be reviewed for promotion if it repeats.
7. **Adding a new token is a design decision.** It must be reflected in `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` (token table) in the same change set.

## Anti-patterns

- `style="padding: 16px;"` on a slot child (slot already owns padding via `--app-surface-padding`).
- `width: 320px;` on a sidebar (use `--app-sidebar-width` clamped by `--app-sidebar-min` / `--app-sidebar-max`).
- `border-radius: 8px;` on a surface (use `--app-surface-radius`).
- Repeating `gap: var(--app-gutter-inner)` and `border-radius: var(--app-surface-radius)` boilerplate inline when the layout shell already provides them.
- A new "almost-the-same" spacing value (e.g. `--scheduler-gutter-inner: 18px`) when `--app-gutter-inner` is 16px. Tune the global token, do not fork.
