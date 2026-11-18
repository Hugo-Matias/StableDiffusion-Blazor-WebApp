# Phase 1 - Schema Definition & Documentation

## Status
**Phase:** 1  
**Build Status:** N/A (documentation phase) | **Tests:** N/A

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

Define the complete UI schema format for fragments, establish conventions, and document the system before implementation begins. This ensures we have a solid contract between fragments and UI components.

---

## Context

- This is a documentation-first phase
- Output: `FRAGMENT_SCHEMA_GUIDE.md` in `BlazorWebApp/Workflows/`
- The schema will be used by `WorkflowService` to parse fragment `#meta` blocks
- Components will read schema to get constraints (min/max/step) and behavior flags

---

## Execution Checklist

### Step 1.1: Design Complete UI Schema JSON Structure
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Define root `ui` object structure
- [x] Define `parameters` object for designed components
- [x] Define `fields` array for dynamic field rendering
- [x] Define behavior flags (collapsible, chainable, etc.)
- [x] Define source references for select fields

---

### Step 1.2: Create FRAGMENT_SCHEMA_GUIDE.md
**Complexity:** 3
**Status:** [x] Complete

#### Tasks
- [x] Create the guide file
- [x] Document all schema properties
- [x] Include examples for each scenario
- [x] Document field types and their properties
- [x] Document component registration conventions

#### Changes Made
- Created `BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`

---

### Step 1.3: Define Field Types and Properties
**Complexity:** 2
**Status:** [x] Complete

#### Field Types Defined
- `slider`, `numeric`, `seed`, `select`, `text`, `textarea`
- `checkbox`, `switch`, `color`, `file`, `resolution`, `group`

---

### Step 1.4: Define Component Registry Conventions
**Complexity:** 1
**Status:** [x] Complete

#### Initial Components
- PromptsForm, SamplerForm, ResolutionForm, LoraForm
- UpscaleForm, DetailerForm, ConditioningVariationForm, SeedVarianceEnhancerForm

---

### Step 1.5: Review with Example Fragments
**Complexity:** 2
**Status:** [x] Complete - User Approved

#### Schemas Drafted
- sampler.sbn, prompts.sbn, upscale-seedvr2.sbn
- detailer-core.sbn, seed-variance-enhancer.sbn, conditioning-variation.sbn

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1.1 | &check; | 3 | Schema structure complete |
| 1.2 | &check; | 3 | Guide created |
| 1.3 | &check; | 2 | 12 field types defined |
| 1.4 | &check; | 1 | 8 initial components identified |
| 1.5 | &check; | 2 | User approved |

---

## Commit Checkpoints

- [x] After Step 1.2 complete (guide created)
- [x] After Step 1.5 complete (phase complete, user approved)

---

## Phase Summary

### Accomplishments
1. Created comprehensive `FRAGMENT_SCHEMA_GUIDE.md` with full schema documentation
2. Defined 12 field types for dynamic rendering
3. Established component registry conventions (PascalCase + Form suffix)
4. Drafted schemas for 6 key fragments
5. Documented migration path from old to new system

### Metrics
- Field types defined: 12
- Initial components identified: 8
- Example schemas drafted: 6

### Files Created
- `BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`

---

**Phase Status:** Complete &check;
