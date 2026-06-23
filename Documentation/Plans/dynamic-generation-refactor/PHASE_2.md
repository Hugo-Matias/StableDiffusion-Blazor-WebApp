# Phase 2 - Core Infrastructure

## Status
**Phase:** 2  
**Build Status:** &check; Passing | **Tests:** 0/0

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

Create the foundational C# types and services for dynamic parameters. This establishes the core data structures that will be used throughout the system.

---

## Context

### Dependencies
- Phase 1 complete (schema format defined)
- Existing services: `WorkflowService`, `StateService`, `AssetResolverService`

### Files Created
- `Models/GenerationParameters.cs`
- `Models/FragmentParameters.cs`
- `Models/FragmentSchema.cs`
- `Models/SourceAsset.cs`
- `Services/ComponentRegistry.cs`
- `Services/IComponentRegistry.cs`
- `Services/GenerationParameterService.cs`
- `Services/IGenerationParameterService.cs`

### Files Modified
- `Services/IWorkflowService.cs` (added schema parsing methods)
- `Services/WorkflowService.cs` (added schema parsing implementation)
- `Program.cs` (registered new services)

---

## Execution Checklist

### Step 2.1: Create GenerationParameters Model
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `Models/GenerationParameters.cs`
- Dictionary-based storage for Fragments, Assets, Sources
- Helper methods: Clone(), GetFragment(), FlattenForTemplateRendering()

---

### Step 2.2: Create FragmentParameters Model
**Complexity:** 2
**Status:** [x] Complete

#### Changes Made
- Created `Models/FragmentParameters.cs`
- Type-safe value access with GetValue<T>(), SetValue<T>()
- JsonElement handling for deserialization
- Clone and merge support

---

### Step 2.3: Create FragmentSchema Model
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `Models/FragmentSchema.cs`
- Includes: FragmentSchema, ParameterConstraints, FieldSchema
- Validation methods for field schemas
- Type-safe constraint accessors (GetMin<T>, GetMax<T>, GetStep<T>)

---

### Step 2.4: Create SourceAsset Model
**Complexity:** 1
**Status:** [x] Complete

#### Changes Made
- Created `Models/SourceAsset.cs`
- Properties: Label, Type, Data, Filename, FilePath, Width, Height
- Helper methods: GetRawData(), Clone(), Clear(), HasData

---

### Step 2.5: Create ComponentRegistry Service
**Complexity:** 3
**Status:** [x] Complete

#### Changes Made
- Created `Services/IComponentRegistry.cs` interface
- Created `Services/ComponentRegistry.cs` implementation
- Case-insensitive component name lookup
- Registration methods for component types
- Placeholder for default components (to be filled in Phase 4)

---

### Step 2.6: Extend WorkflowService for Schema Parsing
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Added `ParseFragmentSchema()` method
- Added `GetFragmentSchema()` with caching
- Added `GetWorkflowFragmentSchemas()` for workflow pipeline
- Added `ClearSchemaCache()` for cache invalidation
- Updated `IWorkflowService.cs` interface

---

### Step 2.7: Create GenerationParameterService
**Complexity:** 5
**Status:** [x] Complete

#### Changes Made
- Created `Services/IGenerationParameterService.cs` interface
- Created `Services/GenerationParameterService.cs` implementation
- Methods for: fragment CRUD, asset management, source management
- Workflow initialization from pipeline
- Event notification for parameter changes
- Registered in Program.cs (Scoped lifetime)

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 2.1 | &check; | 3 | GenerationParameters model |
| 2.2 | &check; | 2 | FragmentParameters model |
| 2.3 | &check; | 3 | FragmentSchema model |
| 2.4 | &check; | 1 | SourceAsset model |
| 2.5 | &check; | 3 | ComponentRegistry service |
| 2.6 | &check; | 5 | WorkflowService schema parsing |
| 2.7 | &check; | 5 | GenerationParameterService |

---

## Issues & Resolutions

### Issue 1: Nullable return type with generic constraint
**Impact:** Build error in GetFragmentValue<T>
**Resolution:** Changed from `fragment?.GetValue<T>()` to explicit null check with `return default`

### Issue 2: Event notification pattern
**Discussion:** Should we use EventService for OnParametersChanged?
**Resolution:** Yes - created `GenerationParametersChangedEventArgs` and integrated with `IEventService` for proper subscribe/unsubscribe lifecycle. This follows existing patterns (e.g., `ParametersChangedEventArgs`).

---

## Commit Checkpoints

- [x] After Step 2.4 complete (all models created)
- [x] After Step 2.5 complete (ComponentRegistry done)
- [x] After Step 2.7 complete (phase complete)

---

## Phase Summary

### Accomplishments
1. Created unified `GenerationParameters` model with dictionary-based storage
2. Created `FragmentParameters` with type-safe value access
3. Created `FragmentSchema`, `ParameterConstraints`, and `FieldSchema` models
4. Created `SourceAsset` for input images/videos
5. Created `ComponentRegistry` service for fragment-to-component mapping
6. Extended `WorkflowService` with schema parsing and caching
7. Created `GenerationParameterService` for parameter CRUD operations
8. Created `GenerationParametersChangedEventArgs` with typed change events
9. Integrated with `IEventService` for proper event management
10. Registered all new services in DI container

### Files Created
| File | Purpose |
|------|---------|
| `Models/GenerationParameters.cs` | Unified parameter storage |
| `Models/FragmentParameters.cs` | Fragment instance values |
| `Models/FragmentSchema.cs` | UI schema definitions |
| `Models/SourceAsset.cs` | Input image/video data |
| `Services/IComponentRegistry.cs` | Component registry interface |
| `Services/ComponentRegistry.cs` | Component registry implementation |
| `Services/IGenerationParameterService.cs` | Parameter service interface |
| `Services/GenerationParameterService.cs` | Parameter service implementation |
| `Events/GenerationParametersChangedEventArgs.cs` | Typed event args for parameter changes |

### Files Modified
| File | Changes |
|------|---------|
| `Services/IWorkflowService.cs` | Added 4 schema parsing methods |
| `Services/WorkflowService.cs` | Added schema parsing with caching |
| `Program.cs` | Registered ComponentRegistry and GenerationParameterService |

### Build Status
&check; All builds passing

---

**Phase Status:** Complete &check;

---

## Next Phase

**Phase 3: Fragment Updates** - Apply UI schema to actual fragment files
