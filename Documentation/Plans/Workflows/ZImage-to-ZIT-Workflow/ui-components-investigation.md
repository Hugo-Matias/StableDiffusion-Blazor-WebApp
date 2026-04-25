## UI Component Investigation: Z-Image to ZIT Workflow (Revised)

### Objective

Determine the best approach for exposing the two UI-exposed fragment parameters (`strength` from ControlNet, `upscale_by` from UltimateSDUpscale) in the generation form.

---

### Revised Approach: Combined Workflow-Specific Form

After investigation, the user clarified two key constraints:

1. **Future ControlNet enhancement**: A general-purpose `ControlNetForm` will be introduced later (similar to `DetailerForm`), with multi-pass support, chainable instances, and more complex parameters. Creating a simplified `ControlNetForm` now would be wasted effort.

2. **Existing `UpscaleForm` is incompatible**: The current [`UpscaleForm.razor`](BlazorWebApp/Components/Shared/Generation/Fragments/UpscaleForm.razor) expects 6 parameters (`upscale_model`, `upscale_steps`, `upscale_denoise`, `upscale_scale`, `upscale_width`, `upscale_height`) that don't exist in our `UltimateSDUpscaleFragment`.

**Decision**: Create a single combined form specific to this workflow that exposes both parameters together.

---

### Fragment Analysis

#### 1. `QwenImageDiffsynthControlnetFragment`

| Property             | Value                                            |
| -------------------- | ------------------------------------------------ |
| `Metadata.Id`        | `"controlnet"`                                   |
| `Metadata.Component` | `"ControlNetForm"` (needs update)                |
| Parameter            | `strength` (double, 0-2, step 0.01, default 0.2) |

#### 2. `UltimateSDUpscaleFragment`

| Property             | Value                                             |
| -------------------- | ------------------------------------------------- |
| `Metadata.Id`        | `"upscale"`                                       |
| `Metadata.Component` | `"UpscaleForm"` (needs update)                    |
| Parameter            | `upscale_by` (double, 1-4, step 0.1, default 1.5) |

---

### How Fragment Rendering Works

The [`FragmentRenderer.razor`](BlazorWebApp/Components/Shared/Generation/FragmentRenderer.razor) component:

1. Receives a `FragmentReference` (wraps `FragmentParameters` + `FragmentSchema`)
2. Reads `Fragment.Schema.Component` to get the component name
3. Looks up the component type in `ComponentRegistry`
4. Renders via `<DynamicComponent Type="@_componentType" Parameters="@_componentParams" />`

Each fragment form receives:

- `[Parameter] FragmentReference? Fragment` — the fragment data
- `[Parameter] EventCallback OnChanged` — change notification

The form reads/writes values via `ParameterService.GetFragmentProperty(Fragment, "key", default)` and `ParameterService.SetFragmentProperty(Fragment, "key", value, notify: true)`.

---

### Problem: Two Fragments, One Form

The current architecture expects **one component per fragment**. Our two fragments (`controlnet` and `upscale`) each have their own `FragmentReference` and `FragmentParameters`. A single form cannot receive two `FragmentReference` inputs through the `FragmentRenderer` mechanism.

### Options

#### Option A: Hide both fragments, create a separate workflow settings panel

Set `IsHidden = true` on both fragments and create a dedicated settings panel in the workflow that reads/writes both parameters directly via `ParameterService.GetFragmentValue()` / `SetFragmentValue()`.

**Pros**: Clean separation, no architecture changes needed.
**Cons**: Parameters won't appear in the standard fragment list; requires custom placement logic.

#### Option B: Combine into a single "upscale_pipeline" fragment

Merge the two fragment concepts into one `UpscalePipelineFragment` with both `strength` and `upscale_by` parameters, backed by a single `UpscalePipelineForm`.

**Pros**: Single form, single `FragmentReference`, fits the existing architecture perfectly.
**Cons**: Requires refactoring the two C# fragments into one; changes the fragment ID mapping.

#### Option C: Keep both fragments, use one visible + one hidden

Keep `UltimateSDUpscaleFragment` visible with `Component = "ZImageUpscaleForm"`, and hide `QwenImageDiffsynthControlnetFragment` (`IsHidden = true`). The `ZImageUpscaleForm` reads both its own `Fragment` parameter AND the hidden controlnet fragment via `ParameterService.GetFragment("controlnet")`.

**Pros**: Minimal changes to existing fragments; form can access both parameter sets.
**Cons**: Form reads from two sources; the controlnet fragment won't have its own UI section.

#### Option D: Create `ZImageUpscaleForm` for upscale, read controlnet params directly

Similar to Option C but simpler: `UltimateSDUpscaleFragment` gets `Component = "ZImageUpscaleForm"`. The form renders both the `upscale_by` slider (from its own `Fragment`) and the `strength` slider (read via `ParameterService.GetFragment("controlnet")`).

**Pros**: Least invasive; only changes one fragment's Component name and adds one form.
**Cons**: Form accesses a sibling fragment's parameters directly.

---

### Recommendation: Option D

**Rationale**:

- Minimal code changes: only update `UltimateSDUpscaleFragment.Metadata.Component` and create one new form.
- The form naturally groups "upscale pipeline" settings together.
- No need to refactor the ControlNet fragment or change its ID.
- Fits the existing `FragmentRenderer` pattern.
- When the general-purpose `ControlNetForm` is introduced later, we can swap the component reference without touching the form.

---

### Implementation Plan

#### Step 1: Update `UltimateSDUpscaleFragment` Metadata

Change `Component` from `"UpscaleForm"` to `"ZImageUpscaleForm"`:

```csharp
// In UltimateSDUpscaleFragment.cs
public FragmentMetadata Metadata => new()
{
    Id = "upscale",
    Type = FragmentType.Enhancement,
    Title = "Ultimate SD Upscale",
    Component = "ZImageUpscaleForm",  // Changed from "UpscaleForm"
    // ...
};
```

#### Step 2: Create `ZImageUpscaleForm.razor`

Location: `BlazorWebApp/Components/Shared/Generation/Fragments/ZImageUpscaleForm.razor`

```razor
@attribute [FragmentComponent("ZImageUpscaleForm")]
@inject IGenerationParameterService ParameterService

<MudGrid>
    <!-- Upscale Factor slider (from own Fragment) -->
    <MudItem xs="6">
        <MudSlider T="double" @bind-Value="_localUpscaleBy" @bind-Value:after="OnUpscaleByChanged"
                   Min="1" Max="4" Step="0.1" Variant="Variant.Filled" ValueLabel>
            <small>Upscale Factor:</small> @_localUpscaleBy.ToString("F1")x
        </MudSlider>
    </MudItem>

    <!-- ControlNet Strength slider (from sibling "controlnet" fragment) -->
    <MudItem xs="6">
        <MudSlider T="double" @bind-Value="_localStrength" @bind-Value:after="OnStrengthChanged"
                   Min="0" Max="2" Step="0.01" Variant="Variant.Filled" ValueLabel>
            <small>ControlNet Strength:</small> @_localStrength.ToString("F2")
        </MudSlider>
    </MudItem>

    <!-- Resize info -->
    <MudItem xs="12" Class="align-items-center justify-content-center d-flex">
        <MudText Typo="Typo.overline">@_resizeInfo</MudText>
    </MudItem>

    <!-- Help text -->
    <MudItem xs="12">
        <MudAlert Severity="Severity.Info" Dense="true">
            <MudText Typo="Typo.caption">
                <strong>Z-Image Turbo Upscale</strong> performs a two-stage pipeline: base generation at
                ~1568x1356, then tiled upscale with ControlNet tile guidance.
                <br /><br />
                <em>ControlNet strength controls how much the tile preprocessor guides the upscale.
                Lower values (0.1-0.3) preserve the original style; higher values (0.5+) enforce stricter structure.</em>
            </MudText>
        </MudAlert>
    </MudItem>
</MudGrid>

@code {
    [Parameter] public FragmentReference? Fragment { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    private double _localUpscaleBy;
    private double _localStrength;
    private MarkupString _resizeInfo = new();

    protected override void OnParametersSet()
    {
        if (Fragment == null) return;

        // Read upscale_by from own fragment
        _localUpscaleBy = ParameterService.GetFragmentProperty(Fragment, "upscale_by", 1.5);

        // Read strength from sibling "controlnet" fragment
        var controlnetFragment = ParameterService.GetFragment("controlnet");
        _localStrength = controlnetFragment?.GetDouble("strength", 0.2) ?? 0.2;

        RefreshResizeInfo();
    }

    private async Task OnUpscaleByChanged()
    {
        ParameterService.SetFragmentProperty(Fragment, "upscale_by", _localUpscaleBy, notify: true);
        RefreshResizeInfo();
        await OnChanged.InvokeAsync();
    }

    private async Task OnStrengthChanged()
    {
        // Write to sibling fragment
        ParameterService.SetFragmentValue("controlnet", "strength", _localStrength, notify: true);
        await OnChanged.InvokeAsync();
    }

    private void RefreshResizeInfo()
    {
        var baseWidth = 1568;
        var baseHeight = 1356;
        if (ParameterService.PrimaryLatentFragment != null)
        {
            // Latent dims are ~872x1248, pixel dims are ~4x that
            var latentW = ParameterService.GetFragmentProperty(ParameterService.PrimaryLatentFragment, "width", 872);
            var latentH = ParameterService.GetFragmentProperty(ParameterService.PrimaryLatentFragment, "height", 1248);
            baseWidth = latentW * 4 / 2;  // Approximate pixel space
            baseHeight = latentH * 4 / 2;
        }
        var targetW = (int)(baseWidth * _localUpscaleBy);
        var targetH = (int)(baseHeight * _localUpscaleBy);
        _resizeInfo = new MarkupString($"From: {baseWidth}x{baseHeight} px &rarr; To: <strong>{targetW}x{targetH} px</strong> ({_localUpscaleBy:F1}x)");
    }
}
```

#### Step 3: Build and verify

Run `dotnet build` to confirm no compilation errors.

---

### Risks and Considerations

| Risk                                    | Mitigation                                                                                                                                                |
| --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Form accesses sibling fragment directly | Use `ParameterService.GetFragment("controlnet")` which is the established pattern                                                                         |
| ControlNet fragment has no UI section   | Acceptable — it's hidden and controlled through the upscale form                                                                                          |
| Future ControlNetForm migration         | When general ControlNetForm arrives, update `QwenImageDiffsynthControlnetFragment.Metadata.Component` and remove strength slider from `ZImageUpscaleForm` |
| Resize info approximation               | Pixel-space resolution is estimated from latent dims; actual values depend on VAE decode. Info is for display only.                                       |
