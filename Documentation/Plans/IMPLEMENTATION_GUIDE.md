# Implementation Guide

This document establishes conventions for planning and executing implementation sessions with GitHub Copilot. It serves as a reference for consistent, structured development practices.

---

## Table of Contents

1. [Session Phases](#session-phases)
2. [Phase 1: Planning](#phase-1-planning)
3. [Phase 2: Execution](#phase-2-execution)
4. [Phase 3: Documentation](#phase-3-documentation)
5. [Planning Document Structure](#planning-document-structure)
6. [Planning Document Template](#planning-document-template)
7. [Phase Document Template](#phase-document-template)
8. [Context Provision Guidelines](#context-provision-guidelines)
9. [Communication Conventions](#communication-conventions)

---

## Session Phases

Every implementation session follows three distinct phases:

| Phase | Purpose | Exit Criteria |
|-------|---------|---------------|
| **Planning** | Define scope, conventions, roadmap | User approval of implementation plan |
| **Execution** | Implement, test, iterate | All phase steps complete and tested |
| **Documentation** | Update knowledge base | DOC folder updated with relevant details |

---

## Phase 1: Planning

### Objectives
- Expose ideas clearly from both user and assistant
- Discuss conventions that must be maintained throughout the journey
- Predict stress points and debate complexity/value of each detail
- Create a structured roadmap with logical split between execution phases

### Required Activities

1. **Idea Exchange**
   - User describes the problem/feature
   - Assistant asks clarifying questions
   - Both parties propose solutions

2. **Convention Definition**
   - Coding standards to follow
   - Naming conventions
   - Architectural patterns
   - File organization

3. **Stress Point Analysis**
   - Identify potential breaking changes
   - Evaluate complexity vs. value trade-offs
   - Consider backward compatibility
   - Assess testing requirements

4. **Roadmap Creation**
   - Split work into logical phases
   - Define deliverables per phase
   - Establish dependencies between phases
   - Set success criteria
   - **Estimate complexity using Fibonacci points** (1, 2, 3, 5, 8, 13, 21...)

### Deliverable
A planning document folder at `Documentation/Plans/{task-name}/` containing:
- `MAIN_PLAN.md` - The primary implementation roadmap
- Phase documents created as work progresses: `PHASE_{#}.md`
- Supporting documentation as needed

---

## Phase 2: Execution

### Workflow
Execution follows a strict order for each implementation step:

```
1. Initial Code Writing
       ?
2. Test and Debug Features
       ?
3. Discuss Improvements and Tweaks
       ?
4. Update the Phase Document
```

### Key Rules

1. **User Permission Required**
   - Do not proceed to the next phase step until testing is complete
   - User must explicitly approve before updating the phase document

2. **Incremental Implementation**
   - Complete one phase at a time
   - Verify each phase works before proceeding
   - Build only runs after user requests or after completing all file edits for a step

3. **Issue Tracking**
   - Document any blockers or unexpected issues
   - Update the phase document with resolution details

4. **Phase Documents**
   - Create a new document `PHASE_{#}.md` when entering a new phase
   - Update this document after each step completion
   - Include enough context to resume progress in a new session

5. **Detours and Plan Evolution**
   - Detours from the initial plan are acceptable after discussion
   - Append detours to the main plan by:
     - Creating a new phase at the end, OR
     - Splitting a current phase (e.g., 9 ? 9 and 9.5)

### Progress Tracking
Each phase step should be marked in the phase document:
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation
Use **Fibonacci sequence** for complexity points (not time estimates):
- **1 point**: Trivial (simple property change, config update)
- **2 points**: Simple (straightforward refactor, single file change)
- **3 points**: Moderate (multi-file change, simple logic)
- **5 points**: Medium (service extraction, interface creation)
- **8 points**: Complex (component migration, breaking changes)
- **13 points**: Very complex (architecture change, wide impact)
- **21+ points**: Epic (should be split into smaller phases)

This provides a gauge for users to evaluate execution order and task complexity.

---

## Phase 3: Documentation

### Objectives
After all phases are complete:

1. **Update Knowledge Base** (`/Documentation` folder)
   - Add new feature documentation
   - Update existing docs affected by changes
   - Add architectural decision records if significant

2. **Update Related Guides**
   - `TEMPLATE_GUIDE.md` for workflow changes
   - Component documentation for UI changes
   - API documentation for service changes

3. **Final Plan Update**
   - Mark all phases complete
   - Add final notes and lessons learned
   - Archive or link from main docs

---

## Planning Document Structure

### Folder Organization
Each task or feature must have its own folder:
```
Documentation/
??? Plans/
    ??? {task-name}/
        ??? MAIN_PLAN.md        # Primary roadmap (required)
        ??? PHASE_1.md          # Created when Phase 1 begins
        ??? PHASE_2.md          # Created when Phase 2 begins
        ??? PHASE_9.5.md        # Example detour phase
        ??? ...supporting-docs  # As needed
```

### Document Purposes

| Document | Purpose | Created When |
|----------|---------|--------------|
| **MAIN_PLAN.md** | Initial breakdown into phases/steps | Planning session |
| **PHASE_{#}.md** | Detailed execution context per phase | Phase begins |
| Supporting docs | Diagrams, research, notes | As needed |

### Key Conventions
- **No time/date references** - LLMs don't have clear notion of time
- **Use Fibonacci complexity points** instead of hour estimates
- **Each step is a commitable checkpoint** for safe implementation
- **Phase documents must have enough context** to resume in a new session

---

## Planning Document Template

```markdown
# {Feature Name} - Implementation Plan

## Status
**Current Phase:** Planning | Execution (Phase X) | Documentation

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
{Describe the issue or feature request}

---

## Proposed Solution
{High-level description of the approach}

### Key Decisions
| Decision | Rationale |
|----------|-----------|
| {decision} | {why} |

### Conventions
- {Convention 1}
- {Convention 2}

---

## Implementation Phases

### Phase 1: {Name}
**Objective:** {What this phase accomplishes}
**Complexity:** {Fibonacci points} points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - {Description}
- [ ] Step 2 - {Description}

#### Success Criteria
- {Criterion 1}
- {Criterion 2}

---

### Phase 2: {Name}
{Repeat structure}

---

## Stress Points & Risks
| Risk | Mitigation | Complexity |
|------|------------|------------|
| {risk} | {mitigation} | {points} |

---

## Changelog
| Phase | Changes |
|-------|---------|
| Planning | Initial plan created |

---

## Code Examples
{When relevant to provide context for implementation}

---

## References
- {Link to related docs}
- {Link to related code}
```

---

## Phase Document Template

```markdown
# Phase {#} - {Phase Name}

## Status
**Phase:** {#}  
**Build Status:** ? Passing | ?? Issues | **Tests:** {pass}/{total}

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

{What this phase accomplishes - clear, specific goal}

---

## Context

{Any relevant context needed to understand this phase}
- Dependencies on previous phases
- Key architectural decisions
- Files/services involved

---

## Execution Checklist

### Step 1: {Step Name}
**Complexity:** {Fibonacci points}
**Status:** [ ] Not Started

#### Tasks
- [ ] Task 1
- [ ] Task 2

#### Changes Made
{Update after completion}
- {File changed} - {What was done}

---

### Step 2: {Step Name}
{Repeat structure}

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1 | ? | 3 | {Any notes} |
| 2 | [~] | 5 | {In progress} |
| 3 | [ ] | 8 | {Not started} |

---

## Issues & Resolutions

### Issue 1: {Description}
**Impact:** {What was affected}
**Resolution:** {How it was solved}

---

## Commit Checkpoints

- [x] After Step 1 complete
- [x] After Step 2 complete
- [ ] After Step 3 complete

---

## Phase Summary

{After completion - summarize what was accomplished}

### Accomplishments
1. {Achievement 1}
2. {Achievement 2}

### Metrics
- {Relevant metric 1}: {value}
- {Relevant metric 2}: {value}

### Deferred Items
- {Item deferred to future phase}

---

**Phase Status:** Complete ? | In Progress [~] | Blocked [!]

```

---

## Context Provision Guidelines

When starting a planning session, provide:

### Essential Context
1. **Problem Description**
   - What is the current behavior?
   - What is the desired behavior?
   - Why is this change needed?

2. **Scope Indicators**
   - Which files/components are affected?
   - Are there related files that should be examined?
   - What should NOT be changed?

3. **Constraints**
   - Backward compatibility requirements
   - Performance considerations
   - Complexity budget (Fibonacci points)

### Optional Enhancements
- Screenshots or UI mockups
- Example workflow templates
- Related GitHub issues
- Previous discussion context

### File Context Pattern
```markdown
# FILE CONTEXT
The current workspace includes:
- Projects targeting: {framework}
- Relevant files:
  - {file1} - {purpose}
  - {file2} - {purpose}

# ACTIVE FILES
The user has open:
- {file1}
- {file2}
```

### UI / Layout References

Any planning session that touches UI (new page, tab, form, layout change, visual refactor)
must consult the living design-language documents before proposing solutions:

| Reference | Purpose |
|---|---|
| `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` | Core design rules: input variants, density, cascading selectors, dialog shell, **Layout** section (tabbed-page shell, three layout variants, collapsible sidebar, spacing tokens) |
| `BlazorWebApp/wwwroot/site.css` (`:root`) | Global spacing tokens: `--app-gutter-outer`, `--app-gutter-inner`, `--app-sidebar-width`, `--app-sidebar-min/max`, `--app-sidebar-rail-width`, `--app-shell-max-width`, `--app-surface-radius` |
| `BlazorWebApp/Components/Layouts/LayoutDefaults.cs` | Shared elevation constants (`TabsElevation`, `SurfaceElevation`) that cannot be expressed as CSS vars |
| `BlazorWebApp/Components/Layouts/` | Reusable layout primitives (`TabbedPageShell`, `TwoColumnLayout`, `TopbarLayout`, `ContentOnlyLayout`) - all tabbed pages must go through these |

**Rules when planning UI work:**
- Never propose hard-coded spacing (`px-5`, `pa-4`, etc.) on a tabbed page shell or top-level panel paper - tune the tokens instead.
- Never render `MudTabs` directly in a page - use `TabbedPageShell`.
- Pick one of the three documented layout variants (Two-column / Topbar / Content-only). Proposing a fourth variant requires a documented exception in the design-language doc.
- Collapsible sidebar is scoped to pages where content benefits from added width (galleries, grids). Do not apply it to settings-heavy pages like Prompts.
- Form controls default to `Variant.Text`; `Variant.Outlined` is reserved for emphasis and must be explicitly justified.
- **Children of a layout slot render flush.** A component placed inside `Sidebar`, `Topbar`, or `Content` must not wrap its root in `MudPaper` / `MudCard`, must not apply `pa-*` padding classes at the root, and must not set `Elevation` at the root. The layout slot owns elevation, radius, and padding via the `--app-surface-*` tokens. A child may still use its own elevated paper inside the slot when a specific element needs **focus emphasis** (e.g. a header card at `Elevation=2`); that is an explicit exception, not the default.

---

## Communication Conventions

### User Signals

| Signal | Meaning |
|--------|---------|
| "Let's plan..." | Planning mode - gather input before implementing |
| "Resume plan: {plan_file.md}" | Continue work on existing plan |
| "Implement..." | Ready for code changes |
| "Test this" | Run build, check for errors |
| "Update the plan" | Document progress in phase document |
| "Next phase" | Proceed to next implementation step |

### Assistant Behaviors

1. **During Planning**
   - Ask clarifying questions
   - Propose alternatives
   - Highlight trade-offs
   - Estimate complexity using Fibonacci points
   - Wait for user confirmation

2. **During Execution**
   - Provide clear explanations before code
   - Make minimal, focused changes
   - Verify builds compile
   - Wait for testing confirmation
   - Update phase documents with context for resumption

3. **During Documentation**
   - Summarize what was implemented
   - Update all relevant docs
   - Highlight any follow-up items

---

## Quick Reference

### Planning Phase Checklist
- [ ] Problem clearly understood
- [ ] Solution approach agreed
- [ ] Conventions defined
- [ ] Phases identified with complexity points
- [ ] Risks documented
- [ ] Folder created: `Documentation/Plans/{task-name}/`
- [ ] MAIN_PLAN.md created

### Execution Phase Checklist (per step)
- [ ] Code written
- [ ] Build verified
- [ ] Feature tested
- [ ] Improvements discussed
- [ ] Phase document updated
- [ ] User approved

### Documentation Phase Checklist
- [ ] Knowledge base updated
- [ ] Related guides updated
- [ ] MAIN_PLAN.md finalized
- [ ] All phase documents complete
- [ ] Lessons captured

---

*Document version: 2.0*
