# Phase 6 - State &amp; Persistence

## Status
**Phase:** 6  
**Build Status:** &check; Passing | **Tests:** &check; Complete

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

Update state management and ImageService to use the new `GenerationParameters` model directly, eliminating the temporary parameter mapping layer created in Phase 5.

---

## Context

### Dependencies
- Phase 1-5 complete
- `GenerationParameters` model exists and is populated
- `Generate.razor` currently uses temporary mapping to legacy parameters
- `ImageService` uses `Txt2ImgParameters`, `Img2ImgParameters`, `Img2VidParameters`

### Current Flow (Phase 5 - Temporary)
```
GenerationParameters -> MapToTxt2ImgParameters() -> State.ParametersTxt2Img -> ImageService.GetImages()
```

### Target Flow (Phase 6)
```
GenerationParameters -> ImageService.GetImages(GenerationParameters)
```

### Key Files
- `Services/StateService.cs` - State persistence
- `Services/ImageService.cs` - Generation orchestration
- `Services/RouterService.cs` - API calls
- `Pages/Generate.razor` - New generation page
- `Extensions/Parser.cs` - Wildcard/seed parsing

---

## Execution Checklist

### Step 6.1: Update StateService for GenerationParameters
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Added `GenerationParameters` property to `IStateService` interface
- Added `GenerationParameters` property to `State` entity (for DB persistence)
- Added `GenerationParameters` property to `StateService` implementation
- Updated `LoadStateInternal()` to restore GenerationParameters from DB
- Updated `SaveState()` to persist GenerationParameters to DB
- Updated `PublishStateChangedEvents()` to include GenerationParametersChangedEventArgs

#### Key Decisions
- GenerationParameters is owned by StateService (singleton)
- GenerationParameterService operates on StateService.GenerationParameters
- Generate.razor accesses Parameters via `State.GenerationParameters`

---

### Step 6.2: Create GenerationParameters-aware ImageService methods
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Added `GenerateImagesAsync(GenerationParameters, Workflow)` to IImageService
- Added `GenerateVideoAsync(GenerationParameters, Workflow)` to IImageService
- Implemented both methods in ImageService
- Created helper methods:
  - `BuildLegacyParametersFromGenerationParams()` - converts fragments to SharedParameters
  - `BuildTxt2ImgFromGenerationParams()` - builds Txt2ImgParameters with upscale/SeedVR2 support
  - `BuildImg2ImgFromGenerationParams()` - builds Img2ImgParameters with sources
  - `BuildImg2VidFromGenerationParams()` - builds Img2VidParameters with video settings
- Added missing using directive for `BlazorWebApp.Data.Dtos.ComfyUI.Workflow`
- **Migrated ImageService events to EventService pattern:**
  - Removed direct `event Action OnChange` from IImageService/ImageService
  - Created `ImagesGeneratedEventArgs` in `Events/` folder
  - ImageService now injects `IEventService` and publishes `ImagesGeneratedEventArgs`
  - Updated `GeneratedImageTabs.razor` to subscribe via EventService
  - Updated `ImageServiceTests.cs` to mock IEventService

---

### Step 6.3: Update Router/ComfyUI parameter building
**Complexity:** 5
**Status:** [x] Deferred to Phase 9

#### Notes
- Factory methods (`Txt2ImgComfyUI.FromGenerationParameters()`, etc.) can be created later
- The internal conversion in ImageService is sufficient for current needs
- This is a cleanup/optimization task, not blocking functionality

---

### Step 6.4: Integrate wildcard and seed parsing
**Complexity:** 3
**Status:** [x] Complete (part of Step 6.2)

#### Implementation
- `BuildLegacyParametersFromGenerationParams()` calls `Parser.ParseParametersAsync()`
- `Parser.ParseParametersAsync()` handles:
  1. Wildcard expansion via `IWildcardService`
  2. Style application
  3. LoRA parsing
  4. Seed randomization (if -1)
- No additional changes needed - already integrated

---

### Step 6.5: Update Generate.razor to use new flow
**Complexity:** 3
**Status:** [x] Complete

#### Implementation
Generate.razor already updated to:
- Use `State.GenerationParameters` directly via `Parameters` property
- Call `ImageService.GenerateImagesAsync(Parameters, _selectedWorkflow)` for images
- Call `ImageService.GenerateVideoAsync(Parameters, _selectedWorkflow)` for videos
- No legacy mapping methods (`MapToTxt2ImgParameters`, etc.) exist in Generate.razor
- State saved before and after generation

---

### Step 6.6: Test state persistence and recovery
**Complexity:** 2
**Status:** [x] Complete

#### Tasks
- [x] Test save state on page leave
- [x] Test restore state on page load
- [x] Test workflow switch preserves non-workflow-specific settings
- [x] Verify no data loss between sessions

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 6.1 | &check; | 3 | StateService update |
| 6.2 | &check; | 5 | ImageService methods + EventService migration |
| 6.3 | &check; | 5 | Deferred to Phase 9 (cleanup) |
| 6.4 | &check; | 3 | Already integrated in 6.2 |
| 6.5 | &check; | 3 | Generate.razor already updated |
| 6.6 | &check; | 2 | Manual testing verified |

---

## Files Modified This Phase

| File | Changes |
|------|---------|
| `Data/Entities/State.cs` | Added GenerationParameters property |
| `Services/IStateService.cs` | Added GenerationParameters property |
| `Services/StateService.cs` | Implemented GenerationParameters persistence |
| `Services/GenerationParameterService.cs` | Updated to use StateService.GenerationParameters |
| `Services/IImageService.cs` | Added GenerateImagesAsync, GenerateVideoAsync; removed OnChange event |
| `Services/ImageService.cs` | Implemented new generation methods; uses IEventService |
| `Events/ImagesGeneratedEventArgs.cs` | New - event args for generation completion |
| `Components/Shared/Generation/GeneratedImageTabs.razor` | Subscribe to ImagesGeneratedEventArgs via EventService |
| `BlazorWebApp.Tests/Services/ImageServiceTests.cs` | Added IEventService mock |
| `Pages/Generate.razor` | Uses State.GenerationParameters directly |

---

## Design Decisions

### Approach: Incremental Refactor

Rather than rewriting ImageService entirely, we'll:
1. Add new methods that accept `GenerationParameters`
2. Internally convert to legacy format where needed
3. Update callers to use new methods
4. Eventually deprecate legacy methods (Phase 9)

This minimizes risk and allows testing at each step.

### EventService Pattern (Enforced)

All events must go through `IEventService`:
- Decouples publishers from subscribers
- Centralized event management
- Thread-safe publish/subscribe
- Easier testing via mocking

**Before:** `ImageService.OnChange += handler;`  
**After:** `Events.Subscribe<ImagesGeneratedEventArgs>(handler);`

### State Scoping

`GenerationParameters` is scoped (per-session) because:
- Different browser tabs may have different workflows
- State persistence is per-user, not per-session
- The singleton `IGenerationParameterService` manages the current session's parameters

### Serialization Strategy

For state persistence:
```json
{
  "Generation": {
    "CurrentWorkflowId": "guid",
    "WorkflowBase": "Flux",
    "GenerationParameters": {
      "WorkflowId": "guid",
      "Fragments": {
        "prompts": { "FragmentFile": "prompts.sbn", "Values": { "positive": "...", "negative": "..." } },
        "main_sampler": { ... }
      },
      "Assets": { "Model": "flux.safetensors", ... },
      "Sources": { },
      "Loras": [ ]
    }
  }
}
```

---

## Issues &amp; Resolutions

| Issue | Resolution |
|-------|------------|
| ImageService used direct `event Action OnChange` | Migrated to EventService pattern with `ImagesGeneratedEventArgs` |
| Step 6.4 overlapped with 6.2 | Already integrated in BuildLegacyParametersFromGenerationParams |

---

## Commit Checkpoints

- [x] After Step 6.2 complete (new ImageService methods + EventService migration)
- [x] After Step 6.5 complete (Generate.razor verified)
- [x] After Step 6.6 complete (persistence verified via manual testing)

---

## Phase Summary

**All steps complete.** The new `GenerationParameters` model is now:
- Persisted by `StateService.GenerationParameters`
- Used directly by `Generate.razor`
- Processed by `ImageService.GenerateImagesAsync()` / `GenerateVideoAsync()`
- Integrated with wildcard/seed parsing via `Parser.ParseParametersAsync()`
- Events published through `IEventService` (no direct subscriptions)

---

**Phase Status:** Complete &check;
