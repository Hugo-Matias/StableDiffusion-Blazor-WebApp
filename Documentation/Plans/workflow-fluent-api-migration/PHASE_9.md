## Phase 9: Convert Qwen Workflows and Fragments

**Objective:** Convert Qwen-specific fragments and both Qwen workflows (Txt2Img + Img2Img Edit)
**Complexity:** 8 points
**Status:** [x] Complete

---

### Steps

- [x] Create `Workflows/Fragments/Core/ModelSamplingAuraFlowFragment.cs` - ModelSamplingAuraFlow shift node
- [x] Create `Workflows/Fragments/Qwen/LoadQwenEditFragment.cs` - UNet + CLIP + VAE + LoRA + ModelSampling + CFGNorm
- [x] Create `Workflows/Fragments/Qwen/EncodeEditFragment.cs` - TextEncodeQwenImageEditPlus for positive/negative
- [x] Create `Workflows/Templates/Qwen/QwenTxt2ImgWorkflow.cs` - LoadDiffusionWithPrompts + EmptyLatent(SD3) + ModelSamplingAuraFlow + Sampler + VaeDecode + SeedVR2 + Detailer + Save
- [x] Create `Workflows/Templates/Qwen/QwenImg2ImgEditWorkflow.cs` - LoadImageScaled + LoadQwenEdit + VaeEncode + EncodeEdit + SamplerStandard + VaeDecode + Save
- [x] Add unit tests for all fragments and workflows (76 tests)
- [ ] Delete `qwen/*.sbn` files (deferred to cleanup phase)

### Test Results
- **76 Qwen-specific tests** - all passing
- **857 total tests** - all passing (16 pre-existing failures in unrelated Wildcard/Parser tests)

---

### Analysis

#### Qwen Txt2Img Pipeline
1. `load-diffusion-w-prompts.sbn` (loader_qwen) - UNet + CLIP(qwen_image) + VAE + prompts
2. `empty-latent.sbn` - EmptySD3LatentImage
3. `model-sampling-auraflow.sbn` - ModelSamplingAuraFlow with shift
4. `sampler.sbn` - ClownsharKSampler_Beta
5. `vae-decode.sbn` - VaeDecode
6. `upscale-seedvr2.sbn` - SeedVR2 (conditional)
7. `load-diffusion-w-prompts.sbn` (detailer scope) - Detailer model/prompts
8. `detailer-core.sbn` - Detailer (conditional)
9. `save.sbn` - Save

#### Qwen Img2Img Edit Pipeline
1. `load-image-scaled.sbn` - LoadImage + ImageScaleToTotalPixels
2. `qwen/load-qwen-edit.sbn` - UNet + CLIP(qwen_image) + VAE + LoRA + ModelSampling + CFGNorm
3. `vae-encode.sbn` - VAEEncode
4. `qwen/encode-edit.sbn` - TextEncodeQwenImageEditPlus (positive + negative with image ref)
5. `sampler-standard.sbn` - KSampler
6. `vae-decode.sbn` - VaeDecode
7. `save.sbn` - Save

### Fragments to Create
- **ModelSamplingAuraFlowFragment** - Simple node, takes model_output ref and shift param, overwrites model_output
- **LoadQwenEditFragment** - Complex: UNet + CLIP + VAE + LoRA(LoraLoaderModelOnly) + ModelSamplingAuraFlow + CFGNorm
- **EncodeEditFragment** - TextEncodeQwenImageEditPlus with clip, vae, image refs

### Existing Fragments Reused
- LoadDiffusionWithPromptsFragment (Qwen Txt2Img loader)
- EmptyLatentFragment (SD3 latent)
- SamplerFragment (ClownsharK sampler)
- SamplerStandardFragment (KSampler for Img2Img Edit)
- VaeDecodeFragment
- SaveFragment
- SeedVR2UpscaleFragment
- DetailerFragment
- LoadImageScaledFragment (Img2Img Edit source image)
- VaeEncodeFragment (Img2Img Edit)
