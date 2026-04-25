## UI Component Plan: Merged ZImageUpscaleFragment

### Decision: Merge ControlNet + UltimateSDUpscale into Single Fragment

After investigating the fragment rendering architecture and registry dependencies, merging the two UI-exposed fragments into a single `ZImageUpscaleFragment` is the cleanest long-term solution.

---

### Feasibility Analysis

#### Current Stage 2 Build Order (5 steps)

| Step | Fragment                               | Registry Inputs                                                                                            | Registry Outputs          |
| ---- | -------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ------------------------- |
| 9    | `TilePreprocessorFragment`             | `image_output`                                                                                             | `tile_map_output`         |
| 10   | `ModelPatchLoaderFragment`             | (none)                                                                                                     | `model_patch_output`      |
| 11   | `QwenImageDiffsynthControlnetFragment` | `model_output`, `model_patch_output`, `vae_output`, `tile_map_output`                                      | overwrites `model_output` |
| 12   | `UpscaleModelLoaderFragment`           | (none)                                                                                                     | `upscale_model_output`    |
| 13   | `UltimateSDUpscaleFragment`            | `image_output`, `model_output`, `positive_output`, `negative_output`, `vae_output`, `upscale_model_output` | overwrites `image_output` |

#### Dependency Chain

```
TilePreprocessor ──→ tile_map_output ──┐
                                       ├─→ QwenImageDiffsynthControlnet ──→ model_output ──→ UltimateSDUpscale
ModelPatchLoader ──→ model_patch_output┘                                          ↑
UpscaleModelLoader ──→ upscale_model_output ──────────────────────────────────────┘
```

**Key insight**: Steps 9-12 must execute in order because ControlNet (step 11) depends on outputs from steps 9-10, and UltimateSDUpscale (step 13) depends on the patched `model_output` from step 11. Merging steps 11+13 into one fragment preserves this ordering naturally.

---

### Proposed Merged Fragment: `ZImageUpscaleFragment`

#### Location

`BlazorWebApp/Workflows/Fragments/qwen/ZImageUpscaleFragment.cs`

#### Metadata

```csharp
public FragmentMetadata Metadata => new()
{
    Id = "zimage_upscale",
    Type = FragmentType.Enhancement,
    Title = "Z-Image Turbo Upscale",
    Component = "ZImageUpscaleForm",
    Icon = "fa-solid fa-magnifying-glass-plus",
    Order = 60,
    Collapsible = true,
    Parameters =
    [
        new FragmentParameter
        {
            Name = "upscale_by",
            Label = "Upscale Factor",
            Type = ParameterType.Slider,
            Min = 1,
            Max = 4,
            Step = 0.1,
            DefaultValue = 1.5
        },
        new FragmentParameter
        {
            Name = "strength",
            Label = "ControlNet Strength",
            Type = ParameterType.Slider,
            Min = 0,
            Max = 2,
            Step = 0.01,
            DefaultValue = 0.2
        }
    ]
};
```

#### Parameters Class

```csharp
public class Parameters
{
    public double UpscaleBy { get; set; } = 1.5;
    public double Strength { get; set; } = 0.2;
    public string Scope { get; set; } = "";
}
```

#### BuildInternal Logic

The merged `BuildInternal` combines the node creation from both fragments:

```csharp
private static void BuildInternal(
    ComfyWorkflowBuilder builder,
    NodeRegistry registry,
    Parameters p,
    string scope,
    string scopeTitle)
{
    // --- ControlNet phase ---
    var controlnetNodeId = $"{scope}qwen_controlnet";
    var modelRef = registry.GetRef($"{p.Scope}model_output");
    var modelPatchRef = registry.GetRef($"{p.Scope}model_patch_output");
    var vaeRef = registry.GetRef($"{p.Scope}vae_output");
    var tileMapRef = registry.GetRef($"{p.Scope}tile_map_output");

    builder.AddNode(controlnetNodeId, node => node
        .Type("QwenImageDiffsynthControlnet")
        .Title($"{scopeTitle}Qwen Image Diffsynth ControlNet")
        .Input("strength", p.Strength)
        .InputRef("model", modelRef)
        .InputRef("model_patch", modelPatchRef)
        .InputRef("vae", vaeRef)
        .InputRef("image", tileMapRef));

    // Overwrite model_output with ControlNet-patched model
    registry.Register($"{p.Scope}model_output", controlnetNodeId, 0);

    // --- UltimateSDUpscale phase ---
    var upscaleNodeId = $"{scope}ultimate_sd_upscale";
    var imageRef = registry.GetRef("image_output");
    var patchedModelRef = registry.GetRef($"{p.Scope}model_output");  // Now the patched model
    var positiveRef = registry.GetRef($"{scope}positive_output");
    var negativeRef = registry.GetRef($"{scope}negative_output");
    var upscaleVaeRef = registry.GetRef($"{scope}vae_output");
    var upscaleModelRef = registry.GetRef($"{scope}upscale_model_output");

    var tileWidth = (int)(p.UpscaleBy * 1568 / 2 + 32);
    var tileHeight = (int)(p.UpscaleBy * 1356 / 2 + 32);
    tileWidth = (tileWidth / 8) * 8;
    tileHeight = (tileHeight / 8) * 8;

    builder.AddNode(upscaleNodeId, node => node
        .Type("UltimateSDUpscale")
        .Title($"{scopeTitle}Ultimate SD Upscale")
        .Input("upscale_model", "enabled")
        .Input("upscale_by", p.UpscaleBy)
        .Input("tile_width", tileWidth)
        .Input("tile_height", tileHeight)
        .Input("tile_overlap", 64)
        .Input("denoise_strength", 0.4)
        .Input("mask_blur", 8)
        .Input("mask_rounding", 0)
        .Input("padding", 32)
        .Input("seam_fix_mode", "None")
        .Input("force_tile", false)
        .InputRef("image", imageRef)
        .InputRef("model", patchedModelRef)
        .InputRef("positive", positiveRef)
        .InputRef("negative", negativeRef)
        .InputRef("vae", upscaleVaeRef)
        .InputRef("upscale_model", upscaleModelRef));

    // Overwrite image_output with upscaled image
    registry.Register("image_output", upscaleNodeId, 0);
}
```

---

### Workflow Changes

#### New Stage 2 Build Order (3 steps instead of 5)

| Step | Fragment                     | Notes                    |
| ---- | ---------------------------- | ------------------------ |
| 9    | `TilePreprocessorFragment`   | Hidden, unchanged        |
| 10   | `ModelPatchLoaderFragment`   | Hidden, unchanged        |
| 11   | `UpscaleModelLoaderFragment` | Hidden, unchanged        |
| 12   | **`ZImageUpscaleFragment`**  | **Replaces steps 11+13** |

The workflow class changes:

- Remove `_controlnetFragment` and `_ultimateSDUpscaleFragment` fields
- Add `_zImageUpscaleFragment` field
- Replace the two conditional blocks with one:
  ```csharp
  var upscaleFragment = parameters.GetFragment("zimage_upscale");
  if (upscaleFragment?.IsActive == true)
  {
      _zImageUpscaleFragment.Build(builder, registry, new ZImageUpscaleFragment.Parameters
      {
          UpscaleBy = upscaleFragment?.GetDouble("upscale_by", 1.5) ?? 1.5,
          Strength = upscaleFragment?.GetDouble("strength", 0.2) ?? 0.2,
          Scope = ""
      });
  }
  ```
- Update `GetFragments()` to return `_zImageUpscaleFragment` instead of the two separate fragments

---

### UI Form: `ZImageUpscaleForm.razor`

Standard fragment form pattern, single `FragmentReference`, two sliders:

```razor
@attribute [FragmentComponent("ZImageUpscaleForm")]
@inject IGenerationParameterService ParameterService

<MudGrid>
    <MudItem xs="6">
        <MudSlider T="double" @bind-Value="_localUpscaleBy" @bind-Value:after="OnUpscaleByChanged"
                   Min="1" Max="4" Step="0.1" Variant="Variant.Filled" ValueLabel>
            <small>Upscale Factor:</small> @_localUpscaleBy.ToString("F1")x
        </MudSlider>
    </MudItem>
    <MudItem xs="6">
        <MudSlider T="double" @bind-Value="_localStrength" @bind-Value:after="OnStrengthChanged"
                   Min="0" Max="2" Step="0.01" Variant="Variant.Filled" ValueLabel>
            <small>ControlNet Strength:</small> @_localStrength.ToString("F2")
        </MudSlider>
    </MudItem>
    <MudItem xs="12" Class="align-items-center justify-content-center d-flex">
        <MudText Typo="Typo.overline">@_resizeInfo</MudText>
    </MudItem>
    <MudItem xs="12">
        <MudAlert Severity="Severity.Info" Dense="true">
            <MudText Typo="Typo.caption">
                <strong>Z-Image Turbo Upscale</strong> chains base generation into tiled upscale
                with ControlNet tile guidance for high-resolution output.
            </MudText>
        </MudAlert>
    </MudItem>
</MudGrid>
```

---

### Why This Works Without Ordering Problems

1. **Registry dependencies are preserved**: The merged fragment still reads `tile_map_output` and `model_patch_output` (from steps 9-10) before writing the patched `model_output`, then reads that patched model for the upscale. The internal ordering within `BuildInternal` is sequential.

2. **Single `IsActive` gate**: Both ControlNet and upscale are part of the same logical operation. It makes sense that they're enabled/disabled together.

3. **No cross-fragment coupling**: The form reads both parameters from its own `FragmentReference`. No need to access sibling fragments.

4. **Cleaner `GetFragments()`**: One UI-exposed fragment instead of two, reducing the fragment list clutter.

5. **Self-contained Z-Image logic**: The fragment encapsulates the Z-Image Turbo upscale pipeline (ControlNet + UltimateSDUpscale) as a single reusable unit.

---

### Files to Modify/Create

| Action     | File                                                                            | Description         |
| ---------- | ------------------------------------------------------------------------------- | ------------------- |
| **Create** | `BlazorWebApp/Workflows/Fragments/qwen/ZImageUpscaleFragment.cs`                | Merged fragment     |
| **Create** | `BlazorWebApp/Components/Shared/Generation/Fragments/ZImageUpscaleForm.razor`   | UI form             |
| **Delete** | `BlazorWebApp/Workflows/Fragments/qwen/QwenImageDiffsynthControlnetFragment.cs` | Replaced            |
| **Delete** | `BlazorWebApp/Workflows/Fragments/Enhancements/UltimateSDUpscaleFragment.cs`    | Replaced            |
| **Modify** | `BlazorWebApp/Workflows/Templates/ZImage/ZImageTxt2ImgUpscaleWorkflow.cs`       | Use merged fragment |

---

### Risks and Mitigations

| Risk                                  | Impact                               | Mitigation                                                                           |
| ------------------------------------- | ------------------------------------ | ------------------------------------------------------------------------------------ |
| Fragment becomes Z-Image specific     | Not reusable for other architectures | Acceptable — the ControlNet + upscale combo is Z-Image-specific                      |
| Future general ControlNet enhancement | May need to split again              | The general ControlNetForm can coexist; this fragment handles the Z-Image Turbo case |
| Deleting existing fragments           | Breaks references                    | Update workflow class in same commit                                                 |
