# Phase 8 - Unified Generate Page Layout

## Status
**Phase:** 8  
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

Make the unified `/generate` page match the layout and functionality of the current `Txt2Img.razor`, `Img2Img.razor`, and `Img2Vid.razor` pages, ensuring all parameter components render correctly.

### Current State
- ? `SourcesPanel.razor` and `SourceItem.razor` already working (image/video input)
- ? `Generate.razor` exists but has wrong layout (dropdown selector, 6/6 split)
- ? NavBar shows workflow buttons but links to old `/txt2img/{id}` routes
- ? Generate.razor uses dropdown instead of navbar workflow buttons
- ? Layout doesn't match old pages (Assets ? Prompts ? Left/Right columns)
- ? Missing resolution panel with quick res buttons
- ? Missing sampler settings panel
- ? Missing toggleable feature sections (Upscale, SeedVR2, etc.)

### Target State
- NavBar workflow buttons redirect to `/generate/{workflow_id}`
- Generate.razor matches old page layout:
  ```
  ???????????????????????????????????????????????????????????????
  ?                    WorkflowAssetsPanel                       ?
  ???????????????????????????????????????????????????????????????
  ?  PromptFields + Generate Button (full width)                ?
  ???????????????????????????????????????????????????????????????
  ?  Sources (if any)          ?                                ?
  ?  Resolution Panel          ?   GeneratedImageTabs /         ?
  ?  Sampler Settings          ?   GeneratedVideoTabs           ?
  ?  Toggleable Features       ?                                ?
  ?  (Upscale, SeedVR2, etc)  ?                                ?
  ???????????????????????????????????????????????????????????????
  ```

---

## Context

### Navigation Flow
1. **Base Selector** (navbar) - User selects base (Flux, SD, Wan, etc.)
2. **Workflow Buttons** (navbar) - All workflows for that base appear as nav links
3. **Generate Page** - `/generate/{workflow_id}` shows the selected workflow's form

### Key Files to Modify
- `Components/Shared/NavBar.razor` - Update links to `/generate/{id}`
- `Pages/Generate.razor` - Complete layout rewrite
- `Components/Shared/Generation/ResolutionPanel.razor` - Extract from GenerateFormTxt2Img
- `Components/Shared/Generation/SamplerSettingsPanel.razor` - Extract from GenerateFormTxt2Img

### Reference Files (Current Implementation)
- `Pages/Txt2Img.razor` - Layout pattern to follow
- `Components/Txt2Img/GenerateFormTxt2Img.razor` - Settings to extract
- `Components/Shared/Generation/PromptFields.razor` - Reuse for prompts

---

## Execution Checklist

### Step 8.1: Update NavBar Workflow Links
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Change `/txt2img/{workflow.Id}` to `/generate/{workflow.Id}`
- [x] Change `/img2img/{workflow.Id}` to `/generate/{workflow.Id}`
- [x] Change `/img2vid/{workflow.Id}` to `/generate/{workflow.Id}`
- [x] Add color-coded MudIcon for each mode type
- [x] Fix StableDiffusion filtering bug (enum index 0 issue)

#### Files
- `Components/Shared/NavBar.razor`

#### Resolution
- Changed all workflow links to use `/generate/{id}` route
- Added `MudIcon` with colors: Primary (Txt2Img), Secondary (Img2Img), Tertiary (Img2Vid)
- Fixed bug where `ModelBase.StableDiffusion` (enum index 0) was being filtered out by `!= default` check
- Created `HasWorkflowBaseSelected()` method to properly check if a base is selected

---

### Step 8.2: Create ResolutionPanel Component
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Extract resolution section from `GenerateFormTxt2Img.razor`
- [x] Create `ResolutionPanel.razor` with:
  - Quick resolution buttons (from settings)
  - Scale buttons (x0.5, Rotate, Lock, x2)
  - Width/Height sliders
  - Aspect ratio lock functionality
- [x] Wire to `GenerationParameters.Fragments["main_sampler"]` or dedicated resolution fragment
- [x] Support reading/writing width/height from fragment parameters

#### Component API
```razor
<ResolutionPanel Width="@_width"
                 WidthChanged="HandleWidthChanged"
                 Height="@_height"
                 HeightChanged="HandleHeightChanged" />
```

#### Files
- `Components/Shared/Generation/ResolutionPanel.razor` (new)

---

### Step 8.3: Create SamplerSettingsPanel Component
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Extract sampler settings from `GenerateFormTxt2Img.razor`
- [x] Create `SamplerSettingsPanel.razor` with:
  - Sampler dropdown
  - Scheduler dropdown
  - Steps slider
  - Seed field + random/restore buttons
  - CFG Scale / Guidance slider (based on workflow base)
  - Batch size slider
- [x] Wire to `GenerationParameters.Fragments["main_sampler"]`
- [x] Handle Flux guidance vs standard CFG based on workflow base

#### Component API
```razor
<SamplerSettingsPanel Parameters="@_samplerFragment"
                      WorkflowBase="@_selectedWorkflow.Base"
                      OnValueChanged="HandleValueChanged" />
```

#### Files
- `Components/Shared/Generation/SamplerSettingsPanel.razor` (new)

---

### Step 8.4: Create Fragment Form Components
**Complexity:** 5
**Status:** [x] Complete

#### Tasks
- [x] Create `Fragments/` subfolder for fragment-specific forms
- [x] Create `CollapsibleFeatureSection.razor` - reusable toggle+collapse wrapper
- [x] Create `empty-latent.sbn` fragment for resolution/latent
- [x] Create `LatentForm.razor` - uses ResolutionPanel + batch size
- [x] Create `SamplerForm.razor` - replaces SamplerSettingsPanel
- [x] Create `UpscaleForm.razor` - for upscale.sbn
- [x] Create `SeedVR2Form.razor` - for upscale-seedvr2.sbn
- [x] Move + refactor `ConditioningVariationForm.razor` to Fragments/
- [x] Move + refactor `SeedVarianceEnhancerForm.razor` to Fragments/
- [x] Update `upscale.sbn` with UI schema
- [x] Update `ComponentRegistry.cs` with new component locations
- [x] Remove obsolete components (SamplerSettingsPanel, old forms)

#### Component API Pattern
All fragment forms follow this pattern:
```razor
<ComponentForm 
    FragmentId="fragment-name"
    Param1="@value1" Param1Changed="HandleParam1Changed"
    Param2="@value2" Param2Changed="HandleParam2Changed"
    OnChanged="HandleAnyChange" />
```

#### Files Created
- `Components/Shared/Generation/Fragments/SamplerForm.razor`
- `Components/Shared/Generation/Fragments/LatentForm.razor`
- `Components/Shared/Generation/Fragments/UpscaleForm.razor`
- `Components/Shared/Generation/Fragments/SeedVR2Form.razor`
- `Components/Shared/Generation/Fragments/ConditioningVariationForm.razor`
- `Components/Shared/Generation/Fragments/SeedVarianceEnhancerForm.razor`
- `Components/Shared/Generation/CollapsibleFeatureSection.razor`
- `Workflows/Fragments/empty-latent.sbn`

#### Files Removed
- `Components/Shared/Generation/SamplerSettingsPanel.razor`
- `Components/Shared/Generation/ConditioningVariationForm.razor` (old location)
- `Components/Shared/Generation/SeedVarianceEnhancerForm.razor` (old location)
- `Components/Shared/Generation/ToggleableFeaturesPanel.razor`

#### Design Decisions
1. **Fragments/ subfolder** - Separates fragment-specific forms from reusable sub-components
2. **Consistent API** - All forms use individual parameters with EventCallbacks (not parameter objects)
3. **FragmentId property** - Each form knows which fragment it's associated with
4. **OnChanged callback** - Optional unified callback for any parameter change
5. **CollapsibleFeatureSection** - Generic wrapper for optional features with toggle+collapse

---

### Step 8.4b: Rewire Templates to Use empty-latent.sbn
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Remove latent node from `load-diffusion.sbn`
- [x] Remove latent node from `load-diffusion-w-prompts.sbn`
- [x] Remove latent node from `load-checkpoint.sbn`
- [x] Update `z-image/txt2img.sbn` - add `empty-latent.sbn` fragment
- [x] Update `sd/txt2img.sbn` - add `empty-latent.sbn` with `EmptyLatentImage` class
- [x] Update `qwen/txt2img.sbn` - add `empty-latent.sbn` fragment
- [x] Leave `flux/load-flux.sbn` as-is (special case with VAEEncodeAdvanced)
- [x] Leave `chroma/txt2img.sbn` as-is (inline JSON, technical debt)

#### Special Cases
- **flux/load-flux.sbn** - Uses `VAEEncodeAdvanced` which needs width/height internally for both empty latent AND VAE encode. Left unchanged.
- **chroma/txt2img.sbn** - Uses inline JSON workflow, not fragment-based. Left as technical debt.
- **Img2Img templates** - Don't use empty-latent (they encode from input images)
- **Img2Vid templates** - Use video-specific latent creation

#### Fragment Changes

| Fragment | Change |
|----------|--------|
| `load-diffusion.sbn` | Removed `latent_output` and latent node |
| `load-diffusion-w-prompts.sbn` | Removed `latent_output` and latent node |
| `load-checkpoint.sbn` | Removed `latent_output` and latent node |
| `empty-latent.sbn` | Added `latent_class` parameter for node type selection |

#### Template Changes

| Template | Change |
|----------|--------|
| `z-image/txt2img.sbn` | Added `empty-latent.sbn` step, removed res params from loader |
| `sd/txt2img.sbn` | Added `empty-latent.sbn` with `latent_class: "EmptyLatentImage"` |
| `qwen/txt2img.sbn` | Added `empty-latent.sbn` step, removed res params from loader |
| `flux/txt2img.sbn` | No change (special case) |

#### Design Notes
- `latent_class` parameter in `empty-latent.sbn` defaults to `"EmptySD3LatentImage"` for modern architectures
- SD checkpoint workflows use `"EmptyLatentImage"` (older 4-channel latent format)
- Detailer loaders still receive width/height for now (will be cleaned up when detailer form is implemented)

---

### Step 8.4c: Plan PromptsForm Refactor (PromptFields &rarr; PromptsForm)
**Complexity:** 5
**Status:** [x] Complete

#### Objective
Refactor `PromptFields.razor` into `PromptsForm.razor` to follow the same pattern as other fragment forms, removing dependency on `SharedParameters` generic type.

#### Current State Analysis

**`PromptFields<T>` Dependencies on `SharedParameters`:**

| Usage | Location | Migration |
|-------|----------|-----------|
| `Parameters.Prompt` | TextFieldAutocomplete binding | Replace with `Prompt` string parameter |
| `Parameters.NegativePrompt` | TextFieldAutocomplete binding | Replace with `NegativePrompt` string parameter |
| `Parameters.Loras` | `HandleLoraSelected()` adds to list | Emit via `OnLoraSelected` callback |
| `ParametersChanged.InvokeAsync(Parameters)` | Multiple places | Replace with specific callbacks |
| `GenerateButton Parameters="Parameters"` | Passes to generate button | Use `OnGenerateClicked` (already supported) |

#### New Component API

```razor
<PromptsForm 
    FragmentId="prompts"
    Prompt="@_prompt"
    PromptChanged="HandlePromptChanged"
    NegativePrompt="@_negativePrompt"
    NegativePromptChanged="HandleNegativePromptChanged"
    OnGenerate="HandleGenerate"
    OnSkip="HandleSkip"
    OnInterrupt="HandleInterrupt"
    OnLoraSelected="HandleLoraAdded"
    OnStylesChanged="HandleStylesChanged"
    OnPromptTagAppended="HandlePromptTagAppended"
    OnNegativePromptTagAppended="HandleNegativePromptTagAppended"
    ButtonDisabled="@_isGenerating" />
```

#### Migration Checklist

| Current | New |
|---------|-----|
| `@typeparam T where T : SharedParameters` | Remove generic |
| `[Parameter] public T Parameters` | `[Parameter] public string? Prompt` + `[Parameter] public string? NegativePrompt` |
| `[Parameter] public EventCallback<T> ParametersChanged` | `EventCallback<string> PromptChanged` + `EventCallback<string> NegativePromptChanged` |
| `@bind-Value="@Parameters.Prompt"` | `Value="@Prompt" ValueChanged="HandlePromptChanged"` |
| `@bind-Value="@Parameters.NegativePrompt"` | `Value="@NegativePrompt" ValueChanged="HandleNegativePromptChanged"` |
| `Parameters.Loras.Add(lora)` | `await OnLoraSelected.InvokeAsync(lora)` |
| `GenerateButton Parameters="Parameters"` | `GenerateButton OnGenerateClicked="OnGenerate"` |

#### Functionality Preservation

| Feature | Current Implementation | New Implementation |
|---------|----------------------|-------------------|
| **Autocomplete** | Works on `Parameters.Prompt` | Works on `Prompt` property directly |
| **Tag Appending** | `OnPromptTagAppended` callback | Keep as-is, parent handles |
| **Style Selection** | Adds to `State.State.Generation.Styles` | Keep as-is (uses State service) |
| **Style Writing** | `Parser.ParseParameters(Parameters, ...)` | Emit new prompt values via callbacks |
| **LoRA Selection** | Adds to `Parameters.Loras` | Emit via `OnLoraSelected` callback |
| **LLM Prompt Applied** | Sets `Parameters.Prompt/NegativePrompt` | Emit via `PromptChanged`/`NegativePromptChanged` |
| **Keyboard Shortcut** | Ctrl+Enter triggers generate | Keep as-is |
| **Negative Prompt Disable** | Checks `_disableNegativeBases` | Keep as-is (uses State service) |

#### Key Code Changes Required

1. **Remove generic type parameter:**
   ```razor
   @* OLD *@
   @typeparam T where T : SharedParameters
   
   @* NEW *@
   @* (no typeparam) *@
   ```

2. **Replace Parameters with individual props:**
   ```csharp
   // OLD
   [Parameter] public T Parameters { get; set; }
   [Parameter] public EventCallback<T> ParametersChanged { get; set; }
   
   // NEW
   [Parameter] public string? Prompt { get; set; }
   [Parameter] public EventCallback<string> PromptChanged { get; set; }
   [Parameter] public string? NegativePrompt { get; set; }
   [Parameter] public EventCallback<string> NegativePromptChanged { get; set; }
   ```

3. **Update TextFieldAutocomplete bindings:**
   ```razor
   @* OLD *@
   @bind-Value="@Parameters.Prompt"
   
   @* NEW *@
   Value="@Prompt" ValueChanged="HandlePromptValueChanged"
   ```

4. **LoRA handling - emit instead of mutate:**
   ```csharp
   // OLD
   Parameters.Loras.Add(lora);
   await ParametersChanges.InvokeAsync(Parameters);
   
   // NEW
   [Parameter] public EventCallback<Lora> OnLoraSelected { get; set; }
   await OnLoraSelected.InvokeAsync(lora);
   ```

5. **Style writing - emit new values:**
   ```csharp
   // OLD
   Parser.ParseParameters(Parameters, State.State.Generation.Styles);
   
   // NEW
   var (newPrompt, newNegative) = Parser.ApplyStylesToPrompts(Prompt, NegativePrompt, State.State.Generation.Styles);
   await PromptChanged.InvokeAsync(newPrompt);
   await NegativePromptChanged.InvokeAsync(newNegative);
   ```

6. **GenerateButton - use parameterless callback:**
   ```razor
   @* OLD *@
   <GenerateButton Parameters="Parameters" OnGenerate="OnGenerate" ... />
   
   @* NEW *@
   <GenerateButton OnGenerateClicked="OnGenerate" ... />
   ```

#### Files to Modify/Create

| Action | File | Status |
|--------|------|--------|
| Create | `Fragments/PromptsForm.razor` | ? Created |
| Update | `ComponentRegistry.cs` - add `PromptsForm` | ? Updated |
| Keep | `PromptFields.razor` - keep for backward compatibility | ? Kept (legacy pages still use it) |

#### Implementation Summary

**Created `PromptsForm.razor`** with the following API:
```razor
<PromptsForm 
    FragmentId="prompts"
    Prompt="@_prompt"
    PromptChanged="HandlePromptChanged"
    NegativePrompt="@_negativePrompt"
    NegativePromptChanged="HandleNegativePromptChanged"
    OnGenerate="HandleGenerate"
    OnSkip="HandleSkip"
    OnInterrupt="HandleInterrupt"
    OnLoraSelected="HandleLoraAdded"
    OnStylesChanged="HandleStylesChanged"
    OnPromptTagAppended="HandlePromptTagAppended"
    OnNegativePromptTagAppended="HandleNegativePromptTagAppended"
    ButtonDisabled="@_isGenerating" />
```

**Key Changes from `PromptFields<T>`:**
1. Removed generic type parameter `@typeparam T where T : SharedParameters`
2. Replaced `Parameters` object with individual `Prompt` and `NegativePrompt` string parameters
3. LoRA selection now emits via `OnLoraSelected` callback (parent manages list)
4. Uses `GenerateButton OnGenerateClicked` instead of `Parameters`-based callback
5. `WritePrompts()` now uses `Parser.ParseStyles()` directly and emits new values via callbacks
6. All functionality preserved: autocomplete, styles, LLM enhancer, keyboard shortcuts

#### Parent Page Responsibilities (Generate.razor)

After this refactor, `Generate.razor` will need to:

```csharp
// State
private string _prompt = "";
private string _negativePrompt = "";
private List<Lora> _loras = new();

// Handlers
private void HandlePromptChanged(string value)
{
    _prompt = value;
    // Sync to fragment if needed
}

private void HandleNegativePromptChanged(string value)
{
    _negativePrompt = value;
}

private void HandleLoraAdded(Lora lora)
{
    if (!_loras.Any(l => l.Name == lora.Name))
        _loras.Add(lora);
}
```

#### Dependencies Verified

- [x] `GenerateButton` supports `OnGenerateClicked` ?
- [x] `TextFieldAutocomplete` supports `Value`/`ValueChanged` pattern ?
- [x] `Parser.ParseStyles()` works with string inputs ?
- [x] `TagDrawer` callbacks work without `Parameters` object ?

---

### Step 8.5: Rewrite Generate.razor Layout
**Complexity:** 5
**Status:** [x] Complete

#### Tasks
- [x] Remove workflow dropdown selector (workflow comes from URL via NavBar)
- [x] Match layout to Txt2Img.razor:
  1. WorkflowAssetsPanel (full width, collapsed)
  2. PromptsForm + Generate button (full width, inside component)
  3. MudGrid with Left (xs=6) and Right (xs=6) columns
- [x] Left column content:
  - LoraForm
  - SourcesPanel (if workflow.Sources exists)
  - LatentForm (resolution)
  - SamplerForm
  - CollapsibleFeatureSection for optional fragments
- [x] Right column: GeneratedImageTabs or GeneratedVideoTabs
- [x] Wire prompts to fragment with local state sync
- [x] Handle workflow initialization from URL parameter
- [x] Discover fragment IDs dynamically from workflow Pipeline

#### Implementation Details

**Layout Structure:**
```
???????????????????????????????????????????????????????????????
?                    WorkflowAssetsPanel                       ?
???????????????????????????????????????????????????????????????
?  PromptsForm (full width, includes Generate button)         ?
???????????????????????????????????????????????????????????????
?  LoraForm                  ?                                ?
?  SourcesPanel (if any)     ?   GeneratedImageTabs /         ?
?  LatentForm                ?   GeneratedVideoTabs           ?
?  SamplerForm               ?                                ?
?  [Optional Features]       ?                                ?
?   - Upscale                ?                                ?
?   - SeedVR2                ?                                ?
?   - CondVar                ?                                ?
?   - SVE                    ?                                ?
???????????????????????????????????????????????????????????????
```

**Key Implementation Patterns:**
1. **Hybrid State Approach:**
   - Local variables (`_prompt`, `_width`, `_steps`, etc.) for UI binding
   - Synced to fragment values on change
   - Initialized from fragments on workflow load

2. **Fragment Discovery:**
   - `DiscoverFragments()` iterates workflow Pipeline
   - Matches fragment files to `Parameters.Fragments` keys
   - Sets `_latentFragmentId`, `_samplerFragmentId`, etc.

3. **Two GetFragmentValue Methods:**
   - `GetFragmentValue<T>()` for reference types (string)
   - `GetFragmentValueNullable<T>()` for value types (int, long, float)

4. **Optional Fragments:**
   - `GetOptionalFragments()` returns fragments that need CollapsibleFeatureSection
   - `RenderOptionalFragmentForm()` creates RenderFragment for each type
   - Each uses the dynamic component rendering pattern

#### Files Modified
- `Pages/Generate.razor` - Complete rewrite

---

### Step 8.6: Wire PromptFields to GenerationParameters
**Complexity:** -
**Status:** [x] Merged into Step 8.4c

*This step has been merged into Step 8.4c (PromptsForm refactor). The original plan to create an adapter has been replaced with a full refactor of PromptFields into PromptsForm.*

---

### Step 8.7: Wire Sources to Workflow Composition
**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks
- [ ] Verify `WorkflowService.ComposeWorkflowFromTemplate` receives Sources
- [ ] Ensure source data is passed to Scriban template context
- [ ] Template should access `{{ Image }}` for source image data
- [ ] Test with qwen/img2img-edit.sbn workflow

#### Files
- `Services/WorkflowService.cs`
- `Services/ImageService.cs`

---

### Step 8.8: Test All Workflow Types
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Test Flux txt2img workflow
- [ ] Test SD txt2img workflow
- [ ] Test Qwen img2img-edit workflow (with source image)
- [ ] Test Wan img2vid workflow (with source image)
- [ ] Verify all parameter changes persist
- [ ] Verify generation works for each type

#### Test Checklist
| Workflow | Sources | Resolution | Sampler | Toggles | Generate |
|----------|---------|------------|---------|---------|----------|
| Flux Txt2Img | N/A | [ ] | [ ] | [ ] | [ ] |
| SD Txt2Img | N/A | [ ] | [ ] | [ ] | [ ] |
| Qwen Img2Img | [ ] | [ ] | [ ] | [ ] | [ ] |
| Wan Img2Vid | [ ] | [ ] | [ ] | [ ] | [ ] |

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 8.1 | [x] | 1 | NavBar links &rarr; Fixed StableDiffusion enum bug |
| 8.2 | [x] | 3 | ResolutionPanel |
| 8.3 | [x] | 3 | SamplerSettingsPanel &rarr; Replaced by SamplerForm |
| 8.4 | [x] | 5 | Fragment form components + CollapsibleFeatureSection |
| 8.4b | [x] | 3 | Rewire templates to use empty-latent.sbn |
| 8.4c | [x] | 5 | PromptsForm refactor (PromptFields &rarr; PromptsForm) |
| 8.5 | [x] | 5 | Generate.razor rewrite |
| 8.6 | [x] | - | *(Merged into 8.4c)* |
| 8.7 | [ ] | 3 | Sources to workflow |
| 8.8 | [ ] | 2 | E2E testing |

**Total Complexity:** 30 points

---

## Files Modified This Phase

| File | Changes |
|------|---------|
| `Components/Shared/NavBar.razor` | Updated links to `/generate/{id}`, added color icons, fixed enum bug |
| `Components/Shared/Generation/ResolutionPanel.razor` | New component (sub-component for resolution controls) |
| `Components/Shared/Generation/CollapsibleFeatureSection.razor` | New component (toggle+collapse wrapper) |
| `Workflows/Fragments/empty-latent.sbn` | New fragment for latent/resolution |
| `Workflows/Fragments/upscale.sbn` | Added UI schema |
| **Fragments Folder Components:** | |
| `Components/Shared/Generation/Fragments/PromptsForm.razor` | New - replaces PromptFields for new pages |
| `Components/Shared/Generation/Fragments/SamplerForm.razor` | New - replaces SamplerSettingsPanel |
| `Components/Shared/Generation/Fragments/LatentForm.razor` | New - wraps ResolutionPanel + batch size |
| `Components/Shared/Generation/Fragments/UpscaleForm.razor` | New - for upscale.sbn |
| `Components/Shared/Generation/Fragments/SeedVR2Form.razor` | New - for upscale-seedvr2.sbn |
| `Components/Shared/Generation/Fragments/ConditioningVariationForm.razor` | Moved + refactored |
| `Components/Shared/Generation/Fragments/SeedVarianceEnhancerForm.razor` | Moved + refactored |
| **Loader Fragments (latent removed):** | |
| `Workflows/Fragments/load-diffusion.sbn` | Removed latent node |
| `Workflows/Fragments/load-diffusion-w-prompts.sbn` | Removed latent node |
| `Workflows/Fragments/load-checkpoint.sbn` | Removed latent node |
| **Template Updates (added empty-latent.sbn):** | |
| `Workflows/Templates/z-image/txt2img.sbn` | Added `empty-latent.sbn` fragment |
| `Workflows/Templates/sd/txt2img.sbn` | Added `empty-latent.sbn` with EmptyLatentImage |
| `Workflows/Templates/qwen/txt2img.sbn` | Added `empty-latent.sbn` fragment |
| `flux/txt2img.sbn` | No change (special case) |
| **Removed:** | |
| `Components/Shared/Generation/SamplerSettingsPanel.razor` | Replaced by SamplerForm |
| `Components/Shared/Generation/ConditioningVariationForm.razor` | Moved to Fragments/ |
| `Components/Shared/Generation/SeedVarianceEnhancerForm.razor` | Moved to Fragments/ |
| `Components/Shared/Generation/ToggleableFeaturesPanel.razor` | Removed (was tightly coupled) |
| **Services:** | |
| `Services/ComponentRegistry.cs` | Added PromptsForm registration |
| **Pages:** | |
| `Pages/Generate.razor` | Complete layout rewrite - matches Txt2Img pattern |
| **Pending:** | |
| `Services/WorkflowService.cs` | Sources in template context (Step 8.7) |

---

## Folder Structure

After this phase, the Generation folder structure is:

```
Components/Shared/Generation/
??? Fragments/                          # Fragment-specific form components
?   ??? PromptsForm.razor               # prompts (replaces PromptFields for new pages)
?   ??? SamplerForm.razor               # sampler.sbn
?   ??? LatentForm.razor                # empty-latent.sbn  
?   ??? UpscaleForm.razor               # upscale.sbn
?   ??? SeedVR2Form.razor               # upscale-seedvr2.sbn
?   ??? ConditioningVariationForm.razor # conditioning-variation.sbn
?   ??? SeedVarianceEnhancerForm.razor  # seed-variance-enhancer.sbn
??? CollapsibleFeatureSection.razor     # Toggle+collapse wrapper
??? ResolutionPanel.razor               # Sub-component for resolution
??? PromptFields.razor                  # Legacy prompt input (kept for old pages)
??? LoraForm.razor                      # LoRA management
??? SourcesPanel.razor                  # Image/video input sources
??? SourceItem.razor                    # Individual source
??? GenerateButton.razor                # Generate action button
```

---

## Design Decisions

### Reuse Existing Components
Rather than building from scratch:
- `PromptFields.razor` - Existing, may need adapter
- `LoraForm.razor` - Existing, reuse as-is
- `SourcesPanel.razor` + `SourceItem.razor` - Already working
- `WorkflowAssetsPanel.razor` - Existing, reuse as-is
- `GeneratedImageTabs.razor` / `GeneratedVideoTabs.razor` - Existing

### Extract vs Duplicate
For ResolutionPanel and SamplerSettingsPanel:
- **Extract** from GenerateFormTxt2Img.razor (keep old pages working during transition)
- Don't delete old components yet (Phase 10 handles legacy removal)

### Fragment Parameter Mapping
New panels read/write from `GenerationParameters.Fragments`:
- `Fragments["main_sampler"].Values["steps"]`
- `Fragments["main_sampler"].Values["seed"]`
- `Fragments["main_sampler"].Values["cfg"]`
- `Fragments["prompts"].Values["prompt"]`
- `Fragments["prompts"].Values["negative_prompt"]`

### Workflow Base Detection
For CFG vs Guidance:
- Flux uses "Guidance" (distilled CFG)
- Other bases use standard "CFG Scale"
- Check `_selectedWorkflow.Base == ModelBase.Flux`

---

## Issues &amp; Resolutions

| Issue | Resolution |
|-------|------------|
| StableDiffusion workflows not showing in navbar | `ModelBase.StableDiffusion` is at enum index 0, same as `default(ModelBase)`. The `!= default` check incorrectly filtered it out. Fixed by using `HasWorkflowBaseSelected()` method that checks if current base has matching workflows. |
| MudSlider values snapping back to 0 | MudBlazor controls need local state with `@bind-Value:after` pattern. Using `Value` + `ValueChanged` directly causes race conditions where the component re-renders before parent state updates. Fixed in `ResolutionPanel.razor`, `SamplerForm.razor`, `LatentForm.razor`, `DynamicField.razor`, `SeedVR2Form.razor`, `UpscaleForm.razor`, `ConditioningVariationForm.razor`, `SeedVarianceEnhancerForm.razor`. |
| `FragmentParameters.GetValueOrDefault<T>` returning 0 for value types | The `value ?? defaultValue` pattern doesn't work for value types since `GetValue<T>` returns `default(T)` (0 for numbers), not `null`. Fixed by checking key existence first in `GetValueOrDefault`. |
| Optional fragments included in workflow even when disabled | Fragments were all initialized with `IsActive = true`. Fixed `GenerationParameterService.InitializeFragmentsFromPipeline` to read `defaultCollapsed` from the fragment's UI schema. If `defaultCollapsed = true`, the fragment starts as `IsActive = false` (optional, not included). |
| CollapsibleFeatureSection expanding by default | `DefaultExpanded` parameter wasn't being passed. Added `DefaultExpanded="false"` to the `CollapsibleFeatureSection` usage in `Generate.razor`. |

---

## Commit Checkpoints

- [x] After Step 8.1 complete (NavBar updated)
- [x] After Step 8.2 complete (ResolutionPanel)
- [x] After Step 8.3 complete (SamplerSettingsPanel)
- [x] After Step 8.4 complete (Fragment forms created)
- [x] After Step 8.4b complete (Templates rewired)
- [x] After Step 8.4c complete (PromptsForm refactor plan)
- [x] After Step 8.5 complete (Generate.razor rewritten)
- [ ] After Step 8.8 complete (All workflows tested)

---

## Deferred Items (Move to Phase 9+)

The following were in original Phase 8 but are deferred:
- **VideoSourceItem improvements** - Current implementation works, polish later
- **Node Chaining UI** - Phase 9 scope
- **Advanced validation UI** - Can add after core functionality works

---

**Phase Status:** Not Started [ ]
