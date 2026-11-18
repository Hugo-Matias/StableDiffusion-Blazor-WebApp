# ADR: UI Design Test Bed for Pattern Consistency

## Status

**Proposed** - Planning phase

## Context

The BlazorWebApp has grown to include multiple tabbed pages (Generate, Prompts, Resources, CivitAI, Scheduler, Danbooru) with a documented design language (`04-UI-DESIGN-LANGUAGE.md`), shared layout components, global tokens, and style conventions. However:

### Current Pain Points

1. **No visual reference** - The design language doc is text-only. Developers must hunt through existing pages to find "correct" examples, which may themselves be inconsistent.

2. **Inconsistent implementations** - Organic evolution has led to deviations:
   - Hard-coded spacing (`px-5`, `pa-4`) on page shells instead of using tokens
   - Wrong form variants (`Variant.Outlined` on inputs where `Variant.Text` is documented)
   - Direct `MudTabs` rendering instead of using `TabbedPageShell`
   - Double-wrapped surfaces (root `MudPaper` inside layout slots)
   - Duplicated `.send-to-*` rules in component `.razor.css`

3. **No safe iteration space** - Design changes must be done directly in production pages, creating risk:
   - Inline experiments affect real UI
   - Rollback requires git history
   - A/B testing requires feature flags or branches

4. **No consistency enforcement** - Violations are caught only during PR review (manual, error-prone)

5. **Extraction friction** - When a pattern needs to become a shared primitive, there's no established workflow beyond "refactor in place"

### Why This Matters

- **Onboarding:** New contributors don't know which patterns to follow
- **Velocity:** Developers reinvent UI instead of reusing primitives
- **Quality:** Inconsistencies accumulate, making future refactors harder
- **Maintenance:** Shared primitives (tokens, layout components) aren't exercised in isolation

## Decision

Build a **development-only page** (`/dev/design-testbed`) that:

1. **Displays all UI patterns** from the design language doc as working examples
2. **Allows inline iteration** without affecting production (sandbox for experiments)
3. **Automates consistency audits** by scanning existing pages for violations
4. **Tracks extraction progress** from experimental inline styles to shared primitives

This serves as:

- **Pattern library** (see the rules in action)
- **Sandbox** (prototype safely)
- **Audit tool** (enforce consistency)
- **Extraction pipeline** (formalize promotion to shared code)

## Alternatives Considered

### Alternative 1: External Design System Site (e.g., Storybook)

**Pros:**

- Industry-standard tool
- Rich component documentation features
- Isolated from app runtime

**Cons:**

- Requires separate infrastructure (build, hosting)
- Disconnected from live app tokens/themes
- Higher setup/maintenance cost
- Devs must context-switch to external site

**Why rejected:** Overhead is too high for a single-app internal tool. We want rapid iteration with live app context.

---

### Alternative 2: Markdown Documentation with Screenshots

**Pros:**

- Lightweight (no code needed)
- Easy to version control

**Cons:**

- Screenshots go stale immediately
- Not interactive (can't toggle options, see live token values)
- No audit automation possible
- Manual extraction workflow (no tracking)

**Why rejected:** Static docs don't solve the consistency enforcement or iteration workflow problems.

---

### Alternative 3: Unit Tests for Design Patterns

**Pros:**

- Automated enforcement
- Fails builds on violations

**Cons:**

- Hard to visualize patterns (tests don't render UI)
- Requires Bunit or similar (adds test complexity)
- Doesn't help with design iteration or onboarding
- No way to see correct vs incorrect side-by-side

**Why rejected:** Tests enforce but don't teach. We need a visual reference.

---

### Alternative 4: Enforce via Roslyn Analyzers

**Pros:**

- Real-time feedback in IDE
- Blocks violations before commit

**Cons:**

- High development cost (writing analyzers is hard)
- Hard-coded in compiled tooling (harder to evolve rules)
- Doesn't solve the visual reference or iteration sandbox problems

**Why rejected:** Useful as a follow-up to Phase 8's audit tool, but not a replacement for the test bed itself. Consider for future enhancement.

---

### Alternative 5: Do Nothing (Rely on PR Review Only)

**Pros:**

- Zero implementation cost

**Cons:**

- Manual, error-prone
- Reviewers may not know the rules
- No way to prevent future violations
- Doesn't help with prototyping or extraction workflow

**Why rejected:** Violates the principle of "make the right thing easy" - if we have conventions, we should encode them.

---

## Consequences

### Positive

- **Single source of truth** - Design language doc + test bed = complete reference
- **Safe iteration** - Prototype inline without risk
- **Automated enforcement** - Audit report catches violations
- **Faster onboarding** - New devs see patterns in action
- **Extraction discipline** - Tracked workflow from experiment to shared primitive

### Negative

- **Maintenance burden** - Test bed must stay in sync with design language evolution (mitigated by making it part of PR template)
- **Initial complexity** - 40 Fibonacci points to build (but incremental value per phase)
- **Potential for staleness** - Inline experiments may linger without extraction (mitigated by TTL rule: extract or delete after 2 sprints)

### Risks

1. **Test bed becomes a junk drawer** - Unrelated experiments accumulate
   - **Mitigation:** Strict scope (design system patterns only), TTL rule for inline CSS
2. **False positives from audit tool** - Pattern scanner flags valid edge cases
   - **Mitigation:** Start with high-confidence patterns, add suppression comments
3. **Dev page ships to production** - Environment guard bypassed
   - **Mitigation:** Double-guard (route registration + runtime check), build warning in Release config

## Implementation

See [MAIN_PLAN.md](./MAIN_PLAN.md) for full details:

- **9 phases**, **40 Fibonacci points**
- Phases 1-7: Pattern display (low risk, incremental value)
- Phase 8: Audit tooling (higher complexity, can be simplified if needed)
- Phase 9: Documentation sync

First deliverable: Phase 1 (3 points) - basic page, token display, layout examples

## Related Decisions

- [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md) remains the **primary source of truth**
- Test bed is a **supporting tool**, not a replacement for the doc
- Extraction to shared primitives still requires manual code changes (test bed tracks intent, doesn't automate the refactor)

## Review Schedule

- After Phase 1: Validate approach (is this useful?)
- After Phase 8: Evaluate audit tool effectiveness (false positive rate, coverage)
- After Phase 9: Full retrospective (did this solve the pain points?)

---

**Date:** April 29, 2026  
**Approved by:** Planning session  
**Revisit after:** Phase 1 completion
