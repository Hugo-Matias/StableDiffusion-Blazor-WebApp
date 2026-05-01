# Workflow Template System - Complete Guide

This document provides comprehensive documentation for creating and converting ComfyUI workflows into the modular C# fluent builder system.

For workflow-specific UI integration standards, component reuse rules, and field-to-control mappings, also read `WORKFLOW_UI_CONVERSION_GUIDE.md` and `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Core Conventions](#core-conventions)
3. [Workflow Class Structure](#workflow-class-structure)
4. [Assets System](#assets-system)
5. [Fragment Class Structure](#fragment-class-structure)
6. [Scope System](#scope-system)
7. [Pipeline Data Flow](#pipeline-data-flow)
8. [Conditional Logic](#conditional-logic)
9. [Fragment Design Principles](#fragment-design-principles)
10. [Converting Raw Workflows](#converting-raw-workflows)
11. [LTX Fragment Reuse](#ltx-fragment-reuse)
12. [Examples](#examples)
13. [Troubleshooting](#troubleshooting)

---

## System Overview

The workflow template system uses a **Fluent Builder API** where:

1. **Workflow classes** (C# in `Workflows/Templates/`) implement `IWorkflowBuilder` and define the overall workflow structure
2. **Fragment classes** (C# in `Workflows/Fragments/`) implement `IFragmentBuilder` and are reusable node groups
3. **ComfyWorkflowBuilder** provides a fluent API for constructing nodes
4. **NodeRegistry** manages output references between fragments
5. **Scope System** enables multiple model sets with isolated namespaces
6. **Assets** allow dynamic model selection via UI dropdowns

### Key Design Goals

| Goal                      | Description                                                           |
| ------------------------- | --------------------------------------------------------------------- |
| **Modularity**            | Fragments can be reused across multiple workflows and architectures   |
| **Flexibility**           | Any fragment can be instantiated multiple times with different scopes |
| **Architecture-Agnostic** | Features like Detailer/Upscale work with any model base               |
| **Streamlined**           | Single `scope` parameter controls namespace isolation                 |
| **Type Safety**           | Compile-time validation, IntelliSense, and strongly-typed parameters  |
| **Testability**           | Unit tests for every fragment and workflow                            |

### Architecture

```
Workflow Classes (C# IWorkflowBuilder)
    |
    v
ComfyWorkflowBuilder (fluent API)
    |
    v
Fragment Classes (C# IFragmentBuilder)
    |
    v
NodeBuilder (fluent node construction)
    |
    v
NodeRegistry (output reference tracking)
    |
    v
ComfyUI Workflow JSON
```

---

## Core Conventions

### 1. Workflow Description (Required)

Every workflow's `WorkflowMetadata` MUST set a `Description`. The description is surfaced through `IInfoService`
in the right-side Info drawer (`InfoDrawer.razor`) - **do not** add a banner, alert, or other on-page hint
above prompts. The drawer is the single, dismissable surface for workflow guidance and keeps the page chrome
quiet.

Guidelines:

- 1-3 sentences in plain language.
- Lead with what the workflow does, not which nodes it wires.
- Mention required inputs only when they're not obvious from the workflow name (e.g. "expects a clean
  front-facing portrait", "needs a separate audio track").
- End with the "when to pick this" cue if the workflow overlaps with siblings (e.g. "use this for quick
  image-to-video tests before moving to specialized workflows").

The description is rendered as the drawer's `Overview`. Source-slot labels are auto-published to a
`Required Inputs` section by the Generate page, so don't repeat them in the description text.

### 2. Output Naming Convention

All outputs follow the pattern: `{scope}{type}_output`

| Output Name       | Description           |
| ----------------- | --------------------- |
| `model_output`    | Model/UNet output     |
| `clip_output`     | CLIP encoder output   |
| `vae_output`      | VAE output            |
| `latent_output`   | Latent image output   |
| `positive_output` | Positive conditioning |
| `negative_output` | Negative conditioning |
| `image_output`    | Decoded image output  |

### 3. Scope System

The `scope` parameter controls namespace isolation:

- **Loader fragments**: `scope` controls where outputs are **written**
- **Processing fragments**: `scope` controls where model inputs are **read from**
- **Pipeline outputs** (`latent_output`, `image_output`): Always written to main (no scope)

```csharp
// Main pipeline - no scope (writes to model_output, clip_output, etc.)
_loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters { ... });

// Detailer scope - writes to detailer_model_output, detailer_clip_output, etc.
_loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters { ... },
    scope: "detailer_", scopeTitle: "Detailer ");

// Detailer reads from detailer_ scope, writes image_output to main
_detailerFragment.Build(builder, registry, new DetailerFragment.Parameters { Scope = "detailer_", ... });
```

### 4. Pipeline Flow Pattern

**Critical concept:** Processing fragments overwrite main pipeline outputs.

```
LoadDiffusion (no scope)     -> model_output, clip_output, vae_output
LoraLoader (no scope)        -> overwrites model_output, clip_output
EmptyLatent (no scope)       -> latent_output
Prompts (no scope)           -> positive_output, negative_output
Sampler (no scope)           -> overwrites latent_output
VaeDecode (no scope)         -> image_output

LoadDiffusionWithPrompts (scope: detailer_) -> detailer_model_output, etc.
Detailer (scope: detailer_)  -> reads detailer_*, reads image_output, overwrites image_output

Save (no scope)              -> reads image_output (always exists)
```

**Why this works:**

- Conditional fragments (detailer, upscale) overwrite `image_output` when active
- When skipped, the previous `image_output` remains valid
- `save` always finds `image_output` regardless of which optional fragments ran

---

## Workflow Class Structure

Workflow classes are stored in `Workflows/Templates/{Base}/` directories and implement `IWorkflowBuilder`.

### Required Interface

```csharp
public interface IWorkflowBuilder
{
    WorkflowMetadata Metadata { get; }
    ComfyWorkflow Build(GenerationParameters parameters);
    IEnumerable<IFragmentBuilder> GetFragments();
}
```

### Naming Convention

`{Base}{Mode}Workflow.cs` - Examples: `AnimaTxt2ImgWorkflow.cs`, `FluxTxt2ImgWorkflow.cs`, `WanImg2VidWorkflow.cs`

### Workflow Metadata

```csharp
public WorkflowMetadata Metadata => new()
{
    Title = "Txt2Img",
    Description = "One-line summary of what this workflow does and when to pick it.",
    Base = Data.Enums.ModelBase.Anima,
    Mode = ModeType.Txt2Img,
    Assets = [ ... ],
    CompatibleResourceBaseModels = ["Anima"]
};
```

| Field                          | Description                                                                                                                                                                                                                                                                                |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Title`                        | Display name in UI                                                                                                                                                                                                                                                                         |
| `Description`                  | **Required.** Short paragraph (1-3 sentences) shown in the right-side Info drawer. Explain what the workflow does, what inputs it expects, and when a user should pick it over similar workflows. Do **not** add a top-of-page banner; the description is consumed by `IInfoService` only. |
| `Base`                         | Model base enum: `StableDiffusion`, `Flux`, `Chroma`, `Qwen`, `ZImage`, `Wan`, `Anima`, `LTX`                                                                                                                                                                                              |
| `Mode`                         | Mode type: `Txt2Img`, `Img2Img`, `Upscale`, `Img2Vid`, `Txt2Vid`, `Vid2Vid`                                                                                                                                                                                                                |
| `Assets`                       | Dynamic model selectors (see [Assets System](#assets-system))                                                                                                                                                                                                                              |
| `CompatibleResourceBaseModels` | CivitAI base model strings for filtering asset selectors and LoRA lists (see below)                                                                                                                                                                                                        |

#### CompatibleResourceBaseModels

This property declares which CivitAI base model types are compatible with the workflow. It is used to:

- Filter checkpoint/diffusion model dropdowns in the asset selector to show only compatible models
- Filter LoRA autocomplete results to show only LoRAs trained for compatible architectures

**How to pick the right values:** Reference `Data/CivitAI/basemodels.json` for the complete list of valid CivitAI base model strings. Choose all entries that the workflow's architecture can load. For example, an SD 1.5 workflow should include `"SD 1.5"`, `"SD 1.5 LCM"`, `"SD 1.5 Hyper"`, `"SD 1.4"`, etc.

> **Important:** Do not hardcode base model strings without checking `basemodels.json`. New entries may be added as CivitAI evolves.

### GetFragments()

Returns UI-visible fragments in display order. Hidden/utility fragments (e.g., `LoadDiffusionFragment`) are NOT included here.

```csharp
public IEnumerable<IFragmentBuilder> GetFragments()
{
    yield return _promptsFragment;
    yield return _emptyLatentFragment;
    yield return _samplerStandardFragment;
    yield return _seedVR2UpscaleFragment;
    yield return _detailerFragment;
}
```

---

## Assets System

Assets allow you to specify which models a workflow requires. The application dynamically generates UI dropdowns for users to select these models.

### Asset Definition

```csharp
new WorkflowAsset
{
    Parameter = "Model",
    Label = "Model",
    Type = AssetType.DiffusionModel,
    DefaultValue = "anima-preview3-base.safetensors",
    Order = 1,
    ColumnSize = 4
}
```

### Asset Properties

| Property       | Required | Type      | Description                                                                 |
| -------------- | -------- | --------- | --------------------------------------------------------------------------- |
| `Parameter`    | Yes      | string    | Key used to retrieve value: `parameters.Assets?.GetValueOrDefault("Model")` |
| `Label`        | No       | string    | Display name in UI. Defaults to `Parameter`                                 |
| `Type`         | Yes      | AssetType | Asset type enum (see below)                                                 |
| `DefaultValue` | No       | string    | Default filename from ComfyUI                                               |
| `Order`        | No       | int       | Display order (lower = first). Default: 0                                   |
| `ColumnSize`   | No       | int       | Grid column width (1-12). Default: 6                                        |

### Asset Types

| Type                 | ComfyUI Source (folder / loader node)                                                         | Description                                                                                 |
| -------------------- | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `CheckpointModel`    | `models/checkpoints` (`CheckpointLoaderSimple.ckpt_name`, `LTXAVTextEncoderLoader.ckpt_name`) | Traditional SD checkpoint files plus LTX text-projection ckpts that read from `checkpoints` |
| `DiffusionModel`     | `models/diffusion_models` (`UNETLoader.unet_name`)                                            | Diffusion / UNet model files (Flux, Anima, ZImage, LTX UNet, etc.)                          |
| `Vae`                | `models/vae` (`VAELoader.vae_name`, `VAELoaderKJ.vae_name`)                                   | Image / video / audio VAEs that resolve from `models/vae`                                   |
| `Clip`               | `models/text_encoders` (`CLIPLoader.clip_name`, `DualCLIPLoader.clip_*`)                      | CLIP / T5 / Gemma text encoder models                                                       |
| `ClipVision`         | `models/clip_vision` (`CLIPVisionLoader.clip_name`)                                           | CLIP vision encoder models                                                                  |
| `UpscaleModel`       | `models/upscale_models` (`UpscaleModelLoader.model_name`)                                     | Pixel-space upscalers (ESRGAN-style)                                                        |
| `LatentUpscaleModel` | `models/latent_upscale_models` (`LatentUpscaleModelLoader.model_name`)                        | Latent / diffusion-space upscalers (LTX spatial upscaler)                                   |
| `ControlNet`         | `models/controlnet` (`ControlNetLoader.control_net_name`)                                     | ControlNet conditioning models                                                              |
| `Lora`               | `models/loras` (LoRA loaders)                                                                 | LoRA / IC-LoRA adapter files                                                                |

> Note: the folder path on the right is the **default** ComfyUI mapping. The
> source of truth is the loader node's `/object_info/{ClassType}` combo, NOT the
> folder name. See "Validating Loader Nodes via /object_info" below before
> picking an `AssetType` for a new node.

### Validating Nodes via /object_info

Always probe **every upstream node** referenced by the workflow JSON before
writing the C# build pipeline — not just loaders. Probing catches four classes
of bugs that the compiler cannot:

1. **Wrong class name** — upstream JSON often uses a custom-pack variant that
   is not installed locally. Example: the Adonis workflow ships
   `ImageScaleToTotalPixelsX` (third-party `scale-image-to-total-pixels-advanced`),
   while the built-in node is `ImageScaleToTotalPixels` and exposes a
   different input set and output shape.
2. **Wrong input names** — widget order in the JSON is positional, but the
   builder writes named inputs. Example: `SharkOptions_Beta` widgets
   `[laplacian, 1, 1, false]` map to `noise_type_init`, `s_noise_init`,
   `denoise_alt`, `channelwise_cfg` — never to `noise_type_init_eta`,
   `noise_type_init_eta_var`, `noise_normalize`.
3. **Invalid default COMBO values** — defaults must be exact members of the
   COMBO list. Example: `ClownsharKSampler_Beta.sampler_name` has no bare
   `res_2s` entry; the actual member is `exponential/res_2s`.
4. **Wrong output count / order** — chained references break silently when
   an output index is invalid. Example: built-in `ImageScaleToTotalPixels`
   returns only `IMAGE`; pull width/height from a separate `GetImageSize`
   probe (outputs `width, height, batch_size`).

Two loader-specific gotchas that bit LTX 2.3 still apply:

- The upstream LTX 2.3 audio VAE slot has **two interchangeable loader nodes**
  with different folder backings:
  - `VAELoaderKJ.vae_name` (KJNodes, default in upstream visual JSONs) reads
    from `models/vae` — same folder as the regular video VAE.
  - `LTXVAudioVAELoader.ckpt_name` (official LTX, used by some embedded API
    prompts) reads from `models/checkpoints`.
    Templates default to VAELoaderKJ (asset type `Vae`). Workflows that need the
    checkpoint-backed loader should call the LTX loader fragment's `Build(...)`
    Parameters overload with `AudioVaeNodeType = "LTXVAudioVAELoader"` and an
    `AssetType.CheckpointModel` asset for the dropdown.
- `LTXAVTextEncoderLoader.ckpt_name` reads from `models/checkpoints` — it is
  the LTX text-projection ckpt, NOT a copy of the diffusion UNet name. Type
  it as `CheckpointModel`.
- `LatentUpscaleModelLoader.model_name` resolves against `models/latent_upscale_models`,
  a separate folder from `UpscaleModelLoader.model_name`'s `models/upscale_models`.
  Two distinct enums (`UpscaleModel` and `LatentUpscaleModel`) keep the dropdown
  showing the correct list.
- Nodes using `DualCLIPLoader` (LTX, Flux) require **two distinct `Clip`
  assets**. Don't share a single `ClipName` across both inputs — upstream JSONs
  always wire two different files (e.g., Gemma + LTX text projection).

How to validate:

1. **Probe every class_type from the workflow JSON** (loaders, samplers,
   conditioning helpers, image/latent ops, options nodes, save nodes — all of
   them). Run:
   ```powershell
   Utils/Probe-ComfyObjectInfo.ps1 -Nodes <Node1>,<Node2>,<Node3>,...
   ```
   against a live ComfyUI instance (default `http://localhost:8188`). The
   script hits `/object_info/{ClassType}` and prints category, output sockets,
   and required inputs (including COMBO option samples).
2. For **every node** the workflow will emit, verify:
   - The class is registered (no `MISSING NODE` line). If a node is missing,
     **stop and present the finding to the user before writing any code**.
     Report the class name, the custom-node pack it belongs to (from the
     upstream JSON's `cnr_id` / `properties` field when available), and
     optionally a built-in alternative with explicit notes on differences
     (input name changes, lost output sockets, etc.). Never swap to an
     alternative unilaterally — the user decides whether to install the pack
     or accept a substitution. Proceed with implementation only after
     explicit user approval.
   - The required input names match what the C# builder will write (use
     `Invoke-RestMethod /object_info/<Node> | ConvertTo-Json -Depth 10` for
     full optional-input details when needed).
   - All hardcoded default values for COMBO inputs (sampler names, schedulers,
     noise types, weight dtypes, clip types, etc.) are exact COMBO members.
     Cross-check against the live list — defaults like `res_2s`, `flux2`,
     `default` look plausible but only some are actual COMBO entries.
   - The output count and ordering match what the workflow chains as
     `InputFromNode(<input>, <node>, <index>)` — read the node's
     `output` / `output_name` arrays.
3. For loader nodes, additionally confirm the COMBO option list matches what
   `/models/{folder}` returns and pick (or add) the matching `AssetType`.
   If a folder is new, add a getter to `IComfyUIService` /
   `ComfyUIService` that calls `GetNodeInputOptionsAsync(<NodeClass>, <inputName>)`,
   wire it into `AssetResolverService.GetAssetOptions`, and register both enum
   values in `Models.AssetType` and `Workflows.Models.AssetType` (plus
   `WorkflowService.ConvertAssetType`).
4. Record the probe result in the conversion plan (Phase 1) so the user can
   see which nodes were verified and which were swapped or dropped.

This validation step is **mandatory** for any new workflow conversion. Probing
only the loaders is no longer sufficient.

### Using Assets in Workflows

```csharp
// In Build() method - retrieve with fallback
var modelName = parameters.Assets?.GetValueOrDefault("Model") ?? "default-model.safetensors";
var clipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "default-clip.safetensors";
```

### Column Layout

| ColumnSize | Width       | Use Case                                             |
| ---------- | ----------- | ---------------------------------------------------- |
| 12         | Full width  | Single large dropdown                                |
| 6          | Half width  | Two dropdowns per row                                |
| 4          | One-third   | Three dropdowns per row (Model + CLIP + VAE)         |
| 3          | One-quarter | Four dropdowns per row (Flux: Model + 2 CLIPs + VAE) |

---

## Fragment Class Structure

Fragments are stored in `Workflows/Fragments/` organized by category:

```
Fragments/
  Core/           # Shared across all bases (Sampler, Prompts, Save, etc.)
  Enhancements/   # Optional features (Detailer, Upscale, LoRA, etc.)
  Loaders/        # Model loading with prompts
    flux/           # Flux-specific fragments
    ltx/            # LTX-specific fragments
    qwen/           # Qwen-specific fragments
    wan/            # Wan-specific fragments
```

### Required Interface

```csharp
public interface IFragmentBuilder
{
    FragmentMetadata Metadata { get; }
    void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters,
               NodeRegistry registry, string scope = "", string scopeTitle = "");
}
```

### Fragment Metadata

```csharp
public FragmentMetadata Metadata => new()
{
    Id = "main_sampler",
    Type = FragmentType.Sampler,
    Title = "Sampler",
    Component = "SamplerForm",      // Required today for user-visible fluent fragments
    Icon = "fa-solid fa-dice",
    Order = 50,
    Collapsible = true,
    IsHidden = false,               // true for utility fragments (no UI)
    Parameters = [ ... ]
};
```

For the current fluent workflow runtime, user-visible fragments should declare a registered
`Component`. `FragmentMetadata.Parameters` are bridged into schema constraints, but metadata-only
dynamic field rendering is not implemented in `FragmentRenderer` yet.

### Fragment Parameters (UI Definition)

Parameters define what the UI renders for user control:

```csharp
Parameters =
[
    new FragmentParameter
    {
        Name = "sampler_name",
        Label = "Sampler",
        Type = ParameterType.Select,
        Source = new DynamicSource("Backend", "Samplers")  // Fetched from ComfyUI
    },
    new FragmentParameter
    {
        Name = "steps",
        Label = "Steps",
        Type = ParameterType.Slider,
        Min = 1, Max = 150, Step = 1,
        DefaultValue = 20
    }
]
```

### Dynamic Sources

Parameters can fetch their options dynamically from ComfyUI:

| Source                                       | Description                             |
| -------------------------------------------- | --------------------------------------- |
| `new DynamicSource("Backend", "Samplers")`   | Sampler algorithms available in ComfyUI |
| `new DynamicSource("Backend", "Schedulers")` | Scheduler types available in ComfyUI    |

### Dual Build Pattern

Fragments always implement the interface build method, and many also expose fragment-specific
overloads or helpers for clearer workflow composition.

1. **From GenerationParameters** (interface method) - reads from fragment state dictionary
2. **From explicit `Parameters`** (common overload) - direct construction by workflow
3. **Fragment-specific helpers** (when needed) - for example indexed overloads or `BuildAll()`

```csharp
// Interface method - used when fragment reads its own state
public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters,
                  NodeRegistry registry, string scope = "", string scopeTitle = "")

// Explicit parameters - common when the workflow builds the fragment directly
public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry,
                  Parameters fragmentParams, string scope = "")

// Helper method - used by fragments that expand multiple nodes from a collection
public void BuildAll(ComfyWorkflowBuilder builder, NodeRegistry registry,
                     IList<Lora>? loras, string scope = "")
```

### Fragment Types

| Type          | Scope Behavior                                         | Example                                           |
| ------------- | ------------------------------------------------------ | ------------------------------------------------- |
| **Loader**    | Writes outputs to scope                                | `LoadDiffusionFragment`, `LoadCheckpointFragment` |
| **Processor** | Reads from scope, writes to main                       | `SamplerFragment`, `VaeDecodeFragment`            |
| **Feature**   | Reads models from scope, reads/writes pipeline to main | `DetailerFragment`, `UpscaleFragment`             |
| **Terminal**  | Reads from main only                                   | `SaveFragment`                                    |

---

## Scope System

### Single Parameter Design

The `scope` parameter serves dual purpose:

- For loaders: Prefix for output names
- For processors/features: Prefix for input lookups

```csharp
// Loader: writes to scoped outputs
registry.Register($"{scope}model_output", $"{scope}unet_loader", 0);

// Processor: reads from scoped inputs
var modelRef = registry.GetRef($"{scope}model_output");
```

### Standard Scopes

| Scope             | Usage                                            |
| ----------------- | ------------------------------------------------ |
| `""` (empty)      | Main generation pipeline                         |
| `"detailer_"`     | Detailer pass 0 (legacy / single-pass default)   |
| `"detailer_{i}_"` | Chained detailer pass `i >= 1` (head/hands/etc.) |

Pass 0 intentionally uses the unindexed `"detailer_"` scope to preserve back-compat with
saved parameter snapshots from the single-pass era.

### Detailer Multi-Pass Conventions

The Detailer block is **chainable**: a workflow can run N passes in sequence (e.g. face, hands, feet). Each pass:

1. Loads its own (potentially different) UNet/CLIP/VAE using the scoped loader fragment.
2. Loads its own LoRA stack from `parameters.GetDetailerLoras(i)`.
3. Reads the latest `image_output` from the main registry and writes `image_output` back, so the next pass continues from the freshly-detailed image.

Per-pass parameter storage on the `detailer` fragment:

| Key prefix  | Applies to                                                                      |
| ----------- | ------------------------------------------------------------------------------- |
| (none)      | Pass 0 - keys like `detailer_prompt`, `detailer_seed`, ... (legacy)             |
| `pass_{i}_` | Pass `i >= 1` - keys like `pass_1_detailer_prompt`, `pass_1_detailer_seed`, ... |

Additional top-level detailer key:

| Key          | Meaning                                                     |
| ------------ | ----------------------------------------------------------- |
| `pass_count` | Number of chained detailer passes. Default `1` when absent. |

### Detailer Prompt Fallback

`detailer_prompt` / `detailer_negative_prompt` (and their `pass_{i}_`-prefixed variants) are
**optional overrides**: an empty or whitespace-only value must transparently fall back to the
main prompts. Always resolve via `FragmentParameters.GetStringOrFallback(key, main)` (or the
equivalent extension on `FragmentParameters?`). Never use `??` alone - it treats `""` as set.

### Detailer LoRA Wiring

Every detailer-capable workflow must emit the detailer-scoped LoRA loop:

```csharp
_loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);
```

after the scoped loader and before the `DetailerFragment.Build` call. `GetDetailerLoras(i)`
returns (and lazily creates) the list for pass `i`. Lists are independent across passes and
independent from the main `parameters.Loras`. `LoraLoaderFragment.BuildAll` prefixes node IDs
with the scope so passes never collide.

---

## Pipeline Data Flow

### Complete Flow Diagram (Image Txt2Img)

```
+----------------------------------------------------------+
| LoadDiffusion (scope: "")                                 |
|   WRITES: model_output, clip_output, vae_output          |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| LoraLoader (scope: "")                  [CONDITIONAL]     |
|   READS: model_output, clip_output                       |
|   WRITES: model_output, clip_output (overwrites)         |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| EmptyLatent (scope: "")                                   |
|   WRITES: latent_output                                  |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| Prompts (scope: "")                                       |
|   READS: clip_output (LoRA-modified if LoRAs active)     |
|   WRITES: positive_output, negative_output               |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| Sampler (scope: "")                                       |
|   READS: model_output, positive_output, negative_output, |
|          latent_output                                   |
|   WRITES: latent_output (overwrites)                     |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| VaeDecode (scope: "")                                     |
|   READS: latent_output, vae_output                       |
|   WRITES: image_output                                   |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| SeedVR2Upscale (scope: "")              [CONDITIONAL]     |
|   READS: image_output                                    |
|   WRITES: image_output (overwrites)                      |
+----------------------------------------------------------+
                          |
                          v   (repeat per pass 0..N-1)
+----------------------------------------------------------+
| LoadDiffusionWithPrompts (scope: "detailer_{i}_")         |
|   WRITES: {scope}model_output, {scope}clip_output,       |
|           {scope}vae_output, {scope}positive_output,     |
|           {scope}negative_output                         |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| LoraLoader(s) (scope: "detailer_{i}_")  [PER PASS]        |
|   READS/WRITES: {scope}model_output, {scope}clip_output  |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| Detailer (scope: "detailer_{i}_")       [CONDITIONAL]     |
|   READS: {scope}model_output, {scope}clip_output,        |
|          {scope}vae_output, {scope}positive_output,      |
|          {scope}negative_output, image_output (main)     |
|   WRITES: image_output (overwrites; next pass chains)    |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| Save (no scope)                                           |
|   READS: image_output (always exists)                    |
+----------------------------------------------------------+
```

### LoRA Wiring

LoRA loading is critical because it modifies `model_output` and `clip_output` before they are consumed by prompt encoding and sampling:

```
LoadDiffusion -> model_output, clip_output
                      |              |
                      v              v
LoraLoader(s) -> model_output*, clip_output*  (overwrites with LoRA-modified)
                      |              |
                      v              v
Prompts       -> reads clip_output* for text encoding
Sampler       -> reads model_output* for sampling
```

**Two LoRA approaches:**

1. **App-generated nodes** (`LoraLoaderFragment`): Explicit `LoraLoader` nodes that chain `model_output` and `clip_output`. Used by UNet-based workflows (Anima, ZImage, Flux).
2. **PCLazyLoader** (`LoadCheckpointFragment`): LoRA syntax parsed from prompt text by `PCLazyLoraLoader` nodes. Used by checkpoint-based workflows (StableDiffusion).

---

## Conditional Logic

### Fragment Conditions

Conditional fragments are handled with simple C# `if` statements in the workflow's `Build()` method:

```csharp
// Check if fragment is active (user toggled it on)
var seedVr2Fragment = parameters.GetFragment("seed_vr2");
if (seedVr2Fragment?.IsActive == true)
{
    _seedVR2UpscaleFragment.Build(builder, registry, new SeedVR2UpscaleFragment.Parameters { ... });
}
```

### Type-Safe Parameter Access

Use extension methods on `FragmentParameters` for safe value retrieval:

```csharp
var fragment = parameters.GetFragment("main_sampler");
var steps = fragment?.GetInt("steps", 20) ?? 20;
var cfg = fragment?.GetDouble("cfg", 7.0) ?? 7.0;
var seed = fragment?.GetLong("seed", -1) ?? -1;
var sampler = fragment?.GetString("sampler_name", "euler") ?? "euler";
```

---

## Fragment Design Principles

### 1. Loaders Write to Scope

```csharp
registry.Register($"{scope}model_output", $"{scope}unet_loader", 0);
registry.Register($"{scope}clip_output", $"{scope}clip_loader", 0);
```

### 2. Processors Read from Scope, Write to Main

```csharp
// Read from scope
var modelRef = registry.GetRef($"{scope}model_output");
// Write to main (no scope prefix)
registry.Register("latent_output", p.SamplerId, 0);
```

### 3. Features Read Models from Scope, Pipeline from Main

```csharp
// DetailerFragment reads image from main, models from scope
var imageRef = registry.GetRef("image_output");
var modelRef = registry.GetRef($"{scope}model_output");
```

### 4. Maximize Fragment Reuse

Before creating a new fragment, check if an existing one handles the node type. Fragments should be generic enough to work across model bases:

| Node Pattern                                  | Existing Fragment                  |
| --------------------------------------------- | ---------------------------------- |
| UNETLoader + CLIPLoader + VAELoader           | `LoadDiffusionFragment`            |
| CheckpointLoaderSimple + PCLazy prompts       | `LoadCheckpointFragment`           |
| UNETLoader + CLIPLoader + VAELoader + prompts | `LoadDiffusionWithPromptsFragment` |
| UNETLoader + DualCLIPLoader (Flux)            | `LoadFluxFragment`                 |
| KSampler                                      | `SamplerStandardFragment`          |
| ClownsharKSampler_Beta                        | `SamplerFragment`                  |
| EmptyLatentImage / EmptySD3LatentImage        | `EmptyLatentFragment`              |
| CLIPTextEncode (positive + negative)          | `PromptsFragment`                  |
| VAEDecode                                     | `VaeDecodeFragment`                |
| SaveImage                                     | `SaveFragment`                     |
| LoraLoader (app-generated)                    | `LoraLoaderFragment`               |
| FaceDetailer                                  | `DetailerFragment`                 |
| SeedVR2 upscale pipeline                      | `SeedVR2UpscaleFragment`           |
| ImageUpscaleWithModel + sampler               | `UpscaleFragment`                  |

### 5. Hardcoded vs UI-Exposed Values

When implementing a workflow, some values are hardcoded (not relevant to the end user) while others are exposed via fragment parameters:

| Category             | Examples                                    | Exposed?                      |
| -------------------- | ------------------------------------------- | ----------------------------- |
| Model infrastructure | `clip_type`, `weight_dtype`, `device`       | No - hardcoded                |
| Latent class         | `EmptyLatentImage` vs `EmptySD3LatentImage` | No - hardcoded per base       |
| Save prefix          | `tmp/img`                                   | No - always temp folder       |
| Sampler class        | `KSampler` vs `ClownsharKSampler_Beta`      | No - hardcoded per workflow   |
| Prompt text          | positive/negative                           | Yes - via PromptsFragment     |
| Resolution           | width/height/batch                          | Yes - via EmptyLatentFragment |
| Sampler params       | steps/cfg/denoise/seed                      | Yes - via SamplerFragment     |
| Sampler/Scheduler    | algorithm names                             | Yes - dynamic from ComfyUI    |
| Model files          | model/clip/vae                              | Yes - via Assets              |

---

## Converting Raw Workflows

This section provides a step-by-step guide for converting raw ComfyUI workflow JSON files into the fluent builder system.

> **Agent-driven workflow:** Use the `.github/prompts/workflow-conversion.prompt.md` prompt in Copilot Chat to run the conversion as a guided, multi-phase agent session. The agent follows the exact steps below, plans the workflow and its UI integration together, presents that plan for user approval before writing any code, and confirms build plus UI integration risks at the end.

### Conversion Process

#### Step 1: Analyze the Raw Workflow

1. Open the raw JSON workflow
2. Identify logical node groups:
   - **Loading**: Model loaders, CLIP loaders, VAE loaders
   - **Encoding**: Text encoders, image encoders
   - **Processing**: Samplers, model patchers
   - **Post-processing**: Upscalers, detailers
   - **Output**: VAE decode, save

3. Note model-specific nodes and their parameters
4. Identify nodes to remove:
   - Debug/preview nodes
   - Duplicate save nodes
   - Nodes not relevant to the pipeline

#### Step 2: Planning Phase (Required)

**Before creating any files, discuss and document all of the following with the user:**

1. **Fragment Reuse Assessment**
   - Map each node to an existing fragment (see table above)
   - Only create new fragments when no existing one covers the node type
   - New fragments should be generic enough for other workflows to reuse

2. **Model Loading Strategy**
   - UNet-based (UNETLoader + CLIPLoader + VAELoader): Use `LoadDiffusionFragment`
   - Checkpoint-based (CheckpointLoaderSimple): Use `LoadCheckpointFragment`
   - Flux (dual CLIP): Use `LoadFluxFragment`

3. **LoRA Strategy**
   - App-generated nodes (`LoraLoaderFragment`): Default for UNet-based workflows
   - PCLazy approach (`LoadCheckpointFragment`): For checkpoint-based workflows

4. **Sampler Selection**
   - `KSampler`: Use `SamplerStandardFragment`
   - `ClownsharKSampler_Beta`: Use `SamplerFragment` (advanced features like eta, bongmath)

5. **Enhancement Fragments** (discuss with user — these are always optional add-ons)

   Applicable to **image generation** workflows (Txt2Img, Img2Img):

   | Enhancement            | Fragment                                                | Description                                                                                       |
   | ---------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
   | SeedVR2 Upscale        | `SeedVR2UpscaleFragment`                                | High-quality latent upscaling via unsampling/resampling. Recommended default for image workflows. |
   | FaceDetailer           | `DetailerFragment` + `LoadDiffusionWithPromptsFragment` | Facial detail pass with optional separate model and prompts. Requires scoped loader.              |
   | Standard Upscale       | `UpscaleFragment`                                       | Image upscaling using an upscale model + optional resampling pass.                                |
   | Seed Variance          | `SeedVarianceEnhancerFragment`                          | Subtle variation injection. Rarely used.                                                          |
   | Conditioning Variation | `ConditioningVariationFragment`                         | Prompt conditioning variation. Rarely used.                                                       |

   **Not applicable** to video workflows (Img2Vid). For video, discuss frame interpolation instead.

   Explicitly ask the user:
   - Which enhancements to include
   - Whether the detailer should share the main model or use a separate model asset

6. **UI Component Review**

   Review every UI-visible fragment before the main plan is approved:
   - Determine whether an existing form component can be reused unchanged
   - Determine whether a new form component must be created
   - Validate that the proposed UI follows `WORKFLOW_UI_CONVERSION_GUIDE.md`
   - Validate that the visual approach follows `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
   - Document the field-to-control mapping for each exposed parameter

   Present a table for user validation:

   | Fragment          | Parameters exposed                   | Component decision | Status | Notes                                    |
   | ----------------- | ------------------------------------ | ------------------ | ------ | ---------------------------------------- |
   | `main_sampler`    | sampler, scheduler, steps, cfg, seed | `SamplerForm`      | Reuse  | Matches existing sampler UX              |
   | `controlnet_tile` | strength                             | `ControlNetForm`   | New    | No existing single-field controlnet form |

7. **Hardcoded vs UI-Exposed Values**

   Present a table for user validation before proceeding:

   | Parameter      | Value in JSON      | Exposed in UI? | Reason                           |
   | -------------- | ------------------ | -------------- | -------------------------------- |
   | Sampler name   | `euler`            | Yes            | User controls sampling algorithm |
   | Steps          | `20`               | Yes            | User controls quality vs speed   |
   | CFG            | `7.0`              | Yes            | User controls prompt adherence   |
   | Seed           | `-1`               | Yes            | User controls reproducibility    |
   | Width/Height   | `1024`             | Yes            | User controls resolution         |
   | `clip_type`    | `stable_diffusion` | No             | Infrastructure detail            |
   | `weight_dtype` | `default`          | No             | Infrastructure detail            |
   | Save prefix    | `tmp/img`          | No             | Always temp folder               |

   Use this table as a template; fill in values from the actual workflow JSON.

8. **Default Values**
   - Use values from the raw JSON as sensible defaults
   - CFG, steps, sampler, scheduler should match the model's recommended settings

> **Validation gate:** Do NOT proceed to implementation until the user explicitly approves the full plan, including the enhancement selection, UI component decisions, field mappings, and the UI-exposed vs hardcoded table.

#### Step 3: Implementation

1. **Add ModelBase enum value** (if new base) in `Data/Enums.cs`
2. **Create any new fragment classes** in the appropriate `Workflows/Fragments/` subdirectory
3. **Create or update fragment form components** in `Components/Shared/Generation/Fragments/` when the plan requires UI work
   - Follow `WORKFLOW_UI_CONVERSION_GUIDE.md`
   - Use `[FragmentComponent("...")]` for auto-discovery
   - Follow the app's UI design language instead of copying legacy form quirks blindly
4. **Create the workflow class** in `Workflows/Templates/{Base}/{Base}{Mode}Workflow.cs`
5. **Build and verify compilation**

#### Step 4: Verification

- Build compiles without errors
- Workflow appears in UI with correct metadata
- Every UI-visible fragment resolves to the intended component
- New or reused form components match the current design language and field mapping plan
- Assets populate correctly
- Generation produces valid ComfyUI JSON
- ComfyUI executes the workflow successfully

---

## LTX Fragment Reuse

The LTX 2.3 workflow pack ships a stack of reusable fragments under `Workflows/Fragments/Ltx/`. Most are `IsHidden = true` building blocks composed inside a workflow's `Build()` method - they expose no UI but can be instantiated freely with different `scope` prefixes. The user-facing fragments (`prompts`, `ltx_video_settings`, `ltx_sampler`, `ltx_scheduler`, plus the enhancement toggles) are surfaced via `GetFragments()`.

### Loaders

| Fragment                 | Purpose                                                                                                                                                                                                                                             | Used by                                                                  |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `LtxLoadModelFragment`   | Single-file checkpoint (`CheckpointLoaderSimple`) + AudioVAE + UpscaleModel.                                                                                                                                                                        | `LtxImg2VidWorkflow` (Comfy-default I2V path).                           |
| `LtxLoadSplitFragment`   | Split safetensors: `UNETLoader` + `DualCLIPLoader` + `VAELoader` + audio VAE loader (`VAELoaderKJ` by default; switchable to `LTXVAudioVAELoader`) + `LatentUpscaleModelLoader` + always-on `LTXVChunkFeedForward` / `LTX2SamplingPreviewOverride`. | `LtxTxt2VidWorkflow`, `LtxFml2VidWorkflow`, `LtxControlVid2VidWorkflow`. |
| `LtxLoadSplitAvFragment` | Same as `LtxLoadSplitFragment` but the CLIP slot uses the AV-aware `LTXAVTextEncoderLoader`. Required by AV / lip-sync workflows that ship audio prompt features through the AV text projection.                                                    | `LtxV2vJustTalkWorkflow`, `LtxTalkingAvatarWorkflow`.                    |

**Dual-loader policy:** Use `LtxLoadModelFragment` when the upstream JSON ships a single checkpoint; use `LtxLoadSplitFragment` for split-file packs (UNet + Clip + Vae); use `LtxLoadSplitAvFragment` for any workflow that needs audio conditioning fed through the text encoder (Just-Talk, Talking Avatar). All three register the same five outputs (`{scope}model_output`, `{scope}clip_output`, `{scope}vae_output`, `{scope}audio_vae_output`, `{scope}upscale_model_output`) so downstream fragments are interchangeable.

### Inputs (sources / assets)

| Fragment               | Purpose                                                                                                                                                                                                                                                   | Registers                                                                                                                                                                                        |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `LtxLoadImageFragment` | `LoadImage` -> resize -> `LTXVPreprocess`.                                                                                                                                                                                                                | `{scope}preprocessed_image`.                                                                                                                                                                     |
| `LtxLoadVideoFragment` | `VHS_LoadVideoFFmpeg` -> longer-edge resize -> `GetImageSizeAndCount` + first/last frame split.                                                                                                                                                           | `{scope}loaded_video_images`, `{scope}loaded_video_first_frame`, `{scope}loaded_video_last_frame`, `{scope}loaded_video_width`, `{scope}loaded_video_height`, `{scope}loaded_video_frame_count`. |
| `LtxLoadAudioFragment` | `LoadAudio` -> `TrimAudioDuration` (clamped to `LtxVideoSettings.duration`). Reads `parameters.Sources["audio_track"]`.                                                                                                                                   | `{scope}audio_input`.                                                                                                                                                                            |
| `LtxOmniVoiceFragment` | OmniVoice voice-clone: `LoadAudio` (reference voice) -> `OmniVoiceWhisperLoader` -> `OmniVoiceVoiceCloneTTS` -> `TrimAudioDuration`. Reads `parameters.Sources["reference_audio"]` and (in the default overload) `prompts.positive` as the spoken script. | `{scope}audio_input` (interchangeable with `LtxLoadAudioFragment`).                                                                                                                              |

### Latent / conditioning

| Fragment                         | Purpose                                                                                                          | Notes                                                                                                                                                         |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LtxEmptyLatentFragment`         | `EmptyLTXVLatentVideo` (+ optional `LTXVEmptyLatentAudio`).                                                      | Set `SkipAudio = true` whenever a real audio track is encoded later (e.g. OmniVoice or `LtxLoadAudioFragment`); avoids emitting an unused empty audio latent. |
| `LtxVaeEncodeVideoFragment`      | `VAEEncode` over a video frame batch.                                                                            | Re-registers `{scope}video_latent` (or a custom `OutputName`) so V2V workflows reuse the AV concat / sampler chain unchanged.                                 |
| `LtxAudioVaeEncodeFragment`      | `LTXVAudioVAEEncode` -> `SolidMask` -> `SetLatentNoiseMask`.                                                     | Reads `{scope}audio_input` + `{scope}audio_vae_output`; registers `{scope}audio_latent`.                                                                      |
| `LtxImgToVideoFragment`          | `LTXVImgToVideoInplace` (image conditioning).                                                                    | Re-registers `ltx_positive_output`, `ltx_negative_output`, `video_latent`.                                                                                    |
| `LtxAddLatentGuideFragment`      | `LTXVAddLatentGuide` (e.g. last-frame guide for V2V).                                                            | Re-registers conditioning + latent.                                                                                                                           |
| `LtxAddVideoIcLoraGuideFragment` | `LTXAddVideoICLoRAGuide` (control-pose guide).                                                                   | Phase 4 (DWPose Control).                                                                                                                                     |
| `LtxAudioVideoMaskFragment`      | `LTXVAudioVideoMask` time-range AV mask.                                                                         | Used by V2V Just-Talk for face-region lip-sync.                                                                                                               |
| `LtxFaceMaskFragment`            | `FaceSegment` -> `BlockifyMask` -> `LTXVPreprocessMasks` -> `LTXVSetVideoLatentNoiseMasks`.                      | V2V Just-Talk face lock.                                                                                                                                      |
| `LtxConcatAVLatentFragment`      | `LTXVConcatAVLatent`.                                                                                            | Combines `video_latent` + `audio_latent` into `av_latent_output`.                                                                                             |
| `LtxConditioningFragment`        | `LTXVConditioning` (frame_rate). Has `BuildCropped` overload that runs `LTXVCropGuides` between sampling passes. | Always run after `PromptsFragment`.                                                                                                                           |
| `LtxIcLoraLoaderFragment`        | `LTXICLoRALoaderModelOnly`.                                                                                      | Phase 4 (Control-reference Vid2Vid).                                                                                                                          |
| `LtxControlPreprocessorFragment` | `DWPreprocessor` -> `ImageBlend` (multiply / 0.5).                                                               | Phase 4.                                                                                                                                                      |

### Sampling / scheduling

| Fragment                    | Purpose                                                                                                                                                                                                                                |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LtxSchedulerFragment`      | `LTXVScheduler` (auto / manual sigmas modes). Registers `{scope}sigmas_output`.                                                                                                                                                        |
| `LtxSamplingPassFragment`   | `KSamplerSelect` + `RandomNoise` + `BasicGuider` (LTX path) + `SamplerCustomAdvanced`. Set `UseRegistrySigmas = true` to read `{SigmasInputName}` from a previous scheduler pass; otherwise pass `Sigmas` as a comma-separated string. |
| `LtxUpsampleLatentFragment` | `LTXVLatentUpscale` (between passes).                                                                                                                                                                                                  |
| `LtxDecodeFragment`         | `VAEDecode` + `LTXVAudioVAEDecode` + `VHS_VideoCombine`.                                                                                                                                                                               |

### Enhancement toggles (header-only)

All four are `IsHidden = false` header-only fragments with an `IsActive` toggle. Workflows opt-in by reading `IsActive` and conditionally calling `BuildPatch(builder, registry)`.

| Fragment                               | Patch                                                                                                                                                                                                                            |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LtxNagEnhancementFragment`            | `NAG_Patcher` on `model_output`.                                                                                                                                                                                                 |
| `LtxSageAttentionEnhancementFragment`  | `SageAttentionPatcher` on `model_output`.                                                                                                                                                                                        |
| `LtxMelSeparationEnhancementFragment`  | Mel-band audio separation patch on `audio_vae_output`.                                                                                                                                                                           |
| `LtxRefinementPassEnhancementFragment` | Drives the optional second sampler pass (auto / manual sigmas, sampler name, cfg, seed). The **workflow** is responsible for the actual `Upsample -> CropGuides -> Concat -> Scheduler -> Sampler` pass when `IsActive` is true. |

### Standard build order for an LTX AV workflow

```
1. AV split loader (LtxLoadSplitAvFragment)
2. Optional NAG / Sage model patches
3. LoraLoaderFragment.BuildAll
4. Source loader (LtxLoadImageFragment / LtxLoadVideoFragment)
5. (V2V only) LtxVaeEncodeVideoFragment for source frames + last-frame split
6. Audio path (LtxLoadAudioFragment OR LtxOmniVoiceFragment)
   -> Optional Mel patch
   -> LtxAudioVaeEncodeFragment
7. PromptsFragment + LtxConditioningFragment
8. (T2V/I2V only) LtxEmptyLatentFragment with SkipAudio = true
   (V2V uses encoded video_latent from step 5)
9. (Img2Vid) LtxImgToVideoFragment
   (V2V Just-Talk) LtxAddLatentGuideFragment + LtxFaceMaskFragment + LtxAudioVideoMaskFragment
   (Control) LtxIcLoraLoaderFragment + LtxControlPreprocessorFragment + LtxAddVideoIcLoraGuideFragment
10. LtxConcatAVLatentFragment (pass1)
11. LtxSchedulerFragment
12. LtxSamplingPassFragment (UseRegistrySigmas = true)
13. Optional refinement pass: LtxUpsampleLatentFragment -> LtxConditioningFragment.BuildCropped
    -> LtxImgToVideoFragment (pass2) / LtxConcatAVLatentFragment (pass2)
    -> LtxSchedulerFragment (scope: "pass2_")
    -> LtxSamplingPassFragment (PassId: "pass2", SigmasInputName: "pass2_sigmas_output")
14. LtxDecodeFragment
```

---

## Examples

### Minimal Workflow (Anima Txt2Img)

```csharp
public class AnimaTxt2ImgWorkflow : IWorkflowBuilder
{
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly SamplerStandardFragment _samplerStandardFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img",
        Description = "One-line summary shown in the Info drawer.",
        Base = Data.Enums.ModelBase.Anima,
        Mode = ModeType.Txt2Img,
        CompatibleResourceBaseModels = ["Anima"],
        Assets =
        [
            new WorkflowAsset { Parameter = "Model", Label = "Model", Type = AssetType.DiffusionModel,
                                DefaultValue = "anima-preview3-base.safetensors", Order = 1, ColumnSize = 4 },
            new WorkflowAsset { Parameter = "Clip", Label = "CLIP", Type = AssetType.Clip,
                                DefaultValue = "qwen_3_06b_base.safetensors", Order = 2, ColumnSize = 4 },
            new WorkflowAsset { Parameter = "Vae", Label = "VAE", Type = AssetType.Vae,
                                DefaultValue = "qwen_image_vae.safetensors", Order = 3, ColumnSize = 4 }
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerStandardFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // 1. Load models
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "anima-preview3-base.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_06b_base.safetensors",
            ClipType = "stable_diffusion",    // Hardcoded - not relevant to user
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "qwen_image_vae.safetensors"
        });

        // 2. LoRAs (app-generated nodes)
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 3. Empty latent
        var latentFragment = parameters.GetFragment("latent");
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = latentFragment?.GetInt("width", 1024) ?? 1024,
            Height = latentFragment?.GetInt("height", 1024) ?? 1024,
            BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1,
            LatentClass = "EmptyLatentImage"   // Hardcoded - Anima uses standard latent
        });

        // 4. Encode prompts (reads clip_output, possibly LoRA-modified)
        var promptsData = parameters.GetFragment("prompts");
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsData?.GetString("positive", "") ?? "",
            Negative = promptsData?.GetString("negative", "") ?? ""
        });

        // 5. Sample (KSampler - hardcoded class type)
        var samplerData = parameters.GetFragment("main_sampler");
        var seed = samplerData?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerStandardFragment.Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerId = "sampler_main",
            Title = "KSampler",
            SamplerName = samplerData?.GetString("sampler_name", "er_sde") ?? "er_sde",
            Scheduler = samplerData?.GetString("scheduler", "simple") ?? "simple",
            Steps = samplerData?.GetInt("steps", 30) ?? 30,
            Cfg = samplerData?.GetDouble("cfg", 4.0) ?? 4.0,
            Denoise = samplerData?.GetDouble("denoise", 1.0) ?? 1.0,
            Seed = seed
        });

        // 6. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 7. Save (always to temp folder)
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"         // Hardcoded - temp folder for cleanup
        });

        return builder.ToComfyWorkflow(registry);
    }
}
```

### With Detailer

```csharp
// In Build() method, after VaeDecode:

var detailerFragment = parameters.GetFragment("detailer");
if (detailerFragment?.IsActive == true)
{
    // Load separate scoped models for detailer
    _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
    {
        UnetName = detailerFragment.GetString("detailer_checkpoint")
                   ?? parameters.Assets?.GetValueOrDefault("Model") ?? "model.safetensors",
        ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "clip.safetensors",
        ClipType = "stable_diffusion",
        VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "vae.safetensors",
        Positive = detailerFragment.GetString("detailer_prompt")
                   ?? promptsFragment?.GetString("positive", "") ?? "",
        Negative = detailerFragment.GetString("detailer_negative_prompt")
                   ?? promptsFragment?.GetString("negative", "") ?? ""
    }, scope: "detailer_", scopeTitle: "Detailer ");

    _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
    {
        Scope = "detailer_",
        DetectionModel = detailerFragment.GetString("detailer_detection_model", "bbox/face_yolov8m.pt"),
        // ... other detailer params
    });
}
```

---

## Troubleshooting

### Asset dropdown is empty

1. Check that ComfyUI is running and connected
2. Verify the asset `Type` enum value matches a valid ComfyUI endpoint
3. Check browser console for API errors

### Workflow not appearing in UI

1. Ensure the class implements `IWorkflowBuilder`
2. Verify `ModelBase` enum value exists in `Data/Enums.cs`
3. Check that the workflow is discovered via reflection (public, non-abstract class)

### "No output registered" error

1. Ensure previous fragment registered the required output
2. Check output name includes correct scope prefix
3. Verify fragment execution order in `Build()` method

### Node ID conflicts

1. Use different scopes for multiple fragment instances
2. Check node IDs use `{scope}` prefix internally

### LoRA not applying

1. Verify `LoraLoaderFragment.BuildAll()` is called AFTER `LoadDiffusionFragment`
2. Verify it is called BEFORE `PromptsFragment` (so clip_output is LoRA-modified)
3. Check that LoRAs are enabled in `parameters.Loras`

---

## Quick Reference

### Scope Parameter

| Fragment Type | scope controls     | Writes to                    |
| ------------- | ------------------ | ---------------------------- |
| Loader        | Output prefix      | `{scope}model_output`, etc.  |
| Processor     | Input prefix       | Main (`latent_output`, etc.) |
| Feature       | Model input prefix | Main (`image_output`)        |
| Terminal      | N/A                | N/A                          |

### Common Build Order (Image Txt2Img)

```
1. LoadDiffusion / LoadCheckpoint / LoadFlux
2. LoraLoader (if app-generated)
3. EmptyLatent
4. Prompts
5. Sampler
6. VaeDecode
7. [SeedVR2Upscale]  (conditional)
8. [Detailer]         (conditional, with scoped loader)
9. Save
```

### Parameter Type Reference

| ParameterType | UI Control      | Example                                   |
| ------------- | --------------- | ----------------------------------------- |
| `Slider`      | Range slider    | Steps, CFG, Denoise                       |
| `Number`      | Number input    | Seed                                      |
| `TextArea`    | Multi-line text | Prompts                                   |
| `Select`      | Dropdown        | Sampler, Scheduler (dynamic from ComfyUI) |
| `Toggle`      | Checkbox        | Feature on/off                            |
