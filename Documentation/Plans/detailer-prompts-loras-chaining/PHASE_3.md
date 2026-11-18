# Phase 3 - Workflow Integration

## Status
**Phase:** 3
**Build Status:** (pending)

## Objective
- Ensure every workflow runs `LoraLoaderFragment.BuildAll(...)` for the main scope (where not already done).
- Every detailer-capable workflow runs `LoraLoaderFragment.BuildAll(..., parameters.DetailerLoras, scope: "detailer_")` after the detailer-scoped loader and before `_detailerFragment.Build(...)`.
- Normalize the `detailer_prompt` / `detailer_negative_prompt` fallback-to-main pattern.

## Workflows audited

Detailer-capable:
- SDTxt2ImgWorkflow
- AnimaTxt2ImgWorkflow
- ChromaTxt2ImgWorkflow
- ErnieTxt2ImgWorkflow
- FluxTxt2ImgWorkflow
- Flux2KleinTxt2ImgWorkflow
- QwenTxt2ImgWorkflow
- ZImageTxt2ImgWorkflow
- ZImageImg2ImgWorkflow

## Conventions
- SD workflow's `LoadCheckpointFragment` uses `PCLazyLoraLoader` which parses prompt-embedded `<lora:...>` syntax. Adding `LoraLoaderFragment.BuildAll` AFTER that is safe: explicit `LoraLoader` nodes chain via `model_output`/`clip_output` registry keys, so both approaches compose correctly.
- Always call `BuildAll` even if the list is empty; internally it no-ops when `loras == null || loras.Count == 0`.
- The scope parameter is mandatory when working with the detailer sub-pipeline.

## Issues / Decisions

(filled after completion)
