# Z-Image to ZIT Workflow Conversion Plan

## Overview

Convert the "Z-Image to ZIT Workflow" ComfyUI workflow into a C# fluent builder workflow class. This workflow chains Z-Image Base generation into Z-Image Turbo upscale via UltimateSDUpscale with ControlNet tile guidance -- a two-stage pipeline producing high-resolution, structurally consistent output.

---

## Problem Statement

The existing [`ZImageTxt2ImgWorkflow`](BlazorWebApp/Workflows/Templates/ZImage/ZImageTxt2ImgWorkflow.cs) generates images at base resolution only. The Z-Image to ZIT workflow provides a two-stage pipeline:

1. **Stage 1**: Generate at base resolution using Z-Image Base + ClownsharKSampler
2. **Stage 2**: Upscale using UltimateSDUpscale with QwenImageDiffsynth ControlNet tile guidance

This workflow requires new fragments for VAE merging, model patch loading, ControlNet application, tile preprocessing, upscale model loading, and the UltimateSDUpscale node.

---

## Key Decisions

| Decision                 | Choice              | Rationale                                                            |
| ------------------------ | ------------------- | -------------------------------------------------------------------- |
| Separate workflow mode   | Yes                 | Two-stage pipeline is architecturally distinct from single-stage T2I |
| Advanced sampler options | Hardcoded           | Use JSON template values; expose only standard sampler params        |
| VAE merge ratio          | Hardcoded (0.3/0.7) | Infrastructure detail, not user-facing                               |
| ControlNet strength      | UI-exposed          | User may want to adjust guidance intensity                           |
| FaceDetailer support     | Not included        | Out of scope for initial conversion                                  |

---

## Phase 1: New Fragments

### 1.1 `VaeMergeFragment` - Core/VAEMergeFragment.cs

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

| Parameter    | Type   | Default                                                    | UI-Exposed |
| ------------ | ------ | ---------------------------------------------------------- | ---------- |
| `patch_name` | string | "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors" | No (asset) |

**Nodes Created**:

- `ModelPatchLoader`

**Outputs**: `model_patch_output`

---

### 1.3 `QwenImageDiffsynthControlnetFragment` - qwen/QwenImageDiffsynthControlnetFragment.cs

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

### `ZImageTxt2ImgUpscaleWorkflow` - Templates/ZImage/ZImageTxt2ImgUpscaleWorkflow.cs

**Metadata**:

```csharp
Title = "Txt2Img Upscale"
Base = ModelBase.ZImage
Mode = ModeType.Txt2Img
CompatibleResourceBaseModels = ["ZImageTurbo", "ZImageBase"]
```

**Assets**:
| Parameter | Type | Default |
|-----------|------|---------|
| `Model` | DiffusionModel | "z_image_turbo_bf16.safetensors" |
| `Clip` | Clip | "qwen_3_4b.safetensors" |
| `Vae` | Vae | "ae.safetensors" |
| `VaeMergeA` | Vae | "Z-image-ae.safetensors" |
| `VaeMergeB` | Vae | "Ultra_flux_For_Z-image-vae.safetensors" |
| `ControlNet` | ControlNet | "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors" |
| `UpscaleModel` | UpscaleModel | "x1_ITF_SkinDiffDetail_Lite_v1.pth" |

**Build Order**:

1. LoadDiffusion (UNet + CLIP + VAE)
2. VaeMerge (dual VAE merge)
3. LoraLoader (if any)
4. EmptyLatent
5. Prompts
6. SeedVarianceEnhancer (conditional)
7. Sampler (Stage 1 - ClownsharKSampler)
8. VaeDecode (Stage 1 output)
9. TilePreprocessor (prepare ControlNet input)
10. ModelPatchLoader (load ControlNet)
11. QwenImageDiffsynthControlnet (patch model)
12. UpscaleModelLoader
13. UltimateSDUpscale (Stage 2)
14. Save

**UI-Exposed Fragments**:

- `prompts` - Prompt encoding
- `latent` - Resolution control
- `main_sampler` - Stage 1 sampler
- `seed_variance_enhancer` - Seed variation (conditional)
- `upscale` - Upscale factor control (new)
- `controlnet` - ControlNet strength (new)

---

## Phase 3: Fragment Reuse Table

| Component                 | class_type(s)                     | Fragment                               | Status   |
| ------------------------- | --------------------------------- | -------------------------------------- | -------- |
| UNet + CLIP + VAE loading | UNETLoader, CLIPLoader, VAELoader | `LoadDiffusionFragment`                | Existing |
| VAE Merge                 | VAELoader x2, VAEMerge            | `VaeMergeFragment`                     | **New**  |
| LoRA loading              | LoraLoader                        | `LoraLoaderFragment`                   | Existing |
| Empty latent              | EmptyLatentImage                  | `EmptyLatentFragment`                  | Existing |
| Prompt encoding           | CLIPTextEncode                    | `PromptsFragment`                      | Existing |
| Seed variance             | SeedVarianceEnhancer              | `SeedVarianceEnhancerFragment`         | Existing |
| Stage 1 sampler           | ClownsharKSampler_Beta            | `SamplerFragment`                      | Existing |
| VAE Decode                | VAEDecode                         | `VaeDecodeFragment`                    | Existing |
| Model patch loading       | ModelPatchLoader                  | `ModelPatchLoaderFragment`             | **New**  |
| ControlNet application    | QwenImageDiffsynthControlnet      | `QwenImageDiffsynthControlnetFragment` | **New**  |
| Tile preprocessing        | AIO_Preprocessor                  | `TilePreprocessorFragment`             | **New**  |
| Upscale model loading     | UpscaleModelLoader                | `UpscaleModelLoaderFragment`           | **New**  |
| Ultimate SD Upscale       | UltimateSDUpscale                 | `UltimateSDUpscaleFragment`            | **New**  |
| Image saving              | Image Saver                       | `SaveFragment`                         | Existing |

---

## Phase 4: UI-Exposed vs Hardcoded Values

| Parameter                    | JSON Value            | UI-Exposed? | Reason                   |
| ---------------------------- | --------------------- | ----------- | ------------------------ |
| Sampler name                 | linear/euler          | Yes         | User controls algorithm  |
| Scheduler                    | simple                | Yes         | User controls scheduling |
| Steps                        | 9                     | Yes         | Quality control          |
| CFG                          | 1                     | Yes         | Prompt adherence         |
| Denoise                      | 1.0                   | Yes         | Generation mode          |
| Eta                          | 0.5                   | Yes         | Sampler behavior         |
| Seed                         | 142                   | Yes         | Reproducibility          |
| Width/Height                 | 1568x1356             | Yes         | Resolution               |
| Batch size                   | 1                     | Yes         | Throughput               |
| Upscale factor               | 1.5                   | Yes         | Output resolution        |
| ControlNet strength          | 0.2                   | Yes         | Guidance intensity       |
| Clown DetailBoost weight     | 0.22                  | No          | Advanced tuning          |
| Clown DetailBoost method     | model                 | No          | Advanced tuning          |
| Clown DetailBoost mode       | hard                  | No          | Advanced tuning          |
| Clown SigmaScaling s_noise   | 0.86                  | No          | Advanced tuning          |
| Clown SDE noise type         | brownian              | No          | Advanced tuning          |
| Shark noise type             | brownian              | No          | Advanced tuning          |
| VAE Merge ratio              | 0.3/0.7               | No          | Infrastructure           |
| Tile preprocessor resolution | 1024                  | No          | Auto-scaled              |
| Upscale model                | x1_ITF_SkinDiffDetail | No          | Fixed for ZIT            |
| Save prefix                  | tmp/img               | No          | Always temp              |

---

## Phase 5: ModelBase Enum

No new enum value needed. Reuses existing `ModelBase.ZImage`.

**CompatibleResourceBaseModels**: `["ZImageTurbo", "ZImageBase"]`

---

## Phase 6: Default Values Summary

| Parameter           | Default                               |
| ------------------- | ------------------------------------- |
| Sampler             | linear/euler                          |
| Scheduler           | simple                                |
| Steps               | 9                                     |
| CFG                 | 1                                     |
| Denoise             | 1.0                                   |
| Eta                 | 0.5                                   |
| Resolution          | 872x1248 (latent) / 1568x1356 (pixel) |
| Upscale factor      | 1.5                                   |
| ControlNet strength | 0.2                                   |
| VAE Merge ratio     | 0.3 (Z-image-ae) / 0.7 (Ultra_flux)   |

---

## Stress Points

1. **VAE Merge complexity**: The dual-VAE merge pattern is unique to this workflow. Must ensure the merged VAE is used consistently in both Stage 1 decode and Stage 2 upscale.

2. **ControlNet data flow**: The tile map from Stage 1 image feeds into ControlNet, which patches the model for Stage 2. This creates a dependency chain where Stage 2 cannot start until Stage 1 completes.

3. **ClownsharKSampler advanced options**: The Clown/Shark option nodes are hardcoded with JSON values. If users later want to tune these, the fragment parameters must be extended.

4. **Tile size calculation**: The workflow uses `MathExpression` nodes for dynamic tile sizing (`a * b / 2 + 32`). This logic must be replicated in the UltimateSDUpscale fragment.

---

## Implementation Order

1. Create `VaeMergeFragment` (Core)
2. Create `ModelPatchLoaderFragment` (Core)
3. Create `TilePreprocessorFragment` (Core)
4. Create `QwenImageDiffsynthControlnetFragment` (qwen)
5. Create `UpscaleModelLoaderFragment` (Core)
6. Create `UltimateSDUpscaleFragment` (Enhancements)
7. Create `ZImageTxt2ImgUpscaleWorkflow` (Templates/ZImage)
8. Write unit tests for new fragments
9. Write workflow integration test
10. Build and validate
