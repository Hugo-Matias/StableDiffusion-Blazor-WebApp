# WebUI Removal - Implementation Plan

## Status
**Current Phase:** Planning
**Last Updated:** 2025-01-XX

---

## Problem Statement

The codebase currently supports two backend systems for image generation:
1. **WebUI (A1111/AUTOMATIC1111)** - The original backend, now deprecated
2. **ComfyUI** - The current and only supported backend going forward

Compromises were made during the ComfyUI integration to maintain backward compatibility with WebUI. This creates:
- Code duplication and maintenance burden
- Confusing dual-path logic throughout the codebase
- Unused DTOs, services, and UI components
- Configuration clutter

**Goal:** Remove all WebUI references and configurations that wouldn't break ComfyUI integration, resulting in a cleaner, ComfyUI-focused codebase.

---

## Proposed Solution

A phased removal approach that:
1. Preserves all ComfyUI functionality
2. Removes WebUI-specific code paths
3. Cleans up shared code that has WebUI-only logic
4. Simplifies the architecture

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| Keep `SDAPIService` temporarily | Some utility methods may need migration to ComfyUI |
| Remove WebUI pages first | Clear, isolated removal |
| Keep script settings that ComfyUI uses | ADetailer, ControlNet configs used by ComfyUI workflows |
| Remove `IsWebuiUp` state checks | Simplifies conditional rendering |
| Keep ScriptParameters DTOs temporarily | Used by ManagerService and UI components even for ComfyUI |

### Conventions
- Mark files for removal with `[REMOVE]` in commit messages
- Test ComfyUI generation after each phase
- Preserve any shared models/DTOs used by ComfyUI

---

## Scope Analysis

### Files to Remove (WebUI-Specific)

#### Pages (4 files)
- [ ] `BlazorWebApp/Pages/WebUI/Txt2ImgWebUI.razor`
- [ ] `BlazorWebApp/Pages/WebUI/Img2ImgWebUI.razor`
- [ ] `BlazorWebApp/Pages/WebUI/UpscaleWebUI.razor`
- [ ] `BlazorWebApp/Pages/WebUI/UpscaleWebUI.razor.css`

#### WebUI-Specific DTOs (3 files - Safe to Remove)
- [ ] `BlazorWebApp/Data/Dtos/WebUI/Txt2ImgWebUI.cs`
- [ ] `BlazorWebApp/Data/Dtos/WebUI/Img2ImgWebUI.cs`
- [ ] `BlazorWebApp/Data/Dtos/WebUI/UpscaleWebUI.cs`

#### ScriptParameters DTOs (10 files - KEEP for Phase 1, evaluate later)
These are used by `ManagerService` to initialize parameters and by UI components:
- `BlazorWebApp/Data/Dtos/WebUI/SharedWebUI.cs` - Contains base script parameter classes
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersADetailer.cs` - **Used by ComfyUI** (detailer fragments)
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersControlNet.cs` - **Used by ComfyUI** (some preprocessors)
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersCutoff.cs` - WebUI-only
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersDynamicPrompts.cs` - WebUI-only (replaced by LLM Enhancer)
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersIncantations.cs` - WebUI-only
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersMultiDiffusion.cs` - WebUI-only
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersRegionalPrompter.cs` - WebUI-only
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersUltimateUpscale.cs` - WebUI-only
- `BlazorWebApp/Data/Dtos/WebUI/ScriptParametersXYZPlot.cs` - WebUI-only

#### Services
- [ ] `BlazorWebApp/Services/SDAPIService.cs` (full removal after dependencies cleaned)

### Files to Modify

#### Services
- [ ] `BlazorWebApp/Services/ManagerService.cs`
  - Remove `IsWebuiUp` property and `OnWebuiStateChanged` event
  - Remove `_sdapi` field and dependency injection
  - Remove WebUI-specific code paths in methods
  - Simplify dual-path methods to ComfyUI-only

#### Models
- [ ] `BlazorWebApp/Models/AppSettings.cs`
  - Remove `WebuiSettingsModel` class
  - Remove `Webui` property from `AppSettings`
  - Remove `using BlazorWebApp.Data.Dtos.WebUI;`
  - **Keep**: Script settings used by ComfyUI (ControlNet, ADetailer)
  - **Evaluate**: Other script settings in ScriptsSettingsModel

- [ ] `BlazorWebApp/Models/CmdFlags.cs` - WebUI-only, remove

#### Components
- [ ] `BlazorWebApp/Components/Shared/NavBar.razor`
  - Remove `M.IsWebuiUp` check and WebUI navigation links
  - Remove `M.OnWebuiStateChanged` subscription

- [ ] `BlazorWebApp/Components/Shared/MainLayout.razor`
  - Remove `M.IsWebuiUp` references
  - Remove `OnWebuiStateChanged` subscription
  - Remove `SDAPI.CheckWebuiState()` call in OnInitializedAsync

- [ ] `BlazorWebApp/Components/Shared/Generation/ADetailerForm.razor`
  - Remove `M.IsWebuiUp` conditional rendering
  - Keep only ComfyUI path (`ADetailerModelFormComfyUI`)

- [ ] `BlazorWebApp/Components/Shared/Generation/ControlNetForm.razor`
  - Uses `SDAPIService` for ControlNet models - needs alternative

#### Program.cs
- [ ] Remove `SDAPIService` registration

---

## Implementation Phases

### Phase 1: Remove WebUI Pages and Navigation
**Objective:** Remove the WebUI-specific page components and navigation
**Status:** [ ] Not Started

#### Tasks
- [ ] Delete `BlazorWebApp/Pages/WebUI/` folder and all contents
- [ ] Update `NavBar.razor`:
  - Remove `M.IsWebuiUp` conditional and WebUI nav links
  - Remove `M.OnWebuiStateChanged` subscription
- [ ] Update `MainLayout.razor`:
  - Remove `SDAPI.CheckWebuiState()` call
  - Remove `M.IsWebuiUp` assignment
  - Remove `OnWebuiStateChanged` subscription
  - Simplify `OnBackendStateChanged()` method
- [ ] Build and verify no broken references

#### Success Criteria
- No WebUI pages accessible
- ComfyUI pages still functional
- Clean build with no errors

---

### Phase 2: Remove WebUI-Only DTOs
**Objective:** Remove WebUI-specific data transfer objects
**Status:** [ ] Not Started

#### Tasks
- [ ] Delete `BlazorWebApp/Data/Dtos/WebUI/Txt2ImgWebUI.cs`
- [ ] Delete `BlazorWebApp/Data/Dtos/WebUI/Img2ImgWebUI.cs`
- [ ] Delete `BlazorWebApp/Data/Dtos/WebUI/UpscaleWebUI.cs`
- [ ] Update any remaining references (will cause compile errors to fix)
- [ ] Build and verify

#### Success Criteria
- No WebUI-specific DTOs for generation
- Clean build

---

### Phase 3: Clean ManagerService (IsWebuiUp Removal)
**Objective:** Remove WebUI state tracking and simplify service
**Status:** [ ] Not Started

#### Tasks
- [ ] Remove `IsWebuiUp` property
- [ ] Remove `OnWebuiStateChanged` event
- [ ] Remove `_isWebuiUp` field
- [ ] Simplify methods that had dual WebUI/ComfyUI paths:
  - `GetWorkflowModels()` - Remove WebUI branch
  - `GetSDVAEs()` - Remove WebUI branch (uses CmdFlags)
  - `GetSDADetailerModels()` - Remove WebUI branch
  - `GetSamplers()` - Remove WebUI branch
  - `GetSchedulers()` - Remove WebUI branch
  - `GetUpscalers()` - Remove WebUI branch
  - `GetStyles()` - Remove WebUI ternary
  - `GetOptions()` - Remove WebUI branch
  - `GetResourceTypeDirectories()` - Remove WebUI branch
  - `SerializeInfo()` - Remove WebUI branch
  - `GetDynamicPromptsVersion()` - Remove method entirely (WebUI-only)
  - `SetCurrentModel()` - Remove WebUI PostOptions call
  - `SetCurrentVae()` - Remove WebUI PostOptions call
  - `ConvertPathTag()` - Simplify model_name handling
- [ ] Remove `PostOptions()` method (only calls WebUI API)
- [ ] Remove `GetCmdFlags()` method (WebUI-only)
- [ ] Build and verify

#### Success Criteria
- No `IsWebuiUp` references in ManagerService
- All methods use ComfyUI paths only
- Clean build

---

### Phase 4: Clean UI Components (WebUI Conditionals)
**Objective:** Remove WebUI conditionals from remaining UI components
**Status:** [ ] Not Started

#### Tasks
- [ ] Update `ADetailerForm.razor`:
  - Remove `M.IsWebuiUp` conditional
  - Keep only `ADetailerModelFormComfyUI` component
- [ ] Update `ControlNetForm.razor`:
  - Replace `SDAPIService` call with ComfyUI alternative for ControlNet models
  - Or remove ControlNet form if not used in ComfyUI
- [ ] Search for other `IsWebuiUp` usages and clean up
- [ ] Search for `SDAPI` usages in components
- [ ] Build and verify

#### Success Criteria
- No `IsWebuiUp` references in UI
- No direct `SDAPIService` usage in components
- Clean build

---

### Phase 5: Remove SDAPIService and Dependencies
**Objective:** Remove the WebUI API service completely
**Status:** [ ] Not Started

#### Tasks
- [ ] Remove `_sdapi` field from `ManagerService` constructor
- [ ] Delete `BlazorWebApp/Services/SDAPIService.cs`
- [ ] Remove from `Program.cs` DI registration: `builder.Services.AddHttpClient<SDAPIService>();`
- [ ] Remove `using BlazorWebApp.Data.Dtos.WebUI;` from ManagerService
- [ ] Delete `BlazorWebApp/Models/CmdFlags.cs`
- [ ] Remove `CmdFlags` property from ManagerService
- [ ] Fix any remaining compile errors
- [ ] Build and verify

#### Success Criteria
- No SDAPIService in codebase
- No CmdFlags model
- Clean build

---

### Phase 6: Clean AppSettings & Script Parameters
**Objective:** Remove WebUI settings and evaluate script parameters
**Status:** [ ] Not Started

#### Tasks
- [ ] Remove `WebuiSettingsModel` class from `AppSettings.cs`
- [ ] Remove `Webui` property from `AppSettings`
- [ ] Remove `using BlazorWebApp.Data.Dtos.WebUI;` directive
- [ ] Fix ManagerService: `CreateADetailerModel()` uses `Settings.Webui.ClipSkip.Value`
  - Either keep ClipSkip in a different location or use default
- [ ] Review ScriptsSettingsModel - identify WebUI-only settings:
  - **Keep**: ControlNet, ADetailer (used by ComfyUI)
  - **Remove**: Cutoff, DynamicPrompts, MultiDiffusion, RegionalPrompter, UltimateUpscale, XYZPlot, Incantations
- [ ] Build and verify

#### Success Criteria
- No WebUI-specific settings
- Settings used by ComfyUI preserved
- Clean build

---

### Phase 7: Remove WebUI Script Parameters (Deferred)
**Objective:** Remove WebUI-only ScriptParameters DTOs
**Status:** [ ] Not Started

**Note:** This phase may require significant refactoring of `SharedParameters`, `Txt2ImgParameters`, and `Img2ImgParameters` as they have `Scripts` property containing all script parameters.

#### Tasks
- [ ] Analyze usage of Scripts property in parameters
- [ ] Determine if Scripts property should be simplified for ComfyUI
- [ ] Remove unused script parameter classes:
  - `ScriptParametersCutoff.cs`
  - `ScriptParametersDynamicPrompts.cs`
  - `ScriptParametersIncantations.cs`
  - `ScriptParametersMultiDiffusion.cs`
  - `ScriptParametersRegionalPrompter.cs`
  - `ScriptParametersUltimateUpscale.cs`
  - `ScriptParametersXYZPlot.cs`
- [ ] Simplify or remove `SharedWebUI.cs` if no longer needed
- [ ] Update ManagerService script initializers
- [ ] Build and verify

#### Success Criteria
- Only ComfyUI-relevant script parameters remain
- Parameters models simplified
- Clean build

---

### Phase 8: Final Cleanup
**Objective:** Remove any remaining WebUI artifacts
**Status:** [ ] Not Started

#### Tasks
- [ ] Search codebase for "WebUI", "webui", "WEBUI" strings
- [ ] Search for "IsWebuiUp", "WebuiState" references
- [ ] Search for "sdapi", "SDAPI" references
- [ ] Search for "CmdFlags" references
- [ ] Remove any dead code paths
- [ ] Update comments and documentation
- [ ] Bump StateVersion in appsettings if settings schema changed
- [ ] Build and full test

#### Success Criteria
- No WebUI references in codebase (except historical comments if needed)
- All ComfyUI features functional
- Clean, simplified codebase

---

## Stress Points & Risks

| Risk | Mitigation |
|------|------------|
| Script parameters used by ComfyUI | Carefully review which ScriptParameters are used in ComfyUI workflow templates before removing |
| State migration issues | Ensure existing saved states don't break when WebUI settings are removed |
| Shared models between backends | Map dependencies before removing to avoid breaking ComfyUI |
| ControlNetForm uses SDAPIService | Need to find ComfyUI alternative or remove form |
| ADetailer uses Settings.Webui.ClipSkip | Move ClipSkip to different settings location or use default |
| Scripts property in parameters | May need significant refactoring to simplify |

---

## Dependencies to Verify

Before removing ScriptParameters, verify if they're used in:
- [x] ComfyUI workflow templates (`.sbn` files) - Only detailer fragments use ADetailer-like params
- [x] ComfyUI DTO mappings (`Txt2ImgComfyUI.cs`, etc.)
- [x] ManagerService initialization - All scripts initialized but most WebUI-only
- [x] UI Components - ADetailerForm, ControlNetForm, and others use script params

### Script Parameters Usage Analysis

| Script | Used in ComfyUI? | Used in UI? | Action |
|--------|-----------------|-------------|--------|
| ControlNet | Partially (preprocessor enum) | Yes (ControlNetForm.razor) | **KEEP for now** |
| ADetailer | Yes (detailer fragments) | Yes (ADetailerForm.razor) | **KEEP** |
| Cutoff | No | Yes (CutoffForm.razor) | Remove (Phase 7) |
| DynamicPrompts | No (LLM Enhancer replaced) | Yes (DynamicPromptsForm.razor) | Remove (Phase 7) |
| MultiDiffusion | No | Yes (MultiDiffusion forms) | Remove (Phase 7) |
| RegionalPrompter | No | Yes (RegionalPrompterForm.razor) | Remove (Phase 7) |
| UltimateUpscale | No | Yes (UltimateUpscaleForm.razor) | Remove (Phase 7) |
| XYZPlot | No | Yes (XYZPlotForm.razor) | Remove (Phase 7) |
| Incantations | No | Yes (IncantationsForm.razor) | Remove (Phase 7) |

---

## Changelog

| Date | Phase | Changes |
|------|-------|---------|
| 2025-XX-XX | Planning | Initial plan created |

---

## Code Examples

### Before: NavBar.razor with WebUI
```razor
@if (M.Projects.Count > 0)
{
    if (M.IsWebuiUp)
    {
        <MudNavLink Href="/webui/txt2img" ...>Txt2Img</MudNavLink>
        <MudNavLink Href="/webui/img2img" ...>Img2Img</MudNavLink>
        <MudNavLink Href="/webui/upscale" ...>Upscale</MudNavLink>
    }
    if (M.IsComfyUIUp && M.State.Generation.Workflows != null ...)
    {
        // ComfyUI workflow links
    }
}
```

### After: NavBar.razor ComfyUI Only
```razor
@if (M.Projects.Count > 0 && M.IsComfyUIUp && M.State.Generation.Workflows != null ...)
{
    // ComfyUI workflow links only
}
```

### Before: ManagerService.GetSamplers()
```csharp
public async Task GetSamplers()
{
    if (IsWebuiUp) Samplers = await _sdapi.GetSamplers();
    else if (IsComfyUIUp) Samplers = await _capi.GetSamplers();
    else Samplers = new();
    OnSamplersSchedulersChanged?.Invoke();
}
```

### After: ManagerService.GetSamplers()
```csharp
public async Task GetSamplers()
{
    Samplers = IsComfyUIUp ? await _capi.GetSamplers() : new();
    OnSamplersSchedulersChanged?.Invoke();
}
```

### Before: ADetailerForm.razor
```razor
@if (M.IsWebuiUp)
{
    <MudTabs ...>
        <MudTabPanel Text="Model 1">
            <ADetailerModelForm Parameters="Parameters.Model1"/>
        </MudTabPanel>
        ...
    </MudTabs>
}
@if (M.IsComfyUIUp)
{
    <ADetailerModelFormComfyUI Parameters="Parameters.Model1" Mode="Mode"/>
}
```

### After: ADetailerForm.razor
```razor
<ADetailerModelFormComfyUI Parameters="Parameters.Model1" Mode="Mode"/>
```

---

## References

- `BlazorWebApp/Models/AppSettings.cs` - Main settings file
- `BlazorWebApp/Services/ManagerService.cs` - Central service orchestrating backends
- `BlazorWebApp/Services/SDAPIService.cs` - WebUI API service to remove
- `BlazorWebApp/Components/Shared/NavBar.razor` - Navigation with dual backend support
- `BlazorWebApp/Data/Dtos/WebUI/` - WebUI-specific DTOs folder
- `BlazorWebApp/Pages/WebUI/` - WebUI pages folder
- `BlazorWebApp/Components/Shared/Generation/ADetailerForm.razor` - Uses IsWebuiUp
- `BlazorWebApp/Components/Shared/Generation/ControlNetForm.razor` - Uses SDAPIService
