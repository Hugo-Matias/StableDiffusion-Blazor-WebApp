# Phase 2 - Fluent Workflow Fragments And Loader Modes

## Status

**Current Step:** Implemented and validated; ready for user review before Phase 3  
**Parent Plan:** `Documentation/Plans/qwen-character-reference-sheet/MAIN_PLAN.md`  
**Complexity:** 13 points

---

## Scope

This phase adds fluent workflow building blocks for the Qwen Character Reference workflow. It does not add the page UI, run service, ComfyUI submission orchestration, or output persistence service.

---

## Checklist

- [x] Probe the final split diffusion loader node schema before implementation.
- [x] Add/parameterize a Qwen loader abstraction for AIO checkpoint and split stack modes.
- [x] Reuse app LoRA chaining after either loader mode.
- [x] Add Qwen edit-plus-pro conditioning fragment for `TextEncodeQwenImageEditPlusPro_lrzjason`.
- [x] Add shot render fragment/builder that emits the repeated subgraph from a slot definition.
- [x] Preserve Qwen target/crop/upscale defaults from probe.
- [x] Add workflow/fragments tests for emitted node classes, inputs, output indexes, and loader-mode exclusivity.

---

## Probe Notes

Live ComfyUI probe verified these Phase 2 node classes:

- `UNETLoader`: required `unet_name`, `weight_dtype`; output `MODEL` index 0.
- `CheckpointLoaderSimple`: required `ckpt_name`; outputs `MODEL`, `CLIP`, `VAE` at indexes 0, 1, 2. `Base/Qwen-Rapid-AIO-NSFW-v19.safetensors` is present in the checkpoint combo.
- `CLIPLoader`: required `clip_name`, `type`; optional `device`; output `CLIP` index 0. `qwen_2.5_vl_7b_fp8_scaled.safetensors` and `qwen_image` are present.
- `VAELoader`: required `vae_name`; output `VAE` index 0. `qwen_image_vae.safetensors` is present.
- `TextEncodeQwenImageEditPlusPro_lrzjason`: outputs conditioning index 0, latent index 1, image refs indexes 2-6, main-ref conditioning index 7, pad info index 8.
- `KSampler`: `euler` sampler and `simple` scheduler are present.
- `RTXVideoSuperResolution`: input is `images`; quality default is `ULTRA`.
- `easy cleanGpuUsed`: registered with required `anything` input and `*` output.

---

## Implementation Notes

- Keep these builders out of normal `WorkflowService` discovery unless a later phase adds a hidden workflow strategy. The Character run service will compose directly from these building blocks.
- Loader mode should normalize outputs to `model_output`, `clip_output`, and `vae_output`.
- LoRA chaining should use the existing app `LoraLoaderFragment.BuildAll()` after either loader mode.
- Shot output node IDs must be deterministic from slot IDs so Phase 3 can map history outputs back to slots.
- Implemented `QwenCharacterReferenceWorkflowComposer` as a direct composer rather than an `IWorkflowBuilder`, so it will not appear in the Generate-page workflow discovery list.
- Implemented hidden fragments for AIO/split loading, Qwen Plus Pro conditioning, and per-slot shot rendering.
- Output metadata is returned as expected `SaveImage` node ids and slot labels through `CharacterReferenceWorkflowBuildResult`.
- The RTX node preserves the source `scale by multiplier` intent with `resize_type.scale = 2` and `quality = ULTRA`; this matches the dynamic input key found in the source subgraph.
- `easy cleanGpuUsed` is emitted only when the advanced cleanup toggle is enabled and is placed immediately before `SaveImage`.

---

## Validation Plan

- Focused fragment/composer tests under `BlazorWebApp.Tests/Workflows/Fragments/`.
- File diagnostics for new workflow files.
- Focused `dotnet test` filter for new Character workflow tests.

## Validation Results

- `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~QwenCharacterReferenceWorkflowComposerTests --no-restore` passed: 7 tests.
- `dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterStateTests --no-restore` passed: 7 tests.
- File diagnostics for new workflow files and updated Character state reported no errors.
- Existing repo warnings remain, including known package vulnerability warnings for `Magick.NET-Q16-AnyCPU` and nullability/analyzer warnings outside this phase's scope.
