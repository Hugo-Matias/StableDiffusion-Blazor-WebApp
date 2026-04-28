---
description: "Condensed UI Design Language checklist applied to all Razor and component CSS edits in BlazorWebApp. Enforces TabbedPageShell, layout variants, surface policy, form variants, .send-to-btn, and token usage."
applyTo: "BlazorWebApp/**/*.razor,BlazorWebApp/**/*.razor.css"
---

# UI Design Language - Quick Checklist

Authoritative source: [Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md](../../Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md). When in doubt, read it. The rules below are a reminder, not a substitute.

## Layout & Shells

- Tabbed pages render through `TabbedPageShell`. Never use raw `MudTabs` in a page.
- Use only the documented layout variants: `TwoColumnLayout`, `TopbarLayout`, `ContentOnlyLayout`. New variants require explicit discussion and a doc update.
- Do not set `PanelClass` on `MudTabs`, do not set `ApplyEffectsToContainer="true"`.
- Do not use `MudContainer MaxWidth=...` to drive page width. Width is clamped by `--app-shell-max-width`.

## Surface Policy

- Layout slots (`Sidebar`, `Topbar`, `Content`) are the cards. Their children render **flush**:
  - no root `MudPaper` / `MudCard`,
  - no root `pa-*` / `px-*` / `py-*`,
  - no root `Elevation`.
- A nested focus surface (Elevation >= 2) is allowed for deliberate emphasis on a specific element inside a slot. Document the rationale if it is new.

## Form Variants

- Form controls (`MudSelect`, `MudTextField`, `MudNumericField`, `MudAutocomplete`): `Variant.Text`.
- `Variant.Outlined`: emphasis only (dialog primary action, empty-state, warnings).
- `Variant.Filled`: not used in forms.
- Use `Dense="true"` on selects/text fields inside multi-field grids.
- Group fields with `MudStack Spacing="2"` (vertical) or `MudGrid Spacing="2"` (horizontal). Separate logical groups in a dialog with `MudDivider`.

## Selectors

- No free-text input where a backend list exists.
- Large/searchable lists: `MudAutocomplete` with `SearchFunc`.
- Bounded lists: `MudSelect`.
- Cascading selectors must refresh downstream and clear invalid current selections.
- Filter selectors offer an "all / any" option and are NOT persisted as the primary target.

## Buttons

- Simple non-primary actions: `.send-to-btn` (`<button class="send-to-btn ...">` with `MudIcon` + `<span>`) or icon-only `MudIconButton Size="Small" Variant="Variant.Text"`. Wrap clusters in `<div class="send-to-section">` + `<div class="send-to-buttons">`.
- Primary action: `MudButton Variant="Filled" Color="Primary"`.
- Destructive: `MudButton Color="Error"`.
- DO NOT use `MudButton Variant="Outlined"` for flat-row simple actions.
- DO NOT redeclare `.send-to-*` rules in a component-scoped `.razor.css`. The single source is `BlazorWebApp/wwwroot/css/send-to.css`.

## CSS Scope

- Static styling: in CSS files. Inline `style="..."` is for dynamic values bound from C# only.
- Shared concerns: `BlazorWebApp/wwwroot/css/*.css`.
- Component-specific concerns: matching `*.razor.css`.
- Component card widths expose a CSS variable (e.g. `--civitai-card-width`) so parents can drive sizing.

## Spacing Tokens

Hard-coded shell spacing is forbidden. See [tokens.instructions.md](./tokens.instructions.md) for the token list and rules.

## When in Doubt

- Mirror the patterns already in use on the Generate page (`PromptsForm`, `LoraForm`) and the Resources / CivitAI tabs.
- If you need a new shared style, token, layout variant, or MudBlazor override: stop, surface the choice, and update `04-UI-DESIGN-LANGUAGE.md` in the same change set.
