# Workflow UI Conversion Guide

This guide documents the UI integration standards for converting ComfyUI workflows into the app's fluent C# workflow system. Use it together with `TEMPLATE_GUIDE.md`, `FRAGMENT_SCHEMA_GUIDE.md`, and `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.

Workflow conversion is not complete when only the fragment and workflow classes compile. A workflow is properly integrated only when its UI-visible fragments render with the app's existing interaction patterns, visual language, and parameter semantics.

---

## Purpose

Use this guide when a workflow conversion needs any of the following:

- Reusing an existing fragment form component
- Creating a new fragment form component
- Renaming a fragment component target in `FragmentMetadata`
- Deciding which JSON values become user-facing controls
- Verifying that new workflow UI matches the Generate page baseline

---

## Required References

Read these before planning UI work for a workflow conversion:

- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`
- `BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`
- `BlazorWebApp/Components/Shared/Generation/Fragments/*.razor`

Recommended representative examples:

- `PromptsForm.razor` for major prompt-oriented surfaces and text-entry behavior
- `SamplerForm.razor` for dynamic options + sliders + seed controls
- `LatentForm.razor` for resolution and compact grid layout
- `DetailerForm.razor` for tabbed multi-pass UIs and grouped advanced controls
- `SeedVR2Form.razor` and `UpscaleForm.razor` for mixed select / slider / numeric layouts
- `ConditioningVariationForm.razor` for simple single-purpose enhancement forms

---

## Planning Checklist

Validate all UI concerns before the main implementation plan is approved.

### 1. Component Reuse Audit

For every UI-visible fragment, determine one of these outcomes:

- Reuse an existing component unchanged
- Reuse an existing component with fragment metadata changes only
- Create a new component because the parameter set or UX differs materially

Document this in a table during planning:

| Fragment          | Parameters exposed                   | Component decision | Status | Notes                                  |
| ----------------- | ------------------------------------ | ------------------ | ------ | -------------------------------------- |
| `main_sampler`    | sampler, scheduler, steps, cfg, seed | `SamplerForm`      | Reuse  | Matches existing control set           |
| `controlnet_tile` | strength                             | `ControlNetForm`   | New    | Existing forms do not cover this shape |

Do not assume an existing form is reusable just because the feature name is similar. Reuse only when the control set and interaction model match.

### 2. Visual Integration Review

Before the plan is approved, confirm:

- The fragment belongs in the existing Generate page layout without adding a new shell pattern
- The new UI follows the design-language defaults for spacing, density, and field variants
- Any multi-pass or multi-mode behavior uses an existing tabbed or grouped pattern already present in the app
- No fragment form adds a root `MudPaper` or extra shell padding unless it is intentionally a major surface like `PromptsForm`
- Core workflow settings are always visible and non-collapsible. `Collapsible = true` is reserved for true optional enhancements/add-ons such as Sage/NAG patches, RIFE/frame interpolation, detailer, upscale, or other passes the workflow can run without.

### 3. Field Mapping Review

For every UI-exposed parameter, decide the control type before coding.

Required planning output:

| Parameter      | JSON value | UI control              | Existing example | Reason                              |
| -------------- | ---------- | ----------------------- | ---------------- | ----------------------------------- |
| `sampler_name` | `euler`    | `MudSelect`             | `SamplerForm`    | Small dynamic list from backend     |
| `steps`        | `20`       | `MudSlider<int>`        | `SamplerForm`    | Bounded range with frequent tuning  |
| `seed`         | `-1`       | `MudNumericField<long>` | `SamplerForm`    | Wide numeric range with exact entry |

### 4. Approval Gate

The workflow plan is not ready for approval until the user has reviewed:

- Fragment reuse vs new fragment creation
- Component reuse vs new component creation
- UI-exposed vs hardcoded values
- Any notable UX behavior such as tabs, derived info text, chained passes, or dependent selectors

---

## MudBlazor Standards

These standards are derived from the existing generation components and the app design-language document.

### Layout

- Prefer `MudGrid Spacing="2"` as the default fragment-form layout.
- Use `MudItem xs="6"` for paired controls, `xs="4"` for dense triples, and `xs="12"` for full-width prompt or status areas.
- Use `MudStack Spacing="2"` or `MudStack Spacing="3"` when the form is naturally vertical or includes compact action bars.
- Keep fragment forms flush. Do not add a root `MudPaper` for ordinary fragment components.

### Variant and Density

- New `MudSelect`, `MudTextField`, `MudNumericField`, and `MudAutocomplete` controls should follow the design-language baseline: `Variant.Text`.
- Use `Dense="true"` for select and text-entry controls inside compact multi-field grids.
- Existing fragment forms contain legacy controls without explicit `Variant.Text`; do not copy those omissions into new workflow-specific components.
- `Variant.Outlined` is reserved for emphasis controls, utility chips, and actions, not for dense parameter grids.
- Sliders in current generation forms consistently use `Variant.Filled` with `ValueLabel`; keep that pattern unless there is a strong reason to diverge.

### Supporting Elements

- Use `MudAlert Dense="true"` for concise help, warnings, or missing-backend guidance.
- Use `MudText Typo="Typo.overline"` or `Typo.caption` for derived read-only info such as resize summaries.
- Use tabs only when the fragment has true mode or pass separation, such as prompts or detailer passes.

---

## Common Field Mappings

Use these mappings by default unless the workflow has a compelling UX reason to do otherwise.

| Parameter shape                                  | Preferred control                                                                | Typical examples                              |
| ------------------------------------------------ | -------------------------------------------------------------------------------- | --------------------------------------------- |
| Small bounded option list from backend or schema | `MudSelect<string>`                                                              | sampler, scheduler, detection model, upscaler |
| Large searchable backend list                    | `MudAutocomplete<string>`                                                        | models, LoRAs, wildcards when search matters  |
| Long freeform text                               | `MudTextField<string>` with `Variant.Text`, `Lines`, `AutoGrow`                  | prompt overrides, notes                       |
| Prompt authoring with backend completion         | `TextFieldAutocomplete`                                                          | positive and negative prompt entry            |
| Bounded integer or float adjusted frequently     | `MudSlider<T>` with `ValueLabel`                                                 | steps, cfg, denoise, strength, upscale factor |
| Exact numeric entry or very wide numeric range   | `MudNumericField<T>`                                                             | seed, batch size, video length, block counts  |
| Boolean toggle                                   | `MudCheckBox<bool>` or `MudChip`/button toggle when matching an existing pattern | enable flags, compact toolbar toggles         |
| Derived display-only value                       | `MudText` or dense `MudAlert`                                                    | resize summaries, explanatory notes           |

Guidance by parameter semantics:

- Use sliders for normalized ranges like `0..1`, `1..4`, or other bounded tuning values.
- Use numeric fields for values the user may paste, type precisely, or set outside a comfortable slider range.
- Prefer selects over free text whenever the backend already exposes valid options.

Metadata ownership rule:

- `FragmentMetadata.Parameters` is the single source of truth for parameter defaults, labels, static select options, min/max/step constraints, and dynamic option sources.
- Designed components receive `FragmentReference`; use `Fragment.Schema.GetConstraints("parameter_name")` to read static options, defaults, min/max, and step values.
- Use `ParameterService.GetResolvedOptions(Fragment.Id, "parameter_name")` when a parameter uses backend/ComfyUI-resolved options and the resolved runtime list is needed.
- Do not hardcode `_options = [...]`, slider min/max/step, or default literals in a Razor form when those values are already declared in `FragmentParameter`. A literal fallback is acceptable only as a defensive fallback after schema lookup or as explicit old-value normalization.
- If changing a `FragmentParameter.Options` or `DefaultValue` does not change the custom component UI after rebuild/restart, the component is bypassing metadata and must be fixed before the integration is considered complete.

---

## Fragment Form Implementation Pattern

Current fragment forms follow this implementation shape:

1. Add `[FragmentComponent("ComponentName")]` for auto-discovery.
2. Accept `[Parameter] public FragmentReference? Fragment { get; set; }`.
3. Accept `[Parameter] public EventCallback OnChanged { get; set; }`.
4. Inject `IGenerationParameterService` for reads/writes.
5. Keep local `_local...` state for MudBlazor-bound controls to prevent snap-back.
6. Sync local state from the fragment in `OnParametersSet()`.
7. Write changes back through `ParameterService.SetFragmentProperty(..., notify: true)`.
8. Read defaults/options/constraints from `Fragment.Schema` or `ParameterService.GetResolvedOptions`; do not duplicate `FragmentParameter` values in component fields.

Skeleton:

```razor
@attribute [FragmentComponent("MyFragmentForm")]
@inject IGenerationParameterService ParameterService

<MudGrid Spacing="2">
    <MudItem xs="12">
        <MudSlider T="float"
                   @bind-Value="_localStrength"
                   @bind-Value:after="OnStrengthChanged"
                   Min="0" Max="1" Step="0.01f"
                   Variant="Variant.Filled"
                   ValueLabel>
            <small>Strength:</small> @_localStrength.ToString("F2")
        </MudSlider>
    </MudItem>
</MudGrid>

@code {
    [Parameter] public FragmentReference? Fragment { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    private float _localStrength;
    private ParameterConstraints _strengthConstraints = new();

    protected override void OnParametersSet()
    {
        if (Fragment == null) return;
        _strengthConstraints = Fragment.Schema?.GetConstraints("strength") ?? new ParameterConstraints();
        var defaultStrength = _strengthConstraints.GetDefault<float>() ?? 0.5f;
        _localStrength = ParameterService.GetFragmentProperty(Fragment, "strength", defaultStrength);
    }

    private async Task OnStrengthChanged()
    {
        ParameterService.SetFragmentProperty(Fragment, "strength", _localStrength, notify: true);
        await OnChanged.InvokeAsync();
    }
}
```

If the fragment uses backend-resolved options, prefer `ParameterService.GetResolvedOptions(Fragment.Id, "parameter")` or schema constraints already bridged into `Fragment.Schema`. If a select has static options, bind the select items from `Fragment.Schema.GetConstraints("parameter").Options`, not from a component-local list.

---

## Reuse Rules

- Reuse an existing component only when its control set, labels, and parameter semantics already match the fragment.
- Create a new component when reusing an existing one would introduce dead fields, missing fields, or misleading labels.
- Prefer a dedicated new component over mutating a shared component to support an unrelated parameter shape.
- If a fragment is user-visible, set `FragmentMetadata.Component` explicitly. Do not rely on metadata-only dynamic rendering for fluent fragments.
- Reused or new components must treat schema metadata as authoritative. They may customize layout, grouping, enable/disable behavior, and derived display text, but they must not own duplicate option/default/constraint tables.
- Do not use `Collapsible = true` to save vertical space for required settings. In the Generate page this creates optional-feature semantics. Use it only when the fragment has a meaningful enable/disable state and the workflow remains valid when it is inactive.

---

## Verification Checklist

After implementation, verify UI behavior in addition to build success.

- The workflow appears in the Generate UI.
- Each UI-visible fragment renders the intended component.
- No fragment shows a missing-component warning.
- Dynamic select options populate correctly.
- Static select options and defaults change when `FragmentMetadata.Parameters` changes, proving the component is reading schema metadata.
- Sliders, numeric fields, and text inputs write back to `GenerationParameters` without snap-back.
- Any derived info text updates as dependent values change.
- The visual result matches the app's existing design language and Generate page density.

If build-only validation is all that is available, explicitly note the remaining runtime UI risk.
