---
name: blazor-design-language
description: "Apply the repo design language for MudBlazor and custom UI. Use when creating a page, tabbed page, dialog, toolbar, sidebar, topbar, gallery, Razor component, or component CSS and you need TabbedPageShell, layout variants, send-to buttons, AssetViewer reuse, or token-based spacing."
user-invocable: false
---

# Blazor Design Language

Use this skill for higher-level UI integration decisions that go beyond a single Razor syntax fix.

## When To Use

- Add a new page or tabbed page
- Add a dialog, media viewer, toolbar, or sidebar
- Create or restyle a Razor component or `.razor.css` file
- Decide between `TwoColumnLayout`, `TopbarLayout`, and `ContentOnlyLayout`
- Apply `.send-to-btn`, card-grid, or token-based spacing rules

## Procedure

1. Treat `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` as the source of truth.
2. For Razor and component CSS changes, also follow the quick instruction files under `.github/instructions/`.
3. Choose only from the documented shell patterns:
   - `TabbedPageShell`
   - `TwoColumnLayout`
   - `TopbarLayout`
   - `ContentOnlyLayout`
4. Keep slot children flush. The layout slot is the card surface; child roots should not add their own `MudPaper`, root padding, or root elevation.
5. Use the repo's form baseline:
   - `Variant.Text` for text/select/autocomplete/numeric controls
   - `Dense="true"` inside compact grids
   - `MudGrid Spacing="2"` and `MudStack Spacing="2"`
6. Use `.send-to-btn` for simple non-primary flat-row actions.
7. Reuse `AssetViewer` for fullscreen media viewing instead of creating parallel viewers.
8. Use tokens from `wwwroot/site.css` and elevation constants from `LayoutDefaults.cs`. Do not hardcode shell spacing or duplicate near-identical values.
9. If the change requires a new shared layout, token, or visual pattern, update the design-language documentation in the same change set.

## Guardrails

- Never render raw `MudTabs` directly in a page that should use `TabbedPageShell`.
- Never use `MudContainer MaxWidth=...` to control page width.
- Never redeclare `.send-to-*` rules in component-scoped CSS.
- Never solve recurring spacing with raw pixel values when a token should own it.

## Key Anchors

- `../../../Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- `../../instructions/design-language.instructions.md`
- `../../instructions/tokens.instructions.md`
- `../../../BlazorWebApp/Components/Layouts/`
- `../../../BlazorWebApp/wwwroot/site.css`
- `../../../BlazorWebApp/wwwroot/css/send-to.css`
- `../../../BlazorWebApp/Components/Shared/AssetViewer.razor`
