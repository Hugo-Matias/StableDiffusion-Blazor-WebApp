# Phase 8 - Source Input UI Components

## Status
**Phase:** 8  
**Build Status:** Pending | **Tests:** Pending

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** &rarr; 2. **Test and Debug Features** &rarr; 3. **Discuss Improvements** &rarr; 4. **Update This Document**
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

Create UI components that allow users to select source images/videos for workflows that require input media (img2img, img2vid, pose2vid).

The infrastructure is already in place:
- ? Workflow templates have `Sources` arrays defined
- ? `WorkflowService.ParseSourcesFromTemplate()` parses Sources
- ? `GenerationParameterService.InitializeSourcesFromWorkflow()` creates `SourceAsset` entries
- ? `GenerationParameters.Sources` dictionary stores source data

**What's missing:** UI components to display source inputs and capture user selections.

---

## Context

### Dependencies
- Phase 7 complete (templates have Sources)
- `SourceAsset` model exists in `GenerationParameters`
- `SourcesPanel.razor` and `SourceItem.razor` created in Phase 4 (stub/skeleton)
- Existing `ImageInput.razor` component can be reused

### Current State

**Workflow Template (qwen/img2img-edit.sbn):**
```json
"Sources": [
  { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
]
```

**GenerationParameters.Sources after workflow selection:**
```csharp
Sources = {
  ["source_image"] = new SourceAsset { Label = "Source Image", Type = "image" }
}
```

**What needs to happen:**
1. `SourceInputPanel` reads `ParameterService.Current.Sources`
2. Renders input control for each source
3. User selects image/video &rarr; updates `SourceAsset.Base64Data`
4. Workflow composition uses `Sources["source_image"].Base64Data`

### Key Files
- `Components/Shared/Generation/SourcesPanel.razor` (created in Phase 4 - needs update)
- `Components/Shared/Generation/SourceItem.razor` (created in Phase 4 - needs update)
- `Components/Img2Img/ImageInput.razor` (existing reusable component)
- `Pages/Generate.razor` (needs SourceInputPanel integration)
- `Services/GenerationParameterService.cs` (source management)
- `Services/WorkflowService.cs` (workflow composition)

---

## Execution Checklist

### Step 8.1: Update SourcesPanel Component
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Update `SourcesPanel.razor` to iterate over `ParameterService.Current.Sources`
- [ ] Render `SourceItem` for each source with image type
- [ ] Handle empty sources (don't render panel if no sources defined)
- [ ] Add visual indicator for required vs optional sources

#### Expected Behavior
- Panel shows when workflow has Sources defined
- Panel hides when workflow has no Sources
- Each source displays its label and an input control

#### Files
- `Components/Shared/Generation/SourcesPanel.razor`

---

### Step 8.2: Update SourceItem Component
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Update `SourceItem.razor` to accept `SourceAsset` and source ID
- [ ] Integrate with existing `ImageInput.razor` for image upload
- [ ] Support drag-and-drop, file picker, and paste
- [ ] Update `SourceAsset.Base64Data` when image selected
- [ ] Show image preview when selected
- [ ] Add clear/remove button

#### Binding Pattern
```csharp
// SourceItem should update the source directly
private async Task OnImageSelected(string base64Data)
{
    ParameterService.Current.Sources[SourceId].Base64Data = base64Data;
    ParameterService.NotifyParametersChanged();
}
```

#### Files
- `Components/Shared/Generation/SourceItem.razor`

---

### Step 8.3: Integrate SourceInputPanel into Generate.razor
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Add SourcesPanel below workflow selector or above prompts
- [ ] Show only when `_selectedWorkflow?.Sources?.Count > 0`
- [ ] Ensure proper layout within the parameter form area
- [ ] Verify panel re-renders when workflow changes

#### Layout Position
```
???????????????????????????????????????????
?         Workflow Selector               ?
???????????????????????????????????????????
?         [Sources Panel]                 ? ? NEW (when workflow has sources)
???????????????????????????????????????????
?         Prompts                         ?
???????????????????????????????????????????
?         Fragments (Sampler, etc.)       ?
???????????????????????????????????????????
```

#### Files
- `Pages/Generate.razor`

---

### Step 8.4: Create VideoSourceItem Component
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `VideoSourceItem.razor` for video file selection
- [ ] Support video file upload (mp4, webm, etc.)
- [ ] Show video preview or first frame thumbnail
- [ ] Store video data in `SourceAsset.Base64Data` or file path
- [ ] Wire into SourcesPanel when `source.Type == "video"`

#### Notes
- Pose2vid workflow requires video input
- May need to store as file path instead of base64 (video files can be large)
- Consider using ComfyUI's upload endpoint for video files

#### Files
- `Components/Shared/Generation/VideoSourceItem.razor`

---

### Step 8.5: Wire Sources to Workflow Composition
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Verify `WorkflowService.ComposeWorkflowFromTemplate` can access Sources
- [ ] Ensure source data is passed to Scriban template context
- [ ] Template should be able to use `{{ Image }}` or `{{ Sources.source_image.Base64Data }}`
- [ ] Test with qwen/img2img-edit.sbn workflow

#### Template Usage Example
```json
{
  "id": "load_image",
  "fragment": "load-image-scaled.sbn",
  "parameters": {
    "image": {{ Image | json }}  // This should receive the source image data
  }
}
```

#### Files
- `Services/WorkflowService.cs`
- `Services/ImageService.cs` (may need to pass sources when building workflow)

---

### Step 8.6: Add Required Source Validation
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Add validation that required sources have data before generation
- [ ] Disable Generate button if required sources are empty
- [ ] Show validation message for missing required sources
- [ ] Update `GenerationParameterService.ValidateForGeneration()` method

#### Validation Logic
```csharp
public bool ValidateForGeneration(Workflow workflow, out List<string> errors)
{
    errors = new List<string>();
    
    if (workflow.Sources != null)
    {
        foreach (var source in workflow.Sources.Where(s => s.Required))
        {
            if (!Current.Sources.TryGetValue(source.Id, out var asset) || 
                string.IsNullOrEmpty(asset.Base64Data))
            {
                errors.Add($"Required source '{source.Label}' is not provided.");
            }
        }
    }
    
    return errors.Count == 0;
}
```

#### Files
- `Services/GenerationParameterService.cs`
- `Pages/Generate.razor`

---

### Step 8.7: Test End-to-End Source-based Generation
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Test Qwen img2img-edit with actual image input
- [ ] Test Wan img2vid with actual image input
- [ ] Test pose2vid with video input (if VideoSourceItem complete)
- [ ] Verify generated output uses source correctly
- [ ] Verify source is cleared when switching workflows

#### Test Cases
1. **Qwen Img2Img-Edit:**
   - Select workflow
   - Source panel appears with "Source Image" input
   - Upload image
   - Enter prompt: "add a hat to the person"
   - Generate
   - Verify output shows edited image

2. **Wan Img2Vid:**
   - Select workflow
   - Source panel appears with "Source Image" input
   - Upload image
   - Set video parameters
   - Generate
   - Verify video output animates the source image

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 8.1 | [ ] | 3 | SourcesPanel update |
| 8.2 | [ ] | 3 | SourceItem update |
| 8.3 | [ ] | 2 | Generate.razor integration |
| 8.4 | [ ] | 3 | VideoSourceItem (optional, can defer) |
| 8.5 | [ ] | 3 | Workflow composition wiring |
| 8.6 | [ ] | 2 | Validation |
| 8.7 | [ ] | 2 | E2E testing |

---

## Files Modified This Phase

| File | Changes |
|------|---------|
| `Components/Shared/Generation/SourcesPanel.razor` | Full implementation |
| `Components/Shared/Generation/SourceItem.razor` | Full implementation |
| `Components/Shared/Generation/VideoSourceItem.razor` | New file |
| `Pages/Generate.razor` | Add SourcesPanel |
| `Services/GenerationParameterService.cs` | Add validation |
| `Services/WorkflowService.cs` | Ensure sources passed to template |

---

## Design Decisions

### Reuse Existing ImageInput Component
The `ImageInput.razor` component from `Components/Img2Img/` already handles:
- File upload via picker
- Paste from clipboard
- Drag and drop
- Image preview
- Clear functionality

We should wrap or extend this rather than building from scratch.

### Base64 vs File Path
For images: Use Base64 (they're typically manageable size)
For videos: Consider file path or ComfyUI upload endpoint (videos can be 100s of MB)

### Source Clearing on Workflow Switch
When user switches to a different workflow:
- `InitializeSourcesFromWorkflow()` is called
- Old sources are replaced with new workflow's sources
- User must re-select any source media

### Layout Position
Sources appear between workflow selector and prompts because:
- They're input-specific (like prompts, not global settings)
- User should see what input is required before configuring generation params
- Matches natural workflow: select workflow ? provide input ? configure ? generate

---

## Issues &amp; Resolutions

| Issue | Resolution |
|-------|------------|
| TBD | TBD |

---

## Commit Checkpoints

- [ ] After Step 8.2 complete (SourceItem working)
- [ ] After Step 8.3 complete (Integrated into Generate page)
- [ ] After Step 8.5 complete (Sources wired to workflow)
- [ ] After Step 8.7 complete (E2E tested)

---

**Phase Status:** Not Started [ ]
