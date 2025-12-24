# Phase 3 - Template Conversion (Simple Fragments)

## Status
**Phase:** 3  
**Build Status:** ? Passing | **Tests:** ? 53/53 Passing

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules
- **Each step = commit checkpoint** - test thoroughly before proceeding
- **Minimal changes only** - focused on phase objectives
- **Document all issues and resolutions** in this file
- **This document must have enough context** to resume in a new session
- **User permission required** before next step

---

## Objective

Convert simple fragment files from Scriban (`.sbn`) to Fluid/Liquid syntax. Start with fragments that have no loops or complex conditionals to validate the conversion approach before tackling complex fragments.

---

## Context

- **Phase 2 Complete:** Core service infrastructure using Fluid is in place
- **Current State:** Fragment files use Scriban syntax (`.sbn` extension)
- **Target State:** Fragment files use Fluid/Liquid syntax (keeping `.sbn` extension for now)
- **Key Change:** `#meta...#end` ? `{% meta %}...{% endmeta %}`

---

## Conversion Reference

See `FLUID_CONVENTIONS.md` for full syntax reference.

### Quick Reference

| Feature | Scriban | Fluid |
|---------|---------|-------|
| Meta block | `#meta ... #end` | `{% meta %}...{% endmeta %}` |
| Output variable | `{{ var }}` | `{{ var }}` |
| Logic block | `{{~ if condition ~}}` | `{% if condition %}` |
| End block | `{{~ end ~}}` | `{% endif %}` |
| Default value | `{{ var ?? "default" }}` | `{{ var \| default: "default" }}` |
| JSON encode | `{{ var \| json }}` | `{{ var \| json }}` |
| Get ref | `{{ get_ref("key") }}` | `{% get_ref "key" %}` |
| For loop | `{{~ for item in items ~}}` | `{% for item in items %}` |
| Loop index | `{{ for.index }}` | `{{ forloop.index0 }}` |

---

## Execution Checklist

### Step 3.1: Identify Simple Fragments
**Complexity:** 1  
**Status:** [x] Complete

#### Tasks
- [x] List all fragment files in `Workflows/Fragments/`
- [x] Categorize by complexity (simple vs complex)
- [x] Select 3-4 simple fragments for initial conversion

#### Selection Criteria for "Simple"
- No `for` loops
- No complex conditionals (simple `if` is OK)
- No string concatenation with `+` operator
- Static or simple dynamic output keys

#### Fragments Selected for Conversion
1. `save.sbn` - Very simple, only uses `??` and `get_ref`
2. `vae-decode.sbn` - Simple, uses `??` and `get_ref`
3. `empty-latent.sbn` - Uses `{{ if scope }}` pattern
4. `load-diffusion.sbn` - Uses `{{ if scope }}` pattern, multiple outputs

---

### Step 3.2: Convert First Simple Fragment (save.sbn)
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Convert meta block syntax (`#meta...#end` ? `{% meta %}...{% endmeta %}`)
- [x] Convert default value syntax (`??` ? `| default:`)
- [x] Convert `get_ref` calls (`{{ get_ref("key") }}` ? `{% get_ref "key" %}`)
- [x] Add unit test for fragment syntax
- [x] Verify output matches expected JSON

#### Conversion Applied
```liquid
{% meta %}
{
  "outputs": {
    "save_node": { "node": "save", "index": 0 }
  }
}
{% endmeta %}

{
  "save": {
    "inputs": {
      "filename_prefix": {{ filename_prefix | default: "tmp/img" | json }},
      "images": {% get_ref "image_output" %}
    },
    "class_type": "SaveImage"
  }
}
```

---

### Step 3.3: Convert Remaining Simple Fragments
**Complexity:** 3  
**Status:** [x] Complete

#### Fragments Converted
1. **vae-decode.sbn**
   - Converted meta block
   - Converted `get_ref` with variable key using `{% assign %}`
   - Uses: `{% assign vae_key = scope | default: "" | append: "vae_output" %}{% get_ref vae_key %}`

2. **empty-latent.sbn**
   - Converted meta block with conditional scope
   - Converted `{{ if scope }}` ? `{% if scope %}`
   - Converted `{{ end }}` ? `{% endif %}`
   - Converted `??` ? `| default:`

3. **load-diffusion.sbn**
   - Converted meta block with multiple conditional outputs
   - Converted all `{{ if scope }}` patterns

---

### Step 3.4: Verify Build and Tests
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Run full build - passing
- [x] Run FluidTemplateServiceTests (30 tests) - passing
- [x] Run WorkflowServiceTests (23 tests) - passing
- [x] Total: 53 tests passing

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 3.1 | ?      | 1          | 4 fragments selected |
| 3.2 | ?      | 2          | save.sbn converted |
| 3.3 | ?      | 3          | 3 more fragments converted |
| 3.4 | ?      | 2          | All tests pass |

**Total Phase Complexity:** 8 points

---

## Fragment Inventory

### Converted Fragments (Phase 3)
| Fragment | Status | Notes |
|----------|--------|-------|
| `save.sbn` | ? Converted | Simple, uses `get_ref` and `default` |
| `vae-decode.sbn` | ? Converted | Uses dynamic `get_ref` with assign |
| `empty-latent.sbn` | ? Converted | Uses `{% if scope %}` pattern |
| `load-diffusion.sbn` | ? Converted | Multiple outputs with scope |

### Remaining Simple Fragments (Can convert as needed)
| Fragment | Complexity | Notes |
|----------|------------|-------|
| `vae-encode.sbn` | Simple | Similar to vae-decode |
| `load-image.sbn` | Simple | No loops |
| `load-image-scaled.sbn` | Simple | No loops |
| `upscale.sbn` | Simple | Uses get_ref |

### Complex Fragments (Phase 4)
| Fragment | Complexity | Notes |
|----------|------------|-------|
| `lora-loader.sbn` | Complex | Has loops for LoRAs |
| `load-checkpoint.sbn` | Complex | Multiple conditionals |
| `prompts.sbn` | Medium | May have conditionals |
| `sampler.sbn` | Medium | Uses get_ref patterns |
| `wan/load-model-sage.sbn` | Complex | The problematic one |
| `detailer-core.sbn` | Complex | Complex conditionals |

---

## Conversion Patterns Validated

### Pattern 1: Simple Get Ref
```liquid
{% get_ref "static_key" %}
```

### Pattern 2: Dynamic Get Ref with Assign
```liquid
{% assign key = scope | default: "" | append: "suffix" %}{% get_ref key %}
```

### Pattern 3: Conditional Scope in Keys
```liquid
"{% if scope %}{{ scope }}{% endif %}output_name"
```

### Pattern 4: Default Values
```liquid
{{ variable | default: "fallback" | json }}
```

---

## Issues & Resolutions

| Issue | Resolution |
|-------|------------|
| Dynamic `get_ref` key with concatenation | Use `{% assign %}` to build key, then `{% get_ref variable %}` |
| `{{ if scope }}` inline in strings | Convert to `{% if scope %}{{ scope }}{% endif %}` |

---

## Files Modified

| File | Action | Description |
|------|--------|-------------|
| `Fragments/save.sbn` | Converted | Simple save node |
| `Fragments/vae-decode.sbn` | Converted | VAE decoder with dynamic ref |
| `Fragments/empty-latent.sbn` | Converted | Latent image with scope |
| `Fragments/load-diffusion.sbn` | Converted | Model loaders with scope |
| `FluidTemplateServiceTests.cs` | Updated | Added 3 fragment syntax tests |

---

## Tests Added

| Test | Purpose |
|------|---------|
| `RenderAsync_WithSaveFragmentSyntax_ShouldRenderCorrectly` | Validates save.sbn pattern |
| `RenderAsync_WithEmptyLatentFragmentSyntax_ShouldRenderCorrectly` | Validates scope conditional pattern |
| `RenderAsync_WithVaeDecodeFragmentSyntax_ShouldRenderCorrectly` | Validates dynamic get_ref pattern |

---

## Phase Summary

### Accomplishments
1. ? Identified and categorized all fragment files
2. ? Converted 4 simple fragments to Fluid syntax
3. ? Validated conversion patterns with 3 new unit tests
4. ? All 53 tests passing (30 Fluid + 23 Workflow)

### Key Patterns Established
- **Meta block:** `{% meta %}...{% endmeta %}` works correctly
- **Dynamic keys:** Use `{% assign %}` for string concatenation before `{% get_ref %}`
- **Conditionals in strings:** `{% if scope %}{{ scope }}{% endif %}` inline
- **Default values:** `{{ var | default: "value" }}` works correctly

### Ready for Phase 4
- Convert complex fragments with loops and conditionals
- Focus on the problematic `wan/load-model-sage.sbn`
- Handle LoRA loader with loops

---

**Phase Status:** ? Complete - Ready for Phase 4 (Complex Fragment Conversion)
