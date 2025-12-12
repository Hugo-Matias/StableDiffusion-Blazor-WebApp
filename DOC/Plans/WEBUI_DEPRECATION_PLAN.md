# WebUI Deprecation & ComfyUI Simplification Plan

## Status
**Phase:** Phase 6 - Rename ComfyUI Components & Pages ? COMPLETE
**Started:** 2025-01-14  
**Current Step:** Phase 6.5 - Remove WebUI-Only Scripts (to fix compilation errors)
**Next Step:** Phase 6.5 - Remove WebUI-Only Scripts

---

## Changelog

| Date | Phase | Description |
|------|-------|-------------|
| 2025-01-14 | Phase 6 | ? **ComfyUI Files Renamed!** Successfully renamed 7 files (3 pages + 4 components) removing "ComfyUI" suffix. Updated @page directives from `/comfyui/*` to `/*`. Updated NavBar routes. Removed empty Pages/ComfyUI folder. ADetailerModelFormComfyUI.razor skipped (will be removed in Phase 6.5). **Still 103 compilation errors** from script system - will be fixed in Phase 6.5. Ready for Phase 6.5. |
| 2025-01-14 | Phase 5 | ? **SDAPIService Removed!** Successfully removed SDAPIService.cs file and DI registration from Program.cs. **Expected:** 103 compilation errors (up from 92) - added errors from ManagerService, RouterService, ImageService, WildcardsPanel, and Resources page. All will be fixed in Phase 6.5 (script cleanup). Ready for Phase 6.5. |
| 2025-01-14 | Phase 4 | ? **WebUI DTOs Removed!** Successfully removed entire Data/Dtos/WebUI folder containing 13 files (4 WebUI API DTOs + 9 Script parameter DTOs). **Expected:** 92 compilation errors from script forms, model classes, and ManagerService factory methods - these will be fixed in Phase 6.5 (script cleanup). Decision: Proceed to Phase 6.5 next to fix compilation errors before Phase 5. Ready for Phase 6.5. |
| 2025-01-14 | Phase 3 | ? **WebUI Components Removed!** Successfully removed all 4 WebUI component files (GenerateFormTxt2Img.razor + CSS, GenerateFormImg2Img.razor + CSS). Build passes without errors. Note: UltimateUpscaleForm and ADetailerModelForm will be removed in Phase 6.5 (Script cleanup). Ready for Phase 4. |
| 2025-01-14 | Phase 2 | ? **WebUI Pages Removed!** Successfully removed all 4 WebUI page files (Txt2ImgWebUI.razor, Img2ImgWebUI.razor, UpscaleWebUI.razor, UpscaleWebUI.razor.css). Removed empty Pages/WebUI folder. Build passes without errors. No navigation references to update (NavBar only shows ComfyUI workflows). Ready for Phase 3. |
| 2025-01-14 | Phase 1.7 | ? **Updated Decision - Remove ALL Scripts!** After review, decided to remove ControlNet and ADetailer as well. Both have partial/incomplete ComfyUI implementations not actively used in generation pages. Better to start fresh with clean ComfyUI workflow-based design. **Impact:** Now removing ALL 9 scripts (0 kept), 14 forms, 9 DTOs, ~450 lines from ManagerService, ~800 lines from AppSettings.cs. Total: ~1500+ lines removed. Maintains current Settings ? State ? DTO architecture. Future scripts will be ComfyUI-native. Restored Phases 7 & 8, file/folder matrix. Updated estimate: ~16 hours (up from 14). |
| 2025-01-14 | Phase 1.7 | ? **Script Parameters Deep Audit Complete!** Analyzed all 9 ScriptParameters DTOs. Discovered only 2 scripts (ControlNet, ADetailer) have partial ComfyUI implementation. 7 scripts are WebUI-only extensions with no ComfyUI equivalent. Identified ~1000+ lines of initialization boilerplate that can be removed. Created Phase 6.5 for script cleanup. Updated impact assessment: now 60 files affected (up from 42), ~14 hours estimated (up from 10.5). |
| 2025-01-14 | Phase 1 | ? **COMPLETE** - Audit complete! Found 14 files to remove, 17 to move/rename, ~42 total files affected. Risk: Low. Estimated effort: ~10.5 hours. Ready to proceed with Phase 2. |
| 2025-01-14 | Phase 1 | Created deprecation plan. Starting audit of WebUI code. |

---

## Objective

Remove all Automatic1111 WebUI-related code and simplify ComfyUI naming conventions:
- **Remove:** All WebUI backend components, pages, and DTOs
- **Rename:** `*ComfyUI` components/pages ? `*` (remove ComfyUI suffix)
- **Simplify:** Architecture to support only ComfyUI as the primary backend
- **Clean:** Reduce migration workload by removing obsolete code

---

## Rationale

1. **Single Backend Focus:** ComfyUI is now the primary and only supported backend
2. **Simplified Naming:** No need for "ComfyUI" suffix when it's the only option
3. **Reduced Complexity:** Less code to maintain and migrate
4. **Clearer Architecture:** One implementation per feature
5. **Migration Efficiency:** Fewer components to migrate in Phase 8

---

## Known WebUI Locations

### 1. Pages (WebUI Folder)
- `BlazorWebApp/Pages/WebUI/` - Entire folder can be removed
- Expected files:
  - `Txt2Img.razor`
  - `Img2Img.razor`
  - `Upscale.razor` (or `Extras.razor`)
  - Any other WebUI-specific pages

### 2. Data Transfer Objects (WebUI Folder)
- `BlazorWebApp/Data/Dtos/WebUI/` - Entire folder needs evaluation
- Contains WebUI API request/response models
- Some DTOs might be reused by script parameters (need careful analysis)

### 3. Components
- `BlazorWebApp/Components/Txt2Img/GenerateFormTxt2Img.razor`
- `BlazorWebApp/Components/Img2Img/GenerateFormImg2Img.razor`
- `BlazorWebApp/Components/Shared/Generation/UltimateUpscaleForm.razor` (WebUI-specific)
- `BlazorWebApp/Components/Shared/Generation/ADetailerModelForm.razor` (WebUI-specific)
- Any other components with WebUI-specific logic

### 4. Services
- `SDAPIService.cs` - Automatic1111 API service (entire service can be removed)
- Methods in other services that check `IsWebuiUp` or use WebUI-specific logic

### 5. Models/Parameters
- WebUI-specific parameter classes in `Models/` folder
- Script parameters that are WebUI-only

---

## Implementation Phases

### Phase 1: Audit & Impact Analysis ? COMPLETE
**Objective:** Identify all WebUI-related code and assess removal impact

#### Tasks
- [x] **1.1 Scan Pages/WebUI Folder**
  - List all files in `Pages/WebUI/`
  - Document what each page does
  - Identify any shared components used by these pages
  
- [x] **1.2 Scan Data/Dtos/WebUI Folder**
  - List all DTO classes
  - Identify which DTOs are WebUI-specific
  - Find DTOs that might be reused elsewhere (script parameters)
  - Document dependencies on these DTOs
  
- [x] **1.3 Search for WebUI Components**
  - Find all `GenerateForm*` components (non-ComfyUI)
  - Search for components checking `M.IsWebuiUp` or `Backend.IsBackendAvailable == false`
  - Identify WebUI-specific script forms
  
- [x] **1.4 Search for SDAPIService Usage**
  - Find all `@inject SDAPIService` directives
  - Find all constructor injections of `SDAPIService`
  - Document what each usage does
  
- [x] **1.5 Search for ComfyUI-suffixed Files**
  - Find all files with "ComfyUI" in the name
  - List files that need renaming
  - Check for routing configurations that need updates

- [x] **1.6 Create Impact Matrix**
  - Document all files to be removed
  - Document all files to be renamed
  - Document potential breaking changes
  - Estimate effort per file

- [x] **1.7 Script Parameters Deep Audit** ? NEW
  - Analyzed all 9 ScriptParameters DTOs for ComfyUI usage
  - Identified which scripts are actually implemented in ComfyUI
  - Documented initialization complexity in ManagerService
  - Evaluated simplification opportunities

#### Deliverables
- Complete file inventory (? Added below)
- Impact assessment matrix (? Complete)
- Risk analysis (? Complete)
- Script parameters analysis (? NEW - Added below)
- Phase 2 preparation checklist

---

### Phase 1.7 Results: Script Parameters Deep Audit

#### Script Implementation Status in ComfyUI

| Script | ComfyUI Implementation | WebUI Only | Decision | Rationale |
|--------|----------------------|------------|----------|-----------|
| **ControlNet** | ?? Partial | ? No | ? **REMOVE** | Partial implementation, not used in generation pages. Better to reimplement cleanly. |
| **ADetailer** | ?? Partial (Separate) | ? No | ? **REMOVE** | Separate WebUI/ComfyUI forms indicate incomplete integration. Reimplement with workflows. |
| **Cutoff** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only extension, no ComfyUI equivalent |
| **DynamicPrompts** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only extension, complex HuggingFace integration |
| **RegionalPrompter** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only extension, region-based prompting |
| **MultiDiffusion (TiledDiffusion)** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only extension, tiled generation |
| **MultiDiffusion (TiledVAE)** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only extension, VAE tiling |
| **UltimateUpscale** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only script, tiled upscaling |
| **XYZPlot** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only script, parameter grid generation |
| **Incantations** | ? Not Implemented | ? Yes | ? **REMOVE** | WebUI-only extension (PAG, MultiConcept, Seek) |

**Summary:**
- **Keep:** 0 scripts (starting fresh for ComfyUI)
- **Remove:** 9 scripts (all have issues or are WebUI-only)

**Decision Rationale:**
Even ControlNet and ADetailer, which have some ComfyUI code, are:
1. Not actively used in current generation pages
2. Have incomplete/split implementations (WebUI vs ComfyUI forms)
3. Part of the legacy WebUI-era initialization complexity
4. Better reimplemented with clean ComfyUI workflow-based design

**Future Plan:** When ComfyUI scripts are needed, implement from scratch using:
- Workflow-based parameter mapping (not WebUI factory pattern)
- Direct integration with ComfyUI nodes
- Simpler initialization (no complex factory methods)

#### Initialization Complexity Analysis

**Current Situation:**
ManagerService has **10 script factory methods** (`Create*` methods) totaling **~450 lines** of boilerplate code. Each method:
1. Reads from `Settings.Scripts.*` configuration
2. Initializes complex nested objects
3. Returns fully configured `ScriptParameters*` instances

**Example of Current Complexity:**
```csharp
public ScriptParametersADetailer CreateADetailer()
{
    return new ScriptParametersADetailer()
    {
        IsEnabled = Settings.Scripts.ADetailer.IsEnabled,
        IsAlwaysOn = true,
        SkipImg2Img = false,
        Model1 = CreateADetailerModel(Settings.Scripts.ADetailer.Models[0]),
        Model2 = CreateADetailerModel(Settings.Scripts.ADetailer.Model),
        Model3 = CreateADetailerModel(Settings.Scripts.ADetailer.Model),
        Model4 = CreateADetailerModel(Settings.Scripts.ADetailer.Model),
        Model5 = CreateADetailerModel(Settings.Scripts.ADetailer.Model)
    };
}

public ScriptParametersADetailerModel CreateADetailerModel(string model)
{
    return new ScriptParametersADetailerModel()
    {
        Model = model,
        Prompt = Settings.Scripts.ADetailer.Prompt,
        // ... 40+ more property assignments from settings
    };
}
```

**Problem:** This initialization logic is:
1. **Verbose** - Lots of boilerplate mapping settings to parameters
2. **Legacy Compromise** - Kept from WebUI API compatibility
3. **Unnecessary for ComfyUI** - ComfyUI workflows use different parameter mapping
4. **Maintenance Burden** - Changes require updates in multiple places
5. **Partial Implementations** - Some scripts have incomplete ComfyUI support

**Solution:** Remove entire system, reimplement cleanly when needed for ComfyUI workflows.

#### Architecture Clarification

**Current Parameter Handling (Keeping This Approach):**
```
Settings (AppSettings.cs)
    ? (provides defaults)
State (Database persistence)
    ? (current generation params)
DTOs (API mapping)
    ? (API call structure)
ComfyUI/WebUI API
```

**What We're Removing:**
- Script-specific factory methods in ManagerService
- Script-specific settings in AppSettings.cs
- Script-specific DTOs in Data/Dtos/WebUI/
- Script-specific properties in parameter models

**What We're Keeping:**
- Settings ? State ? DTO architecture
- Basic parameter models (Txt2Img, Img2Img, etc.)
- Workflow-based parameter system
- Current initialization approach for core parameters

**Future Script Implementation Will:**
- Follow same Settings ? State ? DTO pattern
- Use ComfyUI workflow nodes directly (not WebUI factory pattern)
- Keep settings for defaults, state for persistence, DTOs for API calls
- Have simpler initialization (no complex nesting)

#### Recommendations

**Immediate Actions (This Deprecation):**
1. ? **Remove ALL 9 script DTOs** - No partial implementations kept
2. ? **Remove ALL 14 script forms** - Including ControlNet, ADetailer
3. ? **Remove ALL 10 factory methods** - Complete cleanup (~450 lines)
4. ? **Remove ALL script settings** - Clean AppSettings.cs (~800 lines)
5. ? **Remove script properties** - From Txt2Img/Img2Img parameter models

**Future Reimplementation (When Needed):**
1. Create ComfyUI-specific script forms (not WebUI ports)
2. Map directly to ComfyUI workflow nodes
3. Keep Settings ? State ? DTO architecture
4. Use simpler initialization (leverage workflow templates)
5. No factory method pattern - direct construction

**Impact:**
- **Lines removed:** ~1500+ lines (factories + settings + forms)
- **DTOs removed:** 9 files (all scripts)
- **Forms removed:** 14 files (all script forms)
- **Settings classes removed:** 9 classes from AppSettings.cs
- **Breaking changes:** None - scripts weren't actively used in ComfyUI workflows
- **Architecture:** Maintains current parameter handling approach

#### Additional Findings

**ControlNet Dependencies (To Remove):**
- Form: `ControlNetForm.razor` - Remove (already migrated to ISettingsService in Phase 8, but not used)
- API: `SDAPIService.GetControlNetModels()` - Remove with SDAPIService
- Tabs: `ControlNetTabs.razor`, `ControlNetTabsDynamic.razor` - Remove
  
**ADetailer Dependencies (To Remove):**
- Form: `ADetailerModelFormComfyUI.razor` - Remove (partial implementation)
- WebUI Form: `ADetailerModelForm.razor` - Remove (complex WebUI version)
- Wrapper: `ADetailerForm.razor` - Remove
- ComfyUI implementation incomplete and not integrated in generation workflows

**Settings Cleanup:**
After removing all scripts, also clean up:
- `AppSettings.ScriptsSettingsModel` - Remove entire class
- `BlazorDiffusion.json` - Remove entire `Scripts` section
- Script initialization in `StateService` - Remove all script parameter initialization
- `Models/Txt2ImgScriptParameters.cs` - Remove entire class
- `Models/Img2ImgScriptParameters.cs` - Remove entire class

**Total Code Reduction:**
- ManagerService: ~450 lines (factory methods)
- AppSettings.cs: ~800 lines (settings classes)
- Forms: 14 files
- DTOs: 9 files
- Model classes: 2 files (script parameters)
- **Grand Total: ~1500+ lines removed**

---

### Phase 2: Remove WebUI Pages ? COMPLETE
**Objective:** Remove all WebUI-specific pages

#### Tasks
- [x] Remove `Pages/WebUI/` folder entirely
- [x] Update `_Imports.razor` if it references WebUI namespace
- [x] Remove any routing to WebUI pages
- [x] Remove NavMenu/NavBar links to WebUI pages
- [x] Remove any page-specific CSS files
- [x] Verify build passes
- [x] Update documentation

#### Success Criteria
- [x] `Pages/WebUI/` folder deleted
- [x] No routing to WebUI pages
- [x] No navigation links to WebUI pages
- [x] Build successful
- [x] No runtime errors

#### Notes
- All 4 files removed successfully (3 pages + 1 CSS)
- WebUI folder deleted (was empty)
- No NavMenu/NavBar links to update (NavBar only shows ComfyUI workflows)
- No routing configuration needed (Blazor pages use @page directive)
- Build passes without errors
- ? **Phase 2 Complete - Ready for Phase 3**

---

### Phase 3: Remove WebUI Components
**Objective:** Remove all WebUI-specific generation form components

#### Tasks
- [ ] Remove `GenerateFormTxt2Img.razor` (WebUI version)
- [ ] Remove `GenerateFormImg2Img.razor` (WebUI version)
- [ ] Remove `UltimateUpscaleForm.razor` (if WebUI-specific)
- [ ] Remove `ADetailerModelForm.razor` (WebUI-specific)
- [ ] Remove any other WebUI-specific components found in audit
- [ ] Verify no references remain to deleted components
- [ ] Verify build passes

#### Success Criteria
- All WebUI components removed
- No import errors
- Build successful
- No warnings about missing components

---

### Phase 4: Evaluate & Clean WebUI DTOs
**Objective:** Remove WebUI-specific DTOs, preserve reusable ones

#### Tasks
- [ ] **4.1 Categorize DTOs**
  - Mark DTOs as "Remove" (WebUI-only)
  - Mark DTOs as "Keep" (Used by scripts/parameters)
  - Mark DTOs as "Evaluate" (Unclear usage)
  
- [ ] **4.2 Remove WebUI-Only DTOs**
  - Delete API request/response models
  - Delete WebUI-specific configuration models
  - Verify no references remain
  
- [ ] **4.3 Move Reusable DTOs**
  - Move script parameter DTOs to `Data/Dtos/` (remove WebUI folder)
  - Update namespaces
  - Update all references
  
- [ ] **4.4 Clean Up**
  - Delete `Data/Dtos/WebUI/` folder if empty
  - Verify build passes
  - Run tests

#### Success Criteria
- WebUI-only DTOs removed
- Reusable DTOs preserved and relocated
- All namespaces updated
- Build and tests successful

---

### Phase 5: Remove SDAPIService
**Objective:** Remove the Automatic1111 API service

#### Tasks
- [ ] Remove all `@inject SDAPIService` from components
- [ ] Remove SDAPIService from DI registration (`Program.cs`)
- [ ] Delete `Services/SDAPIService.cs`
- [ ] Remove any SDAPIService-specific configuration
- [ ] Update any code that referenced SDAPIService
- [ ] Verify build passes
- [ ] Run tests

#### Success Criteria
- SDAPIService completely removed
- No injection errors
- No missing service registrations
- Build and tests successful

---

### Phase 6: Rename ComfyUI Components & Pages
**Objective:** Simplify naming by removing "ComfyUI" suffix

#### Tasks
- [ ] **6.1 Rename Pages**
  - `Txt2ImgComfyUI.razor` ? `Txt2Img.razor`
  - `Img2ImgComfyUI.razor` ? `Img2Img.razor`
  - `Img2VidComfyUI.razor` ? `Img2Vid.razor`
  - Update `@page` directives (routing)
  - Update page titles/metadata
  
- [ ] **6.2 Rename Components**
  - `GenerateFormTxt2ImgComfyUI.razor` ? `GenerateFormTxt2Img.razor`
  - `GenerateFormImg2ImgComfyUI.razor` ? `GenerateFormImg2Img.razor`
  - `GenerateFormImg2VidComfyUI.razor` ? `GenerateFormImg2Vid.razor`
  - `ADetailerModelFormComfyUI.razor` ? `ADetailerModelForm.razor`
  - Update any component references
  
- [ ] **6.3 Update References**
  - Search for old component names in all `.razor` files
  - Update NavMenu/NavBar links
  - Update any dynamic component loading
  - Update any route parameters
  
- [ ] **6.4 Update Documentation**
  - Update README
  - Update component migration log
  - Update architecture diagrams
  
- [ ] **6.5 Verify**
  - Build passes
  - All routes work
  - No broken navigation
  - Run full application test

#### Success Criteria
- All ComfyUI suffixes removed
- Routing updated and working
- Navigation working correctly
- Build successful
- No naming confusion

---

### Phase 6.5: Remove WebUI-Only Scripts
**Objective:** Remove all WebUI-only script forms, DTOs, factories, and settings
**Status:** [ ] Not Started

**Background:** Deep audit revealed that **all 9 scripts are WebUI-only** or have incomplete/partial ComfyUI implementations not used in generation workflows. Decision: Remove all scripts and start fresh with cleaner parameter initialization when ComfyUI scripts are implemented.

**Updated Decision:** Remove **all scripts** including ControlNet and ADetailer:
- ControlNet: Partial ComfyUI implementation, not actively used in generation pages
- ADetailer: Separate WebUI/ComfyUI forms indicate incomplete integration
- Better to start fresh with proper ComfyUI workflow-based approach

#### Tasks
- [ ] **6.5.1 Remove ALL Script Forms** (10 files)
  - [ ] Remove `ControlNetForm.razor`
  - [ ] Remove `ControlNetTabs.razor`
  - [ ] Remove `ControlNetTabsDynamic.razor`
  - [ ] Remove `ADetailerForm.razor`
  - [ ] Remove `ADetailerModelForm.razor` (WebUI)
  - [ ] Remove `ADetailerModelFormComfyUI.razor` (partial ComfyUI)
  - [ ] Remove `CutoffForm.razor`
  - [ ] Remove `DynamicPromptsForm.razor`
  - [ ] Remove `IncantationsForm.razor`
  - [ ] Remove `MultiDiffusionTiledDiffusionForm.razor`
  - [ ] Remove `MultiDiffusionTiledVaeForm.razor`
  - [ ] Remove `RegionalPrompterForm.razor`
  - [ ] Remove `UltimateUpscaleForm.razor`
  - [ ] Remove `XYZPlotForm.razor`
  
- [ ] **6.5.2 Remove ALL Script Factory Methods from ManagerService** (~450 lines)
  - [ ] Remove `CreateControlNet()`
  - [ ] Remove `CreateADetailer() + `CreateADetailerModel()`
  - [ ] Remove `CreateCutoff()`
  - [ ] Remove `CreateDynamicPrompts()`
  - [ ] Remove `CreateUltimateUpscale()`
  - [ ] Remove `CreateMultiDiffusionTiledDiffusion()`
  - [ ] Remove `CreateMultiDiffusionTiledVae()`
  - [ ] Remove `CreateRegionalPrompter()`
  - [ ] Remove `CreateXYZPlot()`
  - [ ] Remove `CreateIncantationsModel()`
  
- [ ] **6.5.3 Remove ALL Script Settings from AppSettings.cs** (~800 lines)
  - [ ] Remove `ControlNetSettingsModel` + nested classes
  - [ ] Remove `ADetailerSettingsModel` + nested classes
  - [ ] Remove `CutoffSettingsModel` + nested classes
  - [ ] Remove `DynamicPromptsSettingsModel` + nested classes
  - [ ] Remove `UltimateUpscaleSettingsModel` + nested classes
  - [ ] Remove `MultiDiffusionSettingsModel` + nested classes
  - [ ] Remove `RegionalPrompterSettingsModel` + nested classes
  - [ ] Remove `XYZPlotSettingsModel` + nested classes
  - [ ] Remove `IncantationsSettingsModel` + nested classes
  
- [ ] **6.5.4 Remove ALL Script DTOs** (9 files from Data/Dtos/WebUI/)
  - [ ] Remove `ScriptParametersControlNet.cs`
  - [ ] Remove `ScriptParametersADetailer.cs`
  - [ ] Remove `ScriptParametersCutoff.cs`
  - [ ] Remove `ScriptParametersDynamicPrompts.cs`
  - [ ] Remove `ScriptParametersIncantations.cs`
  - [ ] Remove `ScriptParametersMultiDiffusion.cs`
  - [ ] Remove `ScriptParametersRegionalPrompter.cs`
  - [ ] Remove `ScriptParametersUltimateUpscale.cs`
  - [ ] Remove `ScriptParametersXYZPlot.cs`
  
- [ ] **6.5.5 Remove Script Parameters from Models**
  - [ ] Remove `Scripts` property from `Txt2ImgParameters`
  - [ ] Remove `Scripts` property from `Img2ImgParameters`
  - [ ] Remove `Txt2ImgScriptParameters` class
  - [ ] Remove `Img2ImgScriptParameters` class
  
- [ ] **6.5.6 Update StateService Parameter Initialization**
  - [ ] Remove ALL script initialization calls
  - [ ] Remove script factory method calls from `InitializeParameters()`
  - [ ] Simplify parameter initialization logic
  
- [ ] **6.5.7 Update BlazorDiffusion.json**
  - [ ] Remove ALL script settings from default settings file
  - [ ] Remove entire `Scripts` section from AppSettings
  - [ ] Verify file is valid JSON after cleanup
  
- [ ] **6.5.8 Remove Script Enums**
  - [ ] Remove `ControlNetPreprocessor` enum from `Data/Enums.cs`
  - [ ] Remove any other script-specific enums
  
- [ ] **6.5.9 Verify Build**
  - [ ] Run build to catch any missed references
  - [ ] Fix any compilation errors
  - [ ] Run existing tests

#### Success Criteria
- ALL script forms removed (14 files)
- ALL script factory methods removed (~450 lines from ManagerService)
- ALL script settings removed (~800 lines from AppSettings.cs)
- ALL script DTOs removed (9 files)
- Script parameters removed from Models
- Build passes without errors
- No references to any scripts in codebase
- BlazorDiffusion.json cleaned and validated
- Ready for fresh ComfyUI script implementation when needed

#### Notes
- **Total code reduction: ~1500+ lines**
- Clean slate for ComfyUI workflow-based script implementation
- Maintains current architecture: Settings (defaults) ? State (persistence) ? DTOs (API mapping)
- No changes to parameter handling strategy - keeping existing approach
- Future scripts will follow same pattern but with ComfyUI-native design

---

### Phase 7: Update Migration Log
**Objective:** Reflect WebUI removal in Phase 8 migration tracking

#### Tasks
- [ ] Remove WebUI components from migration log
- [ ] Remove ALL script components from migration log
- [ ] Update component counts
- [ ] Adjust "Remaining" estimates
- [ ] Update group progress
- [ ] Recalculate completion percentages
- [ ] Update Phase 8 scope

#### Success Criteria
- Migration log accurate
- Component counts correct
- No references to removed WebUI components
- No references to removed script components
- Clear path forward for Phase 8

---

### Phase 8: Final Cleanup & Verification
**Objective:** Ensure complete removal and working application

#### Tasks
- [ ] **8.1 Code Cleanup**
  - Search for `IsWebuiUp` references and remove
  - Remove WebUI-specific comments
  - Remove unused `using` statements
  - Remove empty folders
  - Search for script-related comments and remove
  
- [ ] **8.2 Configuration Cleanup**
  - Remove WebUI-specific settings from `appsettings.json`
  - Remove WebUI-specific environment variables
  - Update configuration documentation
  
- [ ] **8.3 Full Application Test**
  - Test Txt2Img generation (without scripts)
  - Test Img2Img generation (without scripts)
  - Test Img2Vid generation
  - Test all navigation
  - Test all major features
  - Verify no script UI elements remain
  
- [ ] **8.4 Documentation Update**
  - Update main refactor plan to reference this deprecation
  - Update README to reflect ComfyUI-only architecture
  - Update developer guide
  - Update user documentation (if any)
  - Document that scripts will be reimplemented with ComfyUI workflows
  
- [ ] **8.5 Commit & Tag**
  - Commit all changes
  - Create meaningful commit message
  - Tag release (e.g., `v2.1.0-webui-removed`)

#### Success Criteria
- No WebUI references in code
- No script system references in code
- Application fully functional (basic generation without scripts)
- All tests passing
- Documentation updated
- Clean commit history

---

## File Inventory (Phase 1 Audit Complete ?)

### Pages to Remove (WebUI) ? COMPLETE
```
[?] BlazorWebApp/Pages/WebUI/Txt2ImgWebUI.razor - REMOVED Phase 2
[?] BlazorWebApp/Pages/WebUI/Img2ImgWebUI.razor - REMOVED Phase 2
[?] BlazorWebApp/Pages/WebUI/UpscaleWebUI.razor - REMOVED Phase 2
[?] BlazorWebApp/Pages/WebUI/UpscaleWebUI.razor.css - REMOVED Phase 2
```
**Total:** 4 files (3 pages + 1 CSS) ? ALL REMOVED

### Components to Remove (WebUI)
```
[x] BlazorWebApp/Components/Txt2Img/GenerateFormTxt2Img.razor
[x] BlazorWebApp/Components/Img2Img/GenerateFormImg2Img.razor
[x] BlazorWebApp/Components/Shared/Generation/UltimateUpscaleForm.razor` (WebUI-specific)
[x] BlazorWebApp/Components/Shared/Generation/ADetailerModelForm.razor` (WebUI-specific)
```
**Total:** 4 files (2 components + 2 CSS)
**Note:** ADetailerModelForm removed in script cleanup phase

### DTOs - WebUI Specific (To Remove)
```
[x] BlazorWebApp/Data/Dtos/WebUI/Txt2ImgWebUI.cs - WebUI API request DTO
[x] BlazorWebApp/Data/Dtos/WebUI/Img2ImgWebUI.cs - WebUI API request DTO
[x] BlazorWebApp/Data/Dtos/WebUI/UpscaleWebUI.cs - WebUI API request DTO
[x] BlazorWebApp/Data/Dtos/WebUI/SharedWebUI.cs - WebUI shared models
```
**Total:** 4 files (API-specific DTOs)

### DTOs - ALL Script Parameters (To Remove)
**All scripts being removed - no partial ComfyUI implementation kept:**
```
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersControlNet.cs ? REMOVE (partial/unused)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersADetailer.cs ? REMOVE (partial/unused)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersCutoff.cs ? REMOVE (WebUI-only)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersDynamicPrompts.cs ? REMOVE (WebUI-only)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersIncantations.cs ? REMOVE (WebUI-only)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersMultiDiffusion.cs ? REMOVE (WebUI-only)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersRegionalPrompter.cs ? REMOVE (WebUI-only)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersUltimateUpscale.cs ? REMOVE (WebUI-only)
[x] BlazorWebApp/Data/Dtos/WebUI/ScriptParametersXYZPlot.cs ? REMOVE (WebUI-only)
```
**Total:** 9 files (ALL script DTOs - delete entirely)

### Script Forms (To Remove - ALL Scripts)
**All script forms being removed:**
```
[x] BlazorWebApp/Components/Shared/Generation/ControlNetForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/ControlNetTabs.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/ControlNetTabsDynamic.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/ADetailerForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/ADetailerModelForm.razor ? REMOVE (WebUI)
[x] BlazorWebApp/Components/Shared/Generation/ADetailerModelFormComfyUI.razor ? REMOVE (partial)
[x] BlazorWebApp/Components/Shared/Generation/CutoffForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/DynamicPromptsForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/IncantationsForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/MultiDiffusionTiledDiffusionForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/MultiDiffusionTiledVaeForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/RegionalPrompterForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/UltimateUpscaleForm.razor ? REMOVE
[x] BlazorWebApp/Components/Shared/Generation/XYZPlotForm.razor ? REMOVE
```
**Total:** 14 script form files (remove ALL during Component cleanup phase)

### Script Factory Methods (To Remove from ManagerService)
**ALL factory methods being removed:**
```
[x] CreateControlNet() - ~15 lines ? REMOVE
[x] CreateADetailer() + CreateADetailerModel() - ~60 lines ? REMOVE
[x] CreateCutoff() - ~15 lines ? REMOVE
[x] CreateDynamicPrompts() - ~25 lines ? REMOVE
[x] CreateUltimateUpscale() - ~20 lines ? REMOVE
[x] CreateMultiDiffusionTiledDiffusion() - ~35 lines ? REMOVE
[x] CreateMultiDiffusionTiledVae() - ~10 lines ? REMOVE
[x] CreateRegionalPrompter() - ~20 lines ? REMOVE
[x] CreateXYZPlot() - ~15 lines ? REMOVE
[x] CreateIncantationsModel() - ~25 lines ? REMOVE
```
**Total:** ~450 lines to remove from ManagerService

### Settings Cleanup (ALL Script Settings)
**ALL script settings classes in AppSettings.cs being removed:**
```
[x] ControlNetSettingsModel + nested classes (~200 lines)
[x] ADetailerSettingsModel + nested classes (~150 lines)
[x] CutoffSettingsModel + nested classes (~40 lines)
[x] DynamicPromptsSettingsModel + nested classes (~100 lines)
[x] UltimateUpscaleSettingsModel + nested classes (~120 lines)
[x] MultiDiffusionSettingsModel + nested classes (~140 lines)
[x] RegionalPrompterSettingsModel + nested classes (~50 lines)
[x] XYZPlotSettingsModel + nested classes (~70 lines)
[x] IncantationsSettingsModel + nested classes (~80 lines)
```
**Estimated:** ~800 lines from AppSettings.cs

### Files to Rename (ComfyUI ? Simplified)

**Pages:**
```
[x] Pages/ComfyUI/Txt2ImgComfyUI.razor ? Pages/Txt2Img.razor
[x] Pages/ComfyUI/Img2ImgComfyUI.razor ? Pages/Img2Img.razor
[x] Pages/ComfyUI/Img2VidComfyUI.razor ? Pages/Img2Vid.razor
```
**Total:** 3 pages

**Components:**
```
[x] Components/Txt2Img/GenerateFormTxt2ImgComfyUI.razor ? Components/Txt2Img/GenerateFormTxt2Img.razor
[x] Components/Txt2Img/GenerateFormTxt2ImgComfyUI.razor.css ? Components/Txt2Img/GenerateFormTxt2Img.razor.css
[x] Components/Img2Img/GenerateFormImg2ImgComfyUI.razor ? Components/Img2Img/GenerateFormImg2Img.razor
[x] Components/Img2Vid/GenerateFormImg2VidComfyUI.razor ? Components/Img2Vid/GenerateFormImg2Vid.razor
```
**Total:** 4 components (3 generation forms + 1 CSS)
**Note:** ADetailerModelFormComfyUI removed entirely in script cleanup

### Services to Remove
```
[x] BlazorWebApp/Services/SDAPIService.cs
```
**Total:** 1 service file

### Folder Cleanup
```
[?] BlazorWebApp/Pages/WebUI/ - Delete entire folder after removing files - REMOVED Phase 2
[x] BlazorWebApp/Pages/ComfyUI/ - Delete entire folder after moving files
[x] BlazorWebApp/Data/Dtos/WebUI/ - Delete entire folder after removing all DTOs
```
**Total:** 3 folders to clean up (1 complete, 2 pending)

---

## Impact Assessment Matrix

| Category | Files Affected | Complexity | Risk Level | Estimated Time |
|----------|----------------|------------|------------|----------------|
| WebUI Pages | 4 files (3 pages + 1 CSS) | Low | **Very Low** | 30 min |
| WebUI Components | 4 files (2 components + 2 CSS) | Low | **Very Low** | 30 min |
| WebUI DTOs | 4 files (API models) | Low | **Very Low** | 30 min |
| **Script Forms (ALL)** | **14 files** | **Medium** | **Low** | **2 hours** |
| **Script DTOs (ALL)** | **9 files** | **Low** | **Low** | **1 hour** |
| **Script Factory Methods** | **~450 lines in ManagerService** | **Medium** | **Low** | **1.5 hours** |
| **Script Settings** | **~800 lines in AppSettings.cs** | **Medium** | **Low** | **2 hours** |
| **Script Model Properties** | **2 classes (Txt2Img/Img2Img)** | **Low** | **Low** | **30 min** |
| SDAPIService | 1 file + references | Medium | **Low** | 1 hour |
| ComfyUI Renaming | 7 files (3 pages + 4 components) | Medium | **Low** | 2 hours |
| Reference Updates | ~10-15 files | Medium | **Low** | 2 hours |
| Routing Updates | 3 pages | Low | **Low** | 30 min |
| Testing | All changes | Medium | **Low** | 2 hours |
| **Total** | **~55 files** | **Medium** | **Low** | **~16 hours** |

### File Summary (Updated)
- **Remove:** 45 files total
  - 4 WebUI pages (pages + CSS)
  - 4 WebUI components (components + CSS)
  - 4 WebUI DTOs (API models)
  - 9 Script DTOs (ALL scripts)
  - 14 Script Forms (ALL scripts)
  - 1 Service (SDAPIService)
  - ~450 lines from ManagerService (ALL factories)
  - ~800 lines from AppSettings.cs (ALL settings)
  - Script properties from parameter models
- **Rename:** 7 files
  - 3 ComfyUI pages
  - 4 ComfyUI components (+ CSS)
- **Update References:** ~10-15 files (components using moved pages, updated routing)
- **Clean Folders:** 3 folders (WebUI, ComfyUI, Dtos/WebUI/)

### Complexity Breakdown (Updated)
- **Low Complexity (55%):** Simple file deletions, obvious renames
- **Medium Complexity (40%):** Removing script system, settings cleanup, routing changes, factory method removal
- **High Complexity (5%):** Verifying generation still works without script system

### Risk Assessment (Updated)
- **Very Low Risk (60%):** Removing unused WebUI code
- **Low Risk (35%):** Removing entire script system (unused in ComfyUI workflows)
- **Medium Risk (5%):** Ensuring generation pages work without script UI components

### Effort Breakdown by Phase
| Phase | Effort | Notes |
|-------|--------|-------|
| Phase 2 (Pages) | 30 min | Simple deletion |
| Phase 3 (Components) | 30 min | Simple deletion |
| Phase 4 (DTOs) | 1.5 hours | Remove all 13 DTOs (4 WebUI + 9 scripts) |
| Phase 5 (SDAPIService) | 1 hour | Remove service + update references |
| Phase 6 (Renaming) | 2 hours | Rename 7 ComfyUI files + routing |
| Phase 6.5 (Scripts) | 6 hours | Remove ALL scripts (forms, factories, settings, DTOs, models) |
| Phase 7 (Migration Log) | 30 min | Update tracking |
| Phase 8 (Verification) | 2.5 hours | Full app testing without scripts |
| **Total** | **~16 hours** | Complete WebUI + Script system removal |

