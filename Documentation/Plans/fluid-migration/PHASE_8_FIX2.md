# Phase 8 Critical Fix #2 - JSON Parsing Issue

## Issue
The `.workflow` files contain **embedded Fluid template syntax** like `{{ Model | json }}` which is not valid JSON until rendered. The `WorkflowService.GetWorkflows()` was using **regex-based parsing** which couldn't handle this correctly.

## Root Cause

The `WorkflowTemplateParser` has two parsing methods:
1. **`ParseWorkflowTemplateAsync()`** - Renders Fluid templates FIRST, then parses JSON ?
2. **`ParseWorkflowTemplate()`** - Uses regex on raw template text ?

The `WorkflowService.GetWorkflows()` was calling the **wrong method** - it was using the regex fallback instead of the Fluid-aware async method.

## The Problem with Regex Parsing

Workflow files contain Fluid syntax:
```json
{
  "Title": "Text to Image",
  "Assets": [
    { "parameter": "Model", "default": {{ Model | default: "model.safetensors" | json }} }
  ]
}
```

**Regex parsing** tries to extract JSON directly from this, which fails because `{{ }}` is not valid JSON.

**Fluid parsing** first renders to:
```json
{
  "Title": "Text to Image",
  "Assets": [
    { "parameter": "Model", "default": "model.safetensors" }
  ]
}
```

Then parses the valid JSON.

## Solution

Changed `WorkflowService.GetWorkflows()` and `FindWorkflowTemplatePath()` to use:
```csharp
// ? WRONG - Regex parsing
var workflow = _templateParser.ParseWorkflowTemplate(templateText);

// ? CORRECT - Fluid rendering then JSON parsing
var workflow = _templateParser.ParseWorkflowTemplateAsync(templateText).GetAwaiter().GetResult();
```

## Why This Matters

The `.workflow` extension is for files that are:
1. **JSON-first** (base structure)
2. **With embedded Fluid** (for dynamic values like `{{ Model }}`)
3. **Must be rendered before parsing**

This is why we have `WorkflowTemplateParser.SafeDefaults` - to provide fallback values during initial parsing when no actual generation parameters exist yet.

## Files Changed
- `BlazorWebApp/Services/WorkflowService.cs`
  - `GetWorkflows()` - Now uses async Fluid parsing
  - `FindWorkflowTemplatePath()` - Now uses async Fluid parsing

## Impact
- ? **Workflow templates now parse correctly** as JSON with Fluid placeholders
- ? **No more regex parsing issues** with complex template syntax
- ? **Consistent parsing** across all workflow operations
- ? **Better error messages** when templates are malformed

## Testing
1. Restart application
2. Check logs for any parsing errors
3. Verify all workflows load correctly
4. Confirm no "Failed to parse workflow template" warnings

## Status
- ? **Fix Applied**
- ? **Build Passing**
- ?? **Pending Manual Testing** (restart required)
