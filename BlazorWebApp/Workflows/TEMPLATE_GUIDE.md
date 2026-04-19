# Workflow Template System - Complete Guide

This document provides comprehensive documentation for creating and converting ComfyUI workflows into the modular C# fluent builder system.

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
11. [Examples](#examples)
12. [Troubleshooting](#troubleshooting)

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

### 1. Output Naming Convention

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

### 2. Scope System

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

### 3. Pipeline Flow Pattern

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
    Base = Data.Enums.ModelBase.Anima,
    Mode = ModeType.Txt2Img,
    Assets = [ ... ]
};
```

| Field    | Description                                                                            |
| -------- | -------------------------------------------------------------------------------------- |
| `Title`  | Display name in UI                                                                     |
| `Base`   | Model base enum: `StableDiffusion`, `Flux`, `Chroma`, `Qwen`, `ZImage`, `Wan`, `Anima` |
| `Mode`   | Mode type: `Txt2Img`, `Img2Img`, `Upscale`, `Img2Vid`                                  |
| `Assets` | Dynamic model selectors (see [Assets System](#assets-system))                          |

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

| Type              | ComfyUI Endpoint            | Description                                            |
| ----------------- | --------------------------- | ------------------------------------------------------ |
| `CheckpointModel` | `checkpoints`               | Traditional SD checkpoint files (.safetensors, .ckpt)  |
| `DiffusionModel`  | `diffusion_models` / `unet` | Diffusion/UNet model files (Flux, Anima, ZImage, etc.) |
| `Vae`             | `vae`                       | VAE model files                                        |
| `Clip`            | `text_encoders` / `clip`    | CLIP text encoder models                               |
| `ClipVision`      | `clip_vision`               | CLIP vision encoder models                             |

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
  Flux/           # Flux-specific fragments
  Wan/            # Wan-specific fragments
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
    Component = "SamplerForm",      // Blazor component for UI
    Icon = "fa-solid fa-dice",
    Order = 50,
    Collapsible = true,
    IsHidden = false,               // true for utility fragments (no UI)
    Parameters = [ ... ]
};
```

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

Fragments support two build methods:

1. **From GenerationParameters** (interface method) - reads from fragment state dictionary
2. **From explicit Parameters** (overload) - direct construction by workflow

```csharp
// Interface method - used when fragment reads its own state
public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters,
                  NodeRegistry registry, string scope = "", string scopeTitle = "")

// Explicit parameters - preferred in workflows for clarity
public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry,
                  Parameters fragmentParams, string scope = "")
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

| Scope         | Usage                    |
| ------------- | ------------------------ |
| `""` (empty)  | Main generation pipeline |
| `"detailer_"` | Detailer model loading   |

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
                          v
+----------------------------------------------------------+
| LoadDiffusionWithPrompts (scope: "detailer_")             |
|   WRITES: detailer_model_output, detailer_clip_output,   |
|           detailer_vae_output, detailer_positive_output,  |
|           detailer_negative_output                       |
+----------------------------------------------------------+
                          |
                          v
+----------------------------------------------------------+
| Detailer (scope: "detailer_")           [CONDITIONAL]     |
|   READS: detailer_model_output, detailer_clip_output,    |
|          detailer_vae_output, detailer_positive_output,  |
|          detailer_negative_output, image_output (main!)  |
|   WRITES: image_output (overwrites main)                 |
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

> **Agent-driven workflow:** Use the `.github/prompts/workflow-conversion.prompt.md` prompt in Copilot Chat to run the conversion as a guided, multi-phase agent session. The agent follows the exact steps below, presents a plan for user approval before writing any code, and confirms the build at the end.

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

6. **Hardcoded vs UI-Exposed Values**

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

7. **Default Values**
   - Use values from the raw JSON as sensible defaults
   - CFG, steps, sampler, scheduler should match the model's recommended settings

> **Validation gate:** Do NOT proceed to implementation until the user explicitly approves the full plan, including the enhancement selection and the UI-exposed vs hardcoded table.

#### Step 3: Implementation

1. **Add ModelBase enum value** (if new base) in `Data/Enums.cs`
2. **Create any new fragment classes** in the appropriate `Workflows/Fragments/` subdirectory
3. **Create the workflow class** in `Workflows/Templates/{Base}/{Base}{Mode}Workflow.cs`
4. **Build and verify compilation**

#### Step 4: Verification

- Build compiles without errors
- Workflow appears in UI with correct metadata
- Assets populate correctly
- Generation produces valid ComfyUI JSON
- ComfyUI executes the workflow successfully

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
        Base = Data.Enums.ModelBase.Anima,
        Mode = ModeType.Txt2Img,
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
