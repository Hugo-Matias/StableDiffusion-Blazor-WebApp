# Phase 6 - Convert Wan (Img2Vid) Workflows and Fragments

## Status
**Phase:** 6
**Build Status:** Passing | **Tests:** 305/305 passing
**Phase Status:** [~] In Progress

---

## Objective

Convert **both** Wan video generation workflows and **all** Wan-specific fragments to C#. This is the most complex conversion phase due to:
- **Two distinct workflows** with different architectures (Img2Vid uses dual-model high/low noise; SteadyDancer uses WanVideo native nodes)
- **16+ fragment `.sbn` files** to convert across Wan and shared directories
- **Dual-model sampling** pattern (high noise + low noise KSamplerAdvanced split by step midpoint)
- **Video-specific nodes** (VHS_VideoCombine, RIFE VFI, PainterI2V, WanVideo* nodes)
- **Source inputs** (images, videos) not present in Txt2Img workflows
- **LoRA handling** unique to Wan (model-only LoRA loaders per high/low model)
- **Conditional frame interpolation** affecting output frame rate calculation

**Deliverables:**
- 2 workflow classes (`WanImg2VidWorkflow.cs`, `WanSteadyDancerWorkflow.cs`)
- ~16 fragment classes (new Wan-specific + shared utility fragments)
- Unit tests for all fragments and workflows
- Deletion of all corresponding `.sbn` files

---

## Analysis

### Workflow 1: `wan/img2vid.sbn` (Dual-Model Img2Vid)

**Architecture:** Uses two separate UNet models (high noise + low noise) with SageAttention + TorchSettings, split sampling at step midpoint.

**Pipeline Flow:**
```
Load High Model (UNETLoader -> SageAttention -> TorchSettings)
Load Low Model  (UNETLoader -> SageAttention -> TorchSettings)
Load CLIP + VAE (CLIPLoader + VAELoader)
[Optional: LoRA High/Low (per model)]
ModelSamplingSD3 (High) -> high_sampled_model_output
ModelSamplingSD3 (Low)  -> low_sampled_model_output
Load Image + Resize (LoadImage -> ImageResizeKJv2)
CLIP Vision (CLIPVisionLoader -> CLIPVisionEncode)
Prompts (CLIPTextEncode x2)
PainterI2V (video conditioning + latent creation)
KSamplerAdvanced (High) - steps 0 to midpoint, add noise
KSamplerAdvanced (Low)  - steps midpoint to end, no noise, uses high latent
Clean VRAM
VAE Decode (standard)
[Conditional: Frame Interpolation (ImageScaleBy -> RIFE VFI)]
Video Save (VHS_VideoCombine)
```

**Assets:**
| Parameter | Label | Type | Default |
|-----------|-------|------|---------|
| HighModel | High Model | DiffusionModel | wan22RemixT2VI2V_i2vHighV20.safetensors |
| LowModel | Low Model | DiffusionModel | wan22RemixT2VI2V_i2vLowV20.safetensors |
| Clip | CLIP | Clip | umt5_xxl_fp8_e4m3fn_scaled.safetensors |
| ClipVision | CLIP Vision | ClipVision | clip_vision_h.safetensors |
| Vae | VAE | Vae | wan_2.1_vae.safetensors |

**Sources:**
| Id | Label | Type | Required |
|----|-------|------|----------|
| source_image | Source Image | image | true |

**Unique Patterns:**
1. **Dual-model loading**: Same fragment (`load-model-sage.sbn`) called twice with different prefixes
2. **LoRA per model**: Each model can independently have LoRAs applied (high/low paths from `Lora` objects)
3. **Step splitting**: High sampler runs `0 -> steps/2`, Low sampler runs `steps/2 -> 10000`
4. **PainterI2V conditioning**: Combines image, CLIP vision, prompts, VAE into video conditioning + latent
5. **Conditional frame interpolation**: When active, adjusts output frame rate by multiplier

---

### Workflow 2: `wan/pose2vid-steadydancer.sbn` (SteadyDancer Pose2Vid)

**Architecture:** Uses WanVideo native node family (WanVideoModelLoader, WanVideoSamplerSettings, etc.) with pose detection and SteadyDancer embeds.

**Pipeline Flow:**
```
Load Video (VHS_LoadVideo)
Get Image Size & Count (from video frames)
Load Reference Image (LoadImage)
Resize Subject Image (ImageResizeKJv2)
Load Wan Model (WanVideoModelLoader -> BlockSwap -> LoRA)
Load Wan VAE (WanVideoVAELoader)
Load CLIP Vision (CLIPVisionLoader)
Text Encode (WanVideoTextEncodeCached)
Pose Detection (OnnxDetectionModelLoader -> PoseAndFaceDetection -> DrawViTPose -> Resize)
I2V Encode (WanVideoClipVisionEncode -> WanVideoImageToVideoEncode)
SteadyDancer Embeds (WanVideoEncode + CLIPVisionEncode -> WanVideoAddSteadyDancerEmbeds)
Context Options (WanVideoContextOptions)
Wan Sampler (WanVideoScheduler -> WanVideoSamplerSettings -> WanVideoSamplerFromSettings)
Wan Video Decode (WanVideoDecode)
[Conditional: Concat Preview (ImageConcatMulti)]
Video Save (VHS_VideoCombine)
```

**Assets:**
| Parameter | Label | Type | Default |
|-----------|-------|------|---------|
| Model | Model | DiffusionModel | Wan21_SteadyDancer_fp8_e4m3fn_scaled_KJ.safetensors |
| Vae | VAE | Vae | wan_2.1_vae.safetensors |
| ClipVision | CLIP Vision | ClipVision | clip_vision_h.safetensors |
| TextEncoder | Text Encoder | Clip | umt5_xxl_fp16.safetensors |
| SpeedLora | Speed LoRA | Lora | Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors |

**Sources:**
| Id | Label | Type | Required |
|----|-------|------|----------|
| source_video | Source Video | video | true |
| reference_image | Reference Image | image | false |

**Unique Patterns:**
1. **WanVideo node family**: Completely different node types from standard ComfyUI
2. **Pose detection pipeline**: ONNX model loading -> detection -> drawing -> resize
3. **SteadyDancer embeds**: Combines pose latents + clip vision into special embed format
4. **Context options**: Sliding window for long video generation
5. **Video input**: Loads video frames, not a single image
6. **Conditional preview concat**: Side-by-side output+source+pose

---

## Fragment Inventory

### Wan-Specific Fragments (New)

| # | Fragment | Source `.sbn` | Used By | Complexity | Notes |
|---|---------|---------------|---------|------------|-------|
| 1 | `LoadModelSageFragment` | `wan/load-model-sage.sbn` | Img2Vid | 3 | Parameterized prefix (high/low). UNETLoader -> SageAttention -> TorchSettings |
| 2 | `LoadClipVaeFragment` | `wan/load-clip-vae.sbn` | Img2Vid | 2 | CLIPLoader + VAELoader |
| 3 | `LoraLoaderModelOnlyFragment` | `wan/lora-loader-model-only.sbn` | Img2Vid | 3 | Model-only LoRA, dynamic input/output names |
| 4 | `ModelSamplingSD3Fragment` | `wan/model-sampling-sd3.sbn` | Img2Vid | 2 | Applies shift to model |
| 5 | `WanLoadImageFragment` | `wan/load-image.sbn` | Img2Vid | 3 | LoadImage + ImageResizeKJv2, has UI metadata |
| 6 | `ClipVisionEncodeFragment` | `wan/clip-vision.sbn` | Img2Vid | 2 | CLIPVisionLoader + CLIPVisionEncode |
| 7 | `WanPromptsFragment` | `wan/prompts.sbn` | Img2Vid | 2 | CLIPTextEncode for positive + negative |
| 8 | `PainterI2VFragment` | `wan/painter-i2v.sbn` | Img2Vid | 5 | Video conditioning node, multiple registry refs, has UI metadata |
| 9 | `SamplerAdvancedFragment` | `wan/sampler-advanced.sbn` | Img2Vid | 5 | KSamplerAdvanced with step range, dynamic model/latent inputs, has UI metadata |
| 10 | `FrameInterpolationFragment` | `wan/frame-interpolation.sbn` | Img2Vid, SteadyDancer(future) | 3 | ImageScaleBy -> CleanVRAM -> RIFE VFI, conditional |
| 11 | `DecodeWanFragment` | `wan/decode-wan.sbn` | SteadyDancer | 2 | WanVideoDecode with tiling options |
| 12 | `LoadWanModelFragment` | `wan/load-wan-model.sbn` | SteadyDancer | 5 | Complex: CompileSettings -> ModelLoader -> BlockSwap -> LoRASelect -> SetLoRAs |
| 13 | `LoadWanVaeFragment` | `wan/load-wan-vae.sbn` | SteadyDancer | 2 | WanVideoVAELoader |
| 14 | `TextEncodeWanFragment` | `wan/text-encode-wan.sbn` | SteadyDancer | 2 | WanVideoTextEncodeCached |
| 15 | `PoseDetectionFragment` | `wan/pose-detection.sbn` | SteadyDancer | 5 | OnnxLoader -> PoseDetection -> DrawPose -> Resize, 4 nodes |
| 16 | `SteadyDancerEmbedsFragment` | `wan/steadydancer-embeds.sbn` | SteadyDancer | 5 | PoseEncode + CLIPVision -> AddSteadyDancerEmbeds, 4 nodes |
| 17 | `ContextOptionsFragment` | `wan/context-options.sbn` | SteadyDancer | 2 | WanVideoContextOptions |
| 18 | `SamplerWanFragment` | `wan/sampler-wan.sbn` | SteadyDancer | 3 | WanVideoScheduler -> SamplerSettings -> SamplerFromSettings |
| 19 | `I2VEncodeFragment` | `wan/i2v-encode.sbn` | SteadyDancer | 3 | WanVideoClipVisionEncode + WanVideoImageToVideoEncode |

### Shared Utility Fragments (New - used by Wan workflows)

| # | Fragment | Source `.sbn` | Used By | Complexity | Notes |
|---|---------|---------------|---------|------------|-------|
| 20 | `CleanVramFragment` | `clean-vram.sbn` | Img2Vid | 1 | Simple passthrough for GPU cleanup |
| 21 | `SaveVideoFragment` | `save-video.sbn` | Both | 2 | VHS_VideoCombine with frame rate |
| 22 | `LoadVideoFragment` | `load-video.sbn` | SteadyDancer | 2 | VHS_LoadVideo with frame options |
| 23 | `GetImageSizeFragment` | `get-image-size.sbn` | SteadyDancer | 1 | GetImageSizeAndCount |
| 24 | `LoadImageFragment` | `load-image.sbn` | SteadyDancer | 1 | Simple LoadImage (no resize) |
| 25 | `ResizeImageFragment` | `resize-image-kj.sbn` | SteadyDancer | 2 | ImageResizeKJv2 with registry refs |
| 26 | `ConcatPreviewFragment` | `concat-preview.sbn` | SteadyDancer | 2 | ImageConcatMulti x2, conditional |
| 27 | `LoadClipVisionFragment` | `load-clip-vision.sbn` | SteadyDancer | 1 | CLIPVisionLoader (scoped) |

### Already Converted (Reusable)

| Fragment | Reused By | Notes |
|----------|-----------|-------|
| `VaeDecodeFragment` | Img2Vid (standard VAE decode path) | Latent input from `cleaned_latent_output` |

**Total new fragments: 27**
**Total `.sbn` files to delete: ~30** (19 wan/ + 8 shared + img2vid.sbn + pose2vid-steadydancer.sbn)

---

## Key Technical Challenges

### 1. Dual-Model Architecture (Img2Vid)
The Img2Vid workflow loads two separate UNet models and applies LoRAs independently:
- `LoadModelSageFragment` must accept a **prefix parameter** (`high`/`low`) to generate unique node IDs
- LoRAs check `lora.HighPath`/`lora.LowPath` from `GenerationParameters.Loras`
- `ModelSamplingSD3Fragment` chains to the correct model output (direct or post-LoRA)

**Solution:** Use the existing scope pattern. `LoadModelSageFragment` takes `scope` + `scopeTitle` params, same as `LoadFluxFragment` does for detailer.

### 2. Step Splitting (Img2Vid)
The high/low samplers split work at the step midpoint:
```
High: start_at_step=0, end_at_step=steps/2 (rounded)
Low:  start_at_step=steps/2 (rounded), end_at_step=10000
```
The low sampler also:
- Uses seed=0 (not the user seed)
- Disables add_noise
- Takes latent from high sampler output

**Solution:** `SamplerAdvancedFragment` is a **fully generic** `KSamplerAdvanced` wrapper with no knowledge of dual-model patterns. All step range, noise, and input parameters are explicit. The **workflow** orchestrates both calls and owns the split logic.

**Auto-split toggle:** The `SamplerAdvancedFragment` UI metadata exposes an `auto_split` boolean parameter (default: `true`). When enabled, `WanImg2VidWorkflow.Build()` calculates the midpoint and configures both samplers automatically. When disabled, each sampler's `start_at_step`/`end_at_step` come directly from the user's fragment parameter values, giving full manual control.

This keeps the fragment reusable for any `KSamplerAdvanced` use case while providing a QoL default for the dual-model workflow.

### 3. PainterI2V Multi-Reference Node
PainterI2V takes 7 inputs from different registry sources:
- `image_width_output`, `image_height_output` (from load-image resize)
- `positive_output`, `negative_output` (from prompts)
- `vae_output` (from loader)
- `clip_vision_output` (from clip vision)
- `image_output` (from load-image resize)

**Solution:** All upstream fragments register their outputs. PainterI2V fragment resolves all from registry.

### 4. Conditional Frame Interpolation Affecting Frame Rate
When frame interpolation is active, the save node's frame rate must be:
```
effective_frame_rate = base_frame_rate * interpolation_multiplier
```
And the image input changes from `image_output` to `frames_output`.

**Solution:** The workflow checks `frame_interpolation.IsActive` and adjusts SaveVideo parameters accordingly.

### 5. WanVideo Node Family (SteadyDancer)
SteadyDancer uses completely different node types from standard ComfyUI:
- `WanVideoModelLoader`, `WanVideoVAELoader`, `WanVideoTextEncodeCached`
- `WanVideoSamplerSettings`, `WanVideoSamplerFromSettings`, `WanVideoScheduler`
- `WanVideoClipVisionEncode`, `WanVideoImageToVideoEncode`
- `WanVideoEncode`, `WanVideoAddSteadyDancerEmbeds`
- `WanVideoBlockSwap`, `WanVideoSetBlockSwap`
- `WanVideoLoraSelect`, `WanVideoSetLoRAs`
- `WanVideoContextOptions`, `WanVideoDecode`

These are **NOT** the same as the Img2Vid nodes. No fragment sharing between the two workflows for model loading/sampling.

### 6. Lora Model in Assets (SteadyDancer)
SteadyDancer declares a `SpeedLora` as an **asset** (not via the Loras collection). This is passed directly to `WanVideoLoraSelect`.

**Solution:** Handle as a standard asset in `WorkflowMetadata.Assets` with type `AssetType.Lora`. Add `Lora` to the `AssetType` enum if not present.

### 7. Video Source Input (SteadyDancer)
SteadyDancer takes a **video file** as source input, not an image. The `LoadVideoFragment` must handle VHS_LoadVideo with frame parameters.

### 8. LoRA Handling Differences
- **Img2Vid**: Uses `GenerationParameters.Loras` with `HighPath`/`LowPath` per lora (applied per model)
- **SteadyDancer**: Uses a single LoRA from assets via `WanVideoLoraSelect`

**Solution:** Each workflow handles LoRAs differently in its `Build()` method. No shared LoRA fragment between them.

---

## Execution Checklist

### Step 1: Shared Utility Fragments
**Complexity:** 8
**Status:** [x] Complete

Create utility fragments used across both workflows:
- [x] `CleanVramFragment` (`clean-vram.sbn`) - parameterized node ID, input/output names
- [x] `SaveVideoFragment` (`save-video.sbn`) - VHS_VideoCombine with frame rate, format options
- [x] `LoadVideoFragment` (`load-video.sbn`) - VHS_LoadVideo with frame rate, dimensions, caps
- [x] `GetImageSizeFragment` (`get-image-size.sbn`) - GetImageSizeAndCount, registers width/height/num_frames
- [x] `LoadImageFragment` (`load-image.sbn`) - Simple LoadImage (single node, no resize)
- [x] `ResizeImageFragment` (`resize-image-kj.sbn`) - ImageResizeKJv2 with registry ref inputs
- [x] `LoadClipVisionFragment` (`load-clip-vision.sbn`) - CLIPVisionLoader (scoped)
- [x] Add `AssetType.Lora` to enum
- [x] Add `FragmentType.Input` and `FragmentType.Utility` to enum
- [x] Unit tests: 21 tests passing

**Files to create:**
- `BlazorWebApp/Workflows/Fragments/Core/CleanVramFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/SaveVideoFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/LoadVideoFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/GetImageSizeFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/LoadImageFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/ResizeImageFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/LoadClipVisionFragment.cs`
- `BlazorWebApp.Tests/Workflows/Fragments/CoreUtilityFragmentTests.cs`

### Files Modified
- `BlazorWebApp/Workflows/Models/WorkflowMetadata.cs` - Added `AssetType.Lora`
- `BlazorWebApp/Workflows/Models/FragmentParameter.cs` - Added `FragmentType.Input`, `FragmentType.Utility`

### Step 2: Img2Vid Loader Fragments
**Complexity:** 8
**Status:** [x] Complete

Create the loading fragments specific to the Img2Vid dual-model architecture:
- [x] `LoadModelSageFragment` (`wan/load-model-sage.sbn`) - UNETLoader -> SageAttention -> TorchSettings chain
- [x] `LoadClipVaeFragment` (`wan/load-clip-vae.sbn`) - CLIPLoader (type=wan, device=cpu) + VAELoader
- [x] `LoraLoaderModelOnlyFragment` (`wan/lora-loader-model-only.sbn`) - Model-only LoRA with dynamic names
- [x] `ModelSamplingSD3Fragment` (`wan/model-sampling-sd3.sbn`) - ModelSamplingSD3 with shift
- [x] Unit tests: 20 tests passing (includes 2 integration tests for full dual-model chain + LoRA chaining)

### Step 3: Img2Vid Conditioning Fragments
**Complexity:** 8
**Status:** [x] Complete

Create the image processing, CLIP vision, and prompts fragments:
- [x] `WanLoadImageFragment` (`wan/load-image.sbn`) - LoadImage + ImageResizeKJv2, registers image_output, width, height, original_image
- [x] `ClipVisionEncodeFragment` (`wan/clip-vision.sbn`) - CLIPVisionLoader + CLIPVisionEncode (uses original_image_output)
- [x] `WanPromptsFragment` (`wan/prompts.sbn`) - Dual CLIPTextEncode (positive + negative)
- [x] `PainterI2VFragment` (`wan/painter-i2v.sbn`) - Video conditioning node with UI metadata
- [x] Unit tests: 23 tests passing (includes 1 integration test for full conditioning chain)

### Step 4: Img2Vid Sampling & Output Fragments
**Complexity:** 5
**Status:** [x] Complete

Create the sampling and output fragments:
- [x] `SamplerAdvancedFragment` (`wan/sampler-advanced.sbn`) - Generic KSamplerAdvanced wrapper, has UI metadata with auto_split toggle
- [x] `FrameInterpolationFragment` (`wan/frame-interpolation.sbn`) - ImageScaleBy -> CleanVRAM -> RIFE VFI, conditional
- [x] Unit tests: 16 tests passing (includes 1 integration test for full dual-sampler + interpolation pipeline)

**Fragment dependency chain:**
```
SamplerAdvanced("sampler_high", high model, painter outputs, steps 0->mid) -> latent_output
SamplerAdvanced("sampler_low", low model, latent_output from high, steps mid->end) -> latent_output
CleanVram -> cleaned_latent_output
VaeDecode (existing, uses cleaned_latent_output) -> image_output
[FrameInterpolation if active]
SaveVideo (frame_rate adjusted if interpolation active)
```

**Key implementation notes:**
- `SamplerAdvancedFragment` is a **generic KSamplerAdvanced node builder** - no dual-model awareness
- UI metadata includes `auto_split` toggle (bool, default true) for QoL auto-midpoint calculation
- `SamplerAdvancedFragment.Parameters` must include: `SamplerId`, `Seed`, `Steps`, `Cfg`, `SamplerName`, `Scheduler`, `AddNoise`, `ReturnWithLeftoverNoise`, `StartAtStep`, `EndAtStep`, `ModelInputName`, `PositiveInputName`, `NegativeInputName`, `LatentInputName`, `Title`
- When `auto_split=true`, the workflow calculates midpoint and overrides `StartAtStep`/`EndAtStep` for each sampler call
- When `auto_split=false`, the user sets step ranges manually per sampler via the UI
- `FrameInterpolationFragment` has UI metadata and is conditional (`IsActive` check)
- `FrameInterpolationFragment` inserts 3 nodes: upscale -> clean -> RIFE

**Files to create:**
- `BlazorWebApp/Workflows/Fragments/Wan/SamplerAdvancedFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/FrameInterpolationFragment.cs`

**Commit checkpoint:** Sampling fragments compile and unit tests pass

---

### Step 5: WanImg2VidWorkflow
**Complexity:** 8
**Status:** [x] Complete

Compose all Img2Vid fragments into the complete workflow:
- [x] Create `WanImg2VidWorkflow.cs` implementing `IWorkflowBuilder`
- [x] Define metadata (assets, sources, GUID)
- [x] Implement `GetFragments()` returning UI-visible fragments
- [x] Implement `Build()` composing the full pipeline
- [x] Handle LoRA logic (check `Loras` collection for High/Low paths)
- [x] Handle auto-split toggle: when `auto_split=true`, calculate midpoint and configure samplers; when `false`, pass user step ranges directly
- [x] Handle conditional frame interpolation
- [x] Handle frame rate adjustment when interpolation is active
- [x] Add unit tests for workflow (including both auto-split on/off paths)

**Build order in `Build()`:**
```csharp
1. LoadModelSage (high_)
2. LoadModelSage (low_)
3. LoadClipVae
4. [For each LoRA: LoraLoaderModelOnly for high and/or low]
5. ModelSamplingSD3 (high, input=high_model_output or high_lora_model_output)
6. ModelSamplingSD3 (low, input=low_model_output or low_lora_model_output)
7. WanLoadImage
8. ClipVisionEncode
9. WanPrompts
10. PainterI2V
11. SamplerAdvanced (high: steps 0->mid, add_noise=enable, return_noise=enable)
12. SamplerAdvanced (low: seed=0, steps mid->end, add_noise=disable, return_noise=disable, latent from high)
13. CleanVram
14. VaeDecode (existing)
15. [FrameInterpolation if active]
16. SaveVideo (frame_rate adjusted if interpolation active)
```

**GetFragments() UI order:**
```
- WanLoadImageFragment (resolution UI)
- PainterI2VFragment (video settings UI)  
- SamplerAdvancedFragment (sampling UI)
- FrameInterpolationFragment (optional enhancement)
```

**Files to create:**
- `BlazorWebApp/Workflows/Templates/Wan/WanImg2VidWorkflow.cs`
- `BlazorWebApp.Tests/Workflows/WanImg2VidWorkflowTests.cs`

**Commit checkpoint:** Workflow generates valid JSON, unit tests pass

---

### Step 6: SteadyDancer-Specific Fragments
**Complexity:** 13
**Status:** [x] Complete (38 tests)

Create all fragments unique to the SteadyDancer workflow:
- [x] `LoadWanModelFragment` (`wan/load-wan-model.sbn`) - CompileSettings -> ModelLoader -> BlockSwap -> SetBlockSwap -> LoRASelect -> SetLoRAs
- [x] `LoadWanVaeFragment` (`wan/load-wan-vae.sbn`) - WanVideoVAELoader
- [x] `TextEncodeWanFragment` (`wan/text-encode-wan.sbn`) - WanVideoTextEncodeCached
- [x] `PoseDetectionFragment` (`wan/pose-detection.sbn`) - OnnxLoader -> PoseDetection -> DrawViTPose -> ResizePose
- [x] `I2VEncodeFragment` (`wan/i2v-encode.sbn`) - WanVideoClipVisionEncode + WanVideoImageToVideoEncode
- [x] `SteadyDancerEmbedsFragment` (`wan/steadydancer-embeds.sbn`) - PoseEncode + GetFirstPose + PoseClipVision -> AddSteadyDancerEmbeds
- [x] `ContextOptionsFragment` (`wan/context-options.sbn`) - WanVideoContextOptions
- [x] `SamplerWanFragment` (`wan/sampler-wan.sbn`) - WanVideoScheduler -> SamplerSettings -> SamplerFromSettings
- [x] `ConcatPreviewFragment` (`concat-preview.sbn`) - ImageConcatMulti x2, conditional

**Fragment dependency chain (SteadyDancer):**
```
LoadVideo -> video_frames, frame_count, audio
GetImageSize (from video_frames) -> image_size_info, width, height, num_frames
LoadImage -> image_input
ResizeImage (from image_input, width, height) -> resized_image
LoadWanModel -> model_output
LoadWanVae -> vae_output
LoadClipVision -> clip_vision_output
TextEncodeWan -> text_embeds
PoseDetection (from image_size_info, width, height) -> pose_images
I2VEncode (from resized_image, clip_vision_output, vae_output, width, height, num_frames) -> image_embeds
SteadyDancerEmbeds (from image_embeds, pose_images, clip_vision_output, vae_output) -> steadydancer_embeds
ContextOptions -> context_options
SamplerWan (from model_output, steadydancer_embeds, text_embeds, context_options) -> latent_output
DecodeWan (from latent_output, vae_output) -> image_output
[ConcatPreview (conditional, from image_output, resized_image, pose_images) -> preview_concat]
SaveVideo (from image_output or preview_concat)
```

**Files to create:**
- `BlazorWebApp/Workflows/Fragments/Wan/LoadWanModelFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/LoadWanVaeFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/TextEncodeWanFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/PoseDetectionFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/I2VEncodeFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/SteadyDancerEmbedsFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/ContextOptionsFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/SamplerWanFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Wan/DecodeWanFragment.cs`
- `BlazorWebApp/Workflows/Fragments/Core/ConcatPreviewFragment.cs`

**Commit checkpoint:** All SteadyDancer fragments compile and have unit tests

---

### Step 7: WanSteadyDancerWorkflow
**Complexity:** 8
**Status:** [x] Complete (31 tests)

Compose all SteadyDancer fragments into the complete workflow:
- [x] Create `WanSteadyDancerWorkflow.cs` implementing `IWorkflowBuilder`
- [x] Define metadata (assets including Lora type, sources with video + image, GUID)
- [x] Implement `GetFragments()` returning UI-visible fragments
- [x] Implement `Build()` composing the full pipeline
- [x] Handle conditional preview concatenation (`append_preview` parameter)
- [x] Add unit tests for workflow

---

### Step 8: Integration Testing & Cleanup
**Complexity:** 3
**Status:** [ ] Not Started

- [ ] Test WanImg2Vid workflow execution in ComfyUI generates videos
- [ ] Test WanSteadyDancer workflow execution in ComfyUI generates videos
- [ ] Verify video output quality and frame rates
- [ ] Verify LoRA application works for Img2Vid
- [ ] Delete all converted `.sbn` files:

**Wan fragment `.sbn` files to delete:**
- `wan/load-model-sage.sbn`
- `wan/load-clip-vae.sbn`
- `wan/lora-loader-model-only.sbn`
- `wan/model-sampling-sd3.sbn`
- `wan/load-image.sbn`
- `wan/clip-vision.sbn`
- `wan/prompts.sbn`
- `wan/painter-i2v.sbn`
- `wan/sampler-advanced.sbn`
- `wan/frame-interpolation.sbn`
- `wan/decode-wan.sbn`
- `wan/load-wan-model.sbn`
- `wan/load-wan-vae.sbn`
- `wan/text-encode-wan.sbn`
- `wan/pose-detection.sbn`
- `wan/steadydancer-embeds.sbn`
- `wan/context-options.sbn`
- `wan/sampler-wan.sbn`
- `wan/i2v-encode.sbn`

**Shared fragment `.sbn` files to delete:**
- `clean-vram.sbn`
- `save-video.sbn`
- `load-video.sbn`
- `get-image-size.sbn`
- `load-image.sbn`
- `resize-image-kj.sbn`
- `load-clip-vision.sbn`
- `concat-preview.sbn`

**Wan template `.sbn` files to delete:**
- `wan/img2vid.sbn`
- `wan/pose2vid-steadydancer.sbn`

**Commit checkpoint:** All SteadyDancer fragments compile and have unit tests

---

## Progress Tracking

| Step | Description | Status | Complexity | Notes |
|------|-------------|--------|------------|-------|
| 1 | Shared Utility Fragments | [x] | 8 | 7 fragments + enum updates, 21 tests |
| 2 | Img2Vid Loader Fragments | [x] | 8 | 4 fragments, 20 tests (incl. integration) |
| 3 | Img2Vid Conditioning Fragments | [x] | 8 | 4 fragments, 23 tests (incl. integration) |
| 4 | Img2Vid Sampling & Output | [x] | 5 | 2 fragments, 16 tests (incl. integration) |
| 5 | WanImg2VidWorkflow | [x] | 8 | Workflow + 28 tests, LoRA logic |
| 6 | SteadyDancer Fragments | [x] | 13 | 10 fragments, 40 tests (incl. integration) |
| 7 | WanSteadyDancerWorkflow | [x] | 8 | Workflow + 31 tests, pose pipeline |
| 8 | Integration Testing & Cleanup | [ ] | 3 | ComfyUI testing, delete `.sbn` |
| **Total** | | **93%** | **61** | **27 fragments + 2 workflows** |

---

## Registry Output Map

### Img2Vid Registry Keys

| Key | Registered By | Node | Index | Description |
|-----|---------------|------|-------|-------------|
| `high_model_output` | LoadModelSage(high) | `high_torch` | 0 | High noise model after Sage+Torch |
| `low_model_output` | LoadModelSage(low) | `low_torch` | 0 | Low noise model after Sage+Torch |
| `high_lora_model_output` | LoraModelOnly(high) | `high_lora_loader_N` | 0 | High model after LoRA (optional) |
| `low_lora_model_output` | LoraModelOnly(low) | `low_lora_loader_N` | 0 | Low model after LoRA (optional) |
| `high_sampled_model_output` | ModelSamplingSD3(high) | `high_model_sampling` | 0 | High model with shift applied |
| `low_sampled_model_output` | ModelSamplingSD3(low) | `low_model_sampling` | 0 | Low model with shift applied |
| `clip_output` | LoadClipVae | `clip_loader` | 0 | CLIP for text encoding |
| `vae_output` | LoadClipVae | `vae_loader` | 0 | VAE for decode |
| `image_output` | WanLoadImage | `image_resize` | 0 | Resized input image |
| `image_width_output` | WanLoadImage | `image_resize` | 1 | Image width |
| `image_height_output` | WanLoadImage | `image_resize` | 2 | Image height |
| `original_image_output` | WanLoadImage | `load_image` | 0 | Original unresized image |
| `clip_vision_output` | ClipVisionEncode | `clip_vision_encode` | 0 | Encoded CLIP vision |
| `positive_output` | WanPrompts | `positive_encode` | 0 | Positive conditioning |
| `negative_output` | WanPrompts | `negative_encode` | 0 | Negative conditioning |
| `painter_positive_output` | PainterI2V | `painter_i2v` | 0 | Video-conditioned positive |
| `painter_negative_output` | PainterI2V | `painter_i2v` | 1 | Video-conditioned negative |
| `painter_latent_output` | PainterI2V | `painter_i2v` | 2 | Video latent |
| `latent_output` | SamplerAdvanced(low) | `sampler_low` | 0 | Final sampled latent |
| `cleaned_latent_output` | CleanVram | `clean_sampler` | 0 | Post-cleanup latent |
| `image_output` | VaeDecode | `vae_decoder` | 0 | Decoded frames (overwrites) |
| `frames_output` | FrameInterpolation | `frame_interpolation` | 0 | Interpolated frames (conditional) |

### SteadyDancer Registry Keys

| Key | Registered By | Node | Index | Description |
|-----|---------------|------|-------|-------------|
| `video_frames` | LoadVideo | `load_video` | 0 | Loaded video frames |
| `frame_count` | LoadVideo | `load_video` | 1 | Frame count |
| `audio` | LoadVideo | `load_video` | 2 | Audio track |
| `image_size_info` | GetImageSize | `get_size` | 0 | Image batch for size |
| `width` | GetImageSize | `get_size` | 1 | Video width |
| `height` | GetImageSize | `get_size` | 2 | Video height |
| `num_frames` | GetImageSize | `get_size` | 3 | Number of frames |
| `image_input` | LoadImage | `load_image` | 0 | Reference image |
| `resized_image` | ResizeImage | `resize_subject` | 0 | Resized reference image |
| `model_output` | LoadWanModel | `set_loras` | 0 | Model after LoRA |
| `vae_output` | LoadWanVae | `vae_loader` | 0 | WanVideo VAE |
| `clip_vision_output` | LoadClipVision | `clip_vision_loader` | 0 | CLIP vision model |
| `text_embeds` | TextEncodeWan | `text_encode` | 0 | Text embeddings |
| `pose_images` | PoseDetection | `resize_pose` | 0 | Detected + drawn poses |
| `image_embeds` | I2VEncode | `i2v_encode` | 0 | Image embeddings |
| `steadydancer_embeds` | SteadyDancerEmbeds | `add_steadydancer` | 0 | Combined dancer embeds |
| `context_options` | ContextOptions | `context_opts` | 0 | Context window settings |
| `latent_output` | SamplerWan | `sampler` | 0 | Sampled latent |
| `image_output` | DecodeWan | `decode` | 0 | Decoded video frames |
| `preview_concat` | ConcatPreview | `concat_final` | 0 | Side-by-side preview (conditional) |

---

## Lora Handling Reference (Img2Vid)

The Img2Vid workflow applies LoRAs from `GenerationParameters.Loras` independently per model.

**Verified:** `Lora.cs` already has `HighPath`, `LowPath`, `HasHighPath`, `HasLowPath`, `IsDualModel` properties. No model changes needed.

```csharp
// In WanImg2VidWorkflow.Build():
if (parameters.Loras?.Count > 0)
{
    var currentHighOutput = "high_model_output";
    var currentLowOutput = "low_model_output";
    
    for (int i = 0; i < parameters.Loras.Count; i++)
    {
        var lora = parameters.Loras[i];
        
        if (!string.IsNullOrEmpty(lora.HighPath))
        {
            _loraModelOnlyFragment.Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
            {
                LoraLoaderId = $"high_lora_loader_{i}",
                LoraName = lora.Name,
                LoraPath = lora.HighPath,
                LoraStrength = lora.Strength,
                ModelInputName = currentHighOutput,
                ModelOutputName = "high_lora_model_output"
            });
            currentHighOutput = "high_lora_model_output";
        }
        
        if (!string.IsNullOrEmpty(lora.LowPath))
        {
            _loraModelOnlyFragment.Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
            {
                LoraLoaderId = $"low_lora_loader_{i}",
                LoraName = lora.Name,
                LoraPath = lora.LowPath,
                LoraStrength = lora.Strength,
                ModelInputName = currentLowOutput,
                ModelOutputName = "low_lora_model_output"
            });
            currentLowOutput = "low_lora_model_output";
        }
    }
}
```

## AssetType Enum Check

The SteadyDancer workflow declares a `SpeedLora` asset with type `Lora`. **Confirmed:** `AssetType` enum in `WorkflowMetadata.cs` does NOT have `Lora`. Must add `Lora` in Step 1.

Current enum values: `CheckpointModel`, `DiffusionModel`, `Vae`, `Clip`, `ClipVision`
