# Phase 1 - Foundation Setup

## Status
**Phase:** 1  
**Build Status:** ? Passing | **Tests:** ? 27/27 Passing

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update This Document**
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

Install Fluid.Core NuGet package, create custom meta block, establish basic rendering infrastructure that eliminates regex-based meta section parsing.

---

## Context

- **Current State:** WorkflowService uses Scriban with regex-based `#meta...#end` extraction
- **Target State:** FluidTemplateService with native `{% meta %}...{% endmeta %}` block parsing
- **Key Innovation:** Meta block captures content to context side-channel, not main output
- **No Feature Flag:** Instant cutoff strategy approved - migrate directly

---

## Execution Checklist

### Step 1.1: Install Fluid.Core NuGet Package
**Complexity:** 1  
**Status:** [x] Complete

#### Tasks
- [x] Add `Fluid.Core` package reference to BlazorWebApp.csproj
- [x] Verify package restores successfully
- [x] Confirm build passes

#### Notes
- Installed Fluid.Core v2.11.1

---

### Step 1.2: Create IFluidTemplateService Interface
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Create `Services/Templating/IFluidTemplateService.cs`
- [x] Define core methods: `RenderAsync`, `CreateContext`, `ClearCache`
- [x] Define `FluidRenderContext` class for context encapsulation

---

### Step 1.3: Create FluidTemplateService Implementation
**Complexity:** 5  
**Status:** [x] Complete

#### Tasks
- [x] Create `Services/Templating/FluidTemplateService.cs`
- [x] Implement custom `{% meta %}...{% endmeta %}` block tag
- [x] Implement custom `{{ var | json }}` filter
- [x] Implement custom `{% get_ref "key" %}` tag
- [x] Configure FluidParser with custom registrations
- [x] Implement template caching with `ConcurrentDictionary`

#### Implementation Details
- Meta block uses `RegisterEmptyBlock` (no expression argument)
- Get ref tag uses `RegisterExpressionTag` (takes string expression)
- Meta block content captured to `context.AmbientValues` (side-channel, not main output)
- JSON filter handles all primitive types + complex object serialization
- Case conversion utility for parameter compatibility (snake_case ? PascalCase)

---

### Step 1.4: Create JsonFilter
**Complexity:** 2  
**Status:** [x] Complete (inline in FluidTemplateService)

#### Tasks
- [x] Implement JSON encoding filter for all value types
- [x] Handle: null, string, bool, numbers, complex objects
- [x] Register filter in FluidTemplateService options

#### Notes
- Implemented as static method `JsonFilter` in FluidTemplateService
- Uses `System.Text.Json.JsonSerializer` for complex objects
- Uses Unicode escaping (`\u0022`) for quotes (valid JSON)

---

### Step 1.5: Verify Build and Basic Functionality
**Complexity:** 2  
**Status:** [x] Complete

#### Tasks
- [x] Run build to confirm no errors
- [x] Write unit tests for FluidTemplateService (27 tests)
- [x] Verify meta block captures correctly
- [x] Verify JSON filter works correctly
- [x] Verify get_ref tag works correctly

#### Test Coverage
- Basic rendering
- Meta block capture (with/without variables)
- JSON filter (string, number, boolean, null)
- Get ref tag resolution
- String contains filter
- Default/append filters
- Loop iteration with index
- Conditional evaluation
- Case-insensitive parameter access
- Complex fragment simulation

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1.1 | ?      | 1          | Package installed |
| 1.2 | ?      | 2          | Interface created |
| 1.3 | ?      | 5          | Service implemented |
| 1.4 | ?      | 2          | Inline in service |
| 1.5 | ?      | 2          | 27/27 tests passing |

**Total Phase Complexity:** 12 points

---

## Commit Checkpoints

- [x] After Step 1.1 complete (package installed)
- [x] After Step 1.3 complete (core service created)
- [x] After Step 1.5 complete (phase complete, verified)

---

## Issues & Resolutions

| Issue | Resolution |
|-------|------------|
| `Fluid.Tags` namespace doesn't exist | Removed invalid using statement |
| `MemberAccessStrategy.IgnoreCase` doesn't exist | Removed, Fluid handles case via different mechanism |
| Stale build artifacts causing phantom errors | `dotnet clean` resolved the issue |
| `RegisterExpressionBlock` expects expression | Changed to `RegisterEmptyBlock` for meta block (no expression) |
| Filters can't be used in `if` conditions directly | Use `{% assign %}` first, then reference in `{% if %}` |

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `BlazorWebApp.csproj` | Modified | Added Fluid.Core v2.11.1 package |
| `Services/Templating/IFluidTemplateService.cs` | Created | Interface definition + FluidRenderContext |
| `Services/Templating/FluidTemplateService.cs` | Created | Core implementation with custom blocks/tags/filters |
| `BlazorWebApp.Tests/Services/FluidTemplateServiceTests.cs` | Created | 27 unit tests for FluidTemplateService |

---

## Key Implementation Notes

### Meta Block Implementation
```csharp
// Registered as: {% meta %}...{% endmeta %}
parser.RegisterEmptyBlock("meta", RenderMetaBlock);

// Handler stores content in AmbientValues, NOT in output stream
context.AmbientValues[MetadataContextKey] = metaContent;
return Completion.Normal; // No output to main stream
```

### Get Ref Tag Implementation
```csharp
// Registered as: {% get_ref "key" %}
parser.RegisterExpressionTag("get_ref", RenderGetRefTag);

// Resolves from NodeRegistry stored in AmbientValues
var reference = registry.GetReference(key);
await writer.WriteAsync(reference);
```

### JSON Filter Implementation
```csharp
// Registered as: {{ value | json }}
_options.Filters.AddFilter("json", JsonFilter);

// Handles all types: null, string, bool, numbers, complex objects
```

---

## Phase Summary

### Accomplishments
1. ? Installed Fluid.Core v2.11.1 NuGet package
2. ? Created `IFluidTemplateService` interface with async render methods
3. ? Implemented `FluidTemplateService` with:
   - Custom `{% meta %}...{% endmeta %}` block (side-channel capture)
   - Custom `{% get_ref "key" %}` tag (node reference resolution)
   - Custom `{{ var | json }}` filter (JSON encoding)
   - Custom `{{ str | string_contains: "x" }}` filter
   - Template caching with `ConcurrentDictionary`
4. ? Created 27 comprehensive unit tests - all passing

### Key Validation
- **Parser Bleeding Solved:** Meta block content is captured to context, NOT output stream
- **No Regex Required:** Fluid natively handles `{% meta %}...{% endmeta %}` parsing
- **Backward Compatible:** Same output format as Scriban templates

---

**Phase Status:** ? Complete - Ready for Phase 2 (Core Service Migration)
