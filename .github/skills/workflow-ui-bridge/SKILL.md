---
name: workflow-ui-bridge
description: "Bridge workflow fragments to the Generate UI. Use when creating or editing a fragment form, setting FragmentMetadata.Component, adding a FragmentComponent, resolving dynamic options, binding local MudBlazor state to GenerationParameters, or fixing form snap-back in Generate-page fragment components."
user-invocable: false
---

# Workflow UI Bridge

Use this skill when workflow metadata needs a user-facing form on the Generate page.

## When To Use

- Add a new fragment form component
- Reuse or replace an existing fragment form
- Set or rename `FragmentMetadata.Component`
- Bind controls to `IGenerationParameterService`
- Fix local-state snap-back in MudBlazor controls

## Procedure

1. Audit reuse first. Reuse an existing fragment form only when the control set, labels, and semantics already match.
2. For user-visible fluent fragments, set `FragmentMetadata.Component` explicitly.
3. Create the component under `BlazorWebApp/Components/Shared/Generation/Fragments/`.
4. Mark it with `[FragmentComponent("ComponentName")]` so `ComponentRegistry` can discover it.
5. Follow the current fragment component pattern:
   - `[Parameter] public FragmentReference? Fragment { get; set; }`
   - `[Parameter] public EventCallback OnChanged { get; set; }`
   - inject `IGenerationParameterService`
   - keep local `_local...` state for MudBlazor-bound controls
   - sync from `Fragment` in `OnParametersSet()`
   - write back through `SetFragmentProperty(..., notify: true)`
   - read defaults, labels, static options, min/max, and step values from `Fragment.Schema.GetConstraints("parameter")`
   - read backend/ComfyUI-resolved option lists through `ParameterService.GetResolvedOptions(Fragment.Id, "parameter")` when runtime-resolved values are needed
6. Prefer repo-standard controls:
   - `MudSelect` for bounded options
   - `MudAutocomplete` for large searchable lists
   - `MudSlider` for frequently tuned bounded numbers
   - `MudNumericField` for wide or exact numeric entry
7. Use `Variant.Text`, `Dense="true"`, and `MudGrid Spacing="2"` or `MudStack Spacing="2"`.
8. Keep fragment forms flush. Do not add a root `MudPaper`, root `pa-*`, or root elevation.
9. Use resolved backend options or schema-driven constraints instead of free-text entry where the backend already defines valid values.
10. Before finishing, verify the component has no duplicated `_options = [...]`, default, min/max, or step tables for values already present in `FragmentMetadata.Parameters`.
11. Validate the narrowest possible path: focused build, relevant workflow test, and runtime render verification when available.

## Guardrails

- Do not mutate a shared form just to support an unrelated parameter shape if a new form would be clearer.
- Do not rely on manual component registration when the attribute-based discovery path works.
- Do not add form padding that fights the surrounding Generate-page surface.
- Do not treat designed components as a second source of truth. `FragmentMetadata.Parameters` owns defaults, options, min/max, step values, and dynamic sources; components own layout, local bound state, event handling, and temporary persisted-value normalization only.
- Do not hardcode static select option arrays in a fragment form when the corresponding `FragmentParameter.Options` exists.

## Key Anchors

- `../../../BlazorWebApp/Workflows/WORKFLOW_UI_CONVERSION_GUIDE.md`
- `../../../BlazorWebApp/Attributes/FragmentComponentAttribute.cs`
- `../../../BlazorWebApp/Services/ComponentRegistry.cs`
- `../../../BlazorWebApp/Components/Shared/Generation/Fragments/`
- `../../../BlazorWebApp/Services/GenerationParameterService.cs`
