# CivitAI Base Models & Context-Aware Toolbar - Implementation Plan

## Status
**Current Phase:** Execution (Phase 2 Complete, Phase 3 Next)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits

### Progress Tracking Symbols
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)
- **1**: Trivial (simple property change, config update)
- **2**: Simple (straightforward refactor, single file change)
- **3**: Moderate (multi-file change, simple logic)
- **5**: Medium (service extraction, interface creation)
- **8**: Complex (component migration, breaking changes)
- **13**: Very complex (architecture change, wide impact)
- **21+**: Epic (should be split into smaller phases)

### Key Rules
- **Each step = commitable checkpoint** for safe implementation
- **No time/date references** - use complexity points only
- **Detours are acceptable** after discussion - append to main plan
- **Phase documents must contain enough context** to resume in new sessions
- **Minimal, focused changes** - avoid over-engineering
- **User permission required** before moving to next phase
- **All events must use the pub/sub pattern** implemented by EventService.cs

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

### Problem 1: Hardcoded Base Models
The CivitAI base model list is hardcoded in `CivitaiSettingsModel.BaseModels` (`AppSettings.cs` line 428). CivitAI frequently adds new base models (currently 80+ models across ecosystems), making the hardcoded list quickly outdated. The current flat list also lacks grouping, making navigation difficult compared to CivitAI's own UI which groups models by family/ecosystem.

### Problem 2: Flat Base Model Selector
The base model dropdown in `CivitaiModelsPanel.razor` is a flat `MudSelect` with no grouping. CivitAI's UI groups models by ecosystem family (e.g., "Tencent" containing "Hunyuan 1" and "Hunyuan Video", "SDXL Community" containing "Illustrious" and "NoobAI"). This grouping should be adopted for better UX.

### Problem 3: Static Toolbar
The `TopToolbar` component (rendered inside `MainLayout.razor` as a `MudExpansionPanel`) currently shows only gallery folder/project selection, theme picker, and state presets. This is the same content regardless of which page the user is on. There is an opportunity to make this a centralized, context-aware settings panel that shows relevant settings for the current page (e.g., CivitAI filtering preferences when on `/civitai`).

---

## Proposed Solution

### Part A: Base Model Data Externalization
- Parse `basemodel.constants.ts` into a structured `basemodels.json` file at `Data/CivitAI/`
- Create a PowerShell update script at `Utils/` that fetches the latest TS file from GitHub and regenerates the JSON
- Remove the TS constants file from the repo (only parsed data stays)
- Remove hardcoded `BaseModels` list from `AppSettings.cs`
- Load base models from JSON at startup via a service

### Part B: Grouped Base Model Selector
- Update `CivitaiModelsPanel.razor` to use `MudSelect` with `MudSelectGroup` for family-based grouping
- Model data structure includes ecosystem families, enabling grouped display

### Part C: Context-Aware Toolbar
- Refactor `TopToolbar` to detect the current page route
- Render page-specific settings panels alongside existing global settings
- For CivitAI page: show base model group enable/disable toggles, default search params
- Architecture supports adding more page-specific panels in the future

### Key Decisions
| Decision | Rationale |
|----------|-----------|
| JSON file over API call | CivitAI has no public endpoint for base models; JSON is reliable and versionable |
| PowerShell script for updates | Matches workspace conventions (`.ps1` execution); one-time manual run when needed |
| Remove TS file after parsing | Keep repo clean - only parsed data, no CivitAI source code |
| Family-based grouping | Matches CivitAI's own UI pattern; better UX for 80+ models |
| Route-based toolbar context | Simple, predictable, no complex state management needed |
| Toolbar settings persisted in AppSettings | Consistent with existing settings pattern |

### Conventions
- Base model data lives in `Data/CivitAI/basemodels.json`
- Update script lives in `Utils/Update-CivitaiBaseModels.ps1`
- Toolbar page-specific panels follow naming: `Toolbar{PageName}Settings.razor`
- Use EventService pub/sub for any cross-component communication

---

## Implementation Phases

### Phase 1: Base Model Data Externalization
**Objective:** Extract base models from hardcoded settings into a structured JSON file, create update script, clean up repo
**Complexity:** 5 points
**Status:** [x] Complete

#### Success Criteria
- `basemodels.json` contains all base models with ecosystem/family grouping
- PowerShell script can regenerate the JSON from a fresh GitHub fetch
- No CivitAI TypeScript source code in the repo
- App loads base models from JSON instead of hardcoded list
- Existing base model filtering in `CivitaiModelsPanel.razor` still works

---

### Phase 2: Grouped Base Model Selector
**Objective:** Update the CivitAI model search panel to show base models grouped by ecosystem family
**Complexity:** 3 points
**Status:** [x] Complete

#### Success Criteria
- Base model dropdown shows grouped models (e.g., "Tencent" > "Hunyuan 1", "Hunyuan Video")
- Selecting a model still correctly filters CivitAI API results
- "All" option remains at the top outside any group

---

### Phase 3: Context-Aware Toolbar - Infrastructure
**Objective:** Refactor the toolbar to support page-specific settings panels alongside existing global content
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 3.1 - Create a `ToolbarContext` abstraction that determines which page-specific panel to render based on the current route
- [ ] Step 3.2 - Refactor `TopToolbar.razor` to split into global settings (existing content) and a slot for page-specific content
- [ ] Step 3.3 - Create `ToolbarCivitaiSettings.razor` as the first page-specific panel with: base model group toggles (enable/disable families), default search parameter overrides
- [ ] Step 3.4 - Wire CivitAI toolbar settings to persist in `AppSettings` and apply to `CivitaiModelsPanel` behavior

#### Success Criteria
- Toolbar shows existing global content on all pages
- On `/civitai` page, additional CivitAI-specific settings appear
- On other pages, only global settings appear (no empty space)
- CivitAI settings (disabled families, defaults) persist across sessions
- Architecture is extensible for future page-specific panels

---

## Stress Points & Risks
| Risk | Mitigation | Complexity |
|------|------------|------------|
| TS file format changes in CivitAI repo | Script uses regex patterns that tolerate whitespace changes; manual review after script run | 2 |
| Large number of base models (80+) overwhelming the grouped dropdown | Family grouping naturally organizes; could add collapse/expand or search within select | 2 |
| Toolbar becoming too crowded with page-specific content | Use tabs or sections within toolbar; keep page panels focused and minimal | 3 |
| Breaking existing base model filter behavior | The API param is just a string name - as long as names match, behavior is preserved | 1 |
| Route detection edge cases | Use `NavigationManager.Uri` with simple path matching; well-understood pattern | 1 |

---

## Changelog
| Phase | Changes |
|-------|---------|
| Planning | Initial plan created |
| Phase 1 | Complete - Script, JSON, DTOs, service loading, hardcoded list removed, TS file deleted |
| Phase 2 | Complete - Multi-select with family grouping, All toggle, backward-compatible converter |

---

## References
- `BlazorWebApp/Models/AppSettings.cs` line 428 - Current hardcoded `BaseModels` list
- `BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor` - Base model selector UI
- `BlazorWebApp/Components/Shared/TopToolbar.razor` - Current toolbar component
- `BlazorWebApp/Components/Shared/MainLayout.razor` - Toolbar host (MudExpansionPanel)
- `BlazorWebApp/Data/CivitAI/basemodel.constants.ts` - CivitAI source constants (to be parsed and removed)
- CivitAI GitHub source: `https://github.com/civitai/civitai/blob/main/src/shared/constants/base-model.constants.ts`
