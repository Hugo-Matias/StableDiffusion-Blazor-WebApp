# UI Design Language

Living document. The Generate page is the current baseline for all UI integrations.
When in doubt about spacing, input variants, selector patterns or cascading behaviors,
mirror what `PromptsForm`, `LoraForm` and the Generate-page toolbars already do.

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

---

## Anti-patterns

- Free-text fields for values that exist in a DB table or backend list.
- Free-text fields for paths that can be discovered (LoRAs, checkpoints, VAEs, CLIPs).
- Two independent selectors that should be cascading (e.g., Folder and Project wired separately).
- `Variant.Outlined` on large grids of fields (creates visual noise vs Generate page baseline).
