# Z-Image to ZIT Workflow Conversion Plan

## Overview

Convert the "Z-Image to ZIT Workflow" ComfyUI workflow into C# fluent builder workflow classes. Topology review in Comfy showed the source workflow contains two distinct runnable paths: a two-stage Z-Image Base -> ZIT text-to-image path, and a separate UltimateSDUpscale image-to-image path that starts from a loaded image. The current app implementation intentionally fused these paths, but that is not how the source workflow is wired.

---

## Problem Statement

The existing [`ZImageTxt2ImgWorkflow`](BlazorWebApp/Workflows/Templates/ZImage/ZImageTxt2ImgWorkflow.cs) generates images at base resolution only. The source Z-Image to ZIT workflow provides two related but separate pipelines:

1. **Text-to-image multi-pass generation**: Generate a base latent with Z-Image Base, then run a second ZIT sampler pass using the ZIT model.
2. **Image-to-image ZIT upscale**: Load an input image, preprocess it for tile guidance, patch the ZIT model with QwenImageDiffsynth ControlNet, then run `UltimateSDUpscale`.

This workflow requires new fragments for VAE merging, model patch loading, ControlNet application, tile preprocessing, upscale model loading, and the UltimateSDUpscale node. It likely should be represented as two workflow templates unless we deliberately build an app-only chained enhancement mode.

---

## Topology Correction - 2026-05-06

After loading the source workflow in Comfy and tracing links from `workflow.json`, the original assumptions in this plan were corrected:

| Region                                         | Source Workflow Behavior                                                                                                                                                    | Important Links                                                                                                                         |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Blue / model loading                           | Loads Z-Image Base and ZIT model paths independently, plus the VAE stack. Use single `ae.safetensors` for now because VAE merge is unavailable.                             | Base model feeds Z-Image pass; ZIT model feeds ZIT pass and red upscaler path.                                                          |
| Green / "1. Two Stage Z-Image to ZIT samplers" | True two-stage text-to-image path. `KSamplerAdvanced - Z-image` feeds `ClownsharKSampler - ZIT`; that result is decoded and saved by `Image Saver - ZIT`.                   | `639.KSamplerAdvanced -> 533.ClownsharKSampler - ZIT -> 532.VAEDecode`.                                                                 |
| Red / `UltimateSDUpscale`                      | Separate image-to-image upscale path. It loads/preprocesses an input image and uses the ZIT model + ControlNet patch. It does not consume the green region's decoded image. | `216.LoadImage -> 709.AIO_Preprocessor -> 708.QwenImageDiffsynthControlnet`, and `279/278.Load reroute -> 334.UltimateSDUpscale.image`. |

Current app code now represents this as two app templates: `ZImageTxt2ImgMultiPassWorkflow` for the required green two-pass generation path with optional chained upscale, and `ZImageImg2ImgUpscaleWorkflow` for the red image-to-image upscale path.

Final split:

1. `ZImageTxt2ImgMultiPassWorkflow`: text-to-image, green path as the required core. It exposes both sampler stages as non-collapsible core controls. It also keeps the red `UltimateSDUpscale` pipeline as an optional enhancement that chains from the two-pass decoded image.
2. `ZImageImg2ImgUpscaleWorkflow`: image-to-image, red path as the required core. It starts from a source image and exposes `UltimateSDUpscale` controls as a non-collapsible settings section.
3. Both templates expose the `ModelPatch` asset for `ModelPatchLoader`, plus the upscale model asset. Phase-specific UI labels identify the base path as `Phase 1 - ZIB Base` and the turbo path as `Phase 2 - ZIT Turbo`. Both templates include the standard Detailer enhancement after their main image output.

---

## Key Decisions

| Decision                 | Choice           | Rationale                                                                    |
| ------------------------ | ---------------- | ---------------------------------------------------------------------------- |
| Separate workflow mode   | Yes              | Two-stage pipeline is architecturally distinct from single-stage T2I         |
| Advanced sampler options | Hardcoded        | Use JSON template values; expose only standard sampler params                |
| VAE merge                | Disabled for now | Original node pack is unavailable; use single `ae.safetensors` VAE           |
| ControlNet strength      | UI-exposed       | User may want to adjust guidance intensity                                   |
| ControlNet node type     | UI-toggleable    | Default to original Qwen node, allow Z-Image node for debugging              |
| Upscale sampler settings | UI-exposed       | Stage 2 has distinct sampler behavior from the base pass                     |
| Upscale advanced options | UI-exposed       | Keep tinkering controls inside the upscaler's flush advanced expansion panel |
| FaceDetailer support     | Not included     | Out of scope for initial conversion                                          |

---

## Phase 1: New Fragments

### 1.1 `VaeMergeFragment` - Core/VAEMergeFragment.cs

**Status**: Deferred. The original `VAE Merge` node comes from an old node pack that is no longer available. Initial integration uses the single workflow VAE asset (`ae.safetensors`) and does not build VAE merge nodes.

**Purpose**: Load two VAEs and merge them with weighted sum.

| Parameter    | Type   | Default                                  | UI-Exposed |
| ------------ | ------ | ---------------------------------------- | ---------- |
| `vae_a_name` | string | "Z-image-ae.safetensors"                 | No (asset) |
| `vae_b_name` | string | "Ultra_flux_For_Z-image-vae.safetensors" | No (asset) |
| `ratio`      | float  | 0.3                                      | No         |

**Nodes Created**:

- `VAELoader` x2
- `VAEMerge` (mode: weighted_sum)

**Outputs**: `vae_output` (merged VAE)

---

### 1.2 `ModelPatchLoaderFragment` - Core/ModelPatchLoaderFragment.cs

**Purpose**: Load a model patch (ControlNet) for application to the main model.

| Parameter    | Type   | Default                                                    | UI-Exposed                                                                    |
| ------------ | ------ | ---------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `patch_name` | string | "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors" | No (`ModelPatch` asset from `ModelPatchLoader.name` / `models/model_patches`) |

**Nodes Created**:

- `ModelPatchLoader`

**Outputs**: `model_patch_output`

---

### 1.3 `QwenImageDiffsynthControlnetFragment` - qwen/QwenImageDiffsynthControlnetFragment.cs

**Status**: Implemented inside `ZImageUpscaleFragment` as a generic ControlNet application step. Default node type is `QwenImageDiffsynthControlnet` to stay true to the original workflow; `ZImageFunControlnet` is supported as a debug switch. Optional inputs are ignored for this workflow except the tile image.

**Purpose**: Apply QwenImageDiffsynth ControlNet to the model using preprocessed tile map.

| Parameter  | Type   | Default | UI-Exposed    |
| ---------- | ------ | ------- | ------------- |
| `strength` | float  | 0.2     | Yes           |
| `scope`    | string | ""      | No (internal) |

**Nodes Created**:

- `QwenImageDiffsynthControlnet`

**Inputs Consumed**: `model_output`, `model_patch_output`, `vae_output`, image (from preprocessor)

**Outputs**: Overwrites `model_output` with ControlNet-patched model

---

### 1.4 `TilePreprocessorFragment` - Core/TilePreprocessorFragment.cs

**Status**: Available after installing the AIO preprocessor node pack. Live schema confirms `AIO_Preprocessor.preprocessor` includes `TilePreprocessor`.

**Purpose**: Run AIO Preprocessor with TilePreprocessor to generate tile guidance map.

| Parameter    | Type | Default | UI-Exposed |
| ------------ | ---- | ------- | ---------- |
| `resolution` | int  | 1024    | No         |

**Nodes Created**:

- `AIO_Preprocessor` (TilePreprocessor)

**Inputs Consumed**: `image_output` (from Stage 1)

**Outputs**: `tile_map_output`

---

### 1.5 `UpscaleModelLoaderFragment` - Core/UpscaleModelLoaderFragment.cs

**Purpose**: Load an upscale model for pixel-space upscaling.

| Parameter    | Type   | Default                             | UI-Exposed |
| ------------ | ------ | ----------------------------------- | ---------- |
| `model_name` | string | "x1_ITF_SkinDiffDetail_Lite_v1.pth" | No (asset) |

**Nodes Created**:

- `UpscaleModelLoader`

**Outputs**: `upscale_model_output`

---

### 1.6 `UltimateSDUpscaleFragment` - Enhancements/UltimateSDUpscaleFragment.cs

**Status**: Implemented inside `ZImageUpscaleFragment`. The same fragment can render either as an optional collapsible enhancement in the T2I multi-pass workflow or as a required non-collapsible settings section in the I2I upscale workflow. Advanced UltimateSDUpscale controls live inside a flush inner expansion panel.

**Purpose**: Perform tiled upscale with ControlNet guidance.

| Parameter    | Type   | Default | UI-Exposed    |
| ------------ | ------ | ------- | ------------- |
| `upscale_by` | float  | 1.5     | Yes           |
| `scope`      | string | ""      | No (internal) |

**Nodes Created**:

- `EmptyLatentImage` (for upscale target)
- `UltimateSDUpscale`
- `VAEDecode`

**Inputs Consumed**: `image_output` (Stage 1), `model_output` (ControlNet-patched), `vae_output`, `upscale_model_output`, conditioning

**Outputs**: Overwrites `image_output` with upscaled image

---

## Phase 2: Workflow Class

### `ZImageTxt2ImgMultiPassWorkflow` - Templates/ZImage/ZImageTxt2ImgMultiPassWorkflow.cs

**Metadata**:

```csharp
Title = "Txt2Img Multi Pass"
Base = ModelBase.ZImage
Mode = ModeType.Txt2Img
CompatibleResourceBaseModels = ["ZImageTurbo", "ZImageBase"]
```

**Assets**:
| Parameter | Type | Default |
|-----------|------|---------|
| `Model` | DiffusionModel | "z_image_turbo_bf16.safetensors" / ZIT model (`Phase 2 - ZIT Turbo Model`) |
| `BaseModel` | DiffusionModel | "z_image_bf16.safetensors" (`Phase 1 - ZIB Base Model`) |
| `Clip` | Clip | "qwen_3_4b.safetensors" |
| `Vae` | Vae | "ae.safetensors" |
| `ModelPatch` | ModelPatch | "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors" (`Phase 2 - ZIT Model Patch`) |
| `UpscaleModel` | UpscaleModel | "x1_ITF_SkinDiffDetail_Lite_v1.pth" (`Phase 2 - ZIT Upscale Model`) |

**Build Order**:

1. LoadDiffusion for ZIT model + CLIP + single VAE
2. Load Z-Image Base UNet
3. EmptySD3LatentImage
4. LoraLoader on the ZIT model path, if any
5. Prompts
6. SeedVarianceEnhancer, conditional
7. Base sampler: `KSamplerAdvanced` using the Z-Image Base model
8. ZIT sampler: `ClownsharKSampler_Beta` using the ZIT model and the base sampler latent
9. VaeDecode to produce the two-pass output image
10. Optional ZIT upscale enhancement: TilePreprocessor -> ModelPatchLoader -> UpscaleModelLoader -> Qwen/ZImage ControlNet -> UltimateSDUpscale
11. Optional Detailer enhancement
12. Save

**UI-Exposed Fragments**:

- `prompts` - Prompt encoding
- `latent` - Resolution control
- `base_sampler` - `Phase 1 - ZIB Base`, non-collapsible core control
- `zit_sampler` - `Phase 2 - ZIT Turbo`, non-collapsible core control, visually separated from Phase 1 in the settings panel
- `seed_variance_enhancer` - Seed variation (conditional)
- `zimage_upscale` - Optional chained ZIT upscale enhancement; exposes ControlNet strength/node type, `UltimateSDUpscale` sampler settings, advanced upscale controls
- `detailer` - Optional standard detailer enhancement

### `ZImageImg2ImgUpscaleWorkflow` - Templates/ZImage/ZImageImg2ImgUpscaleWorkflow.cs

**Metadata**:

```csharp
Title = "Img2Img ZIT Upscale"
Base = ModelBase.ZImage
Mode = ModeType.Img2Img
CompatibleResourceBaseModels = ["ZImageTurbo", "ZImageBase"]
```

**Assets**:
| Parameter | Type | Default |
|-----------|------|---------|
| `Model` | DiffusionModel | "z_image_turbo_bf16.safetensors" / ZIT model (`Phase 2 - ZIT Turbo Model`) |
| `Clip` | Clip | "qwen_3_4b.safetensors" |
| `Vae` | Vae | "ae.safetensors" |
| `ModelPatch` | ModelPatch | "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors" (`Phase 2 - ZIT Model Patch`) |
| `UpscaleModel` | UpscaleModel | "x1_ITF_SkinDiffDetail_Lite_v1.pth" (`Phase 2 - ZIT Upscale Model`) |

**Sources**:

| Source         | Type  | Required |
| -------------- | ----- | -------- |
| `source_image` | Image | Yes      |

**Build Order**:

1. Load source image and register it as the main `image_output`
2. LoadDiffusion for ZIT model + CLIP + single VAE
3. LoraLoader on the ZIT model path, if any
4. Prompts
5. TilePreprocessor from the source image
6. ModelPatchLoader and UpscaleModelLoader
7. Qwen/ZImage ControlNet + `UltimateSDUpscale` as the core image-to-image upscale path
8. Optional Detailer enhancement
9. Save

**UI-Exposed Fragments**:

- `prompts` - Prompt encoding for the upscaler/detailer
- `zimage_upscale` - Core, non-collapsible upscaler settings
- `detailer` - Optional standard detailer enhancement

---

## Phase 3: Fragment Reuse Table

| Component                 | class_type(s)                     | Fragment                       | Status   |
| ------------------------- | --------------------------------- | ------------------------------ | -------- |
| UNet + CLIP + VAE loading | UNETLoader, CLIPLoader, VAELoader | `LoadDiffusionFragment`        | Existing |
| VAE Merge                 | VAELoader x2, VAE Merge           | `VaeMergeFragment`             | Deferred |
| LoRA loading              | LoraLoader                        | `LoraLoaderFragment`           | Existing |
| Empty latent              | EmptyLatentImage                  | `EmptyLatentFragment`          | Existing |
| Prompt encoding           | CLIPTextEncode                    | `PromptsFragment`              | Existing |
| Seed variance             | SeedVarianceEnhancer              | `SeedVarianceEnhancerFragment` | Existing |
| Base sampler              | KSamplerAdvanced                  | `KSamplerAdvancedFragment`     | New      |
| ZIT sampler               | ClownsharKSampler_Beta            | `SamplerFragment`              | Existing |
| VAE Decode                | VAEDecode                         | `VaeDecodeFragment`            | Existing |
| Model patch loading       | ModelPatchLoader                  | `ModelPatchLoaderFragment`     | New      |
| ControlNet application    | QwenImageDiffsynthControlnet      | `ZImageUpscaleFragment`        | New      |
| Tile preprocessing        | AIO_Preprocessor                  | `TilePreprocessorFragment`     | **New**  |
| Upscale model loading     | UpscaleModelLoader                | `UpscaleModelLoaderFragment`   | **New**  |
| Ultimate SD Upscale       | UltimateSDUpscale                 | `ZImageUpscaleFragment`        | New      |
| Image saving              | Image Saver                       | `SaveFragment`                 | Existing |

---

## Phase 4: UI-Exposed vs Hardcoded Values

| Parameter                    | JSON Value                   | UI-Exposed? | Reason                                                          |
| ---------------------------- | ---------------------------- | ----------- | --------------------------------------------------------------- |
| Base sampler name            | res_multistep                | Yes         | T2I multi-pass core control                                     |
| Base scheduler               | simple                       | Yes         | T2I multi-pass core control                                     |
| Base steps                   | 8                            | Yes         | T2I multi-pass core control                                     |
| Base CFG                     | 4                            | Yes         | T2I multi-pass core control                                     |
| Base seed                    | randomize                    | Yes         | T2I multi-pass core control                                     |
| ZIT sampler name             | linear/ralston_2s            | Yes         | T2I multi-pass core control                                     |
| ZIT scheduler                | beta                         | Yes         | T2I multi-pass core control                                     |
| ZIT steps                    | 10                           | Yes         | T2I multi-pass core control                                     |
| ZIT CFG                      | 1                            | Yes         | T2I multi-pass core control                                     |
| ZIT denoise                  | 0.56                         | Yes         | T2I multi-pass core control                                     |
| ZIT eta                      | 0.23                         | Yes         | Exposed by `SamplerForm` when the sampler schema includes `eta` |
| ZIT seed                     | randomize                    | Yes         | T2I multi-pass core control                                     |
| Width/Height                 | 1568x1356                    | Yes         | Resolution                                                      |
| Batch size                   | 1                            | Yes         | Throughput                                                      |
| Upscale factor               | 1.5                          | Yes         | Output resolution                                               |
| ControlNet strength          | 0.2                          | Yes         | Guidance intensity                                              |
| ControlNet node type         | QwenImageDiffsynthControlnet | Yes         | Debug swap only; default stays original                         |
| Model patch asset            | Z-Image-Turbo-Fun-Controlnet | Asset       | Loaded through `ModelPatchLoader.name`, not `ControlNetLoader`  |
| Upscale sampler              | deis_2m                      | Yes         | Stage 2 sampler differs from Stage 1                            |
| Upscale scheduler            | beta                         | Yes         | Stage 2 scheduler differs from Stage 1                          |
| Upscale steps                | 6                            | Yes         | Stage 2 quality/time tuning                                     |
| Upscale CFG                  | 1                            | Yes         | Stage 2 prompt adherence                                        |
| Upscale denoise              | 0.21                         | Yes         | Stage 2 redraw strength                                         |
| Upscale seed                 | randomize                    | Yes         | Stage 2 reproducibility                                         |
| Upscale mode type            | Linear                       | Yes         | Tiling order tuning                                             |
| Tile padding                 | 32                           | Yes         | Advanced tiling control                                         |
| Seam fix settings            | None / defaults              | Yes         | Advanced seam tuning                                            |
| Clown DetailBoost weight     | 0.22                         | No          | Advanced tuning                                                 |
| Clown DetailBoost method     | model                        | No          | Advanced tuning                                                 |
| Clown DetailBoost mode       | hard                         | No          | Advanced tuning                                                 |
| Clown SigmaScaling s_noise   | 0.86                         | No          | Advanced tuning                                                 |
| Clown SDE noise type         | brownian                     | No          | Advanced tuning                                                 |
| Shark noise type             | brownian                     | No          | Advanced tuning                                                 |
| VAE Merge ratio              | 0.3/0.7                      | No          | Deferred until a replacement node exists                        |
| Tile preprocessor resolution | 1024                         | No          | Auto-scaled                                                     |
| Upscale model                | x1_ITF_SkinDiffDetail        | Asset       | Original default; backend may need an installed fallback        |
| Save prefix                  | tmp/img                      | No          | Always temp                                                     |

---

## Phase 5: ModelBase Enum

No new enum value needed. Reuses existing `ModelBase.ZImage`.

**CompatibleResourceBaseModels**: `["ZImageTurbo", "ZImageBase"]`

---

## Phase 6: Default Values Summary

| Parameter           | Default                                         |
| ------------------- | ----------------------------------------------- |
| Sampler             | linear/euler                                    |
| Scheduler           | simple                                          |
| Steps               | 9                                               |
| CFG                 | 1                                               |
| Denoise             | 1.0                                             |
| Eta                 | 0.5                                             |
| Resolution          | 872x1248 (latent) / 1568x1356 (pixel)           |
| Upscale factor      | 1.5                                             |
| ControlNet strength | 0.2                                             |
| Upscale sampler     | deis_2m / beta / 6 steps / cfg 1 / denoise 0.21 |
| VAE                 | ae.safetensors (single VAE fallback)            |

---

## Stress Points

1. **VAE Merge unavailable**: The original dual-VAE merge node pack is no longer available. Current integration intentionally uses the single `ae.safetensors` VAE. If a replacement merge node is found later, the plan should restore the dual-VAE path and validate output parity.

2. **ControlNet data flow**: The tile map from Stage 1 image feeds into ControlNet, which patches the model for Stage 2. This creates a dependency chain where Stage 2 cannot start until Stage 1 completes.

3. **ClownOptions / SharkOptions advanced options**: The Clown/Shark option nodes are hardcoded or omitted initially. If users later want to tune these, the sampler fragments must be extended.

4. **Tile size calculation**: The workflow uses `MathExpression` nodes for dynamic tile sizing (`a * b / 2 + 32`). This logic is performed C# side so the workflow does not depend on the old pysssss math node pack.

---

## Implementation Order

1. Disable VAE merge in the workflow and use the single VAE asset
2. Ensure `TilePreprocessorFragment` uses live `AIO_Preprocessor` schema
3. Add a `ModelPatch` asset type backed by `ModelPatchLoader.name` and use it for the ControlNet-style patch asset
4. Ensure `UpscaleModelLoaderFragment` is built before `ZImageUpscaleFragment`
5. Add `KSamplerAdvancedFragment` for the base sampler pass
6. Configure `SamplerFragment` and `ZImageUpscaleFragment` so templates can render core non-collapsible controls or optional enhancements from the same reusable fragments
7. Convert the current T2I template into `ZImageTxt2ImgMultiPassWorkflow`
8. Add `ZImageImg2ImgUpscaleWorkflow`
9. Extend `ZImageUpscaleForm` with basic controls plus a flush advanced expansion panel
10. Write workflow integration tests for the two-pass T2I graph and I2I upscale graph
11. Build and validate
