# Phase 1 - Data Model & Persistence

## Status
**Phase:** 1
**Build Status:** (not yet run) | **Tests:** 0/0

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**
   - Do NOT proceed until testing is complete.
   - User must approve before updating this document.
   - Build runs only after user requests or after completing all file edits.

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules
- Each step = commit checkpoint - test thoroughly before proceeding.
- Minimal changes only - focused on phase objectives.
- Document all issues and resolutions in this file.
- This document must have enough context to resume in a new session.
- User permission required before next step.

---

## Objective

Add per-pass detailer LoRA storage to `GenerationParameters` with correct deep-clone and serialization. No UI or workflow integration yet - user-visible behaviour must remain identical.

For Phase 1 only a **single** `DetailerLoras` list is added (pass 0). Phase 4 generalizes to `Dictionary<int, List<Lora>>` with a compatibility shim.

---

## Context

### Dependencies on previous phases
None - this is the first phase.

### Key architectural decisions (from MAIN_PLAN.md)
- Separate per-pass LoRA lists rather than a `Scope` property on `Lora`.
- Indexed scope strings (`detailer_{i}_`) - zero changes required in existing fragments.
- Legacy `detailer_` keys continue to load as pass 0.

### Files / services involved
- `BlazorWebApp/Models/GenerationParameters.cs` - add `DetailerLoras` property + update `Clone()`.
- `BlazorWebApp/Data/Converters/GenerationParametersJsonConverter.cs` - mirror `Loras` handling.
- `BlazorWebApp/Services/GenerationParameterService.cs` / `IGenerationParameterService.cs` - audit for any implicit reliance on `Loras` that should also apply to `DetailerLoras`.
- `BlazorWebApp/Services/StateService.cs`, `DatabaseService.cs` - audit for persistence paths.
- `BlazorWebApp/Models/GeneratedImagesInfo.cs`, `Data/Entities/Prompt.cs` - audit for metadata round-trip.
- `BlazorWebApp.Tests` - add a small serialization + clone test if the project already has comparable tests.

---

## Execution Checklist

### Step 1: Add `DetailerLoras` Property
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Add `public List<Lora> DetailerLoras { get; set; } = new();` to `GenerationParameters`.
- [ ] Extend `GenerationParameters.Clone()` to deep-copy the new list using `new Lora(l)`.
- [ ] Verify no consumer currently treats `Loras` in a way that must also be applied to `DetailerLoras` beyond serialization (that is handled in Step 2).

#### Changes Made
(fill in after completion)

---

### Step 2: Serialization Audit & Round-trip
**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks
- [ ] Inspect `GenerationParametersJsonConverter.cs` - add read/write for `DetailerLoras` mirroring `Loras`.
- [ ] Check `DatabaseService.cs` / `StateService.cs` for any explicit `Loras` persistence (snapshot/load); mirror where required.
- [ ] Check `GeneratedImagesInfo.cs` / prompt metadata parsers - only if `Loras` is embedded there today.
- [ ] Add a unit test (if an equivalent exists for `Loras`) confirming JSON round-trip preserves `DetailerLoras`.

#### Changes Made
(fill in after completion)

---

### Step 3: Workflow Switch Preservation
**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks
- [ ] Confirm `SaveCurrentWorkflowStateAsync` and `InitializeFromWorkflowAsync` carry `DetailerLoras` across workflow swaps.
- [ ] Document any no-op findings in this file.

#### Changes Made
(fill in after completion)

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [ ] | 1 | Property + Clone |
| 2 | [ ] | 2 | JSON + DB round-trip |
| 3 | [ ] | 1 | Workflow switch preservation |

---

## Issues & Resolutions

(none yet)

---

## Commit Checkpoints

- [ ] After Step 1 complete
- [ ] After Step 2 complete
- [ ] After Step 3 complete

---

## Phase Summary

(fill in after completion)
