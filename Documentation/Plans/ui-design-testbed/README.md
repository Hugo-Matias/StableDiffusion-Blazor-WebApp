# UI Design Test Bed Plan

## Overview

This plan establishes a **development-only page** (`/dev/design-testbed`) that serves as:

- **Living pattern library** - visual catalog of all UI patterns from `04-UI-DESIGN-LANGUAGE.md`
- **Design iteration sandbox** - isolated environment for rapid prototyping
- **Consistency audit dashboard** - automated scanner for design language violations
- **Extraction staging area** - bridge between experiments and production-ready shared primitives

## Documents

| File                               | Purpose                                                                         |
| ---------------------------------- | ------------------------------------------------------------------------------- |
| [MAIN_PLAN.md](./MAIN_PLAN.md)     | Complete implementation plan with 9 phases, complexity estimates, stress points |
| [USAGE_GUIDE.md](./USAGE_GUIDE.md) | How to use the test bed for feature development, prototyping, auditing          |
| Phase documents                    | Created during execution (e.g., `PHASE_1.md`, `PHASE_2.md`)                     |

## Status

**Current Phase:** Planning

## Quick Links

- **Design Language Doc:** [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md)
- **Layout Components:** `BlazorWebApp/Components/Layouts/`
- **Global Tokens:** `BlazorWebApp/wwwroot/site.css`
- **Shared Styles:** `BlazorWebApp/wwwroot/css/`

## Key Decisions

1. **Dev-only page** - Conditionally registered only in Development environment
2. **Inline iteration allowed** - Test bed can use inline CSS for rapid experimentation (must extract before production use)
3. **Audit automation** - Build a simple pattern scanner to detect violations across existing pages
4. **Extraction tracker** - Manual log of patterns ready for promotion to shared primitives

## Complexity

**Total: 40 Fibonacci points** across 9 phases

This is a medium-large effort, but delivers incremental value. Early phases (1-7) are low-risk pattern display, Phase 8 (audit tooling) is higher complexity, Phase 9 is documentation sync.

## Success Criteria

- ✅ All documented UI patterns visualized with correct + incorrect examples
- ✅ Dev page accessible in dev, 404 in production
- ✅ Token values displayed with live computed values
- ✅ Audit report identifies real violations in existing pages
- ✅ Design language doc cross-referenced with test bed

## Related Plans

- `civitai-modals-redesign` - May benefit from test bed for prototyping modal patterns

---

**Ready to begin?** Start with Phase 1: Foundation (3 points)
