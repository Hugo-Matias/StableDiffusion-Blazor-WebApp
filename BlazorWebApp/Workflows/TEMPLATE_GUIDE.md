# Workflow Template System - Complete Guide

This document provides comprehensive documentation for creating and converting ComfyUI workflows into the modular Pipeline-based template system.

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Core Conventions](#core-conventions)
3. [Template File Structure](#template-file-structure)
4. [Assets System](#assets-system)
5. [Fragment File Structure](#fragment-file-structure)
6. [Scope System](#scope-system)
7. [Pipeline Data Flow](#pipeline-data-flow)
8. [Conditional Logic](#conditional-logic)
9. [Fragment Design Principles](#fragment-design-principles)
10. [Converting Raw Workflows](#converting-raw-workflows)
11. [Examples](#examples)
12. [Troubleshooting](#troubleshooting)

---

## System Overview

The workflow template system uses a **Pipeline-based architecture** where:

1. **Templates** (`.sbn` files in `Workflows/Templates/`) define the overall workflow structure
2. **Fragments** (`.sbn` files in `Workflows/Fragments/`) are reusable node groups
3. **Scriban** templating engine handles dynamic value injection
4. **Node Registry** manages references between fragments
5. **Scope System** enables multiple model sets with isolated namespaces
6. **Assets** allow dynamic model selection via UI dropdowns

### Key Design Goals

| Goal | Description |
|------|-------------|
| **Modularity** | Fragments can be reused across multiple workflows and architectures |
| **Flexibility** | Any fragment can be instantiated multiple times with different scopes |
| **Architecture-Agnostic** | Features like Detailer/Upscale work with any model base |
| **Streamlined** | Single `scope` parameter controls namespace isolation |

---

## Core Conventions

### 1. Output Naming Convention

All outputs follow the pattern: `{scope}{type}_output`

| Output Name | Description |
|-------------|-------------|
| `model_output` | Model/UNet output |
| `clip_output` | CLIP encoder output |
| `vae_output` | VAE output |
| `latent_output` | Latent image output |
| `positive_output` | Positive conditioning |
| `negative_output` | Negative conditioning |
| `image_output` | Decoded image output |

### 2. Scope System

The `scope` parameter controls namespace isolation:

- **Loader fragments**: `scope` controls where outputs are **written**
- **Processing fragments**: `scope` controls where model inputs are **read from**
- **Pipeline outputs** (`latent_output`, `image_output`): Always written to main (no scope)

```json
// Main pipeline - no scope (writes to model_output, clip_output, etc.)
{ "fragment": "load-checkpoint.sbn", "parameters": { "ckpt_name": "model.safetensors" } }

// Detailer scope - writes to detailer_model_output, detailer_clip_output, etc.
{ "fragment": "load-checkpoint.sbn", "parameters": { "scope": "detailer_", "scope_title": "Detailer ", ... } }

// Detailer reads from detailer_ scope, writes image_output to main
{ "fragment": "detailer-core.sbn", "parameters": { "scope": "detailer_" } }
```

### 3. Pipeline Flow Pattern

**Critical concept:** Processing fragments overwrite main pipeline outputs.

```
load-checkpoint (no scope) ? model_output, vae_output, latent_output
sampler (no scope) ? reads main, writes latent_output (overwrites)
vae-decode (no scope) ? reads main, writes image_output

load-checkpoint (scope: detailer_) ? detailer_model_output, detailer_vae_output
detailer-core (scope: detailer_) ? reads detailer_, reads image_output, writes image_output (overwrites)

save (no scope) ? reads image_output ? (always exists)
```

**Why this works:** 
- Conditional fragments (detailer, upscale) overwrite `image_output` when active
- When skipped, the previous `image_output` remains valid
- `save` always finds `image_output` regardless of which optional fragments ran

---

## Template File Structure

Templates are stored in `Workflows/Templates/{base}/` directories.

### Required Fields

```json
{
  "Title": "Txt2Img",
  "Base": "Flux",
  "Mode": "txt2img",
  "Assets": [...],
  "Pipeline": [...]
}
```

| Field | Description |
|-------|-------------|
| `Title` | Display name in UI |
| `Base` | Model base: StableDiffusion, SDXL, Flux, Chroma, Wan, Qwen, ZImage, etc. |
| `Mode` | Mode type: txt2img, img2img, upscale, img2vid |
| `Assets` | Dynamic model selectors (see [Assets System](#assets-system)) |
| `Pipeline` | Array of fragment invocations |

---

## Assets System

Assets allow you to specify which models a workflow requires. The application dynamically generates UI dropdowns for users to select these models.

### Asset Definition Format

```json
"Assets": [
  {
    "parameter": "Model",
    "label": "Diffusion Model",
    "type": "DiffusionModel",
    "default": "my-model.safetensors",
    "order": 1,
    "columnSize": 4
  }
]
```

### Asset Properties

| Property | Required | Type | Description |
|----------|----------|------|-------------|
| `parameter` | ? Yes | string | Variable name in templates: `{{ Model }}` |
| `label` | No | string | Display name in UI. Defaults to `parameter` |
| `type` | ? Yes | string | Asset type (see below) |
| `default` | No | string | Default filename from ComfyUI |
| `order` | No | integer | Display order (lower = first). Default: 0 |
| `columnSize` | No | integer | Grid column width (1-12). Default: 6 |

### Asset Types

| Type | ComfyUI Endpoint | Description |
|------|------------------|-------------|
| `CheckpointModel` | `checkpoints` | Traditional SD checkpoint files (.safetensors, .ckpt) |
| `DiffusionModel` | `diffusion_models` / `unet` | Diffusion/UNet model files (Flux, SD3, etc.) |
| `Vae` | `vae` | VAE model files |
| `Clip` | `text_encoders` / `clip` | CLIP text encoder models (T5, ViT, etc.) |
| `ClipVision` | `clip_vision` | CLIP vision encoder models |

### Using Assets in Templates

```json
// Basic usage
"unet_name": {{ Model | json }}

// With default fallback
"vae_name": {{ VAE ?? "ae.safetensors" | json }}

// Multiple assets
"clip_name1": {{ Clip1 ?? "t5xxl.safetensors" | json }},
"clip_name2": {{ Clip2 ?? "vit-l.safetensors" | json }}
```

### Column Layout

| columnSize | Width | Use Case |
|------------|-------|----------|
| 12 | Full width | Single large dropdown |
| 6 | Half width | Two dropdowns per row |
| 4 | One-third | Three dropdowns per row |
| 3 | One-quarter | Four dropdowns per row |

### Complete Assets Example

```json
{
  "Title": "Txt2Img",
  "Base": "Flux",
  "Mode": "txt2img",
  "Assets": [
    { "parameter": "Model", "label": "Model", "type": "DiffusionModel", "default": "flux1-dev.safetensors", "order": 1, "columnSize": 3 },
    { "parameter": "Clip1", "label": "CLIP T5", "type": "Clip", "default": "t5xxl_fp8.safetensors", "order": 2, "columnSize": 3 },
    { "parameter": "Clip2", "label": "CLIP ViT", "type": "Clip", "default": "vit-l.safetensors", "order": 3, "columnSize": 3 },
    { "parameter": "VAE", "label": "VAE", "type": "Vae", "default": "ae.safetensors", "order": 4, "columnSize": 3 }
  ],
  "Pipeline": [...]
}
```

---

## Fragment File Structure

Fragments are stored in `Workflows/Fragments/` (global) or `Workflows/Fragments/{base}/` (base-specific).

### Standard Fragment Template

```scriban
#meta
{
  "outputs": {
    "{{ scope ?? '' }}model_output": { "node": "{{ scope ?? '' }}loader", "index": 0 }
  }
}
#end

{
  "{{ scope ?? '' }}loader": {
    "inputs": {
      "model_name": {{ model_name | json }}
    },
    "class_type": "UNETLoader",
    "_meta": {
      "title": "{{ scope_title ?? '' }}Load Model"
    }
  }
}
```

### Fragment Types

| Type | Scope Behavior | Example |
|------|----------------|---------|
| **Loader** | Writes outputs to scope | `load-checkpoint.sbn`, `load-flux.sbn` |
| **Processor** | Reads from scope, writes to main | `sampler.sbn`, `vae-decode.sbn` |
| **Feature** | Reads models from scope, reads/writes pipeline to main | `detailer-core.sbn`, `upscale.sbn` |
| **Terminal** | Reads from main only | `save.sbn` |

### Metadata Block

```scriban
#meta
{
  "outputs": {
    "output_name": { "node": "node_id", "index": 0 }
  },
  "conditions": {
    "required": ["Feature.IsActive"],
    "excluded_if": ["SomeCondition"]
  }
}
#end
```

---

## Scope System

### Single Parameter Design

The `scope` parameter serves dual purpose:
- For loaders: Prefix for output names
- For processors/features: Prefix for input lookups

```scriban
// Loader: writes to scoped outputs
"{{ scope ?? '' }}model_output": { "node": "{{ scope ?? '' }}loader", "index": 0 }

// Processor: reads from scoped inputs
"model": {{ get_ref ((scope ?? "") + "model_output") }}
```

### Standard Scopes

| Scope | Usage |
|-------|-------|
| `""` (empty) | Main generation pipeline |
| `"detailer_"` | Detailer model loading |

---

## Pipeline Data Flow

### Complete Flow Diagram

```
???????????????????????????????????????????????????????????????????
? load-checkpoint (scope: "")                                      ?
?   WRITES: model_output, clip_output, vae_output,                ?
?           latent_output, positive_output, negative_output        ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
? sampler (scope: "")                                              ?
?   READS: model_output, positive_output, negative_output,        ?
?          latent_output                                           ?
?   WRITES: latent_output (overwrites)                            ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
? vae-decode (scope: "")                                           ?
?   READS: latent_output, vae_output                              ?
?   WRITES: image_output                                          ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
? load-checkpoint (scope: "detailer_")                            ?
?   WRITES: detailer_model_output, detailer_clip_output,          ?
?           detailer_vae_output, detailer_positive_output, etc.   ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
? detailer-core (scope: "detailer_")  [CONDITIONAL]               ?
?   READS: detailer_model_output, detailer_clip_output, etc.      ?
?          image_output (from main!)                              ?
?   WRITES: image_output (overwrites main)                        ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
? save (no scope)                                                  ?
?   READS: image_output ? (always exists)                         ?
???????????????????????????????????????????????????????????????????
```

---

## Conditional Logic

### Fragment Conditions

```scriban
#meta
{
  "conditions": {
    "required": ["Detailer.IsActive"]
  }
}
#end
```

### Template-Level Conditionals

```scriban
"Pipeline": [
  { "fragment": "load-model.sbn", "parameters": { ... } },
  
  {{~ if Upscale.IsActive ~}}
  { "fragment": "upscale.sbn", "parameters": { ... } },
  {{~ end ~}}
  
  { "fragment": "save.sbn", "parameters": {} }
]
```

---

## Fragment Design Principles

### 1. Loaders Write to Scope

```scriban
"outputs": {
  "{{ scope ?? '' }}model_output": { "node": "{{ scope ?? '' }}loader", "index": 0 }
}
```

### 2. Processors Read from Scope, Write to Main

```scriban
"outputs": {
  "latent_output": { "node": "sampler", "index": 0 }  // No scope prefix!
}

// In body:
"model": {{ get_ref ((scope ?? "") + "model_output") }}  // Read from scope
```

### 3. Features Read Models from Scope, Pipeline from Main

```scriban
// detailer-core.sbn
"image": {{ get_ref "image_output" }},                           // From main
"model": {{ get_ref ((scope ?? "detailer_") + "model_output") }}, // From scope
```

---

## Converting Raw Workflows

This section provides a step-by-step guide for converting raw ComfyUI workflow JSON files into the Pipeline-based template system.

### Conversion Process

#### Step 1: Analyze the Raw Workflow

1. Open the raw JSON workflow
2. Identify logical node groups:
   - **Loading**: Model loaders, CLIP loaders, VAE loaders
   - **Encoding**: Text encoders, image encoders
   - **Processing**: Samplers, model patchers
   - **Post-processing**: Upscalers, detailers
   - **Output**: VAE decode, save

3. Note model-specific nodes:
   - Flux: `DualCLIPLoader`, `FluxGuidance`, `ReFluxPatcher`
   - SD3: `CLIPLoader` with type
   - Qwen: `TextEncodeQwenImageEditPlus`, `ModelSamplingAuraFlow`
   - Wan: Video-specific nodes

4. Identify nodes to remove:
   - `FlowSelect` (use Scriban conditions instead)
   - Duplicate save nodes
   - Debug/preview nodes

#### Step 2: Planning Phase (Required)

**Before creating any files, discuss and document:**

1. **Node Grouping Decisions**
   - Which nodes should be combined into a single fragment?
   - Which nodes should be split into separate fragments?
   - Rationale for each decision

2. **Output/Input Chaining**
   - What outputs does each fragment need to register?
   - What inputs does each fragment need to reference?
   - Are there any non-standard output names needed (e.g., `image_input`)?

3. **Fragment Reuse**
   - Can existing fragments handle these nodes?
   - If new fragments are needed, will they be reusable for other workflows?

4. **UI Considerations**
   - What parameters should be exposed to the user?
   - What should have defaults vs. be required?
   - Asset definitions and layout

5. **Architectural Challenges**
   - Any unique node types requiring special handling?
   - Cross-fragment dependencies?
   - Conditional fragment inclusion?

**Request approval before proceeding to implementation.**

#### Step 3: Map to Existing Fragments

Check if existing fragments can handle the nodes:

| Node Pattern | Existing Fragment |
|--------------|-------------------|
| CheckpointLoaderSimple + prompts | `load-checkpoint.sbn` |
| UNETLoader + CLIPLoader + VAE | `load-diffusion.sbn` or `load-diffusion-w-prompts.sbn` |
| UNETLoader + DualCLIPLoader (Flux) | `flux/load-flux.sbn` |
| KSampler / KSamplerAdvanced | `sampler.sbn` or `sampler-standard.sbn` |
| VAEDecode | `vae-decode.sbn` |
| VAEEncode | `vae-encode.sbn` |
| SaveImage | `save.sbn` |
| FaceDetailer | `detailer-core.sbn` |
| LoraLoader | `lora-loader.sbn` |
| ModelSamplingAuraFlow | `model-sampling-auraflow.sbn` |
| LoadImage + Scale | `load-image-scaled.sbn` |

#### Step 4: Create New Fragments (If Needed)

Create new fragments only when:
- Unique node types not covered by existing fragments
- Unique node combinations (e.g., `TextEncodeQwenImageEditPlus`)
- NOT for parameter differences

Follow the [Fragment Design Principles](#fragment-design-principles).

#### Step 5: Create the Template

1. Define metadata (Title, Base, Mode)
2. Define Assets for model selection
3. Build the Pipeline array
4. Map raw workflow parameters to template variables

#### Step 6: Create Conversion Log

**Always create a conversion log** at `Workflows/Logs/{base}_{mode}_conversion.md`.

The log should include:
- Planning discussion and decisions made
- Node mapping to fragments
- New fragments created and rationale
- Assets and parameter mapping
- Special considerations
- Testing checklist
- **Raw workflow JSON appended at the bottom**

See [Conversion Log Template](#conversion-log-template) below.

### Conversion Log Template

```markdown
# {Base} {Mode} Workflow Conversion Log

## Source
- Template: `Workflows/Templates/{base}/{mode}.sbn`

---

## Planning Discussion

### Initial Analysis
- Description of the workflow
- Key observations about node structure

### Decisions Made

#### Node Grouping
| Decision | Rationale |
|----------|-----------|
| Combine X + Y into fragment | Always used together, simplifies template |
| Keep Z separate | May be reused independently |

#### Output/Input Chaining
- What outputs are registered
- What non-standard references are needed

#### UI Considerations
- Parameters to expose
- Default values rationale

---

## Key Design Decisions

### 1. Decision Name

**Decision:** What was decided.

**Rationale:**
- Why this decision was made
- Benefits of this approach

---

## Node Mapping

| Raw Node ID | Raw Class Type | Fragment | Notes |
|-------------|----------------|----------|-------|
| 37 | UNETLoader | load-diffusion-w-prompts.sbn | Model loading |
| 38 | CLIPLoader | load-diffusion-w-prompts.sbn | Combined with UNet |
| 3 | KSampler | sampler.sbn | Standard sampler |
| 8 | VAEDecode | vae-decode.sbn | Standard decode |
| 60 | SaveImage | save.sbn | Terminal |

## Nodes Removed

| Node ID | Class Type | Reason |
|---------|------------|--------|
| 112 | EmptySD3LatentImage | Unused (connected to nothing) |

---

## New Fragments Created

| Fragment | Reason |
|----------|--------|
| `qwen/encode-edit.sbn` | Qwen-specific image edit encoding |

---

## Assets Defined

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| Model | DiffusionModel | qwen_edit.safetensors | Main model |
| Clip | Clip | qwen_clip.safetensors | Text encoder |

## Parameter Mapping

| UI Parameter | Raw Workflow Source | Default | Notes |
|--------------|---------------------|---------|-------|
| Prompt | Node 111 ? prompt | - | Positive prompt |
| Steps | Node 3 ? steps | 20 | Sampler steps |
| Seed | Node 3 ? seed | 42 | Random seed |

---

## Special Considerations

- Notes about unique aspects of this workflow
- Model chain details
- CFG handling notes

---

## Testing Checklist

- [ ] Template loads without errors
- [ ] Assets populate in UI
- [ ] Generation completes successfully
- [ ] Output matches raw workflow output

---

## Raw Workflow Reference

```json
{
  // Full raw workflow JSON appended here
  // IMPORTANT: Clean the following before appending:
  // - Prompt text (use empty string or placeholder)
  // - Image/video filenames (use empty string or placeholder)
  // - Seed values (use placeholder like 0 or 42)
  // - Any personal/test data
}
```

### Raw Workflow Cleanup

Before appending the raw workflow JSON to the log, clean the following:

| Field Type | Example | Clean To |
|------------|---------|----------|
| Prompt text | `"prompt": "a photo of..."` | `"prompt": ""` |
| Negative prompt | `"negative": "bad quality..."` | `"negative": ""` |
| Image filename | `"image": "test_00001.png"` | `"image": ""` |
| Video filename | `"video": "output.mp4"` | `"video": ""` |
| Seed | `"seed": 513958326250122` | `"seed": 0` |
| Filename prefix | `"filename_prefix": "my/test"` | `"filename_prefix": "output"` |

This ensures users can test the workflow directly in ComfyUI without missing references or hardcoded test data.

### Conversion Checklist

Before submitting a conversion:

- [ ] Planning phase completed and documented
- [ ] Approval received before creating files
- [ ] Template metadata complete (Title, Base, Mode)
- [ ] Assets defined for all user-selectable models
- [ ] Pipeline fragments use correct scope
- [ ] All Scriban variables use `| json` filter for JSON output
- [ ] Default values use `??` operator (NOT `| default:`)
- [ ] Conditional features use `#meta` conditions
- [ ] Conversion log created with raw workflow appended
- [ ] Tested with feature combinations

---

## Examples

### Minimal Template

```json
{
  "Title": "Txt2Img",
  "Base": "StableDiffusion",
  "Mode": "txt2img",
  "Assets": [
    { "parameter": "Model", "type": "CheckpointModel", "default": "v1-5.safetensors" }
  ],
  "Pipeline": [
    {
      "fragment": "load-checkpoint.sbn",
      "parameters": {
        "loader_id": "loader",
        "latent_id": "latent",
        "ckpt_name": {{ Model | json }},
        "prompt": {{ Prompt | json }},
        "negative": {{ NegativePrompt | json }},
        "width": {{ Width | json }},
        "height": {{ Height | json }},
        "batch_size": 1
      }
    },
    {
      "fragment": "sampler.sbn",
      "parameters": {
        "sampler_id": "main",
        "sampler_name": {{ SamplerName | json }},
        "scheduler": {{ Scheduler | json }},
        "steps": {{ Steps | json }},
        "cfg": {{ CfgScale | json }},
        "seed": {{ Seed | json }}
      }
    },
    { "fragment": "vae-decode.sbn", "parameters": {} },
    { "fragment": "save.sbn", "parameters": {} }
  ]
}
```

### With Detailer

```json
"Pipeline": [
  // Main generation
  { "fragment": "load-checkpoint.sbn", "parameters": { ... } },
  { "fragment": "sampler.sbn", "parameters": { ... } },
  { "fragment": "vae-decode.sbn", "parameters": {} },
  
  // Detailer (scoped model)
  { 
    "fragment": "load-checkpoint.sbn",
    "parameters": { 
      "scope": "detailer_",
      "scope_title": "Detailer ",
      "ckpt_name": {{ Detailer.Checkpoint | json }},
      ...
    }
  },
  { 
    "fragment": "detailer-core.sbn",
    "parameters": { 
      "scope": "detailer_",
      "detailer_cfg": 8,
      ...
    }
  },
  
  { "fragment": "save.sbn", "parameters": {} }
]
```

---

## Troubleshooting

### Asset dropdown is empty

1. Check that ComfyUI is running and connected
2. Verify the asset `type` is spelled correctly (case-insensitive)
3. Check browser console for API errors

### Asset value not being used in workflow

1. Ensure `parameter` name matches in Assets and template usage
2. Use `| json` filter to properly escape strings
3. Provide fallback: `{{ Model ?? "default.safetensors" | json }}`

### "No Pipeline found in rendered template"

1. Ensure template has `"Pipeline": [...]` array
2. Check for JSON syntax errors (trailing commas)

### Fragment not included

1. Check `#meta` conditions are met
2. Verify parameter values evaluate to true

### "get_ref" returns null

1. Ensure previous fragment registered the required output
2. Check output name includes correct scope
3. Verify fragment order in Pipeline

### Node ID conflicts

1. Use different scopes for multiple fragment instances
2. Check internal node references use `{{ scope ?? '' }}`

---

## Quick Reference

### Scope Parameter

| Fragment | scope controls | Writes to |
|----------|----------------|-----------|
| Loader | Output prefix | `{scope}model_output`, etc. |
| Processor | Input prefix | Main (`latent_output`, etc.) |
| Feature | Model input prefix | Main (`image_output`) |
| Terminal | N/A | N/A |

### Common Patterns

```json
// Main pipeline fragment (no scope)
{ "fragment": "sampler.sbn", "parameters": { "cfg": 7, ... } }

// Scoped loader
{ "fragment": "load-checkpoint.sbn", "parameters": { "scope": "detailer_", ... } }

// Feature using scoped models
{ "fragment": "detailer-core.sbn", "parameters": { "scope": "detailer_", ... } }

// Terminal (always reads main)
{ "fragment": "save.sbn", "parameters": {} }
```

### Scriban Filters and Operators

| Filter/Operator | Usage | Example |
|-----------------|-------|---------|
| `json` | Escape strings for JSON output | `{{ Prompt \| json }}` |
| `??` | Default value (null-coalescing) | `{{ Model ?? "default.safetensors" }}` |
| `math.round` | Round numbers | `{{ Steps \| math.divided_by 4 \| math.round }}` |

#### Default Values Convention

**Always use the `??` operator for default values, NOT the `| default:` filter.**

```scriban
// ? CORRECT - Use ?? operator
{{ my_param ?? "default_value" | json }}

// ? WRONG - Do not use | default: filter
{{ my_param | default: "default_value" | json }}
```

The `??` operator is the proper Scriban null-coalescing operator and should be applied before the `| json` filter.

**Examples:**
```scriban
// String default
"model": {{ model_name ?? "model.safetensors" | json }}

// Numeric default
"steps": {{ steps ?? 20 | json }}

// Chained defaults (fallback chain)
"checkpoint": {{ Detailer.Checkpoint ?? Model ?? "default.safetensors" | json }}

// Empty string default
"scope": {{ scope ?? "" }}
```

---

## Related Files

| File | Purpose |
|------|---------|
| `BlazorWebApp/Models/Workflow.cs` | Workflow and WorkflowAsset classes |
| `BlazorWebApp/Services/WorkflowService.cs` | Template parsing and composition |
| `BlazorWebApp/Services/ManagerService.cs` | Asset get/set methods |
| `BlazorWebApp/Components/Shared/WorkflowAssetSelector.razor` | UI component |
| `BlazorWebApp/Workflows/Templates/` | Workflow template files |
| `BlazorWebApp/Workflows/Fragments/` | Reusable fragment files |
| `BlazorWebApp/Workflows/Logs/` | Workflow conversion documentation |

---

*Document version: 5.3*
*Last updated: Added Scriban default value convention (use ?? not | default:)*
