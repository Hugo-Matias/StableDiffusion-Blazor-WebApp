# WAN Img2Vid Workflow - Integration Summary

## Architecture Principles

### 1. Fragment Reusability ?
- **Single `sampler-advanced.sbn` fragment** with conditional UI via `with_ui` parameter
- HIGH sampler uses `with_ui: true` ? Shows DoubleSamplerForm
- LOW sampler uses `with_ui: false` ? No UI, controlled programmatically
- **Benefit**: Avoids duplication, maintains single source of truth

### 2. Component Reusability ?
- **Uses `LatentForm`** instead of custom `WanResolutionForm`
- LatentForm handles: width, height, batch_size
- `batch_size` flows through flattened parameters to templates
- **Benefit**: No redundant components, consistent UX across workflows

### 3. Parameter Flow
- Parameters from ALL fragments are flattened during template rendering
- `batch_size` from `load_image` (LatentForm) ? available as `{{ batch_size }}` in templates
- Fragment-specific parameters can be explicitly referenced if needed
- **Benefit**: Flexible parameter composition without coupling

## Fragment Classification

| Fragment | Type | Component | UI | Reusability |
|----------|------|-----------|-----|-------------|
| `load-image.sbn` | `latent` | `LatentForm` | ? | **Shared** with txt2img |
| `load-model-sage.sbn` | `loader` | - | ? | - |
| `load-clip-vae.sbn` | `loader` | - | ? | - |
| `lora-loader-model-only.sbn` | `utility` | - | ? | - |
| `model-sampling-sd3.sbn` | `utility` | - | ? | - |
| `prompts.sbn` | `prompts` | `PromptsForm` | ? | **Shared** across workflows |
| `painter-i2v.sbn` | `settings` | `PainterI2VForm` | ? | WAN-specific required config |
| `sampler-advanced.sbn` | `sampler` | `DoubleSamplerForm` | Conditional | **Reusable** via `with_ui` param |
| `clip-vision.sbn` | `utility` | - | ? | - |
| `frame-interpolation.sbn` | `enhancement` | `FrameInterpolationForm` | ? | Video workflows |
| `vae-decode.sbn` | `output` | - | ? | **Shared** across workflows |
| `save-video.sbn` | `output` | - | ? | Video workflows |

## Key Components

### 1. LatentForm (Reused)
**Purpose**: Resolution and batch control

**Parameters**:
- `width` (int): Target width (64-2048, step 32)
- `height` (int): Target height (64-2048, step 32)
- `batch_size` (int): Batch size (1-8) - **flows to `{{ batch_size }}` in templates**

**Fragment**: `wan/load-image.sbn`

**Why Reuse?**
- LatentForm already handles resolution + batch
- `batch_size` semantics are the same (number of parallel generations)
- Parameters flow naturally through flattened params
- Avoids creating redundant `WanResolutionForm`

### 2. PainterI2VForm
**Purpose**: Controls WAN-specific video generation parameters

**Parameters**:
- `length` (int): Video length in frames (17-257, step 8)
  - Template uses `video_length`, fragment uses `length` - handled transparently
- `motion_amplitude` (double): Motion intensity (0.1-3.0)
- `shift` (int): Model sampling shift parameter (1-20)
  - **Cross-fragment update**: Also writes to `model_sampling_high` and `model_sampling_low`

**Fragment**: `wan/painter-i2v.sbn`

**Removed**: `batch_size` - now comes from `LatentForm` via parameter flattening

### 3. DoubleSamplerForm
**Purpose**: Coordinates dual-stage sampling for high and low noise models

**Behavior**:
- Registered ONLY to `sampler_high` fragment (when `with_ui: true`)
- Writes to BOTH `sampler_high` AND `sampler_low` fragments
- Automatically splits steps between high/low samplers
- Both fragments render to payload

**Parameters** (shared between both samplers):
- `sampler_name`: Loaded from `KSamplerAdvanced` node
- `scheduler`: Loaded from `KSamplerAdvanced` node
- `steps`: Total steps (min: 2, max: 50, **step: 2**) - divided by 2
- `cfg`: CFG scale (1.0-15.0)
- `seed`: Random seed (high sampler only, low uses 0)

**Computed Values**:
- High sampler: `start_at_step=0`, `end_at_step=steps/2`, `add_noise=enable`, `return_leftover=enable`
- Low sampler: `start_at_step=steps/2`, `end_at_step=10000`, `add_noise=disable`, `return_leftover=disable`

**Fragment**: `wan/sampler-advanced.sbn` (single reusable fragment with conditional UI)

### 4. FrameInterpolationForm
**Purpose**: RIFE-based frame interpolation (optional enhancement)

**Parameters**:
- `scale_by` (double): Upscale factor before interpolation (1.0-4.0)
- `frame_multiplier` (int): Frame rate multiplier (1-8)
- `rife_model` (string): RIFE model version

**Fragment**: `wan/frame-interpolation.sbn`

**Schema Type**: `enhancement` (optional, collapsible)

## Parameter Mapping: Template ? Fragments

| Template Parameter | Fragment | Fragment Parameter | Component | Source |
|-------------------|----------|-------------------|-----------|--------|
| `HighModel` | - | - | Assets panel | Model selection |
| `LowModel` | - | - | Assets panel | Model selection |
| `Clip` | - | - | Assets panel | CLIP selection |
| `ClipVision` | - | - | Assets panel | CLIP Vision selection |
| `Vae` | - | - | Assets panel | VAE selection |
| `width` | `load_image` | `width` | LatentForm | Resolution |
| `height` | `load_image` | `height` | LatentForm | Resolution |
| `batch_size` | `load_image` | `batch_size` | LatentForm | **Flattened params** |
| `video_length` | `video_settings` | `length` | PainterI2VForm | Video frames |
| `motion_amplitude` | `video_settings` | `motion_amplitude` | PainterI2VForm | Motion control |
| `shift` | `video_settings` + `model_sampling_*` | `shift` | PainterI2VForm | **Cross-fragment update** |
| `seed` | `sampler_high` | `seed` | DoubleSamplerForm | Random seed |
| `steps` | `sampler_high` + `sampler_low` | `steps` | DoubleSamplerForm | **Also updates sampler_low** |
| `cfg` | `sampler_high` + `sampler_low` | `cfg` | DoubleSamplerForm | **Also updates sampler_low** |
| `sampler_name` | `sampler_high` + `sampler_low` | `sampler_name` | DoubleSamplerForm | **Also updates sampler_low** |
| `scheduler` | `sampler_high` + `sampler_low` | `scheduler` | DoubleSamplerForm | **Also updates sampler_low** |
| `frame_interpolation_*` | `frame_interpolation` | Various | FrameInterpolationForm | RIFE interpolation |

## Conditional UI Pattern: `with_ui` Parameter

**Problem**: Prevent duplicate UI for reused fragments (sampler_high + sampler_low)

**Solution**: Conditional `#meta.ui` section in fragment template

```scriban
#meta
{
  "outputs": {
    "latent_output": { "node": "{{ sampler_id }}", "index": 0 }
  }{{~ if with_ui ~}},
  "ui": {
    "type": "sampler",
    "component": "DoubleSamplerForm",
    // ... schema ...
  }{{~ end ~}}
}
#end
```

**Template Usage**:
```json
{
  "id": "sampler_high",
  "fragment": "wan/sampler-advanced.sbn",
  "parameters": {
    "with_ui": true,  // ? Shows DoubleSamplerForm
    // ...
  }
},
{
  "id": "sampler_low",
  "fragment": "wan/sampler-advanced.sbn",
  "parameters": {
    "with_ui": false,  // ? No UI
    // ...
  }
}
```

**Benefits**:
- ? Single fragment file (DRY principle)
- ? Conditional UI rendering
- ? Both instances render to payload
- ? Reusable across workflows

## Fragment Discovery Logic

With the fix in `GenerationParameterService.DiscoverFragments()`:

```csharp
case FragmentType.Unknown:
case FragmentType.Conditioning:
case FragmentType.Output:
    // Include if defaultCollapsed OR has designed component
    if (schema.DefaultCollapsed || schema.HasDesignedComponent)
    {
        _optionalFragments.Add(reference);
    }
    break;
```

This ensures:
- ? `PainterI2VForm` is discovered (has designed component)
- ? `DoubleSamplerForm` is discovered (HIGH only, has designed component + with_ui: true)
- ? `FrameInterpolationForm` is discovered (has designed component + defaultCollapsed)
- ? `LatentForm` is discovered (type=latent, assigned to PrimaryLatentFragment)
- ? `sampler_low` is NOT discovered (with_ui: false ? no UI schema) but still renders to payload

## Workflow Pipeline Order

```
1. loader_high          ? Load high noise model
2. loader_low           ? Load low noise model
3. loader_clip_vae      ? Load CLIP + VAE
4. lora_high_*          ? Apply LoRAs to high model (if any)
5. lora_low_*           ? Apply LoRAs to low model (if any)
6. model_sampling_high  ? Configure high model sampling (shift) [PainterI2VForm]
7. model_sampling_low   ? Configure low model sampling (shift) [PainterI2VForm]
8. load_image           ? Load and resize input image [LatentForm: width/height/batch_size]
9. clip_vision          ? Load CLIP Vision model
10. prompts             ? Encode prompts [PromptsForm]
11. video_settings      ? Configure video params [PainterI2VForm: length/motion/shift]
12. sampler_high        ? High noise sampling (0?steps/2) [DoubleSamplerForm]
13. sampler_low         ? Low noise sampling (steps/2??) [DoubleSamplerForm controlled]
14. clean_vram          ? Clean VRAM after sampling
15. vae_decode          ? Decode latents to frames
16. frame_interpolation ? Interpolate frames (optional) [FrameInterpolationForm]
17. video_save          ? Save video file
```

## Testing Checklist

- [ ] Load WAN Img2Vid workflow
- [ ] Verify all 4 UI components appear:
  - [ ] Resolution (LatentForm from load-image) - width/height/batch_size
  - [ ] Prompts (PromptsForm)
  - [ ] Video Settings (PainterI2VForm) - length, motion_amplitude, shift
  - [ ] Video Sampling (DoubleSamplerForm) - sampler, scheduler, steps, cfg, seed
  - [ ] Frame Interpolation (FrameInterpolationForm) - optional section
- [ ] Verify batch_size from LatentForm flows to `{{ batch_size }}` in template
- [ ] Change Shift in Video Settings ? verify model_sampling_high/low fragments update
- [ ] Change Steps in Video Sampling ? verify both samplers update (check payload_video_generation.json)
- [ ] Verify high sampler: `end_at_step = steps/2`
- [ ] Verify low sampler: `start_at_step = steps/2`
- [ ] Verify sampler_low NOT showing duplicate UI
- [ ] Generate video and verify output
- [ ] Test frame interpolation toggle
- [ ] Verify workflow state persistence

## Files Modified/Created

### Created Files
- `BlazorWebApp\Components\Shared\Generation\Fragments\DoubleSamplerForm.razor`

### Modified Files
- `BlazorWebApp\Components\Shared\Generation\Fragments\PainterI2VForm.razor`
  - Removed `batch_size` (now from LatentForm)
  - Added `shift` with cross-fragment updates
  - Dynamic constraint loading
- `BlazorWebApp\Workflows\Fragments\wan\load-image.sbn`
  - Uses `LatentForm` (includes batch_size)
  - Reuses existing component
- `BlazorWebApp\Workflows\Fragments\wan\painter-i2v.sbn`
  - Removed `batch_size` from schema (comes from LatentForm)
  - Added `shift` to schema
- `BlazorWebApp\Workflows\Fragments\wan\sampler-advanced.sbn`
  - Added conditional `with_ui` parameter
  - Single reusable fragment for both HIGH and LOW
- `BlazorWebApp\Workflows\Fragments\wan\frame-interpolation.sbn`
  - Changed type to "enhancement"
- `BlazorWebApp\Workflows\Templates\wan\img2vid.sbn`
  - Use single sampler-advanced.sbn with `with_ui` flag
  - HIGH: `with_ui: true`, LOW: `with_ui: false`
- `BlazorWebApp\Services\GenerationParameterService.cs`
  - Fixed fragment discovery logic

### Deleted Files
- ~~`BlazorWebApp\Workflows\Fragments\wan\sampler-advanced-high.sbn`~~ (Removed - violates DRY)
- ~~`BlazorWebApp\Workflows\Fragments\wan\sampler-advanced-low.sbn`~~ (Removed - violates DRY)
- ~~`BlazorWebApp\Components\Shared\Generation\Fragments\WanResolutionForm.razor`~~ (Removed - redundant)

## Architecture Benefits

### 1. Reusability ?
- `LatentForm` reused across txt2img and img2vid workflows
- `sampler-advanced.sbn` reusable via `with_ui` parameter
- `PromptsForm`, `vae-decode.sbn` shared across all workflows

### 2. Maintainability ?
- Single source of truth for sampler fragment
- No duplicate components
- Parameter flattening enables flexible composition

### 3. Flexibility ?
- Constraints loaded from schema (no hardcoded values)
- Cross-fragment parameter updates (shift, steps, cfg, etc.)
- Conditional UI rendering via template parameters

### 4. Simplicity ?
- No custom forms where existing ones work (`LatentForm` vs `WanResolutionForm`)
- Natural parameter flow through flattening
- Minimal code duplication

## Summary of Fixes

### ? Fixed: Video Settings (PainterI2VForm) Rendering

**Problem**: `painter-i2v.sbn` was using `"type": "feature"` which is NOT a valid `FragmentType` enum value. This caused it to default to `FragmentType.Unknown` and be treated as optional/collapsible.

**Solution**:
```json
// Before
"type": "feature"

// After
"type": "settings",
"collapsible": false
```

**Valid FragmentType values**:
- `unknown` - Generic, treated as optional
- `loader` - Model loading
- `prompts` - Prompt encoding ?
- `latent` - Resolution/latent settings ?
- `sampler` - Sampling settings ?
- `settings` - **Required workflow configuration** ? (NEW - added for video settings)
- `conditioning` - CLIP/conditioning nodes
- `enhancement` - Optional enhancements ?
- `output` - Output nodes
- `utility` - Helper fragments

**Reasoning**: `settings` is the most appropriate type for required workflow-specific configuration parameters like video generation settings. These fragments:
- Are always visible (non-collapsible by default)
- Contain required workflow parameters
- Are mode/workflow-specific (not shared across workflows)
- Examples: video settings, animation parameters, mode-specific configuration

### ? Sampler (DoubleSamplerForm) Should Now Appear

The sampler fragment uses conditional UI via the `with_ui` parameter:
- `sampler_high` ? `with_ui: true` ? Shows `DoubleSamplerForm`
- `sampler_low` ? `with_ui: false` ? No UI component

This should work correctly now that the fragment discovery logic includes fragments with designed components.

### Current Fragment Types in WAN Workflow:
| Fragment | Type | Component | Collapsible | Order |
|----------|------|-----------|-------------|-------|
| `load-image` | `latent` | `LatentForm` | Yes | 30 |
| `prompts` | `prompts` | `PromptsForm` | Yes | - |
| `sampler-advanced` (HIGH) | `sampler` | `DoubleSamplerForm` | **No** | **35** |
| `painter-i2v` | **`settings`** | `PainterI2VForm` | **No** | 40 |
| `frame-interpolation` | `enhancement` | `FrameInterpolationForm` | Yes | 70 |

**Expected UI Order**:
1. **Resolution (LatentForm)** - order 30, collapsible
2. **Prompts (PromptsForm)** - collapsible
3. **Video Sampling (DoubleSamplerForm)** - order 35, **NOT collapsible** (core sampling settings)
4. **Video Settings (PainterI2VForm)** - order 40, **NOT collapsible** (required WAN config)
5. **Frame Interpolation (FrameInterpolationForm)** - order 70, collapsible (optional enhancement)

**Rationale**:
- **Sampling comes BEFORE video settings** - sampling is the core of generation, video settings are workflow-specific configuration
- **Both are non-collapsible** - they are required for the workflow to function
- This matches the natural generation flow: Resolution ? Prompts ? **Sampling** ? Workflow Config ? Enhancements
