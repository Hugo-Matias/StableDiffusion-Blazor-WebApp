# Fragment UI Schema Guide

This guide documents the complete workflow template and fragment system, including the UI schema format used in fragment `#meta` blocks. It serves as the primary reference for creating new workflow templates and understanding the data flow from Scriban templates to the Generate page.

**Last Updated:** Phase 12 - Service Cleanup &amp; Optimization

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Data Flow Summary](#data-flow-summary)
3. [Workflow Template Structure](#workflow-template-structure)
4. [Fragment Structure](#fragment-structure)
5. [UI Schema Reference](#ui-schema-reference)
6. [FragmentType Enum](#fragmenttype-enum)
7. [Service Responsibilities](#service-responsibilities)
8. [Component Registry](#component-registry)
9. [Default Value Resolution](#default-value-resolution)
10. [Complete Examples](#complete-examples)
11. [Creating New Features](#creating-new-features)
12. [Known Issues &amp; Future Improvements](#known-issues--future-improvements)

---

## Architecture Overview

```
+-----------------------------------------------------------------+
|                   Workflow Template (.sbn)                      |
|  +---------+ +----------+ +-----------------------------------+ |
|  | Assets  | | Sources  | |           Pipeline[]              | |
|  | (models)| |(img/vid) | |  id, fragment, parameters(def)   | |
|  +---------+ +----------+ +-----------------------------------+ |
+-----------------------------------------------------------------+
                              |
         +--------------------+--------------------+
         |                    |                    |
         v                    v                    v
+--------------------+ +--------------------+ +--------------------+
|  Fragment #meta   | |  Fragment #meta   | |  Fragment #meta   |
|  +--------------+ | |  +--------------+ | |  +--------------+ |
|  | type        | | |  | type        | | |  | (no ui)     | |
|  | outputs     | | |  | outputs     | | |  | outputs     | |
|  | conditions  | | |  | conditions  | | |  |             | |
|  | ui: {...}   | | |  | ui: {...}   | | |  |             | |
|  +--------------+ | +--------------------+ +--------------------+
| (designed comp)  |  (dynamic fields)      (utility fragment)
+--------------------+
         |                    |
         v                    v
+-----------------------------------------------------------------+
|                    GenerationParameters                          |
|  +---------------+ +---------------+ +---------------+ +------+ |
|  |  Fragments    | |    Assets     | |   Sources     | | Loras| |
|  | Dict<id,val>  | | Dict<id,val>  | | Dict<id,val>  | | List | |
|  +---------------+ +---------------+ +---------------+ +------+ |
+-----------------------------------------------------------------+
                              |
                              v
+-----------------------------------------------------------------+
|                    ImageService / RouterService                  |
|  GenerationParameters -> ComfyUI Workflow API -> Generated Output |
+-----------------------------------------------------------------+
```

### Key Principles

| Principle | Description |
|-----------|-------------|
| **Single Source of Truth** | Fragment defines both node JSON and UI schema |
| **Template Owns Defaults** | Pipeline `parameters` provide default values, not the schema |
| **Schema Owns Constraints** | Min/max/step live in schema, not AppSettings |
| **Type-Based Discovery** | `FragmentType` enum replaces string heuristics |
| **Hybrid Rendering** | Designed components for mature nodes, dynamic fields for experimental |

---

## Data Flow Summary

### 1. Workflow Loading (Application Start)

```
WorkflowService.GetWorkflows()
    +-- For each .sbn in Templates/
        |-- ParseWorkflowTemplate() -> Workflow object
        |-- ParseAssetsFromTemplate() -> workflow.Assets
        |-- ParseSourcesFromTemplate() -> workflow.Sources
        +-- ParsePipelineFromTemplate() -> workflow.Pipeline (IDs + fragment refs)
```

### 2. Workflow Selection (User navigates to /generate/{id})

```
Generate.razor.OnWorkflowSelected()
    +-- GenerationParameterService.InitializeFromWorkflow(workflow)
        |-- Clear existing Fragments, Assets, Sources
        |-- Initialize Assets from workflow.Assets[].DefaultValue
        |-- Initialize Sources from workflow.Sources[]
        +-- InitializeFragmentsFromPipeline(workflow)
            +-- WorkflowService.GetPipelineSteps(workflow) [CACHED]
                +-- For each step:
                    |-- Create FragmentParameters
                    |-- Priority 1: step.DefaultValues (from template)
                    |-- Priority 2: ParseFragmentDefaults(fragmentFile)
                    +-- Set IsActive = !schema.DefaultCollapsed
```

### 3. Fragment Discovery (Generate.razor)

```
DiscoverFragments()
    +-- For each Parameters.Fragments:
        |-- WorkflowService.GetFragmentSchema(fragmentFile) [CACHED]
        +-- Switch on schema.Type:
            |-- FragmentType.Sampler -> _samplerFragmentId
            |-- FragmentType.Latent -> _latentFragmentId
            |-- FragmentType.Prompts -> _promptsFragmentId
            +-- FragmentType.Enhancement -> optional fragments
```

### 4. Generation (User clicks Generate)

```
ImageService.GenerateImagesAsync(parameters, workflow)
    |-- BuildLegacyParametersFromGenerationParams() [TEMPORARY - Phase 10 removes]
    |   +-- Extract values from fragments -> SharedParameters
    |-- RouterService.PostTxt2Img(legacyParams)
    |   +-- ComfyUIService.PostTxt2Img(dto, clientId, workflow)
    |       +-- WorkflowService.ComposeWorkflowFromTemplate(workflow, dto)
    |           +-- For each Pipeline step:
    |               |-- Merge globalParams + step.parameters
    |               |-- RenderFragment(fragmentText, context)
    |               |   |-- Extract #meta block
    |               |   |-- EvaluateConditions()
    |               |   +-- Render Scriban template
    |               +-- composer.AddRenderedFragment()
    +-- SaveImages() -> Database
```

---

## Workflow Template Structure

Workflow templates are Scriban files (`.sbn`) in `Workflows/Templates/`.

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `Title` | string | Display name in UI |
| `Base` | enum | Model base: `Flux`, `SD`, `ZImage`, `Wan`, `Qwen` |
| `Mode` | enum | Generation mode: `txt2img`, `img2img`, `img2vid` |
| `Pipeline` | array | Ordered list of fragment steps |

### Optional Fields

| Field | Type | Description |
|-------|------|-------------|
| `Assets` | array | Model/resource selections |
| `Sources` | array | Input images/videos required |

### Complete Template Example

```json
{
  "Title": "Txt2Img",
  "Base": "ZImage",
  "Mode": "txt2img",
  "Assets": [
    { 
      "parameter": "Model", 
      "label": "Model", 
      "type": "DiffusionModel", 
      "default": "z_image_turbo.safetensors", 
      "order": 1, 
      "columnSize": 4 
    },
    { 
      "parameter": "Clip", 
      "label": "CLIP", 
      "type": "Clip", 
      "default": "qwen_3_4b.safetensors", 
      "order": 2, 
      "columnSize": 4 
    },
    { 
      "parameter": "Vae", 
      "label": "VAE", 
      "type": "Vae", 
      "default": "ae.safetensors", 
      "order": 3, 
      "columnSize": 4 
    }
  ],
  "Sources": [
    { 
      "id": "source_image", 
      "label": "Input Image", 
      "type": "image", 
      "required": true,
      "parameter": "Image" 
    }
  ],
  "Pipeline": [
    {
      "id": "loader_zimage",
      "fragment": "load-diffusion.sbn",
      "parameters": {
        "unet_name": {{ Model | json }},
        "clip_name": {{ Clip | json }},
        "clip_type": "lumina2",
        "vae_name": {{ Vae | json }}
      }
    },
    {
      "id": "latent",
      "fragment": "empty-latent.sbn",
      "parameters": {
        "width": {{ Width ?? 872 | json }},
        "height": {{ Height ?? 1248 | json }},
        "batch_size": {{ BatchSize ?? 1 | json }},
        "latent_class": "EmptySD3LatentImage"
      }
    },
    {
      "id": "prompts",
      "fragment": "prompts.sbn",
      "parameters": {
        "positive": {{ Prompt | json }},
        "negative": {{ NegativePrompt | json }}
      }
    },
    {
      "id": "main_sampler",
      "fragment": "sampler.sbn",
      "parameters": {
        "sampler_id": "sampler_main",
        "sampler_name": {{ SamplerName ?? "euler" | json }},
        "scheduler": {{ Scheduler ?? "simple" | json }},
        "steps": {{ Steps ?? 9 | json }},
        "cfg": {{ CfgScale ?? 1 | json }},
        "seed": {{ Seed ?? 42 | json }}
      }
    },
    {
      "id": "vae_decode",
      "fragment": "vae-decode.sbn",
      "parameters": {}
    },
    {
      "id": "save",
      "fragment": "save.sbn",
      "parameters": {}
    }
  ]
}
```

### Asset Types

| Type | Description | ComfyUI Model Path |
|------|-------------|-------------------|
| `DiffusionModel` | UNET/DiT models | `diffusion_models/` |
| `Checkpoint` | Full checkpoint | `checkpoints/` |
| `Clip` | Text encoder | `text_encoders/` |
| `Vae` | VAE model | `vae/` |
| `Lora` | LoRA adapter | `loras/` |
| `ControlNet` | ControlNet model | `controlnet/` |
| `Upscaler` | Upscale model | `upscale_models/` |

### Source Definition

Sources define input images/videos for img2img/img2vid workflows:

```json
{
  "id": "source_image",
  "label": "Input Image",
  "type": "image",
  "required": true,
  "parameter": "Image"
}
```

- `id`: Unique ID, used as key in `GenerationParameters.Sources`
- `label`: Display label in UI
- `type`: `"image"` or `"video"`
- `required`: Whether generation requires this input
- `parameter`: Maps to template variable (e.g., `{{ Image | json }}`)

---

## Fragment Structure

Fragments are reusable Scriban files in `Workflows/Fragments/`.

### Complete Fragment Example

```scriban
#meta
{
  "outputs": {
    "latent_output": {"node": "{{ sampler_id }}", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.{{ sampler_id }}.IsActive"]
  },
  "ui": {
    "type": "sampler",
    "component": "SamplerForm",
    "title": "Sampler",
    "icon": "fa-solid fa-dice",
    "order": 50,
    "collapsible": true,
    "chainable": true,
    "parameters": {
      "sampler_name": { "source": "KSampler", "input_name": "sampler_name" },
      "scheduler": { "source": "KSampler", "input_name": "scheduler" },
      "steps": { "min": 1, "max": 150, "step": 1 },
      "cfg": { "min": 1, "max": 30, "step": 0.5 },
      "seed": { "min": -1 }
    }
  }
}
#end

{
  "{{ sampler_id }}": {
    "inputs": {
      "sampler_name": {{ sampler_name | json }},
      "scheduler": {{ scheduler | json }},
      "steps": {{ steps | json }},
      "cfg": {{ cfg | json }},
      "seed": {{ seed | json }},
      "model": {{ get_ref ((scope ?? "") + "model_output") }},
      "positive": {{ get_ref ((scope ?? "") + "positive_output") }},
      "negative": {{ get_ref ((scope ?? "") + "negative_output") }},
      "latent_image": {{ get_ref ((scope ?? "") + "latent_output") }}
    },
    "class_type": "KSampler",
    "_meta": {
      "title": {{ title ?? "Sampler" | json }}
    }
  }
}
```

### #meta Block Properties

| Property | Required | Description |
|----------|----------|-------------|
| `outputs` | Yes | Maps output names to node references |
| `conditions` | No | Conditional inclusion rules |
| `ui` | No | UI rendering configuration |

### outputs Object

Registers fragment outputs for `get_ref()` function:

```json
"outputs": {
  "model_output": { "node": "unet_loader", "index": 0 },
  "clip_output": { "node": "clip_loader", "index": 0 }
}
```

- `node`: The node ID within this fragment
- `index`: Output slot index (usually 0)

### conditions Object

Controls fragment inclusion based on runtime state:

```json
"conditions": {
  "required": ["Fragments.seed_vr2.IsActive", "SeedVR2.IsActive"],
  "excluded_if": ["DisableUpscale"]
}
```

- `required`: ALL conditions must be true for fragment to render
- `excluded_if`: ANY condition being true excludes the fragment

### Scriban Functions Available

| Function | Usage | Description |
|----------|-------|-------------|
| `json` | `{{ value \| json }}` | Serializes value to JSON |
| `get_ref` | `{{ get_ref "output_name" }}` | Gets `[nodeId, index]` array |

---

## UI Schema Reference

The `ui` object in `#meta` defines how the fragment renders in the Generate page.

### UI Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `type` | string | `"unknown"` | Fragment purpose (see [FragmentType](#fragmenttype-enum)) |
| `component` | string | `null` | Designed component name, or null for dynamic |
| `title` | string | Required | Display title |
| `icon` | string | `null` | FontAwesome icon class |
| `collapsible` | bool | `true` | Can be collapsed |
| `defaultCollapsed` | bool | `false` | Initial state; if `true`, fragment starts inactive |
| `chainable` | bool | `false` | Multiple instances allowed |
| `order` | int | `100` | Display order (lower = higher) |
| `parameters` | object | `{}` | Constraints for designed components |
| `fields` | array | `null` | Field definitions for dynamic rendering |

### Order Ranges

| Range | Purpose | Examples |
|-------|---------|----------|
| 0-49 | Core inputs | Prompts (10), LoRAs (15) |
| 50-99 | Primary generation | Latent (20), Sampler (50) |
| 100-149 | Enhancement | Upscale (100), Detailer (120) |
| 150+ | Advanced/experimental | Custom nodes |

### Dynamic Source Configuration

For select fields that query ComfyUI:

```json
"parameters": {
  "sampler_name": { 
    "source": "KSampler",
    "input_name": "sampler_name"
  }
}
```

- `source`: ComfyUI node class_type to query
- `input_name`: The node input field name

The service calls ComfyUI's `/object_info/{source}` API and extracts options from `input.required.{input_name}` or `input.optional.{input_name}`.

---

## FragmentType Enum

Added in Phase 12 for schema-based fragment discovery.

```csharp
public enum FragmentType
{
    Unknown = 0,     // Default
    Loader,          // Model loading (no direct UI)
    Prompts,         // Positive/negative prompts
    Latent,          // Resolution/latent image settings
    Sampler,         // KSampler, sampling settings
    Conditioning,    // CLIP text encode, conditioning
    Enhancement,     // Upscale, detailer, etc.
    Output,          // Save, preview nodes
    Utility          // Helper fragments with no UI
}
```

### Usage in Fragment Schema

```json
"ui": {
  "type": "sampler",
  "component": "SamplerForm",
  ...
}
```

### Discovery in Generate.razor

```csharp
switch (schema.Type)
{
    case FragmentType.Sampler:
        _samplerFragmentId ??= fragmentId;
        break;
    case FragmentType.Latent:
        _latentFragmentId ??= fragmentId;
        break;
    case FragmentType.Enhancement:
        // Renders as optional collapsible section
        break;
}
```

---

## Service Responsibilities

### WorkflowService

**Responsibility:** Template parsing, fragment loading, workflow composition

| Method | Purpose |
|--------|---------|
| `GetWorkflows()` | Load all workflow templates from disk |
| `RefreshWorkflows()` | Reload templates, clear caches |
| `ComposeWorkflowFromTemplate()` | Render complete ComfyUI workflow JSON |
| `ParseFragmentSchema()` | Extract UI schema from fragment |
| `GetFragmentSchema()` | Get cached schema for fragment file |
| `ParsePipelineSteps()` | Extract pipeline steps from template |
| `GetPipelineSteps()` | Get cached pipeline steps for workflow |
| `ParseFragmentDefaults()` | Extract default values from fragment body |
| `ClearSchemaCache()` | Invalidate schema cache |
| `ClearPipelineCache()` | Invalidate pipeline cache |

### GenerationParameterService

**Responsibility:** Runtime parameter state management

| Method | Purpose |
|--------|---------|
| `InitializeFromWorkflow()` | Set up parameters from workflow template |
| `SetFragmentValue()` | Update a fragment parameter value |
| `SetFragmentActive()` | Enable/disable a fragment |
| `GetFragmentValue&lt;T&gt;()` | Read a typed parameter value |
| `ResolveSourceOptionsAsync()` | Query ComfyUI for dynamic select options |
| `CreateSnapshot()` | Clone current state for persistence |
| `LoadParameters()` | Restore state from snapshot |

### ImageService

**Responsibility:** Generation orchestration, file saving, database persistence

| Method | Purpose |
|--------|---------|
| `GenerateImagesAsync()` | New unified image generation |
| `GenerateVideoAsync()` | New unified video generation |
| `GetImages()` | Legacy image generation (to be removed) |
| `GetVideo()` | Legacy video generation (to be removed) |
| `SaveImages()` | Save to disk and database |

### RouterService

**Responsibility:** Route generation requests to ComfyUI

| Method | Purpose |
|--------|---------|
| `PostTxt2Img()` | Route text-to-image request |
| `PostImg2Img()` | Route image-to-image request |
| `PostImg2Vid()` | Route image-to-video request |

---

## Component Registry

Designed components are registered in `ComponentRegistry.cs`:

```csharp
private readonly Dictionary<string, Type> _components = new()
{
    ["PromptsForm"] = typeof(PromptsForm),
    ["SamplerForm"] = typeof(SamplerForm),
    ["LatentForm"] = typeof(LatentForm),
    ["LoraForm"] = typeof(LoraForm),
    ["SeedVR2Form"] = typeof(SeedVR2Form),
    ["DetailerForm"] = typeof(DetailerForm),
    ["ConditioningVariationForm"] = typeof(ConditioningVariationForm),
    ["SeedVarianceEnhancerForm"] = typeof(SeedVarianceEnhancerForm),
};
```

### Component Naming Convention

- Match fragment file: `sampler.sbn` &rarr; `SamplerForm`
- PascalCase with `Form` suffix
- Located in `Components/Shared/Generation/Fragments/`

---

## Default Value Resolution

**Priority Order** (documented in `IGenerationParameterService`):

When initializing fragment parameters, values are resolved in this strict priority order. **Higher priority overrides lower priority.**

| Priority | Source | Description | Persisted |
|----------|--------|-------------|-----------|
| **1 (Highest)** | Saved Workflow State | Previously saved parameters for this workflow (database) | Yes |
| **2** | Pipeline Step Parameters | Values from workflow template's Pipeline step `parameters` block | No |
| **3** | Schema Defaults | Values from fragment `#meta.ui.parameters.*.default` | No |
| **4** | Dynamic Source Resolution | First option from ComfyUI API query (for fields with `source`) | No |

### Important Conventions

1. **Template Pipeline is the primary default source**
   - Each workflow template provides its own defaults in the Pipeline's `parameters` block
   - This allows the same fragment to have different defaults per workflow

2. **Schema defaults are fallbacks**
   - The `#meta.ui.parameters.*.default` provides a fallback if Pipeline doesn't specify
   - Good for rarely-changed values that are consistent across workflows

3. **Fragment body defaults are NOT used for initialization**
   - The `{{ param ?? "default" | json }}` syntax in fragment bodies is ONLY for Scriban rendering fallback
   - Do NOT rely on these for UI initialization - they only apply during template rendering
   - This prevents dual-source confusion

4. **Dynamic sources set defaults during initialization**
   - When `InitializeFromWorkflowAsync()` runs, dynamic sources are pre-resolved
   - If no value is set from priorities 1-3, the first option from ComfyUI API is used
   - Pre-resolved options are cached in `FragmentParameters.ResolvedOptions` for sync UI access

### Example Priority Resolution

```
Fragment: sampler.sbn
Parameter: steps

Priority 1: Database saved state has steps=35 ? Use 35 ?
Priority 2: (skipped)
Priority 3: (skipped)

---

Fragment: sampler.sbn  
Parameter: steps (no saved state)

Priority 1: No saved state
Priority 2: Pipeline has steps={{ Steps ?? 20 | json }} ? Use 20 ?
Priority 3: (skipped)

---

Fragment: sampler.sbn
Parameter: sampler_name (no saved state, no pipeline value)

Priority 1: No saved state  
Priority 2: No pipeline value
Priority 3: Schema has "sampler_name": { "default": "euler" } ? Use "euler" ?

---

Fragment: sampler.sbn
Parameter: sampler_name (no saved/pipeline/schema value)

Priority 1-3: None
Priority 4: ComfyUI returns ["euler", "euler_ancestral", ...] ? Use "euler" ?
```

### Why Dynamic Options in UI?

- `InitializeFromWorkflow()` is synchronous for simplicity
- ComfyUI API calls are async
- UI components handle async naturally in `OnInitializedAsync()`

---

## Complete Examples

### Example 1: Utility Fragment (No UI)

```scriban
#meta
{
  "outputs": {
    "model_output": { "node": "unet_loader", "index": 0 },
    "clip_output": { "node": "clip_loader", "index": 0 },
    "vae_output": { "node": "vae_loader", "index": 0 }
  }
}
#end

{
  "unet_loader": {
    "inputs": {
      "unet_name": {{ unet_name | json }},
      "weight_dtype": "default"
    },
    "class_type": "UNETLoader",
    "_meta": { "title": "Load Diffusion Model" }
  },
  ...
}
```

### Example 2: Enhancement Fragment (Optional)

```scriban
#meta
{
  "outputs": {
    "image_output": { "node": "seedvr2_upscaler", "index": 0 }
  },
  "conditions": {
    "required": ["SeedVR2.IsActive"]
  },
  "ui": {
    "type": "enhancement",
    "component": "SeedVR2Form",
    "title": "SeedVR2 Upscale",
    "icon": "fa-solid fa-expand",
    "order": 100,
    "collapsible": true,
    "defaultCollapsed": true,
    "parameters": {
      "seedvr2_model": { "source": "SeedVR2LoadDiTModel", "input_name": "model" },
      "seedvr2_resolution": { "min": 512, "max": 4096, "step": 64 }
    }
  }
}
#end
...
```

### Example 3: Dynamic Fields (No Designed Component)

```json
"ui": {
  "type": "enhancement",
  "component": null,
  "title": "Experimental Feature",
  "collapsible": true,
  "fields": [
    {
      "parameter": "strength",
      "label": "Effect Strength",
      "type": "slider",
      "min": 0,
      "max": 1,
      "step": 0.01,
      "column": 6
    },
    {
      "parameter": "mode",
      "label": "Mode",
      "type": "select",
      "options": ["fast", "quality"],
      "column": 6
    }
  ]
}
```

---

## Creating New Features

### Adding a New Node (2-3 files)

1. **Create the fragment** (`Workflows/Fragments/my-node.sbn`):
   ```scriban
   #meta
   {
     "outputs": {
       "image_output": { "node": "my_node", "index": 0 }
     },
     "conditions": {
       "required": ["MyNode.IsActive"]
     },
     "ui": {
       "type": "enhancement",
       "component": null,
       "title": "My New Node",
       "collapsible": true,
       "defaultCollapsed": true,
       "fields": [
         { "parameter": "strength", "label": "Strength", "type": "slider", "min": 0, "max": 1, "step": 0.01, "column": 6 }
       ]
     }
   }
   #end
   
   {
     "my_node": {
       "inputs": {
         "strength": {{ strength ?? 0.5 | json }},
         "image": {{ get_ref "image_output" }}
       },
       "class_type": "MyCustomNode",
       "_meta": { "title": "My Node" }
     }
   }
   ```

2. **Add to workflow template** (`Workflows/Templates/.../txt2img.sbn`):
   ```json
   {
     "id": "my_node",
     "fragment": "my-node.sbn",
     "parameters": {
       "strength": {{ MyNode.Strength ?? 0.5 | json }}
     }
   }
   ```

3. **(Optional) Create designed component** if complex UI needed:
   - Create `MyNodeForm.razor`
   - Register in `ComponentRegistry.cs`
   - Update fragment: `"component": "MyNodeForm"`

### Upgrading to Designed Component

When a node matures and needs custom UI:

1. Create `Components/Shared/Generation/Fragments/MyNodeForm.razor`
2. Register in `ComponentRegistry.cs`
3. Update fragment schema: `"component": "MyNodeForm"`
4. Move field definitions to `"parameters"` object with constraints

---

## Known Issues &amp; Future Improvements

### Current Limitations

| Issue | Impact | Planned Resolution |
|-------|--------|-------------------|
| Legacy *Parameters classes | Tight coupling | Phase 10: Remove entirely |
| ImageService builds legacy DTOs | Extra conversion layer | Phase 10: RouterService accepts GenerationParameters |
| Dynamic options require async | Fallback in UI, not service | Acceptable trade-off |
| Some hardcoded fragment IDs | `"prompts"`, `"main_sampler"` | Phase 8: Use FragmentType discovery |

### Phase 10: Legacy Deprecation

Will remove:
- `Txt2ImgParameters`, `Img2ImgParameters`, `Img2VidParameters`
- `Txt2ImgComfyUI`, `Img2ImgComfyUI`, `Img2VidComfyUI`
- `SharedParameters`
- `ParameterMapper.cs`
- Legacy generation pages

### Abstraction Improvements Needed

1. **Generate.razor hardcoding** - Still has some hardcoded fragment ID lookups
   - Solution: Use `FragmentType` enum exclusively

2. **ImageService legacy bridge** - `BuildLegacyParametersFromGenerationParams()`
   - Solution: Phase 10 removes this entirely

3. **Sampler/Scheduler sources** - Still use `Backend.Samplers` magic strings
   - Solution: Standardize to ComfyUI node queries

### State Improvements Needed

1. **Async initialization** - Consider making `InitializeFromWorkflow` async
   - Would allow dynamic option resolution in service

2. **State versioning** - No version number in persisted state
   - Could cause issues on schema changes

### Isolation Improvements Needed

1. **RouterService still uses typed DTOs**
   - Solution: Phase 10 - Accept `GenerationParameters` directly

2. **ImageService knows about fragment IDs**
   - Some coupling via `GetFragment("prompts")` etc.
   - Acceptable for now, could be abstracted later

---

## Quick Reference

### Fragment #meta Template

```json
#meta
{
  "outputs": {
    "output_name": { "node": "node_id", "index": 0 }
  },
  "conditions": {
    "required": ["FeatureName.IsActive"]
  },
  "ui": {
    "type": "enhancement",
    "component": "ComponentName",
    "title": "Display Title",
    "icon": "fa-solid fa-icon",
    "order": 100,
    "collapsible": true,
    "defaultCollapsed": true,
    "chainable": false,
    "parameters": {
      "param_name": { "min": 0, "max": 100, "step": 1 }
    }
  }
}
#end
```

### FragmentType Values

| Type | Use For |
|------|---------|
| `loader` | Model loading fragments |
| `prompts` | Prompt encoding |
| `latent` | Resolution/latent settings |
| `sampler` | Sampling settings |
| `conditioning` | CLIP/conditioning |
| `enhancement` | Upscale/detailer/optional features |
| `output` | Save/preview nodes |
| `utility` | Helper fragments, no UI |

### Source Reference Formats

| Format | Example | Description |
|--------|---------|-------------|
| Node query | `"source": "KSampler", "input_name": "sampler_name"` | Query ComfyUI object_info |
| Static | `"options": ["a", "b", "c"]` | Hardcoded options |

---

*This guide is the authoritative reference for the workflow template and fragment system. Update this document when making architectural changes.*
