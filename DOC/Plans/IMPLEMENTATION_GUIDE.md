# Implementation Guide

This document establishes conventions for planning and executing implementation sessions with GitHub Copilot. It serves as a reference for consistent, structured development practices.

---

## Table of Contents

1. [Session Phases](#session-phases)
2. [Phase 1: Planning](#phase-1-planning)
3. [Phase 2: Execution](#phase-2-execution)
4. [Phase 3: Documentation](#phase-3-documentation)
5. [Planning Document Template](#planning-document-template)
6. [Context Provision Guidelines](#context-provision-guidelines)
7. [Communication Conventions](#communication-conventions)

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

### Deliverable
A planning document at `DOC/Plans/{feature-name}.md` containing:
- Problem statement
- Proposed solution
- Phase breakdown
- Success criteria
- Changelog (updated during execution)

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
4. Update the Plan Document
```

### Key Rules

1. **User Permission Required**
   - Do not proceed to the next phase step until testing is complete
   - User must explicitly approve before updating the plan document

2. **Incremental Implementation**
   - Complete one phase at a time
   - Verify each phase works before proceeding
   - Build only runs after user requests or after completing all file edits for a step

3. **Issue Tracking**
   - Document any blockers or unexpected issues
   - Update the plan document with resolution details

### Progress Tracking
Each phase step should be marked in the plan document:
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

---

## Phase 3: Documentation

### Objectives
After all phases are complete:

1. **Update Knowledge Base** (`/DOC` folder)
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

## Planning Document Template

```markdown
# {Feature Name} - Implementation Plan

## Status
**Current Phase:** Planning | Execution (Phase X) | Documentation
**Last Updated:** {date}

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
**Status:** [ ] Not Started

#### Tasks
- [ ] Task 1
- [ ] Task 2

#### Success Criteria
- {Criterion 1}
- {Criterion 2}

---

### Phase 2: {Name}
{Repeat structure}

---

## Stress Points & Risks
| Risk | Mitigation |
|------|------------|
| {risk} | {mitigation} |

---

## Changelog
| Date | Phase | Changes |
|------|-------|---------|
| {date} | Planning | Initial plan created |

---

## Code Examples
{When relevant to provide context for implementation}

---

## References
- {Link to related docs}
- {Link to related code}
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
   - Time/complexity budget

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

---

## Communication Conventions

### User Signals

| Signal | Meaning |
|--------|---------|
| "Let's discuss..." | Planning mode - gather input before implementing |
| "Implement..." | Ready for code changes |
| "Test this" | Run build, check for errors |
| "Update the plan" | Document progress |
| "Next phase" | Proceed to next implementation step |

### Assistant Behaviors

1. **During Planning**
   - Ask clarifying questions
   - Propose alternatives
   - Highlight trade-offs
   - Wait for user confirmation

2. **During Execution**
   - Provide clear explanations before code
   - Make minimal, focused changes
   - Verify builds compile
   - Wait for testing confirmation

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
- [ ] Phases identified
- [ ] Risks documented
- [ ] Plan document created

### Execution Phase Checklist (per step)
- [ ] Code written
- [ ] Build verified
- [ ] Feature tested
- [ ] Improvements discussed
- [ ] Plan updated
- [ ] User approved

### Documentation Phase Checklist
- [ ] Knowledge base updated
- [ ] Related guides updated
- [ ] Plan finalized
- [ ] Lessons captured

---

*Document version: 1.0*
*Created: {current_date}*
