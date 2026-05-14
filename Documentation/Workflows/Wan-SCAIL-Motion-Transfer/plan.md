# Wan SCAIL Video Multi-Character Motion Transfer - Conversion Plan

## Overview

Converts the `SCAIL+Video+Multi-Character+Motion+Transfer+V1.json` ComfyUI workflow into the Blazor WebUI C# fluent builder system. This is a Wan-based video generation workflow that takes both an image (subject reference) and a video (motion source) input, using SCAIL pose embedding techniques for multi-character motion transfer.

**Baseline:** See `workflow.json` in this directory for the original ComfyUI flow.

---

## Phase 1: Workflow Analysis

### Node Inventory (63 nodes total)

#### Loading Group

| Node Type                        | Key Params                       | Purpose                                  |
| -------------------------------- | -------------------------------- | ---------------------------------------- |
| `WanVideoModelLoader`            | SCAIL model, fp16_fast, sageattn | Main diffusion model                     |
| `LoadWanVideoT5TextEncoder`      | umt5-xxl-enc-bf16, bf16          | T5 text encoder                          |
| `WanVideoVAELoader`              | Wan2_1_VAE_bf16                  | Video VAE                                |
| `CLIPVisionLoader`               | clip_vision_h.safetensors        | CLIP vision for ref image                |
| `WanVideoUni3C_ControlnetLoader` | Uni3C controlnet fp16            | SCAIL pose controlnet                    |
| `DownloadAndLoadNLFModel`        | NLF GitHub release               | Non-linear flow model for pose rendering |
| `OnnxDetectionModelLoader`       | vitpose-l-wholebody, yolov10m    | Pose detection models                    |

#### Model Configuration Group

| Node Type              | Key Params                     | Purpose              |
| ---------------------- | ------------------------------ | -------------------- |
| `WanVideoBlockSwap`    | block_swap_threshold=30        | Memory optimization  |
| `WanVideoSetBlockSwap` | chains blockswap to model      | Apply blockswap      |
| `WanVideoLoraSelect`   | speed lora rank128, strength=1 | Speed LoRA selection |
| `WanVideoSetLoRAs`     | chains lora to model           | Apply LoRA           |

#### Input Group

| Node Type                                  | Key Params              | Purpose             |
| ------------------------------------------ | ----------------------- | ------------------- |
| `LoadImage`                                | Reference image         | Subject reference   |
| `VHS_LoadVideo`                            | Source video for motion | Motion source video |
| `LayerUtility: ImageScaleByAspectRatio V2` | scale to fit            | Resize video frames |
| `GetImageSizeAndCount`                     | from video frames       | Extract W/H/frames  |

#### Pose Detection Group

| Node Type                          | Key Params                     | Purpose                        |
| ---------------------------------- | ------------------------------ | ------------------------------ |
| `DWPreprocessor`                   | detect hand/body/face, yolox_l | Detect poses in video frames   |
| `ConvertOpenPoseKeypointsToDWPose` | max_people=10                  | Convert keypoints format       |
| `NLFPredict`                       | from NLF model + DWPoses       | Non-linear flow prediction     |
| `RenderNLFPoses`                   | width/height, draw_face/hands  | Render pose images             |
| `PoseDetectionVitPoseToDWPose`     | ONNX vitpose detection         | Alternative pose for ref image |

#### Conditioning Group

| Node Type                         | Key Params                | Purpose                             |
| --------------------------------- | ------------------------- | ----------------------------------- |
| `WanVideoClipVisionEncode`        | strength=1, center crop   | CLIP vision encode on ref image     |
| `WanVideoTextEncode`              | positive/negative prompts | Text conditioning                   |
| `WanVideoEmptyEmbeds`             | width/height/num_frames   | Base embeds canvas                  |
| `WanVideoAddSCAILReferenceEmbeds` | strength=1, 0-1 range     | Add ref image embeds (CLIP + image) |
| `WanVideoAddSCAILPoseEmbeds`      | strength=1, 0-1 range     | Add pose embeds on top              |
| `WanVideoUni3C_embeds`            | strength=0.7              | Uni3C controlnet conditioning       |

#### Encoding Group

| Node Type        | Key Params          | Purpose                          |
| ---------------- | ------------------- | -------------------------------- |
| `WanVideoEncode` | ref image -> latent | Encode reference image to latent |

#### Sampling Group

| Node Type                  | Key Params                                        | Purpose                      |
| -------------------------- | ------------------------------------------------- | ---------------------------- |
| `WanVideoSchedulerv2`      | dpm++\_sde, steps=6, shift=7                      | Scheduler v2                 |
| `WanVideoSamplerExtraArgs` | guidance=0, comfy format                          | Extra sampler args + context |
| `WanVideoContextOptions`   | uniform_standard, 81 frames, stride=4, overlap=48 | Context windowing            |
| `WanVideoSamplerv2`        | cfg=1, seed=randomize, force_offload=true         | Main sampler v2              |

#### Decode + Output Group

| Node Type               | Key Params              | Purpose                       |
| ----------------------- | ----------------------- | ----------------------------- |
| `WanVideoDecode`        | tile settings           | Decode latent to video frames |
| `ImageConcatMulti` (x2) | concat preview + result | Side-by-side preview          |
| `PDIMAGE_LongerSize`    | resize longer edge      | Final resize                  |
| `VHS_VideoCombine` (x4) | fps=24, save prefix     | Video output                  |

#### Utility Nodes (to be replaced/dropped)

- `SimpleMath+` (x4) - Frame calc and resize math -> C# calculation
- `GetNode`/`SetNode` - Variable passing -> Registry refs
- `Int` primitives - Widget values -> Inline defaults
- `Note` (x5) - Documentation only -> Drop
- `Fast Groups Bypasser (rgthree)` - UI toggle -> Drop
- `CR Prompt Text` - Replace with `PromptsFragment`

### Nodes to Drop

| Node                             | Reason                                                   |
| -------------------------------- | -------------------------------------------------------- |
| `Note` (x5)                      | Documentation only, not executable                       |
| `Fast Groups Bypasser (rgthree)` | UI toggle, not needed in C#                              |
| `GetNode`/`SetNode`              | Variable passing, replaced by registry refs              |
| `SimpleMath+` (x4)               | Frame calc and resize math, calculated in C#             |
| `CR Prompt Text`                 | Replace with PromptsFragment                             |
| 3 of 4 `VHS_VideoCombine`        | Keep only main output; drop pose debug and temp previews |

---

## Phase 2: Fragment Mapping

### Fragment Reuse Table

| Node/Group                      | class_type(s)                                                                                                | Fragment                                          | Status                         |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------ | ------------------------------------------------- | ------------------------------ |
| Wan Model + BlockSwap + LoRA    | `WanVideoModelLoader`, `WanVideoBlockSwap`, `WanVideoSetBlockSwap`, `WanVideoLoraSelect`, `WanVideoSetLoRAs` | Inline in Build()                                 | **New** (SCAIL-specific chain) |
| T5 Encoder                      | `LoadWanVideoT5TextEncoder`                                                                                  | Inline with model load                            | New                            |
| Wan VAE                         | `WanVideoVAELoader`                                                                                          | `LoadWanVaeFragment`                              | **Existing**                   |
| CLIP Vision                     | `CLIPVisionLoader`                                                                                           | `LoadClipVisionFragment`                          | **Existing**                   |
| Uni3C Controlnet                | `WanVideoUni3C_ControlnetLoader`                                                                             | Inline with model load                            | New                            |
| NLF Model                       | `DownloadAndLoadNLFModel`                                                                                    | Inline in Build()                                 | New                            |
| Video Load + Resize             | `VHS_LoadVideo`, `ImageScaleByAspectRatio V2`                                                                | `LoadVideoFragment` + inline                      | **Existing** + New             |
| Get Image Size                  | `GetImageSizeAndCount`                                                                                       | `GetImageSizeFragment`                            | **Existing**                   |
| Ref Image Load + Resize         | `LoadImage`, `ImageResizeKJv2`                                                                               | `LoadImageFragment` + `ResizeImageFragment`       | **Existing**                   |
| DW Pose Detection               | `DWPreprocessor`, `ConvertOpenPoseKeypointsToDWPose`                                                         | `SCAILPoseDetectionFragment`                      | **New** (hidden)               |
| NLF Pose Rendering              | `NLFPredict`, `RenderNLFPoses`                                                                               | `SCAILPoseRenderingFragment`                      | **New** (hidden)               |
| CLIP Vision Encode              | `WanVideoClipVisionEncode`                                                                                   | Inline in embeds chain                            | New                            |
| Text Encode                     | `WanVideoTextEncode`                                                                                         | `TextEncodeWanFragment`                           | **Existing**                   |
| SCAIL Embeds Chain              | `WanVideoEmptyEmbeds`, `AddSCAILReferenceEmbeds`, `AddSCAILPoseEmbeds`, `Uni3C_embeds`, `WanVideoEncode`     | `SCAILEmbedsFragment`                             | **New** (hidden)               |
| Scheduler + ExtraArgs + Context | `WanVideoSchedulerv2`, `WanVideoSamplerExtraArgs`, `WanVideoContextOptions`                                  | `SCAILSamplerFragment` + `ContextOptionsFragment` | **New** + **Existing**         |
| Samplerv2                       | `WanVideoSamplerv2`                                                                                          | Part of `SCAILSamplerFragment`                    | **New**                        |
| Decode                          | `WanVideoDecode`                                                                                             | `DecodeWanFragment`                               | **Existing**                   |
| Concat Preview                  | `ImageConcatMulti`                                                                                           | `ConcatPreviewFragment`                           | **Existing**                   |
| Save Video                      | `VHS_VideoCombine`                                                                                           | `SaveVideoFragment`                               | **Existing**                   |
| Prompts                         | prompt text                                                                                                  | `PromptsFragment`                                 | **Existing**                   |

### UI Component Review

| Fragment          | Parameters Exposed                                                | Component Decision        | Status  |
| ----------------- | ----------------------------------------------------------------- | ------------------------- | ------- |
| `prompts`         | positive_prompt, negative_prompt                                  | `PromptsForm`             | Reuse   |
| `sampler_scail`   | scheduler, steps, shift, cfg, seed, denoise, force_offload        | `SCAILSamplerForm`        | **New** |
| `context_options` | context_schedule, frames, stride, overlap, freenoise, fuse_method | `SCAILContextOptionsForm` | **New** |
| `scail_embeds`    | ref_strength, pose_strength, uni3c_strength, start/end percents   | `SCAILEmbedsForm`         | **New** |
| `pose_detection`  | detect_hand/body/face, max_people, draw_face/hands                | `SCAILPoseDetectionForm`  | **New** |

### UI-Exposed vs Hardcoded Values

| Parameter             | Value in JSON              | Exposed in UI? | Reason                       |
| --------------------- | -------------------------- | -------------- | ---------------------------- |
| scheduler             | dpm++\_sde                 | Yes            | User controls algorithm      |
| steps                 | 6                          | Yes            | User controls quality        |
| shift                 | 7                          | Yes            | Sampler tuning               |
| cfg                   | 1.0                        | Yes            | User controls adherence      |
| seed                  | randomize                  | Yes            | Reproducibility              |
| force_offload         | true                       | Yes            | Memory management            |
| context_schedule      | uniform_standard           | Yes            | Context strategy             |
| context_frames        | 81                         | Yes            | Video length control         |
| context_stride        | 4                          | Yes            | Context overlap tuning       |
| context_overlap       | 48                         | Yes            | Temporal coherence           |
| freenoise             | true                       | Yes            | Noise strategy               |
| fuse_method           | linear                     | Yes            | Fusion strategy              |
| ref_strength          | 1.0                        | Yes            | Reference adherence          |
| pose_strength         | 1.0                        | Yes            | Motion transfer strength     |
| uni3c_strength        | 0.7                        | Yes            | Controlnet strength          |
| detect_hand/body/face | enable                     | Yes            | Pose detection scope         |
| max_people            | 10                         | Yes            | Multi-character support      |
| draw_face/hands       | true                       | Yes            | Pose rendering detail        |
| block_swap_threshold  | 30                         | No             | Infrastructure (hardcoded)   |
| lora_name             | lightx2v_I2V_14B...rank128 | No             | Speed LoRA (asset param)     |
| model_precision       | fp16_fast                  | No             | Infrastructure               |
| offload_device        | offload_device             | No             | Infrastructure               |
| attention             | sageattn                   | No             | Infrastructure               |
| save prefix           | WanVideo_SCAIL             | No             | Always temp folder           |
| frame_rate            | 24                         | Yes            | In video load form           |
| tile_x/y              | 272/272                    | No             | VAE default (hardcoded)      |
| tile_stride_x/y       | 144/128                    | No             | VAE default (hardcoded)      |
| width/height          | From source video          | Auto           | Calculated from input        |
| num_frames            | From source video          | Auto           | Calculated from input        |
| positive_prompt       | User prompt                | Yes            | PromptsFragment              |
| negative_prompt       | Hardcoded Chinese          | No             | Default negative (hardcoded) |

### Enhancements

No additional enhancements. The workflow already includes context windowing, multi-character pose detection, and preview concatenation.

### ModelBase Enum

Uses existing `ModelBase.Wan` - no new enum needed.

### Compatible Resource Base Models

- `"Wan Video 14B i2v 480p"`
- `"Wan Video 14B i2v 720p"`

### Default Values Summary

| Parameter        | Default          |
| ---------------- | ---------------- |
| scheduler        | dpm++\_sde       |
| steps            | 6                |
| shift            | 7                |
| cfg              | 1.0              |
| seed             | -1 (randomize)   |
| context_schedule | uniform_standard |
| context_frames   | 81               |
| context_stride   | 4                |
| context_overlap  | 48               |
| ref_strength     | 1.0              |
| pose_strength    | 1.0              |
| uni3c_strength   | 0.7              |
| fps              | 24               |

---

## Phase 5: Implementation Plan (Pending ComfyUI Node Probe)

### Files to Create

#### New Fragments (`BlazorWebApp/Workflows/Fragments/wan/`)

1. `SCAILPoseDetectionFragment.cs` - DWPreprocessor + ConvertOpenPoseKeypointsToDWPose pipeline (hidden)
2. `SCAILPoseRenderingFragment.cs` - NLFPredict + RenderNLFPoses pipeline (hidden)
3. `SCAILEmbedsFragment.cs` - EmptyEmbeds -> AddSCAILReferenceEmbeds -> AddSCAILPoseEmbeds -> Uni3C_embeds chain (hidden)
4. `SCAILSamplerFragment.cs` - Schedulerv2 + SamplerExtraArgs + Samplerv2 chain (hidden)

#### New UI Components (`BlazorWebApp/Components/Shared/Generation/Fragments/`)

1. `SCAILSamplerForm.razor` - scheduler, steps, shift, cfg, seed, denoise, force_offload
2. `SCAILContextOptionsForm.razor` - context_schedule, frames, stride, overlap, freenoise, fuse_method
3. `SCAILEmbedsForm.razor` - ref_strength, pose_strength, uni3c_strength, start/end percents
4. `SCAILPoseDetectionForm.razor` - detect_hand/body/face, max_people, draw_face/hands

#### New Workflow Class (`BlazorWebApp/Workflows/Templates/Wan/`)

- `WanSCAILEmbedsWorkflow.cs`

### Build Order (in Workflow.Build())

1. Load Video -> Get Image Size
2. Load Reference Image -> Resize
3. Load Wan Model + BlockSwap + LoRA chain
4. Load VAE, CLIP Vision, T5 Encoder, Uni3C Controlnet, NLF Model
5. Pose Detection: DWPreprocessor -> ConvertOpenPoseKeypointsToDWPose
6. Pose Rendering: NLFPredict -> RenderNLFPoses
7. CLIP Vision Encode (ref image)
8. Text Encode
9. SCAIL Embeds Chain: EmptyEmbeds -> AddSCAILReferenceEmbeds -> AddSCAILPoseEmbeds -> Uni3C_embeds
10. WanVideoEncode (ref image to latent)
11. Scheduler v2 + Extra Args + Context Options
12. Samplerv2
13. Decode
14. Concat Preview (conditional)
15. Save Video

---

## ComfyUI Node Probe Results

> **Completed** - All SCAIL-specific nodes confirmed available on the target ComfyUI instance.

### Confirmed Available (17/17 SCAIL nodes)

| Node                               | Inputs                                                           | Outputs                   |
| ---------------------------------- | ---------------------------------------------------------------- | ------------------------- |
| `WanVideoModelLoader`              | model, base_precision, quantization, load_device                 | model                     |
| `LoadWanVideoT5TextEncoder`        | model_name, precision                                            | wan_t5_model              |
| `WanVideoVAELoader`                | model_name                                                       | vae                       |
| `WanVideoUni3C_ControlnetLoader`   | model, base_precision, quantization, load_device, attention_mode | controlnet                |
| `DownloadAndLoadNLFModel`          | url                                                              | nlf_model                 |
| `DWPreprocessor`                   | image                                                            | IMAGE, POSE_KEYPOINT      |
| `ConvertOpenPoseKeypointsToDWPose` | keypoints, max_people                                            | dw_poses                  |
| `NLFPredict`                       | model, images                                                    | pose_results, bboxes      |
| `RenderNLFPoses`                   | nlf_poses, width, height                                         | image, mask               |
| `PoseDetectionVitPoseToDWPose`     | vitpose_model, images                                            | dw_poses                  |
| `WanVideoEmptyEmbeds`              | width, height, num_frames                                        | image_embeds              |
| `WanVideoAddSCAILReferenceEmbeds`  | embeds, vae, ref_image, strength, start_percent, end_percent     | image_embeds              |
| `WanVideoAddSCAILPoseEmbeds`       | embeds, vae, pose_images, strength, start_percent, end_percent   | image_embeds              |
| `WanVideoUni3C_embeds`             | controlnet, strength, start_percent, end_percent                 | uni3c_embeds              |
| `WanVideoSchedulerv2`              | scheduler, steps, shift, start_step, end_step                    | scheduler                 |
| `WanVideoSamplerExtraArgs`         | (accepts optional inputs)                                        | extra_args                |
| `WanVideoSamplerv2`                | model, image_embeds, cfg, seed, force_offload, scheduler         | samples, denoised_samples |

### Confirmed Available - Reused Wan Nodes

All existing Wan nodes (`WanVideoScheduler`, `WanVideoSamplerSettings`, `WanVideoSamplerFromSettings`, `WanVideoTextEncode`, `WanVideoClipVisionEncode`, `WanVideoContextOptions`, `WanVideoDecode`, `WanVideoEncode`, `GetImageSizeAndCount`, `OnnxDetectionModelLoader`, `PoseAndFaceDetection`, `DrawViTPose`) are also confirmed available.

### Missing Nodes - Current Status

| Node                                       | Replacement Strategy                                                                 |
| ------------------------------------------ | ------------------------------------------------------------------------------------ |
| `LayerUtility: ImageScaleByAspectRatio V2` | Installed and restored in the app workflow for upstream video resize parity.         |
| `PDIMAGE_LongerSize`                       | Drop - not needed for core output. If resize is needed, use standard resize fragment |

### Implementation Impact

- The SCAIL/WanVideoWrapper core nodes are available for the intended source flow.
- `LayerUtility: ImageScaleByAspectRatio V2` is now available in the target install and should be used for the source video resize path.
- The pack that provides `PDIMAGE_LongerSize` is still missing or intentionally not installed; remove this final resize branch for initial testing.
- Omitting `PDIMAGE_LongerSize` remains acceptable for an initial test because it is a preview/post-processing convenience, not the SCAIL conditioning or sampling core.

---

## Integration Check - 2026-05-08

Ground truth: `workflow.json` in this directory. The implementation was checked against the ordered node flow, widget defaults, and node links from that file.

### Source Flow That Must Be Preserved

1. `VHS_LoadVideo` -> `LayerUtility: ImageScaleByAspectRatio V2` or replacement resize -> `GetImageSizeAndCount` / resized frame dimensions.
2. `LoadImage` -> `ImageResizeKJv2`, sized from the resized video frames.
3. `WanVideoModelLoader` -> `WanVideoBlockSwap` -> `WanVideoSetBlockSwap` -> `WanVideoLoraSelect` -> `WanVideoSetLoRAs`.
4. `LoadWanVideoT5TextEncoder`, `WanVideoVAELoader`, `CLIPVisionLoader`, `WanVideoUni3C_ControlnetLoader`, and `DownloadAndLoadNLFModel`.
5. `DWPreprocessor` -> `ConvertOpenPoseKeypointsToDWPose` for motion video frames.
6. `OnnxDetectionModelLoader` -> `PoseDetectionVitPoseToDWPose` for the resized reference image.
7. `NLFPredict` -> `RenderNLFPoses`, using half-size pose-render dimensions from the resized video frame dimensions.
8. `WanVideoClipVisionEncode` and `WanVideoTextEncode`.
9. `WanVideoEmptyEmbeds` -> `WanVideoAddSCAILReferenceEmbeds` -> `WanVideoAddSCAILPoseEmbeds`.
10. `WanVideoEncode` -> `WanVideoUni3C_embeds` -> `WanVideoSamplerExtraArgs`.
11. `WanVideoSchedulerv2` + `WanVideoContextOptions` + `WanVideoSamplerExtraArgs` -> `WanVideoSamplerv2` -> `WanVideoDecode` -> `VHS_VideoCombine`.

### Current Implementation Concerns

| Area                       | Concern                                                                                                                                                                                                                                                                                                                                                                                                                   | Impact                                                                                  |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Video input path           | `WanSCAILEmbedsWorkflow` declares `motion_video`, but does not build `VHS_LoadVideo` or a replacement video resize path. `SCAILPoseDetectionFragment` expects `video_frames`.                                                                                                                                                                                                                                             | Build-time registry failure before pose detection.                                      |
| Reference image source     | `WanLoadImageFragment` reads `source_image`, while this workflow declares `ref_image`; the workflow then registers `ref_image` to `load_ref_image`, a node id that is not created by the fragment.                                                                                                                                                                                                                        | Reference image path can be empty and downstream refs can point to a nonexistent node.  |
| Registry aliases           | Downstream SCAIL fragments consume `vae`, `clip_embeds`, `width`, `height`, and `num_frames`, but current reused fragments register names such as `vae_output`, `clip_vision_output`, `image_width_output`, and `image_height_output`.                                                                                                                                                                                    | Build-time registry failures or invalid references.                                     |
| Model loader chain         | Source uses `WanVideoModelLoader`, `WanVideoBlockSwap`, `WanVideoSetBlockSwap`, `WanVideoLoraSelect`, and `WanVideoSetLoRAs`. Current code uses `UNETLoader`, `PathchSageAttentionKJ`, `ModelPatchTorchSettings`, `ModelSamplingSD3`, and generic `LoraLoader`.                                                                                                                                                           | Node types and data types do not match the source WanVideoWrapper flow.                 |
| CLIP/T5/VAE loaders        | Source uses `LoadWanVideoT5TextEncoder` and `WanVideoVAELoader`; current code uses generic `CLIPLoader` and `VAELoader`.                                                                                                                                                                                                                                                                                                  | `WanVideoTextEncode` and Wan VAE consumers may receive incompatible model types.        |
| CLIP vision encode         | Source uses `WanVideoClipVisionEncode`; current code uses generic `CLIPVisionEncode` and does not register `clip_embeds`.                                                                                                                                                                                                                                                                                                 | `WanVideoAddSCAILReferenceEmbeds` will not receive the expected CLIP embeds input.      |
| Pose detection defaults    | Source `DWPreprocessor` defaults are combo values `enable`, resolution `512`, `bbox_detector=yolox_l.torchscript.pt`, `pose_estimator=dw-ll_ucoco_384_bs5.torchscript.pt`. Current code sends booleans and `dwpose_model=yolox_l.onnx`. Source `OnnxDetectionModelLoader` uses `vitpose-l-wholebody.onnx`, `yolov10m.onnx`, and `CUDAExecutionProvider`; current default uses `vitpose-l-wholebody.pth` and omits device. | Input names/defaults may not match the probed schema or source workflow.                |
| Pose render defaults       | Source `DownloadAndLoadNLFModel` uses `https://github.com/isarandi/nlf/releases/download/v0.3.2/nlf_l_multi_0.3.2.torchscript`; current default is empty. Source render width/height are half of the resized video dimensions.                                                                                                                                                                                            | NLF load/render branch can fail or render at wrong dimensions.                          |
| Embeds/Uni3C flow          | Source feeds `WanVideoAddSCAILPoseEmbeds.image_embeds` to `WanVideoSamplerv2.image_embeds`. `WanVideoUni3C_embeds` receives `render_latent` from `WanVideoEncode`, then feeds `uni3c_embeds` into `WanVideoSamplerExtraArgs`. Current code registers `image_embeds` from `WanVideoUni3C_embeds` and passes pose images via an `images` input.                                                                             | Main conditioning flow is wired differently from source and likely uses invalid inputs. |
| Sampler extra args/context | Source connects `WanVideoContextOptions` into `WanVideoSamplerExtraArgs.context_options`, and `WanVideoSamplerExtraArgs` into `WanVideoSamplerv2.extra_args`. Current code builds context options but does not connect them to extra args, and sends non-source inputs such as `free_noise`/`rope_theta` directly to `WanVideoSamplerExtraArgs`.                                                                          | Context windowing and Uni3C controlnet conditioning are not actually integrated.        |
| Decode/save defaults       | Source decode defaults are `enable_vae_tiling=false`, `tile_x=272`, `tile_y=272`, `tile_stride_x=144`, `tile_stride_y=128`, `normalization=default`; current defaults use 256 tile sizes and omit strides/normalization. Source FPS is derived from the `fps` variable, default `24`; current save default falls back to `30`.                                                                                            | Output timing and VAE decode behavior differ from the source flow.                      |
| UI exposure                | `SCAILSamplerForm`, `SCAILEmbedsForm`, and `SCAILPoseDetectionForm` exist, but the corresponding fragment metadata does not set `Component`. Several checkboxes bind local values without `@bind-Value:after`, so changes such as force-offload, freenoise, and detect hand/body/face are not persisted.                                                                                                                  | SCAIL variables are not reliably exposed or saved in the Generate UI.                   |
| Tests                      | No SCAIL-specific workflow or fragment tests exist under `BlazorWebApp.Tests/Workflows/`.                                                                                                                                                                                                                                                                                                                                 | The broken registry/flow issues are not currently caught by automated validation.       |

### Initial Test Scope

Proceeding without `PDIMAGE_LongerSize` is acceptable for the first test. `LayerUtility: ImageScaleByAspectRatio V2` has been restored for the source video resize path so the workflow now uses upstream sizing behavior before reference-image resize, pose detection, NLF prediction, and final video output.

Runtime follow-up: `RenderNLFPoses` treats `dw_poses` and `ref_dw_pose` as optional inputs, but the SCAIL-Pose implementation can crash when DW hand arrays are empty or contain only one hand (`IndexError` in `shift_dwpose_according_to_nlf`). The app workflow now leaves DWPose alignment inputs disconnected by default and keeps upstream-style DW alignment as an explicit opt-in parity mode. This is a deliberate stability-first deviation from the source graph for initial testing.

Sampler/runtime follow-up: later runs reached sampling but failed inside `sageattention` while Uni3C ControlNet was active. The traceback enters `WanVideoWrapper/uni3c/controlnet.py` and then `sageattention/quant.py`, so this is no longer the SCAIL-Pose hand-alignment failure. The 301 source frames and 76 latent frames are expected Wan temporal compression, not by themselves proof of a mismatched graph. The workflow still derives `width`, `height`, and `num_frames` through `GetImageSizeAndCount` after `LayerUtility: ImageScaleByAspectRatio V2`, keeps `sageattn` as the default attention mode, disables Uni3C embed offload by default to avoid storage-less tensor handoff, and exposes frame cap/stride plus Uni3C attention/offload controls for focused runtime isolation.
