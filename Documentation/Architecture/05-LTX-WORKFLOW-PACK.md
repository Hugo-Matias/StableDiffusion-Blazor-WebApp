# LTX 2.3 Workflow Pack - Architecture Notes

This document covers the LTX 2.3 video pipeline as it exists in the codebase. It complements the workflow-pack plan under `Documentation/Plans/Workflows/LTX-2.3-Workflows/` and `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`. For fragment-level details and the standard build order, read the `LTX Fragment Reuse` section of `TEMPLATE_GUIDE.md`.

## Scope

LTX 2.3 is a video diffusion model with a coupled audio path. The pack covers seven workflow shapes that all share the same five-asset loader and the same two-pass sampler / decode chain:

| Workflow                    | Mode                              | Source slots                      | Audio path                                            | Notes                                                    |
| --------------------------- | --------------------------------- | --------------------------------- | ----------------------------------------------------- | -------------------------------------------------------- |
| `LtxTxt2VidWorkflow`        | Txt2Vid                           | none                              | empty audio latent                                    | Phase 0/1 baseline.                                      |
| `LtxImg2VidWorkflow`        | Img2Vid                           | `source_image`                    | empty audio latent                                    | Phase 1. Single-checkpoint loader.                       |
| `LtxFml2VidWorkflow`        | Img2Vid (first/middle/last frame) | `first_image`, `last_image`       | empty audio latent                                    | Phase 2.                                                 |
| `LtxV2vJustTalkWorkflow`    | Vid2Vid                           | `source_video`, `audio_track`     | `LtxLoadAudioFragment` -> `LtxAudioVaeEncodeFragment` | Phase 5. AV-aware loader + face-mask + last-frame guide. |
| `LtxControlVid2VidWorkflow` | Vid2Vid                           | `motion_video`                    | empty audio latent (no audio)                         | Phase 4. IC-LoRA + DWPose control.                       |
| `LtxTalkingAvatarWorkflow`  | Img2Vid                           | `source_image`, `reference_audio` | `LtxOmniVoiceFragment` -> `LtxAudioVaeEncodeFragment` | Phase 6. AV-aware loader + I2V Inplace + OmniVoice TTS.  |

(Phase 3 and Phase 7 are scheduler / documentation work and do not add new templates.)

## Loader policy

LTX has three loader variants. They all register the same five outputs (`{scope}model_output`, `{scope}clip_output`, `{scope}vae_output`, `{scope}audio_vae_output`, `{scope}upscale_model_output`) so any downstream fragment can be wired against any loader.

- **`LtxLoadModelFragment` (single checkpoint).** Used when the upstream JSON ships a `CheckpointLoaderSimple` node. Maps to the Comfy-default I2V path (`LtxImg2VidWorkflow`). Requires the `Model` asset only for the diffusion / clip / vae triple, plus `AudioVae` and `UpscaleModel`.
- **`LtxLoadSplitFragment` (UNET + DualCLIP + VAE).** The Kijai-split layout used by the rest of the pack. Adds `UNETLoader`, `DualCLIPLoader`, `VAELoader`. Requires `UNet`, `Clip`, `Vae`, `AudioVae`, `UpscaleModel` assets.
- **`LtxLoadSplitAvFragment` (UNET + LTXAV CLIP + VAE).** Identical to `LtxLoadSplitFragment` except the CLIP slot uses `LTXAVTextEncoderLoader`. Required by any workflow whose audio conditioning is fed through the text encoder (V2V Just-Talk and Talking Avatar).

All three loaders apply the always-on `LTXVChunkFeedForward` and `LTX2SamplingPreviewOverride` model patches. Optional patches (`NAG_Patcher`, `SageAttentionPatcher`, Mel separation) are surfaced via header-only enhancement fragments and applied conditionally by the workflow when `IsActive` is true.

## Sampler / scheduler

LTX ships its own scheduler (`LTXVScheduler`) which produces a sigmas list rather than a fixed schedule.

- `LtxSchedulerFragment` supports two modes: `auto` (drives `LTXVScheduler` with `steps`, `max_shift`, `base_shift`, `stretch`, `terminal`) and `manual` (a comma-separated sigma list). Both register `{scope}sigmas_output`.
- `LtxSamplingPassFragment` is a wrapper around `KSamplerSelect` + `RandomNoise` + `BasicGuider` + `SamplerCustomAdvanced`. Set `UseRegistrySigmas = true` to read sigmas from `{SigmasInputName}` (default `sigmas_output`). For the second pass set `SigmasInputName = "pass2_sigmas_output"` after building the scheduler with `scope: "pass2_"`.

Two-pass sampling (low-res generate -> upscale -> high-res refine) is gated by `LtxRefinementPassEnhancementFragment.IsActive`. Workflows are responsible for the upscale -> crop -> concat -> scheduler -> sampler chain when active. The first pass uses `ltx_positive_output` / `ltx_negative_output`; the second pass uses `ltx_cropped_positive_output` / `ltx_cropped_negative_output` after `LtxConditioningFragment.BuildCropped` runs.

## AV (audio + video) data flow

The LTX 2.3 audio pipeline runs a parallel latent through `LTXVConcatAVLatent` so the sampler sees a single combined latent.

```
video_latent     --\
                    LTXVConcatAVLatent -> av_latent_output -> SamplerCustomAdvanced -> LTXVSeparateAVLatent -> VAEDecode + LTXVAudioVAEDecode
audio_latent     --/
```

Audio sources:

- Empty audio latent (`LTXVEmptyLatentAudio`) for txt2vid / img2vid without a real audio track. Emitted by `LtxEmptyLatentFragment` unless `SkipAudio = true`.
- Real user audio: `LtxLoadAudioFragment` (registers `{scope}audio_input`) -> `LtxAudioVaeEncodeFragment` (encodes + applies a zero `SetLatentNoiseMask` so the sampler treats it as fixed conditioning, registers `{scope}audio_latent`).
- Voice-clone TTS: `LtxOmniVoiceFragment` (`OmniVoiceWhisperLoader` + `OmniVoiceVoiceCloneTTS` + `TrimAudioDuration`, registers `{scope}audio_input`) -> same `LtxAudioVaeEncodeFragment`.

Whenever a real audio source is present, the workflow should build `LtxEmptyLatentFragment` with `SkipAudio = true` to avoid emitting an unused empty audio latent that would still consume the audio VAE slot.

## V2V data flow

V2V workflows replace the empty video latent with a `VAEEncode` over the source frames:

```
VHS_LoadVideoFFmpeg -> ResizeImagesByLongerEdge -> GetImageSizeAndCount + GetImageRangeFromBatch (first / last frame split)
                                                |
                                                v
                                      LtxVaeEncodeVideoFragment -> video_latent
                                      LtxVaeEncodeVideoFragment (last frame) -> last_..._latent
```

Lip-sync (`LtxV2vJustTalkWorkflow`) layers four extra fragments on top:

1. `LtxAddLatentGuideFragment` injects the encoded last-frame latent at frame index `-1` with strength 0.7.
2. `LtxFaceMaskFragment` builds a `FaceSegment` -> `BlockifyMask` -> `LTXVPreprocessMasks` -> `LTXVSetVideoLatentNoiseMasks` chain so only the face region is re-sampled.
3. `LtxAudioVideoMaskFragment` time-ranges the audio + video masks so audio drives only the face region.
4. The standard `LtxConcatAVLatentFragment` -> `LtxSchedulerFragment` -> `LtxSamplingPassFragment` chain runs over the masked latent.

## Talking-avatar data flow

`LtxTalkingAvatarWorkflow` reuses the I2V Inplace branch instead of the V2V branch and replaces `LtxLoadAudioFragment` with `LtxOmniVoiceFragment`:

```
LtxLoadImageFragment -> LtxImgToVideoFragment (LTXVImgToVideoInplace) -> video_latent + conditioning
LtxOmniVoiceFragment -> audio_input -> LtxAudioVaeEncodeFragment -> audio_latent
                              \\__ reads parameters.Sources["reference_audio"] for the voice sample
                              \\__ reads parameters.GetFragment("prompts").positive as the spoken script (v1)
```

For v1 the spoken text is read from `prompts.positive`. A dedicated `LtxOmniVoiceForm` Razor component with a separate TTS textbox is intentionally deferred. The OmniVoice node defaults follow the upstream JSON widget tuple (`whisper-small (auto-download)` + `fp16` for the Whisper loader, `OmniVoice-bf16 (auto download)` + `seed = 0` random + `keep_model_loaded = false` for the voice-clone node).

## Mode + asset conventions

- `ModeType.Vid2Vid` was added during Phase 5. Other LTX workflows reuse the existing `Txt2Vid` and `Img2Vid` modes.
- All split-loader workflows declare five assets: `UNet` (DiffusionModel), `Clip`, `Vae`, `AudioVae`, `UpscaleModel`. The single-checkpoint workflow declares `Model` + `TextEncoder`.
- All workflows declare `CompatibleResourceBaseModels = ["LTXV2", "LTXV 2.3"]` so the resource browser filters LoRAs / text encoders correctly.

## Custom node packs required

LTX 2.3 workflows depend on the following ComfyUI custom node packs in addition to comfy core:

| Pack                                                              | Used by                                                        |
| ----------------------------------------------------------------- | -------------------------------------------------------------- |
| `Lightricks/ComfyUI-LTXVideo`                                     | All LTX workflows.                                             |
| `Kosinkadink/ComfyUI-VideoHelperSuite` (VHS)                      | All video-output and `LtxLoadVideoFragment`.                   |
| `nvideo-rgthree/ComfyUI-Easy-Use` (or similar `FaceSegment` pack) | `LtxFaceMaskFragment` (Phase 5).                               |
| `Saganaki22/ComfyUI-OmniVoice-TTS`                                | `LtxOmniVoiceFragment` / `LtxTalkingAvatarWorkflow` (Phase 6). |
| DWPose preprocessor (`comfyui_controlnet_aux`)                    | `LtxControlPreprocessorFragment` (Phase 4).                    |

If a node pack is missing the workflow validation raises a `Node not found` error at submit time. Document required assets in each phase document so users can install them ahead of time.
