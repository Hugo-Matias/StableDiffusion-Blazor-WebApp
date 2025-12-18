# Fragment UI Schema Guide

This guide documents the UI schema format used in fragment `#meta` blocks to define how fragments render in the generation page.

---

## Table of Contents

1. [Overview](#overview)
2. [Schema Structure](#schema-structure)
3. [UI Object Properties](#ui-object-properties)
4. [Parameters Object (Designed Components)](#parameters-object-designed-components)
5. [Fields Array (Dynamic Rendering)](#fields-array-dynamic-rendering)
6. [Field Types Reference](#field-types-reference)
7. [Source References](#source-references)
8. [Component Registry](#component-registry)
9. [Examples](#examples)
10. [Migration Guide](#migration-guide)

---

## Overview

The UI schema enables workflow-driven component rendering. Each fragment declares:
- **What component** renders it (or dynamic fields as fallback)
- **Constraints** for parameters (min, max, step)
- **Behavior flags** (collapsible, chainable)
- **Data sources** for select fields

### Key Principles

| Principle | Description |
|-----------|-------------|
| **Single Source of Truth** | Fragment defines both node JSON and UI schema |
| **Hybrid Rendering** | Designed components for mature nodes, dynamic fields for new/experimental |
| **Template Owns Defaults** | Pipeline `parameters` provide default values, not the schema |
| **Schema Owns Constraints** | Min/max/step live in schema, not AppSettings |

---

## Schema Structure

The UI schema lives in the fragment's `#meta` block under the `ui` property:

```
#meta
{
  "outputs": { ... },
  "conditions": { ... },
  "ui": {
    // UI schema goes here
  }
}
#end

// Fragment node JSON below...
```

### Complete Schema Structure

```json
{
  "ui": {
    "component": "ComponentName | null",
    "title": "Display Title",
    "icon": "fa-solid fa-icon-name",
    "collapsible": true,
    "defaultCollapsed": false,
    "chainable": false,
    "order": 100,
    
    // For designed components:
    "parameters": {
      "param_name": { 
        "min": 0, 
        "max": 100, 
        "step": 1,
        "source": "Backend.Samplers"
      }
    },
    
    // For dynamic rendering (when component is null):
    "fields": [
      { "parameter": "...", "type": "...", ... }
    ]
  }
}
```

---

## UI Object Properties

### Required Properties

| Property | Type | Description |
|----------|------|-------------|
| `title` | string | Display title shown in the UI |

### Optional Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `component` | string \| null | null | Blazor component name. If null, uses dynamic fields |
| `icon` | string | null | FontAwesome icon class for the header |
| `collapsible` | boolean | true | Whether the form section can collapse |
| `defaultCollapsed` | boolean | false | Initial collapsed state |
| `chainable` | boolean | false | Whether multiple instances can be added |
| `order` | integer | 100 | Display order (lower = higher) |
| `parameters` | object | {} | Parameter constraints for designed components |
| `fields` | array | [] | Field definitions for dynamic rendering |

### Behavior Flags

#### `collapsible`
When true, renders with an expand/collapse header. Recommended for optional or advanced features.

#### `chainable`
When true, shows "Add Another" button. Used for samplers, detailers, ControlNets, etc.
Each instance gets a unique ID: `main_sampler`, `refiner_sampler`, etc.

#### `order`
Controls display order in the parameters panel. Suggested ranges:
- 0-49: Core inputs (prompts, sources)
- 50-99: Primary generation (sampler, resolution)
- 100-149: Enhancement (upscale, detailer)
- 150+: Advanced/experimental

---

## Parameters Object (Designed Components)

When using a designed component (`component` is not null), the `parameters` object provides constraints that the component reads at runtime.

### Structure

```json
"parameters": {
  "parameter_name": {
    "min": number,
    "max": number,
    "step": number,
    "source": "string"
  }
}
```

### Properties

| Property | Type | Used By | Description |
|----------|------|---------|-------------|
| `min` | number | slider, numeric | Minimum allowed value |
| `max` | number | slider, numeric | Maximum allowed value |
| `step` | number | slider, numeric | Increment step |
| `source` | string | select | Data source reference (see [Source References](#source-references)) |

### Example

```json
"parameters": {
  "steps": { "min": 1, "max": 150, "step": 1 },
  "cfg": { "min": 1, "max": 30, "step": 0.5 },
  "sampler_name": { "source": "Backend.Samplers" },
  "scheduler": { "source": "Backend.Schedulers" },
  "seed": { "min": -1 }
}
```

### Component Usage

The designed component receives the schema and reads constraints:

```razor
@* SamplerForm.razor *@
<MudSlider T="int" 
           @bind-Value="Values.Steps"
           Min="@Schema.Parameters["steps"].Min"
           Max="@Schema.Parameters["steps"].Max"
           Step="@Schema.Parameters["steps"].Step">
    Steps: @Values.Steps
</MudSlider>
```

---

## Fields Array (Dynamic Rendering)

When `component` is null, the `fields` array defines what UI elements to render dynamically.

### Structure

```json
"fields": [
  {
    "parameter": "string",      // Required: maps to fragment parameter
    "label": "string",          // Required: display label
    "type": "string",           // Required: field type
    "column": 6,                // Optional: grid column width (1-12)
    // Type-specific properties...
  }
]
```

### Common Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `parameter` | string | ? | Fragment parameter name |
| `label` | string | ? | Display label |
| `type` | string | ? | Field type (see [Field Types](#field-types-reference)) |
| `column` | integer | | Grid column width (1-12), default 6 |
| `tooltip` | string | | Help text shown on hover |
| `visible` | string | | Condition expression for visibility |

---

## Field Types Reference

### `slider`

Numeric slider with min/max range.

```json
{
  "parameter": "steps",
  "label": "Steps",
  "type": "slider",
  "min": 1,
  "max": 150,
  "step": 1,
  "column": 6
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `min` | number | ? | Minimum value |
| `max` | number | ? | Maximum value |
| `step` | number | | Increment step, default 1 |

---

### `numeric`

Numeric input field.

```json
{
  "parameter": "seed",
  "label": "Seed",
  "type": "numeric",
  "min": -1,
  "column": 6
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `min` | number | | Minimum value |
| `max` | number | | Maximum value |

---

### `seed`

Specialized seed input with randomize/restore buttons.

```json
{
  "parameter": "seed",
  "label": "Seed",
  "type": "seed",
  "column": 6
}
```

Renders a numeric field with -1 minimum and action buttons.

---

### `select`

Dropdown selection.

```json
{
  "parameter": "sampler_name",
  "label": "Sampler",
  "type": "select",
  "source": "Backend.Samplers",
  "column": 6
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `source` | string | ?* | Data source reference |
| `options` | array | ?* | Static options array |

*One of `source` or `options` is required.

**Static options:**
```json
{
  "parameter": "mode",
  "label": "Mode",
  "type": "select",
  "options": ["fast", "quality", "balanced"],
  "column": 6
}
```

---

### `text`

Single-line text input.

```json
{
  "parameter": "prefix",
  "label": "Filename Prefix",
  "type": "text",
  "column": 12
}
```

---

### `textarea`

Multi-line text input.

```json
{
  "parameter": "prompt",
  "label": "Prompt",
  "type": "textarea",
  "rows": 4,
  "column": 12
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `rows` | integer | | Number of visible rows, default 3 |

---

### `checkbox`

Boolean toggle.

```json
{
  "parameter": "log_to_console",
  "label": "Log to Console",
  "type": "checkbox",
  "column": 6
}
```

---

### `switch`

Boolean toggle (alternative style).

```json
{
  "parameter": "enabled",
  "label": "Enable Feature",
  "type": "switch",
  "column": 12
}
```

---

### `color`

Color picker.

```json
{
  "parameter": "tint_color",
  "label": "Tint Color",
  "type": "color",
  "column": 6
}
```

---

### `file`

File selection (for models, images, etc.).

```json
{
  "parameter": "model",
  "label": "Model",
  "type": "file",
  "source": "Backend.Upscalers",
  "column": 6
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `source` | string | ? | Asset type source |

---

### `resolution`

Paired width/height sliders with aspect ratio controls.

```json
{
  "parameter": "resolution",
  "label": "Resolution",
  "type": "resolution",
  "min": 64,
  "max": 4096,
  "step": 8,
  "column": 12
}
```

Maps to two values: `{parameter}_width` and `{parameter}_height`.

---

### `group`

Visual grouping of related fields (no parameter).

```json
{
  "type": "group",
  "label": "Advanced Settings",
  "collapsible": true,
  "fields": [
    { "parameter": "...", ... },
    { "parameter": "...", ... }
  ]
}
```

---

## Source References

Source references link select fields to data from services.

### Format

```
ServiceName.PropertyName
```

### Available Sources

| Source | Description |
|--------|-------------|
| `Backend.Samplers` | List of available samplers |
| `Backend.Schedulers` | List of available schedulers |
| `Backend.Upscalers` | List of upscale models |
| `Backend.Models` | List of checkpoint models |
| `Backend.Vaes` | List of VAE models |
| `Backend.Clips` | List of CLIP models |
| `Backend.Loras` | List of LoRA models |
| `Backend.ControlNets` | List of ControlNet models |
| `Backend.DetectionModels` | List of detection models (YOLO, etc.) |

### Custom Sources

For fragment-specific options, use static `options` array instead of `source`.

---

## Component Registry

### Naming Conventions

- Component names match the Blazor component filename without extension
- Use PascalCase: `SamplerForm`, `DetailerForm`, `UpscaleForm`
- Suffix with `Form` for clarity

### Registered Components

| Component Name | Fragment(s) | Description |
|----------------|-------------|-------------|
| `PromptsForm` | prompts.sbn | Positive/negative prompt fields with autocomplete |
| `SamplerForm` | sampler.sbn | Sampler, scheduler, steps, CFG, seed |
| `ResolutionForm` | (embedded) | Width/height with quick presets |
| `LoraForm` | (embedded) | LoRA selection and strength |
| `UpscaleForm` | upscale.sbn, upscale-seedvr2.sbn | Upscale model and settings |
| `DetailerForm` | detailer-core.sbn | Face/hand detailer settings |
| `ConditioningVariationForm` | conditioning-variation.sbn | CV switch point |
| `SeedVarianceEnhancerForm` | seed-variance-enhancer.sbn | SVE settings |

### Registration

Components are registered in `ComponentRegistry.cs`:

```csharp
public class ComponentRegistry
{
    private readonly Dictionary<string, Type> _components = new()
    {
        ["PromptsForm"] = typeof(PromptsForm),
        ["SamplerForm"] = typeof(SamplerForm),
        // ... etc
    };
    
    public Type? GetComponent(string name) 
        => _components.GetValueOrDefault(name);
}
```

---

## Examples

### Example 1: Designed Component (Sampler)

```json
#meta
{
  "outputs": {
    "latent_output": {"node": "{{ sampler_id }}", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.{{ sampler_id }}.IsActive"]
  },
  "ui": {
    "component": "SamplerForm",
    "title": "Sampler",
    "icon": "fa-solid fa-dice",
    "collapsible": true,
    "chainable": true,
    "order": 50,
    "parameters": {
      "sampler_name": { "source": "Backend.Samplers" },
      "scheduler": { "source": "Backend.Schedulers" },
      "steps": { "min": 1, "max": 150, "step": 1 },
      "cfg": { "min": 1, "max": 30, "step": 0.5 },
      "seed": { "min": -1 }
    }
  }
}
#end
```

### Example 2: Dynamic Fields (Experimental Node)

```json
#meta
{
  "outputs": {
    "image_output": {"node": "experimental_node", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.experimental.IsActive"]
  },
  "ui": {
    "component": null,
    "title": "Experimental Feature",
    "icon": "fa-solid fa-flask",
    "collapsible": true,
    "order": 150,
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
        "label": "Processing Mode",
        "type": "select",
        "options": ["fast", "quality", "balanced"],
        "column": 6
      },
      {
        "parameter": "iterations",
        "label": "Iterations",
        "type": "numeric",
        "min": 1,
        "max": 10,
        "column": 6
      },
      {
        "parameter": "debug",
        "label": "Debug Output",
        "type": "checkbox",
        "column": 6
      }
    ]
  }
}
#end
```

### Example 3: Utility Fragment (No UI)

```json
#meta
{
  "outputs": {
    "model_output": {"node": "model_loader", "index": 0},
    "clip_output": {"node": "model_loader", "index": 1},
    "vae_output": {"node": "model_loader", "index": 2}
  }
}
#end
```

No `ui` block = no form rendered. Used for loaders, wiring, save nodes, etc.

### Example 4: Prompts Fragment

```json
#meta
{
  "outputs": {
    "positive_output": {"node": "text_positive", "index": 0},
    "negative_output": {"node": "text_negative", "index": 0}
  },
  "ui": {
    "component": "PromptsForm",
    "title": "Prompts",
    "order": 10,
    "collapsible": false,
    "parameters": {
      "positive": { },
      "negative": { }
    }
  }
}
#end
```

### Example 5: Chainable Detailer

```json
#meta
{
  "outputs": {
    "image_output": {"node": "{{ scope }}detailer", "index": 0}
  },
  "conditions": {
    "required": ["Fragments.{{ scope }}detailer.IsActive"]
  },
  "ui": {
    "component": "DetailerForm",
    "title": "Detailer",
    "icon": "fa-solid fa-face-smile",
    "collapsible": true,
    "defaultCollapsed": true,
    "chainable": true,
    "order": 120,
    "parameters": {
      "detailer_detection_model": { "source": "Backend.DetectionModels" },
      "detailer_sampler": { "source": "Backend.Samplers" },
      "detailer_scheduler": { "source": "Backend.Schedulers" },
      "detailer_steps": { "min": 1, "max": 100, "step": 1 },
      "detailer_cfg": { "min": 1, "max": 30, "step": 0.5 },
      "detailer_denoise": { "min": 0, "max": 1, "step": 0.01 },
      "detailer_seed": { "min": -1 }
    }
  }
}
#end
```

---

## Migration Guide

### From Current System to Schema-Based

#### Before (NODE_INTEGRATION_GUIDE approach)

1. Create fragment file
2. Create `*Parameters.cs` model
3. Add property to `Txt2ImgParameters.cs`
4. Add property to `Txt2ImgComfyUI.cs`
5. Update `ParameterMapper.cs`
6. Create `*Form.razor` component
7. Add to `GenerateFormTxt2Img.razor`
8. Update `ImageService.cs`

#### After (Schema-Based approach)

1. Create fragment file with `#meta.ui` schema
2. (Optional) Create designed component if complex UI needed
3. (Optional) Register component in `ComponentRegistry.cs`

**That's it!** Parameters flow automatically through `GenerationParameters.Fragments`.

### Adding a New Node

1. **Create the fragment** with complete `#meta` block:
   ```json
   #meta
   {
     "outputs": { ... },
     "conditions": { "required": ["Fragments.my_node.IsActive"] },
     "ui": {
       "component": null,
       "title": "My New Node",
       "collapsible": true,
       "fields": [ ... ]
     }
   }
   #end
   ```

2. **Add to workflow template**:
   ```json
   {
     "id": "my_node",
     "fragment": "my-node.sbn",
     "parameters": {
       "strength": 0.5,
       "mode": "quality"
     }
   }
   ```

3. **Done!** The UI renders automatically.

### Upgrading to Designed Component

When a node matures and needs custom UI:

1. Create `MyNodeForm.razor` component
2. Register in `ComponentRegistry.cs`
3. Update fragment schema: `"component": "MyNodeForm"`
4. Move field definitions to `"parameters"` object

---

## Validation Rules

### Schema Validation

The `WorkflowService` validates schemas on parse:

| Rule | Error |
|------|-------|
| `title` is required | "Fragment UI schema missing required 'title' property" |
| `fields` required when `component` is null | "Dynamic rendering requires 'fields' array" |
| Field `parameter` is required | "Field missing required 'parameter' property" |
| Field `type` is required | "Field missing required 'type' property" |
| Field `type` must be valid | "Unknown field type: {type}" |
| Slider fields need `min` and `max` | "Slider field '{parameter}' requires 'min' and 'max'" |
| Select fields need `source` or `options` | "Select field '{parameter}' requires 'source' or 'options'" |

### Runtime Validation

Components validate values against schema constraints:

```csharp
// In DynamicField.razor
if (value < field.Min || value > field.Max)
{
    // Show validation error
}
```

---

## Best Practices

1. **Start with dynamic fields** for new nodes, upgrade to designed component when UI matures

2. **Use meaningful parameter names** that match the fragment's input names

3. **Group related fields** using `column` widths that sum to 12

4. **Set sensible order values** to ensure logical UI flow

5. **Make advanced features collapsible** with `defaultCollapsed: true`

6. **Document constraints** in the schema even for designed components

7. **Keep utility fragments clean** - no `ui` block needed for loaders/savers

---

*Last Updated: Based on Dynamic Generation Refactor planning*
