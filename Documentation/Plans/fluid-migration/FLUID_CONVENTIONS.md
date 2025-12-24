# Fluid Template Conventions Guide

## Overview

This document defines the conventions, patterns, and best practices for writing Fluid (Liquid) templates in the workflow system. It serves as the authoritative reference for template/fragment authoring after the migration from Scriban.

---

## Template Syntax Reference

### Delimiters

| Type | Syntax | Example |
|------|--------|---------|
| Output (variable) | `{{ }}` | `{{ model_name }}` |
| Logic (tags) | `{% %}` | `{% if enabled %}...{% endif %}` |
| Comments | `{% comment %}...{% endcomment %}` | `{% comment %}This is hidden{% endcomment %}` |

### Variable Output

```liquid
{{ variable_name }}
{{ variable_name | filter }}
{{ variable_name | filter: argument }}
{{ variable_name | filter1 | filter2 }}
```

---

## Custom Tags

### `{% meta %}...{% endmeta %}`

Defines fragment metadata (outputs, conditions, UI schema). Content is captured to context side-channel, **NOT output to main stream**.

```liquid
{% meta %}
{
  "outputs": {
    "model_output": { "node": "{{ scope }}loader", "index": 0 }
  },
  "conditions": {
    "required": ["model_enabled"]
  },
  "ui": {
    "component": "LoaderForm",
    "title": "Model Loader"
  }
}
{% endmeta %}

{# Main template body starts here #}
"{{ scope }}loader": {
  "class_type": "CheckpointLoaderSimple",
  ...
}
```

**Key Points:**
- Variables inside meta block ARE rendered (e.g., `{{ scope }}`)
- Meta content is JSON - ensure valid JSON structure
- No trailing commas in JSON

### `{% get_ref "key" %}`

Resolves a node reference from the output registry. Outputs ComfyUI-compatible format.

```liquid
"model": {% get_ref "model_output" %}
```

**Output format:** `["node_id", index]`

**Example:**
```liquid
"model": {% get_ref "model_output" %}
{# Renders as: "model": ["loader_node", 0] #}
```

---

## Built-in Filters

### `| json`

Encodes a value as valid JSON. Essential for string values in JSON templates.

```liquid
"sampler_name": {{ sampler | json }}
{# Input: euler_ancestral ? Output: "euler_ancestral" #}

"seed": {{ seed | json }}
{# Input: 12345 ? Output: 12345 #}

"enabled": {{ flag | json }}
{# Input: true ? Output: true #}
```

**Handles:**
- `null` ? `null`
- Strings ? `"quoted string"` (with escaping)
- Booleans ? `true` / `false`
- Numbers ? unquoted number
- Objects ? serialized JSON

### `| default: value`

Provides a fallback value when variable is nil/empty.

```liquid
{{ node_prefix | default: "model" }}_loader
{# If node_prefix is nil ? "model_loader" #}
```

### `| append: string`

Concatenates strings (replaces Scriban's `+` operator).

```liquid
{{ node_prefix | default: "model" | append: "_unet_loader" }}
{# Result: "model_unet_loader" #}
```

### `| string_contains: substring`

Checks if string contains a substring (case-insensitive). Returns boolean.

```liquid
{% assign is_sdxl = model_name | string_contains: "SDXL" %}
{% if is_sdxl %}
  {# SDXL-specific logic #}
{% endif %}
```

**?? Important:** Cannot use filters directly in `{% if %}` conditions. Must assign first.

---

## Control Flow

### Conditionals

```liquid
{% if condition %}
  ...
{% elsif other_condition %}
  ...
{% else %}
  ...
{% endif %}
```

**Operators:**
- `==`, `!=` - equality
- `<`, `>`, `<=`, `>=` - comparison
- `and`, `or` - logical
- `contains` - string/array contains

```liquid
{% if sampler_name == "euler" %}
  ...
{% endif %}

{% if steps > 20 and cfg < 10 %}
  ...
{% endif %}
```

### Loops

```liquid
{% for item in items %}
  {{ item }}
{% endfor %}
```

**Loop variables:**
- `forloop.index` - 1-based index
- `forloop.index0` - 0-based index
- `forloop.first` - true if first iteration
- `forloop.last` - true if last iteration
- `forloop.length` - total items

```liquid
{% for lora in loras %}
  "lora_{{ forloop.index0 }}": {
    "name": {{ lora.name | json }},
    "strength": {{ lora.strength }}
  }{% unless forloop.last %},{% endunless %}
{% endfor %}
```

### Unless

```liquid
{% unless condition %}
  {# Executes if condition is false #}
{% endunless %}
```

---

## Variable Assignment

```liquid
{% assign my_var = "value" %}
{% assign combined = prefix | append: suffix %}
{% assign check = name | string_contains: "test" %}
```

---

## Syntax Migration: Scriban ? Fluid

| Feature | Scriban | Fluid |
|---------|---------|-------|
| Logic delimiters | `{{~ ~}}` | `{% %}` |
| Default values | `{{ var ?? "default" }}` | `{{ var \| default: "default" }}` |
| String concat | `{{ a + b }}` | `{{ a \| append: b }}` |
| Loop index (0-based) | `{{ for.index }}` | `{{ forloop.index0 }}` |
| Loop index (1-based) | `{{ for.index + 1 }}` | `{{ forloop.index }}` |
| First iteration | `{{ for.first }}` | `{{ forloop.first }}` |
| Last iteration | `{{ for.last }}` | `{{ forloop.last }}` |
| Meta blocks | `#meta ... #end` | `{% meta %}...{% endmeta %}` |
| Node references | `get_ref("key")` | `{% get_ref "key" %}` |
| Filter in condition | `{{ if var \| filter }}` | `{% assign x = var \| filter %}{% if x %}` |

---

## JSON Template Best Practices

### Always Use `| json` for String Values

```liquid
{# ? Correct #}
"model_name": {{ model | json }}

{# ? Wrong - missing quotes for string #}
"model_name": {{ model }}
```

### Handle Trailing Commas

JSON doesn't allow trailing commas. Use `{% unless forloop.last %}` pattern:

```liquid
{% for item in items %}
  "{{ item.key }}": {{ item.value | json }}{% unless forloop.last %},{% endunless %}
{% endfor %}
```

### Node Reference Format

Always use `{% get_ref %}` for node inputs:

```liquid
"inputs": {
  "model": {% get_ref "model_output" %},
  "positive": {% get_ref "positive_conditioning" %},
  "negative": {% get_ref "negative_conditioning" %}
}
```

---

## Parameter Naming Conventions

### Case Handling

Parameters are accessible in both `snake_case` and `PascalCase`:

```liquid
{# Both work - service provides both casings #}
{{ model_name }}
{{ ModelName }}
```

### Common Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `scope` | string | Node ID prefix (e.g., `"txt2img_"`) |
| `seed` | int | Generation seed |
| `steps` | int | Sampling steps |
| `cfg` | float | CFG scale |
| `sampler_name` | string | Sampler name |
| `scheduler` | string | Scheduler name |
| `denoise` | float | Denoising strength |
| `width` | int | Image width |
| `height` | int | Image height |

---

## Fragment Structure

### Standard Fragment Template

```liquid
{% meta %}
{
  "outputs": {
    "{{ scope }}output_name": { "node": "{{ scope }}node_id", "index": 0 }
  },
  "conditions": {
    "required": ["fragment_enabled"]
  },
  "ui": {
    "component": "FragmentForm",
    "title": "Fragment Title",
    "icon": "fa-solid fa-icon",
    "order": 50,
    "collapsible": true,
    "parameters": {
      "param_name": { "min": 0, "max": 100, "step": 1 }
    }
  }
}
{% endmeta %}

"{{ scope }}node_id": {
  "class_type": "NodeClassName",
  "inputs": {
    "input_ref": {% get_ref "previous_output" %},
    "param_value": {{ param_name | json }}
  }
}
```

---

## Common Patterns

### Conditional Node Inclusion

```liquid
{% if feature_enabled %}
"{{ scope }}feature_node": {
  "class_type": "FeatureNode",
  "inputs": {
    ...
  }
},
{% endif %}
```

### Dynamic Output Keys

```liquid
{% meta %}
{
  "outputs": {
    "{{ output_key_name }}": { "node": "{{ scope }}dynamic_node", "index": 0 }
  }
}
{% endmeta %}
```

### LoRA Loop

```liquid
{% for lora in loras %}
"{{ scope }}lora_{{ forloop.index0 }}": {
  "class_type": "LoraLoader",
  "inputs": {
    "model": {% if forloop.first %}{% get_ref "model_output" %}{% else %}{% get_ref scope | append: "lora_" | append: forloop.index0 | minus: 1 | append: "_model" %}{% endif %},
    "lora_name": {{ lora.name | json }},
    "strength_model": {{ lora.strength }}
  }
}{% unless forloop.last %},{% endunless %}
{% endfor %}
```

---

## Troubleshooting

### Parse Error: "A value was expected"

**Cause:** Using `RegisterExpressionBlock` for a block that takes no expression.

**Solution:** Block is registered with `RegisterEmptyBlock` - ensure `{% meta %}` has no expression argument.

### Parse Error: "Invalid 'if' tag"

**Cause:** Using a filter directly in an `{% if %}` condition.

**Wrong:**
```liquid
{% if model_name | string_contains: "SDXL" %}
```

**Correct:**
```liquid
{% assign is_sdxl = model_name | string_contains: "SDXL" %}
{% if is_sdxl %}
```

### JSON Encoding Issues

**Cause:** Not using `| json` filter for string values.

**Wrong:**
```liquid
"value": {{ my_string }}
{# Output: "value": some text (invalid JSON) #}
```

**Correct:**
```liquid
"value": {{ my_string | json }}
{# Output: "value": "some text" (valid JSON) #}
```

### Unresolved Node Reference

**Cause:** Referencing an output key that wasn't registered.

**Debug:** Check that the fragment declaring the output runs before the fragment using `{% get_ref %}`.

---

## File Naming

| Type | Extension | Location |
|------|-----------|----------|
| Workflow templates | `.liquid` | `Workflows/Templates/**/*.liquid` |
| Fragment templates | `.liquid` | `Workflows/Fragments/**/*.liquid` |

---

## Version History

| Version | Changes |
|---------|---------|
| 1.0 | Initial document - Phase 1 complete |
| 1.1 | Phase 2 complete - Core service migration |
| 1.2 | Phase 3 complete - Simple fragment conversion patterns validated |

---

## Architecture Notes (Phase 2)

### Call Flow
```
ComfyUIService.PostGenerationAsync()
    ??? WorkflowService.ComposeWorkflowFromGenerationParametersAsync()
        ??? RenderFragmentWithFluidAsync() [per fragment]
            ??? FluidTemplateService.RenderAsync()
                ??? Output: rendered fragment JSON
                ??? Side-channel: captured metadata
```

### Key Integration Points

1. **FluidTemplateService.RenderAsync()**
   - Input: template text, parameters dictionary, optional NodeRegistry
   - Output: tuple of (rendered string, metadata string or null)
   - Metadata captured from `{% meta %}` block via AmbientValues

2. **WorkflowService.RenderFragmentWithFluidAsync()**
   - Builds parameters from SubgraphContext + globalParams
   - Calls FluidTemplateService
   - Extracts outputs/conditions from metadata JSON
   - Evaluates conditions to determine fragment inclusion

3. **ComfyUIService**
   - Uses `ComposeWorkflowFromGenerationParametersAsync()` for all generation
   - Both image and video generation paths use Fluid

---

**Last Updated:** Phase 2 Completion
