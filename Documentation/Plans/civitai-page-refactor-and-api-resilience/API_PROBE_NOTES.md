# CivitAI API Probe Notes

## Purpose

Record sanitized response-shape findings for endpoints used by the CivitAI browser. These notes intentionally avoid raw prompt text and only document structural fields needed by DTO and service code.

## Probe Harness

Use `Probe-CivitaiApi.ps1` from this folder. The script summarizes endpoint status, top-level keys, image item keys, metadata wrapper keys, and nested generation metadata keys without printing prompt values.

Example:

```powershell
& .\Documentation\Plans\civitai-page-refactor-and-api-resilience\Probe-CivitaiApi.ps1 -ModelId 958009
```

## Endpoints Covered

- `v1/models/{modelId}`
- `v1/images?modelId={modelId}&limit=20`
- `v1/images?imageId={imageId}` when an image id can be sampled from model images
- `v1/images?modelVersionId={versionId}&limit=20` when a version id can be sampled from the model

## Current Shape Findings

Probe model: `958009`.

### `v1/models/{modelId}`

- Top-level model payload includes `modelVersions` but no top-level `metadata` object.
- Version image stubs include fields such as `url`, `type`, `width`, `height`, `hash`, `nsfwLevel`, `hasMeta`, and `hasPositivePrompt`.
- Version image stubs do not include full generation metadata.

### `v1/images?modelId={modelId}&limit=20`

- Response shape is `{ items, metadata }`.
- `metadata` can include `nextCursor` and `nextPage`.
- Image items include `id`, `url`, `type`, dimensions, `hash`, `stats`, `modelVersionIds`, and `meta`.
- The first sampled item had wrapper metadata with keys `id` and `meta`, where nested `meta` was null.
- A later sampled item in the same result page had wrapper metadata with keys `id` and `meta`, where nested `meta` was an object.
- Nested generation metadata keys observed included `prompt`, `seed`, `steps`, `cfgScale`, `sampler`, `scheduler`, `denoise`, `height`, `width`, `Model`, `models`, `vaes`, and `comfy`.

### `v1/images?imageId={imageId}`

- Response shape is `{ items, metadata }`.
- For the sampled image id, `metadata` was null.
- The sampled item had wrapper metadata with keys `id` and `meta`, where nested `meta` was null.

### `v1/images?modelVersionId={versionId}&limit=20`

- Response shape is `{ items, metadata }`.
- `metadata` can include `nextCursor` and `nextPage`.
- The sampled item used flat generation metadata directly under `meta`, not a wrapper object.
- Flat metadata keys observed included `prompt`, `seed`, `steps`, `cfgScale`, `sampler`, `resources`, `Model`, `Model hash`, `Denoising strength`, `Hires steps`, `Hires upscale`, and `Hires upscaler`.

## Parser Requirements From Known Shape

- Accept a raw `meta` JSON value that is `null`.
- Accept legacy flat metadata where fields such as `prompt`, `negativePrompt`, `seed`, `steps`, `sampler`, and `cfgScale` are directly under `meta`.
- Accept current wrapper metadata where the raw `meta` object contains wrapper fields such as `id` and a nested `meta` object with generation fields.
- Treat nested `meta: null` as metadata unavailable.
- Parse numeric values from either JSON numbers or strings where practical.
- Preserve resource arrays when available and ignore malformed resource entries.
- Recognize current keys such as `scheduler`, `models`, `vaes`, and `comfy` without throwing if the app cannot map every value yet.
