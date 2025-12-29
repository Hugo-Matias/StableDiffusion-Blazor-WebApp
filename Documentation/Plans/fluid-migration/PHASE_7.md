# Phase 7 - Scriban Removal & Cleanup

## Status
**Phase:** 7  
**Build Status:** [x] Passing | **Tests:** [x] Pending validation

---

## Objective

Remove all Scriban dependencies and cleanup legacy code. The codebase now uses only Fluid for template rendering.

**Goals:**
1. Delete TemplateCacheService
2. Remove Scriban-specific code from WorkflowService
3. Remove legacy sync methods marked `[Obsolete]`
4. Update Program.cs to remove Scriban precompilation
5. Update all services to support only Fluid `.liquid` templates
6. Clean build with zero Scriban references

---

## Context

### Current State
- **Phase 5 Complete:** All workflow templates converted to Fluid (except Chroma - excluded)
- **Phase 6 Skipped:** Manual testing deferred
- **Scriban Usage Remaining:** None - completely removed

---

## Execution Checklist

### Step 7.1: Delete TemplateCacheService
**Complexity:** 2  
**Status:** [x] Complete

### Step 7.2: Clean WorkflowService
**Complexity:** 8  
**Status:** [x] Complete

- [x] Remove `IsFluidTemplate` checks
- [x] Remove `.sbn` file loading
- [x] Only load `.liquid` files
- [x] Remove legacy comments

### Step 7.3: Update Program.cs
**Complexity:** 3  
**Status:** [x] Complete

- [x] Remove TemplateCacheService registration
- [x] Remove precompilation logic

### Step 7.4: Update Project Files
**Complexity:** 1  
**Status:** [x] Complete

- [x] Remove `.sbn` copy rules from csproj
- [x] Only copy `.liquid` files

### Step 7.5: Update Test Files
**Complexity:** 2  
**Status:** [x] Complete

### Step 7.6: Update FragmentSchemaService
**Complexity:** 5  
**Status:** [x] Complete

- [x] Only parse Fluid `{% meta %}...{% endmeta %}` syntax
- [x] Remove legacy `#meta...#end` support
- [x] Only enumerate `.liquid` fragment files
- [x] Update `ParseFragmentDefaults()` for Fluid syntax only

### Step 7.7: Update WorkflowTemplateParser
**Complexity:** 3  
**Status:** [x] Complete

- [x] Remove Scriban references from comments
- [x] Parse only Fluid default syntax
- [x] Rename method to `ParseDefaultValue()`

### Step 7.8: Remove Workflow.IsFluidTemplate
**Complexity:** 2  
**Status:** [x] Complete

- [x] Remove `IsFluidTemplate` property from `Workflow` model
- [x] All workflows are now Fluid by default

### Step 7.9: Update PipelineExpander Syntax
**Complexity:** 3  
**Status:** [x] Complete

- [x] Change `ForeachProcessor` to use `${}` delimiter instead of `{{ }}`
- [x] Update placeholder syntax: `${$index}`, `${lora.Name}`, `${lora.Strength}`
- [x] Maintain backward compatibility with `{{ }}` (deprecated, will warn)
- [x] Update documentation examples
- [x] Update workflow templates to use `${}` syntax
- [x] Remove all `{% raw %}...{% endraw %}` blocks from templates

**Rationale:** Using `${}` delimiter eliminates conflicts with Fluid's `{{ }}` syntax, avoiding the need for verbose `{% raw %}...{% endraw %}` blocks in templates.

**Templates Updated:**
- `BlazorWebApp/Workflows/Templates/wan/img2vid.liquid`
- `BlazorWebApp/Workflows/Templates/z-image/txt2img.liquid`

### Step 7.10: Build and Test
**Complexity:** 3  
**Status:** [x] Complete

- [x] Run full build - **PASSED**

### Step 7.11: Fix Liquid Default Filter Quote Syntax
**Complexity:** 5  
**Status:** [x] Complete ? [x] CORRECTED

- [x] Identified correct Liquid quote syntax (single quotes)
- [x] Initial fix attempt used double quotes (WRONG - caused rendering issues)
- [x] Corrected to use single quotes in all `default:` filters (38 files)
- [x] Update FLUID_CONVENTIONS.md to document CORRECT syntax
- [x] Verify build passes

**Issue:** Fluid/Liquid templates require specific quote syntax in filter arguments.

**Initial Understanding (WRONG):**
We initially thought double quotes were correct:
```liquid
? {{ node_prefix | default: "model" }}  // We thought this was correct
```

**Actual Problem Discovered:**
When using `{{ }}` template output **inside JSON strings**, Fluid includes double quotes in the output:
```liquid
// Template:
"node": "{{ node_prefix | default: "model" }}"

// Renders to BROKEN JSON:
"node": ""model""  ? Double quotes included!
```

**Correct Solution - Use Single Quotes:**
```liquid
? CORRECT - Single quotes in filter arguments:
{{ node_prefix | default: 'model' }}
{{ node_prefix | default: 'model' | append: '_unet_loader' }}
{{ title | default: 'Load Model' | json }}

? WRONG - Double quotes create quote-escaped output:
{{ node_prefix | default: "model" }}  // Outputs: "model" with quotes!
```

**Root Cause:** Liquid/Fluid treats double-quoted strings in filter arguments as string literals that include the quotes in the output. Single quotes are the correct delimiter for string values.

**Files Fixed:** 38 fragment files using automated PowerShell script (`revert-to-single-quotes.ps1`).

**Why This Was Confusing:**
- Most programming languages use double quotes for strings
- JSON requires double quotes
- But **Liquid filter arguments** use single quotes for string literals
- This is standard Liquid syntax (used by Jekyll, Shopify, etc.)

---

## Known Issues & Resolutions

### Issue 1: Quote Handling in Liquid Templates (CORRECTED)
**Symptom:** JSON parsing errors with malformed node references like `"node": ""model""` or `"node": "'model'_unet_loader"`

**Initial Fix Attempt (Phase 7.11):** Changed to double quotes - **THIS WAS WRONG**

**Correct Understanding:** Liquid/Fluid filter arguments require **single quotes** for string literals

**Final Resolution:** Use single quotes exclusively in all Liquid filter arguments

**Prevention:** 
- Document rule in FLUID_CONVENTIONS.md (corrected)
- All developers must follow Liquid syntax conventions strictly
- Single quotes = correct, double quotes = wrong (opposite of what we initially thought!)

