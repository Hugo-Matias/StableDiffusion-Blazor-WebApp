# Expansion Pack System - Implementation Plan

## Status
**Current Phase:** Planning

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update Phase Document**
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

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The current workflow/fragment templating system has become **brittle and unmaintainable**:

### Current Issues
1. **Template Engine Complexity** - Fighting Liquid/Fluid quote syntax in JSON contexts
2. **Meta Block Fragility** - Parser bugs with multi-line content, regex workarounds failing
3. **Multi-Phase Rendering** - Complex workflow ? fragments ? meta extraction ? reference resolution
4. **Manual Maintenance** - Node definitions require constant updates when ComfyUI changes
5. **Custom Node Support** - Cannot handle user's custom ComfyUI node installations
6. **Migration Pain** - Scriban ? Fluid migration consumed significant effort with ongoing issues
7. **Import Difficulty** - Cannot easily import ComfyUI workflows without manual templating

### Root Cause
Using **document templating engines** (designed for HTML/text generation) to produce **structured data** (JSON). This creates constant syntax conflicts, parser edge cases, and maintenance burden.

### User Goal
**Simple workflow:** Export from ComfyUI ? Import to app ? Expose UI controls ? Generate correct payload

---

## Proposed Solution: Expansion Pack System

### High-Level Architecture

**Expansion Pack** = Self-contained module containing:
- **Pack Manifest** (`pack.json`) - Metadata, dependencies, versioning
- **Workflow Definition** (`workflow.json`) - Node structure with parameter bindings
- **UI Component** (Razor) - Blazor form for user input
- **Assets** - Icons, previews, help documentation

### Core Principles

1. **API-Driven Node Discovery** - Query ComfyUI `/object_info` endpoint for schemas
2. **No Template Engine** - Simple `${variable}` substitution (no Liquid/Scriban)
3. **JSON-First** - Work with native ComfyUI JSON format
4. **Auto-Fill Defaults** - Leverage ComfyUI's node schemas for default values
5. **Self-Contained Packs** - Each workflow is independent, versioned, testable
6. **No DLL Loading** - Pure JSON + Razor files (avoid assembly isolation complexity)

---

## Key Architectural Decisions

| Decision | Rationale | Impact |
|----------|-----------|--------|
| **Query ComfyUI API for Node Schemas** | Eliminates manual maintenance, supports custom nodes | Always in sync with user's install |
| **Cache Node Schemas Locally** | Performance + offline support | 1-hour TTL, persisted to disk |
| **Simple `${var}` Expression Syntax** | Minimal parser (~150 lines vs thousands) | Predictable, no quote conflicts |
| **No Template Engine** | Removes Liquid/Scriban complexity entirely | Eliminates current pain points |
| **JSON-First Workflow Structure** | Native ComfyUI format | Easy validation, direct mapping |
| **Razor for UI Components** | Leverage existing Blazor expertise | Type-safe, reusable |
| **Pack Status Validation** | Check dependencies at load time | User sees clear warnings/errors |
| **ComfyUI Offline Handling** | Backend offline = generation disabled | Clear user feedback |

---

## Pack Structure & File Organization

```
BlazorWebApp/
??? Packs/
    ??? txt2img/                        # Example: Simple pack
    ?   ??? pack.json                   # Pack metadata
    ?   ??? workflow.json               # Workflow definition
    ?   ??? ui/
    ?   ?   ??? Txt2ImgForm.razor       # Custom UI
    ?   ??? assets/
    ?       ??? icon.png
    ?       ??? preview.png
    ?
    ??? img2vid-wan/                    # Example: Complex pack
        ??? pack.json
        ??? workflow.json
        ??? ui/
        ?   ??? Img2VidForm.razor
        ??? assets/
            ??? icon.png
            ??? preview.png
            ??? help.md
```

**No `/Packs/core/` needed** - Node schemas come from ComfyUI API!

---

## Expression Syntax Reference

### Supported in `workflow.json`:

| Syntax | Example | Description |
|--------|---------|-------------|
| **Simple Variable** | `${Seed}` | Direct parameter substitution |
| **Null Coalescing** | `${ModelName ?? 'default.safetensors'}` | Use fallback if null/undefined |
| **Ternary** | `${UseHQ ? 'model_720' : 'model_540'}` | Conditional value |
| **Numeric Ternary** | `${UseHQ ? 1280 : 960}` | For width/height/steps |
| **Node Reference** | `@model_loader.MODEL` | Reference another node's output |
| **Literal String** | `"constant_value"` | Fixed string |
| **Literal Number** | `7.0`, `1280` | Fixed numeric value |
| **Literal Boolean** | `true`, `false` | Fixed boolean |

### Auto-Fill Behavior
If a node input is **not specified** in `workflow.json`, the renderer will:
1. Query node schema from ComfyUI
2. Apply default value from schema (if available)
3. Leave required inputs as required (validation fails if missing)

This makes workflows **concise** and **resilient** to schema changes.

---

## Conventions

### File Naming
- Pack IDs: Reverse domain notation (`com.app.pack-name`)
- Files: Lowercase with hyphens (`pack.json`, `workflow.json`)
- UI Components: PascalCase (`Img2VidForm.razor`)

### Versioning
- Packs: Semantic versioning (`1.2.3`)
- Workflow schema: Integer version in `workflow.json` (`schema_version: 1`)

### Parameter Naming
- Parameters: PascalCase (`PositivePrompt`, `UseHighQuality`)
- Node IDs: snake_case (`model_loader`, `vae_decode`)
- ComfyUI inputs: Match schema exactly (`unet_name`, `sampler_name`)

### Categories
- Standard categories: `image`, `video`, `audio`, `upscaling`, `editing`
- UI parameter groups: `Input`, `Model`, `Prompts`, `Generation`, `Output`

### Status Handling
- **Ready**: All dependencies satisfied, fully functional
- **Warning**: Missing optional nodes or minor issues, still usable
- **Disabled**: Missing required nodes, cannot generate
- **Unknown**: ComfyUI offline, validation skipped

---

## Implementation Phases

### Phase 1: Core Foundation (15 points)
**Objective:** Build pack loading system and API-driven node discovery

**Status:** [ ] Not Started

#### Steps
- [ ] Design JSON schemas for `pack.json`, `workflow.json`
- [ ] Create models: `ExpansionPackManifest`, `WorkflowDefinition`, `ComfyNodeSchema`
- [ ] Implement `ComfyNodeDiscoveryService` (API integration)
- [ ] Add `/object_info` endpoint methods to `IComfyUIService`
- [ ] Implement API response parsing (ComfyUI ? `ComfyNodeSchema`)
- [ ] Implement schema caching with 1-hour TTL
- [ ] Implement schema persistence to disk (for offline support)
- [ ] Implement `PackLoaderService` (discover, load, validate)
- [ ] Implement `PackRegistry` (store, query)
- [ ] Create `/Packs` folder structure
- [ ] Add startup integration (call `PackLoaderService.InitializeAsync()`)

#### Success Criteria
- App queries ComfyUI `/object_info` on startup
- Node schemas cached and queryable
- Can load sample pack from disk
- Validation correctly identifies missing nodes
- Offline mode uses persisted cache
- Build passes, no runtime errors

---

### Phase 2: Simple Expression Renderer (10 points)
**Objective:** Implement variable substitution and reference resolution

**Status:** [ ] Not Started

#### Steps
- [ ] Implement `SimpleWorkflowRenderer` base class
- [ ] Implement `${variable}` replacement with regex
- [ ] Implement `${var ?? 'fallback'}` null coalescing
- [ ] Implement `${condition ? 'true' : 'false'}` ternary expressions
- [ ] Implement `@node.OUTPUT` reference resolution
- [ ] Implement auto-fill from ComfyUI node schemas
- [ ] Add error handling with clear messages
- [ ] Unit tests for renderer (all syntax patterns)

#### Success Criteria
- Can render simple workflow with all syntax patterns
- References resolved to correct `[node_id, index]` format
- Auto-fill applies defaults from schemas
- Clear error messages for undefined variables/nodes
- All unit tests pass

---

### Phase 3: Workflow Validation (8 points)
**Objective:** Validate rendered workflows against node schemas

**Status:** [ ] Not Started

#### Steps
- [ ] Implement `WorkflowValidationService`
- [ ] Validate node types exist in ComfyUI
- [ ] Validate required inputs are provided
- [ ] Validate input types match schema expectations
- [ ] Validate numeric ranges (min/max)
- [ ] Validate choice constraints (enums)
- [ ] Validate node references point to valid nodes
- [ ] Generate user-friendly validation error messages

#### Success Criteria
- Invalid workflows rejected with helpful errors
- Valid workflows pass without warnings
- Type mismatches detected and reported
- Missing required inputs detected
- All validation rules tested

---

### Phase 4: UI Integration (13 points)
**Objective:** Dynamic UI component loading and parameter extraction

**Status:** [ ] Not Started

#### Steps
- [ ] Create `PackFormBase<TParameters>` base class
- [ ] Implement `UIComponentResolver` (load Razor from pack)
- [ ] Update Generate page to query `PackRegistry`
- [ ] Add pack selector dropdown (with status indicators)
- [ ] Implement dynamic component rendering
- [ ] Add pack warning display for `Warning` status
- [ ] Disable pack UI for `Disabled` status
- [ ] Wire Generate button to renderer
- [ ] Add ComfyUI offline banner
- [ ] Extract parameters from UI to `Dictionary<string, object>`
- [ ] Integrate with existing `ComfyUIService.QueuePromptAsync()`

#### Success Criteria
- Generate page shows available packs
- Pack selector displays status icons (??, ?)
- Selecting pack loads UI component dynamically
- Form submission extracts parameters correctly
- Generate button calls renderer and submits to ComfyUI
- Offline state prevents generation with clear message

---

### Phase 5: First Pack - Txt2Img (8 points)
**Objective:** Create complete working pack as proof of concept

**Status:** [ ] Not Started

#### Steps
- [ ] Create `/Packs/txt2img/` directory structure
- [ ] Define `pack.json` metadata (id, name, version, dependencies)
- [ ] Define `workflow.json` with parameters and nodes
- [ ] Map txt2img parameters (prompts, seed, steps, cfg, size)
- [ ] Create `Txt2ImgForm.razor` UI component
- [ ] Inherit from `PackFormBase<Txt2ImgParameters>`
- [ ] Add form fields with validation
- [ ] Add pack icon and preview assets
- [ ] Test end-to-end: Load pack ? Fill form ? Render ? Submit to ComfyUI

#### Success Criteria
- Txt2img pack loads successfully
- UI form is functional and validates inputs
- Generates valid ComfyUI workflow JSON
- Successfully submits to ComfyUI and generates image
- No errors in logs

---

### Phase 6: Migration - Img2Vid WAN (13 points)
**Objective:** Migrate existing img2vid workflow to pack format

**Status:** [ ] Not Started

#### Steps
- [ ] Analyze current img2vid workflow structure
- [ ] Identify all required ComfyUI nodes
- [ ] Create `/Packs/img2vid-wan/` structure
- [ ] Define `pack.json` with all dependencies
- [ ] Define `workflow.json` with conditional logic (quality toggle)
- [ ] Map all existing parameters
- [ ] Create `Img2VidForm.razor` UI
- [ ] Implement quality toggle (high/low model selection)
- [ ] Add video settings (frame count, frame rate)
- [ ] Test all parameter combinations
- [ ] Verify feature parity with current system

#### Success Criteria
- Img2vid pack loads successfully
- Quality toggle works (720p vs 540p)
- All parameters function as before
- No regressions from current workflow
- Successfully generates videos

---

### Phase 7: Deprecate Old System (21 points)
**Objective:** Remove Fluid/templating code, migrate remaining workflows

**Status:** [ ] Not Started

#### Steps
- [ ] Audit all existing workflows (identify what needs migration)
- [ ] Convert each workflow to pack format
- [ ] Test each migrated pack
- [ ] Remove `FluidTemplateService.cs`
- [ ] Remove `WorkflowService` template rendering logic
- [ ] Remove Fluid NuGet package references
- [ ] Delete `/Workflows/Fragments/` directory
- [ ] Delete `/Workflows/Templates/*.liquid` files
- [ ] Update `WorkflowService` to use `PackRegistry` and `SimpleWorkflowRenderer`
- [ ] Update Generate page to only use pack system
- [ ] Remove obsolete services from DI registration
- [ ] Update documentation
- [ ] Run full regression testing

#### Success Criteria
- All workflows migrated to packs
- No Fluid/Scriban code remains
- No fragment files remain
- App compiles without warnings
- All existing features work via packs
- No template-related errors in logs

---

### Phase 8: Advanced Features (13 points)
**Objective:** Add polish and developer tools

**Status:** [ ] Not Started

#### Steps
- [ ] Implement pack hot-reload (file watcher for `/Packs`)
- [ ] Create pack validation CLI tool
- [ ] Create ComfyUI workflow importer (JSON ? pack converter)
- [ ] Create node schema sync tool (update from ComfyUI)
- [ ] Add pack browser UI page (view installed packs, details)
- [ ] Add pack enable/disable toggle in UI
- [ ] Add auto-UI generation for simple packs (no custom Razor)
- [ ] Add pack documentation viewer (render help.md)
- [ ] Create pack development guide
- [ ] Add telemetry/logging for pack usage

#### Success Criteria
- File changes auto-reload packs
- Validation tool catches issues before deployment
- Importer simplifies pack creation
- Schema sync keeps definitions current
- Pack browser provides good UX
- Auto-UI works for 80% of simple cases
- Documentation guide is complete

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|------|------------|------------|
| **ComfyUI API schema changes** | Version workflows, test against multiple ComfyUI versions | 5 points |
| **Expression syntax too limited** | Start minimal, add features incrementally based on real needs | 3 points |
| **Dynamic component routing issues** | Leverage proven Blazor patterns, extensive testing | 5 points |
| **Node schema caching stale** | Implement manual refresh button, show cache age in UI | 3 points |
| **Pack dependency hell** | Enforce semantic versioning, dependency graph validation | 5 points |
| **Custom node discovery failures** | Graceful degradation, clear error messages, retry logic | 5 points |
| **Performance with large node lists** | Lazy loading, indexed cache, background sync | 5 points |
| **Workflow version migration** | Store ComfyUI version in metadata, optional migration hooks | 8 points |
| **Breaking changes in packs** | Semantic versioning enforcement, migration warnings in UI | 8 points |
| **Testing coverage for all packs** | Unit test frameworks, integration test harness | 8 points |

**Total Risk Mitigation Effort:** 55 points

---

## Total Complexity Estimate

| Phase | Complexity | Risk Level |
|-------|------------|------------|
| 1. Core Foundation | 15 | Medium |
| 2. Simple Renderer | 10 | Low |
| 3. Workflow Validation | 8 | Low |
| 4. UI Integration | 13 | Medium |
| 5. First Pack (Txt2Img) | 8 | Low |
| 6. Migration (Img2Vid) | 13 | Medium |
| 7. Deprecate Old System | 21 | High |
| 8. Advanced Features | 13 | Low |
| **TOTAL** | **101** | **Epic** |

**Recommendation:** Implement Phases 1-6 first (67 points), validate approach with production use, then decide on Phases 7-8.

---

## Phased Rollout Strategy

### Milestone 1: Proof of Concept (Phases 1-3)
**Goal:** Validate core architecture
- ComfyUI API integration works
- Expression renderer handles all syntax
- Validation catches errors correctly
- **Checkpoint:** Review architecture, decide to proceed or pivot

### Milestone 2: End-to-End (Phases 4-5)
**Goal:** One working pack from UI to ComfyUI
- Full UI integration
- Txt2img pack functional
- **Checkpoint:** Verify user experience, gather feedback

### Milestone 3: Feature Parity (Phase 6)
**Goal:** Match current system capabilities
- Img2vid migrated
- Complex workflows proven
- **Checkpoint:** Validate no regressions

### Milestone 4: Migration Complete (Phase 7)
**Goal:** Remove legacy system
- All workflows converted
- Code cleanup done
- **Checkpoint:** Production ready

### Milestone 5: Polish (Phase 8)
**Goal:** Developer experience and tools
- Easy pack creation
- Good documentation
- **Checkpoint:** Long-term maintainability achieved

---

## Open Design Questions

### Q1: Expression Complexity Limit
**Question:** How complex should `${...}` expressions be?

**Current Plan:** Support `${var}`, `${var ?? 'default'}`, `${cond ? 'a' : 'b'}`

**Potential Additions:**
- Comparisons: `${var > 5 ? 'big' : 'small'}`
- Math: `${width * 2}`, `${steps + 10}`
- Functions: `${lowercase(var)}`, `${round(cfg, 2)}`
- Nested access: `${config.model.name}`

**Decision Needed:** Start minimal, add based on real use cases during pack creation?

---

### Q2: Conditional Node Inclusion
**Question:** Should entire nodes be conditional, or just their inputs?

**Current Plan:** Conditional inputs via ternary: `"unet_name": "${UseHQ ? 'model_720' : 'model_540'}"`

**Alternative:** Support node-level conditions:
```json
{
  "nodes": {
    "model_high": {
      "condition": "${UseHighQuality}",
      "node_type": "UNETLoader",
      "inputs": { "unet_name": "model_720.safetensors" }
    }
  }
}
```

**Decision Needed:** Defer to Phase 8 if many use cases emerge?

---

### Q3: Auto-UI vs Custom UI
**Question:** When should packs use auto-generated UI vs custom Razor components?

**Heuristic:**
- **Auto-UI**: Simple forms, <10 parameters, no complex layout
- **Custom UI**: Tabs, advanced settings, dynamic fields, custom validation

**Implementation:** Phase 8 adds auto-UI generation from `workflow.json` parameters

---

### Q4: Fragment System Fate
**Question:** Do fragments have a place in the new system?

**Current Fragments:** Reusable sub-graphs (e.g., "load-model-sage" = UNETLoader ? Sage ? Torch)

**Options:**
1. **Eliminate** - Everything is explicit nodes in `workflow.json`
2. **Node Macros** - Special syntax like `${@macro:load-model-sage}` expands to multiple nodes
3. **Shared Definitions** - Packs can reference common node groups from library

**Decision Needed:** Start with Option 1 (eliminate), revisit in Phase 8 if patterns emerge

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created |
| Planning | Incorporated ComfyUI API-driven node discovery |
| Planning | Defined simple expression syntax |
| Planning | Established offline handling strategy |
| Planning | Defined pack validation approach |

---

## Code Examples

### Example: Simple `workflow.json` (Txt2Img)

```json
{
  "schema_version": 1,
  "parameters": {
    "Model": { "type": "string", "required": true },
    "Seed": { "type": "integer", "default": -1 },
    "Steps": { "type": "integer", "default": 20 },
    "PositivePrompt": { "type": "text", "required": true }
  },
  "nodes": {
    "model_loader": {
      "node_type": "UNETLoader",
      "inputs": {
        "unet_name": "${Model}"
      }
    },
    "sampler": {
      "node_type": "KSampler",
      "inputs": {
        "model": "@model_loader.MODEL",
        "seed": "${Seed}",
        "steps": "${Steps}"
      }
    }
  }
}
```

### Example: ComfyUI API Response Parsing

```csharp
// Input: ComfyUI /object_info/KSampler response
var response = await _comfyService.GetObjectInfoAsync("KSampler");

// Parse to ComfyNodeSchema
var schema = new ComfyNodeSchema
{
    NodeType = "KSampler",
    DisplayName = "KSampler",
    Category = "sampling",
    Inputs = new List<ComfyNodeInput>
    {
        new() {
            Name = "seed",
            Type = "INT",
            Required = true,
            DefaultValue = 0,
            Min = 0,
            Max = 18446744073709551615
        },
        // ... more inputs
    },
    Outputs = new List<ComfyNodeOutput>
    {
        new() { Name = "LATENT", Type = "LATENT", Index = 0 }
    }
};
```

---

## References

### Current System
- Current workflows: `BlazorWebApp/Workflows/Templates/`
- Current fragments: `BlazorWebApp/Workflows/Fragments/`
- Template service: `BlazorWebApp/Services/Templating/FluidTemplateService.cs`
- Workflow service: `BlazorWebApp/Services/WorkflowService.cs`
- Fluid migration plan: `Documentation/Plans/fluid-migration/PHASE_7.md`

### ComfyUI Documentation
- API Reference: https://github.com/comfyanonymous/ComfyUI/wiki/API-Endpoints
- Object Info: `GET /object_info` and `GET /object_info/{class_type}`
- Custom Nodes: https://github.com/comfyanonymous/ComfyUI/wiki/Custom-Nodes

### Design Decisions
- Implementation Guide: `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
- Architectural patterns: Blazor best practices for dynamic components

---

## Next Steps

**Before proceeding to Phase 1 execution:**

1. ? User approval of this plan
2. ? Decision on open questions (Q1-Q4) - or defer to implementation
3. ? Review complexity estimates - adjust if needed
4. ? Confirm phased rollout strategy
5. ? Any additional concerns or requirements

**User approval needed to proceed with Phase 1.**

---

*Plan Status: Awaiting User Approval*
*Last Updated: Planning Session*
*Total Estimated Complexity: 101 Fibonacci points (Epic)*
