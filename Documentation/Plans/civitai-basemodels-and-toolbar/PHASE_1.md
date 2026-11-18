# Phase 1 - Base Model Data Externalization

## Status
**Phase:** 1  
**Build Status:** Not yet built | **Tests:** N/A

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**
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

Extract CivitAI base models from the hardcoded `AppSettings.cs` list into a structured `basemodels.json` file, create a PowerShell update script to regenerate it from CivitAI's GitHub source, and wire up the app to load from JSON.

---

## Context

- Current hardcoded list: `BlazorWebApp/Models/AppSettings.cs` line 428 (`CivitaiSettingsModel.BaseModels`)
- Source TS file (local copy): `BlazorWebApp/Data/CivitAI/basemodel.constants.ts`
- Source TS file (remote): `https://raw.githubusercontent.com/civitai/civitai/refs/heads/main/src/shared/constants/base-model.constants.ts`
- The TS file contains: `ecosystemFamilies` (grouping), `ecosystems` (with familyId references), `baseModelRecords` (with ecosystemId references)
- The API `baseModels` query param uses the `name` field from `baseModelRecords`
- UI consumer: `BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor` (reads `Settings.Settings.Resources.Civitai.BaseModels`)

---

## Execution Checklist

### Step 1.1: Create PowerShell Update Script
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Create `Utils/Update-CivitaiBaseModels.ps1`
- [x] Script fetches TS from GitHub raw URL
- [x] Parses `baseModelFamilyConfig`, `baseModelConfig`, and `baseModelGroupConfig` (remote file uses different structure than local copy)
- [x] Outputs structured `BlazorWebApp/Data/CivitAI/basemodels.json`
- [x] Script does NOT keep the TS file in the repo

#### Changes Made
- `Utils/Update-CivitaiBaseModels.ps1` - Created script that fetches from `https://raw.githubusercontent.com/civitai/civitai/refs/heads/main/src/shared/constants/base-model.constants.ts`
- Remote file uses `baseModelFamilyConfig` (Record), `baseModelConfig` (array), `baseModelGroupConfig` (Record) - different from local ecosystem-based schema
- Script parses all three structures and resolves family references through group config

---

### Step 1.2: Run Script and Validate JSON Output
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Execute the script
- [x] Validate JSON structure is correct and complete
- [x] Compare model names against current hardcoded list to ensure coverage

#### Results
- 11 families, 60 groups, 77 base models parsed
- All 32 old hardcoded models present in new JSON
- 45 additional models now available

---

### Step 1.3: Create C# Model and Loading Service
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Create DTOs for the JSON structure (families, groups, base models)
- [x] Add loading logic (read JSON at startup, expose via service)
- [x] Registered via existing `CivitaiService` (no new DI registration needed)

#### Changes Made
- `BlazorWebApp/Data/Dtos/CivitaiBaseModelsData.cs` - Created DTOs: `CivitaiBaseModelsData`, `CivitaiBaseModelFamily`, `CivitaiBaseModelGroup`, `CivitaiBaseModelEntry`
- `BlazorWebApp/Services/CivitaiService.cs` - Added `BaseModelsData` property and `LoadBaseModelsData()` called in constructor

---

### Step 1.4: Wire Up and Remove Hardcoded List
**Complexity:** 2
**Status:** [x] Complete

#### Tasks
- [x] Remove `BaseModels` property from `CivitaiSettingsModel`
- [x] Update `CivitaiModelsPanel.razor` to use the new data source
- [x] Verify existing filter behavior still works (build passes)

#### Changes Made
- `BlazorWebApp/Models/AppSettings.cs` - Removed `BaseModels` property from `CivitaiSettingsModel`
- `BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor` - Changed from `Settings.Settings.Resources.Civitai.BaseModels` to `Api.BaseModelsData.Models.Where(m => m.Hidden != true)`

---

### Step 1.5: Remove TS Constants File
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Delete `BlazorWebApp/Data/CivitAI/basemodel.constants.ts`

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1.1 | [x] | 3 | Script created, adapted for remote file structure |
| 1.2 | [x] | 1 | 77 models, 60 groups, 11 families |
| 1.3 | [x] | 3 | DTOs + CivitaiService loading |
| 1.4 | [x] | 2 | Hardcoded list removed, panel updated |
| 1.5 | [x] | 1 | TS file deleted |

---

## Issues & Resolutions

{None yet}

---

## Commit Checkpoints

- [x] After Step 1.2 complete (script + JSON validated)
- [x] After Step 1.4 complete (app wired up, hardcoded list removed)
- [x] After Step 1.5 complete (TS file removed)

---

**Phase Status:** Complete [x]
