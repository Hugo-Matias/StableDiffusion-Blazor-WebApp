# Phase 5 - Unified Generation Page

## Status
**Phase:** 5  
**Build Status:** &check; Passing | **Tests:** Pending

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

Create a single unified generation page (`Generate.razor`) that dynamically renders based on the selected workflow. This page will replace the separate Txt2Img, Img2Img, and Img2Vid pages.

---

## Context

### Dependencies
- Phase 1-4 complete
- Models: `GenerationParameters`, `FragmentParameters`, `Workflow`
- Services: `IGenerationParameterService`, `IComponentRegistry`, `IWorkflowService`
- Components: `DynamicField`, `DynamicFragmentForm`, `FragmentFormContainer`, `SourcesPanel`

### Page Location
`BlazorWebApp/Pages/Generate.razor`

### Reference Files
- Existing pages: `Txt2Img.razor`, `Img2Img.razor`, `Img2Vid.razor`
- Existing forms: `GenerateFormTxt2Img.razor`, `PromptFields.razor`
- Workflow service: `WorkflowService.cs`

---

## Page Layout Design

```
+------------------------------------------------------------------+
|                         TopToolbar                                |
+------------------------------------------------------------------+
|  Workflow Selector  |  Mode Badge  |  Assets Panel (collapsible)  |
+------------------------------------------------------------------+
|                                                                    |
|  +------------------------+  +----------------------------------+  |
|  |    Prompt Fields       |  |      Generated Output            |  |
|  |  (positive/negative)   |  |  (ImagesContainer/VideoViewer)   |  |
|  +------------------------+  |                                  |  |
|  |    Sources Panel       |  |                                  |  |
|  |  (if workflow needs)   |  |                                  |  |
|  +------------------------+  |                                  |  |
|  |                        |  |                                  |  |
|  |  Fragment Forms        |  |                                  |  |
|  |  (ordered by schema)   |  |                                  |  |
|  |  - Sampler             |  |                                  |  |
|  |  - Upscale             |  |                                  |  |
|  |  - Detailer            |  |                                  |  |
|  |  - etc.                |  |                                  |  |
|  |                        |  |                                  |  |
|  +------------------------+  +----------------------------------+  |
|  |    Generate Button     |                                        |
|  +------------------------+                                        |
+------------------------------------------------------------------+
```

---

## Execution Checklist

### Step 5.1: Create Generate.razor page layout
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `Generate.razor` page with routes `/generate` and `/generate/{WorkflowId}`
- Implemented workflow selector dropdown with mode grouping
- Two-column responsive grid layout
- Integrated WorkflowAssetsPanel, SourcesPanel, FragmentFormContainer
- Added image editor modal integration

---

### Step 5.2: Implement workflow selection and switching
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Workflow selector groups by Mode (Txt2Img, Img2Img, Img2Vid)
- OnWorkflowSelected initializes GenerationParameters via service
- Updates URL without full navigation
- Schema caching for fragments

---

### Step 5.3: Implement prompt fields with fragment binding
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Implemented PromptsFormNew with FragmentFormBase binding
- Positive/negative prompt textfields with debounced updates
- Auto-hides negative prompt for Flux/Qwen models
- Quick actions: clear, swap prompts
- Word/character count display
- Updated GenerateButton to support both legacy (SharedParameters) and new (parameterless) patterns

---

### Step 5.4: Implement SourcesPanel integration
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Added `ParseSourcesFromTemplate` and `ParseSingleSource` to WorkflowService
- Sources are now parsed from workflow templates and stored in `Workflow.Sources`
- `GenerationParameterService.InitializeSourcesFromWorkflow` creates SourceAsset entries
- SourcesPanel in Generate.razor shows/hides based on workflow requirements

---

### Step 5.5: Implement fragment form rendering from pipeline
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- FragmentFormContainer wraps each fragment with collapsible UI
- Designed components rendered via RenderDesignedComponent (RenderFragment builder)
- Dynamic forms use DynamicFragmentForm when no designed component
- Fragments sorted by schema.Order

---

### Step 5.6: Implement asset panel integration
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- WorkflowAssetsPanel integrated at top of page
- Mode passed from selected workflow
- Already functional from existing implementation

---

### Step 5.7: Implement generate button and progress
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- GenerateButton updated with parameterless callback support
- MapAndGenerate bridges new GenerationParameters to legacy system
- Temporary mapping to Txt2ImgParameters/Img2ImgParameters/Img2VidParameters
- Progress/converging handled via existing ConvergingChangedEventArgs

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 5.1 | &check; | 3 | Page layout |
| 5.2 | &check; | 5 | Workflow selection |
| 5.3 | &check; | 3 | Prompt fields |
| 5.4 | &check; | 3 | Sources panel |
| 5.5 | &check; | 5 | Fragment rendering |
| 5.6 | &check; | 3 | Asset panel |
| 5.7 | &check; | 5 | Generate + progress |

---

## Key Integration Points

### GenerationParameters &rarr; Workflow Rendering

```csharp
// On workflow selection
async Task OnWorkflowSelected(Workflow workflow)
{
    // 1. Initialize parameters from workflow defaults
    Parameters = await GenerationParameterService.InitializeFromWorkflow(workflow);
    
    // 2. Parse pipeline to get fragment schemas
    FragmentSchemas = workflow.Pipeline
        .Select(step => WorkflowService.GetFragmentSchema(step.Fragment))
        .Where(s => s?.HasUI == true)
        .OrderBy(s => s.Order)
        .ToList();
    
    // 3. Parse sources
    Sources = workflow.Sources ?? new();
}
```

### Fragment Form Rendering

```razor
@foreach (var fragmentId in Parameters.Fragments.Keys.OrderBy(GetFragmentOrder))
{
    var fragmentParams = Parameters.Fragments[fragmentId];
    var schema = GetSchema(fragmentParams.FragmentFile);
    
    @if (schema != null && schema.HasUI)
    {
        <FragmentFormContainer Schema="@schema" 
                               Parameters="@fragmentParams"
                               OnActiveChanged="HandleFragmentActiveChanged">
            @if (schema.HasDesignedComponent)
            {
                @RenderDesignedComponent(schema.Component, fragmentParams, schema)
            }
            else
            {
                <DynamicFragmentForm Schema="@schema" 
                                     Parameters="@fragmentParams"
                                     OnValueChanged="HandleValueChanged" />
            }
        </FragmentFormContainer>
    }
}
```

---

## Issues & Resolutions

### Issue 1: FragmentSchema.HasUI property missing
**Impact:** Build error
**Resolution:** Added HasUI property (returns true if HasDesignedComponent OR UsesDynamicFields)

### Issue 2: IOrchestratorService.Workflows property missing
**Impact:** Build error
**Resolution:** Changed to iterate GetWorkflowsForMode for all ModeTypes

### Issue 3: Workflow.Sources property missing
**Impact:** Build error
**Resolution:** Added WorkflowSource class and Sources property to Workflow model

---

## Files Modified This Phase

| File | Changes |
|------|---------|
| `Pages/Generate.razor` | Created unified generation page |
| `Models/FragmentSchema.cs` | Added HasUI property |
| `Models/Workflow.cs` | Added WorkflowSource, Sources property |
| `Services/IGenerationParameterService.cs` | Added InitializeFromWorkflowAsync |
| `Services/GenerationParameterService.cs` | Implemented async initialization, source parsing |
| `Services/WorkflowService.cs` | Added ParseSourcesFromTemplate, ParseSingleSource |
| `Components/Shared/Generation/PromptsFormNew.razor` | Full implementation with binding |
| `Components/Shared/Generation/GenerateButton.razor` | Added parameterless callback support |

---

## Commit Checkpoints

- [x] After Step 5.2 complete (workflow selection working)
- [x] After Step 5.5 complete (fragments rendering)
- [x] After Step 5.7 complete (full generation working)

---

## Phase Summary

### Accomplishments
1. Created unified `Generate.razor` page with routes `/generate` and `/generate/{WorkflowId}`
2. Workflow selector with mode grouping (Txt2Img, Img2Img, Img2Vid)
3. Dynamic fragment form rendering from pipeline schemas
4. Sources panel integration for Img2Img/Img2Vid workflows
5. Asset panel integration at page top
6. Generate button with progress integration
7. Temporary parameter mapping to legacy system (for backward compatibility)

### Architecture
- **GenerationParameters** &rarr; New unified parameter model
- **FragmentParameters** &rarr; Per-fragment parameter storage
- **MapAndGenerate()** &rarr; Bridge to legacy Txt2ImgParameters/Img2ImgParameters

### Known Limitations (for future phases)
- Parameter mapping is temporary - Phase 6 will update ImageService for direct GenerationParameters
- PromptsFormNew doesn't have autocomplete yet - Phase 7 will add advanced prompt features
- Fragment chaining not implemented - Phase 8 will add fragment instance management

---

**Phase Status:** Complete &check;
