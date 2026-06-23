# Phase 1 - Foundation (tokens + design doc)

## Status
**Phase:** 1 - Complete
**Build Status:** Passing (docs + CSS only, no code behavior change) | **Tests:** n/a

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits for a step

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

---

## Objective

Establish the single source of truth for spacing (CSS tokens) and document the layout conventions in the design-language doc, with zero visual impact on existing pages. All later phases depend on this foundation.

---

## Context

- Tokens live in `BlazorWebApp/wwwroot/site.css` under `:root`. This keeps them global and aligned with the existing override pattern in that file.
- A companion C# constants class `BlazorWebApp/Components/Layouts/LayoutDefaults.cs` holds elevation values (MudBlazor `Elevation` is `int`, cannot be a CSS var).
- The design doc is `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`. A new `## Layout` section is appended after the existing `## Core Rules`.
- No page is touched in this phase - pages keep their current spacing. Visual changes happen starting Phase 3.

### Tokens (baseline)
```css
--app-gutter-outer: 12px;
--app-gutter-inner: 8px;
--app-sidebar-width: 22%;
--app-sidebar-min: 240px;
--app-sidebar-max: 340px;
--app-sidebar-rail-width: 40px;
--app-shell-max-width: 1600px;
--app-surface-radius: 4px;
```

### Constants
```csharp
public const int TabsElevation = 4;
public const int SurfaceElevation = 1;
```

---

## Execution Checklist

### Step 1: Add global CSS tokens
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Append a `:root` block with the 8 tokens to `BlazorWebApp/wwwroot/site.css`
- [x] Place it near the top, under the existing MudBlazor z-index overrides, with a clear section header

#### Changes Made
- `BlazorWebApp/wwwroot/site.css` - added `App Layout Tokens (global)` section with 8 tokens under `:root`.

---

### Step 2: Add `LayoutDefaults` constants class
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] Create `BlazorWebApp/Components/Layouts/LayoutDefaults.cs`
- [x] Expose `TabsElevation` and `SurfaceElevation`

#### Changes Made
- `BlazorWebApp/Components/Layouts/LayoutDefaults.cs` - new file, two public constants.

---

### Step 3: Extend design-language doc with Layout section
**Complexity:** 2
**Status:** [x] Complete

#### Tasks
- [x] Append new `## Layout` section to `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- [x] Document the three variants (Two-column, Topbar, Content-only)
- [x] Document collapsible-sidebar rules (scope: Resources/CivitAI only, rail mode, persisted)
- [x] Document tab-shell rules (centered, rounded, `TabsElevation`, no per-page `PanelClass`)
- [x] Cross-reference token names

#### Changes Made
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - appended `## Layout` section.

---

### Step 4: Visual verification (user)
**Complexity:** 1
**Status:** [x] Complete

#### Tasks
- [x] User confirmed no visual regression
- [x] User approved baseline token values

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | [x] | 1 | CSS tokens |
| 2 | [x] | 1 | LayoutDefaults.cs |
| 3 | [x] | 2 | Design doc Layout section |
| 4 | [x] | 1 | User visual verification - approved |

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [x] After Steps 1+2+3 (all code+docs) complete
- [x] After Step 4 (user verification) complete

---

## Phase Summary

Foundation in place with zero visual impact:
- Global spacing tokens live in `wwwroot/site.css` under `:root`.
- Elevation constants live in `Components/Layouts/LayoutDefaults.cs`.
- `04-UI-DESIGN-LANGUAGE.md` gained a `## Layout` section documenting the three variants, collapsible-sidebar scope, and layout anti-patterns.
- Cross-reference added to `IMPLEMENTATION_GUIDE.md` so future UI planning sessions pick up these conventions automatically.

### Accomplishments
1. Single source of truth for tabbed-page spacing.
2. Documented convention ready to be consumed by Phase 2 components.

### Deferred Items
- None.

**Phase Status:** Complete [x]
