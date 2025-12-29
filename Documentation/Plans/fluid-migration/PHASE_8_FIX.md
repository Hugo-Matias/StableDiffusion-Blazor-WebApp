# Phase 8 Critical Fix - Duplicate Workflow Names

## Issue
After renaming workflow templates to `.workflow`, the Base selector was missing txt2img workflows for:
- Stable Diffusion (SD)
- Qwen
- Z-Image

Only the Flux txt2img workflow was appearing.

## Root Cause

The `GetWorkflows()` method was grouping files by **filename only** (without path):

```csharp
// ? WRONG - Groups all "txt2img.workflow" together
var filesByBaseName = workflowFiles
    .GroupBy(f => Path.GetFileNameWithoutExtension(f.Name))
    .ToDictionary(g => g.Key, g => g.ToList());
```

This caused files with the same name in different directories to be deduplicated:
- `flux/txt2img.workflow` ? Only this one loaded
- `qwen/txt2img.workflow` ? Lost
- `sd/txt2img.workflow` ? Lost
- `z-image/txt2img.workflow` ? Lost

## Solution

Changed to group by **relative path** from Templates directory:

```csharp
// ? CORRECT - Groups by full path
var filesByRelativePath = workflowFiles
    .Select(f => new
    {
        File = f,
        RelativePath = Path.GetRelativePath(templatesPath, f.FullName)
            .Replace(f.Extension, "", StringComparison.OrdinalIgnoreCase)
    })
    .GroupBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.Select(x => x.File).ToList());
```

Now each workflow has a unique key:
- `flux\txt2img` ? Unique
- `qwen\txt2img` ? Unique
- `sd\txt2img` ? Unique
- `z-image\txt2img` ? Unique

## Files Changed
- `BlazorWebApp/Services/WorkflowService.cs` - Fixed `GetWorkflows()` method

## Testing
1. Restart application
2. Navigate to generation page
3. Check Base selector dropdown
4. Verify all workflow types appear:
   - ? Flux txt2img
   - ? SD txt2img
   - ? Qwen txt2img
   - ? Z-Image txt2img
   - ? WAN img2vid
   - ? WAN pose2vid

## Status
- ? **Fix Applied**
- ? **Build Passing**
- ?? **Pending Manual Testing** (restart required)
