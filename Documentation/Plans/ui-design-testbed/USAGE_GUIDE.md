# UI Design Test Bed - Usage Guide

## Quick Start

The UI Design Test Bed (`/dev/design-testbed`) is a **development-only page** where you can:

1. **Reference correct patterns** before implementing new UI
2. **Prototype design changes** without affecting production pages
3. **Audit existing pages** for design language violations
4. **Extract shared primitives** from validated experiments

---

## Workflow: Adding a New UI Feature

### 1. Consult the Test Bed First

Before implementing a new page or component:

1. Navigate to `/dev/design-testbed` (dev environment only)
2. Find the relevant tab:
   - **Form Controls** - for inputs, selects, autocomplete
   - **Buttons & Actions** - for button patterns
   - **Cards & Surfaces** - for layout slots and elevation
   - **Dialogs & Modals** - for modal patterns
3. Review the "Correct" example and copy the markup

### 2. Reference the Design Doc

Each test bed section links to its source rule in `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`. When the test bed example isn't sufficient, read the full rule.

### 3. Implement Using Shared Primitives

- Use tokens from `site.css` (`:root` vars)
- Use layout components from `Components/Layouts/`
- Use shared styles like `.send-to-btn` from `wwwroot/css/`
- Do NOT inline styles or duplicate shared CSS

### 4. Validate

- Compare your implementation to the test bed example
- Run the Audit Report (Phase 8) to catch violations
- Ensure no hard-coded spacing, wrong variants, or anti-patterns

---

## Workflow: Prototyping a Design Change

### 1. Experiment in the Test Bed

- Add a new section to the relevant tab (e.g., "Buttons & Actions")
- Use **inline styles** or **scoped CSS** (`DesignTestbed.razor.css`) freely
- Iterate rapidly without affecting production pages

### 2. Document the Experiment

- Add an entry to the "Extraction Tracker" (Audit Report tab)
- Include:
  - Pattern name
  - Use case (why is this needed?)
  - Inline CSS location (file + line)
  - Decision status (Prototype / Approved / Extracted)

### 3. Validate with Stakeholders

- Share `/dev/design-testbed` link (or screenshot)
- Discuss whether the pattern should be:
  - **Extracted** to shared primitive (if reusable)
  - **Kept scoped** (if one-off, document as exception)
  - **Discarded** (if doesn't meet needs)

### 4. Extract to Shared Primitive

When approved for extraction:

1. **If it's a spacing/sizing value:**
   - Add a CSS variable to `site.css` `:root`
   - Update the "Tokens & Variables" tab to display it
   - Replace inline values with `var(--your-token)`

2. **If it's a style class:**
   - Create or update a stylesheet under `wwwroot/css/`
   - Link it globally from `_Layout.cshtml`
   - Update the relevant test bed tab to use the class

3. **If it's a component:**
   - Create a new component under `Components/Shared/` or relevant feature folder
   - Add props for opt-out behavior (follow `AssetViewer` pattern)
   - Update the test bed to use the component

4. **Update the Design Language Doc:**
   - Add or update the rule in `04-UI-DESIGN-LANGUAGE.md`
   - Cross-reference the test bed tab
   - Document any anti-patterns or exceptions

5. **Remove from Extraction Tracker:**
   - Mark as "Extracted" with link to shared primitive
   - Remove inline CSS from test bed (now uses the extracted primitive)

---

## Workflow: Auditing Existing Pages

### 1. Run the Audit Report

- Navigate to the "Audit Report" tab (Phase 8)
- Click "Scan All Pages"
- Review violations grouped by file

### 2. Prioritize Violations

- **High priority:** Hard-coded spacing on shells, wrong form variants
- **Medium priority:** Missing token usage, direct `MudTabs` rendering
- **Low priority:** Non-critical deviations (document as exceptions if intentional)

### 3. Fix or Document

For each violation:

1. **If it's a clear violation:**
   - Create a refactoring task
   - Reference the test bed section that shows the correct pattern
   - Apply the fix (use shared primitives)

2. **If it's an intentional edge case:**
   - Document the rationale in `04-UI-DESIGN-LANGUAGE.md` (Deviations section)
   - Add a suppression comment in the code (if audit tool supports it)
   - Update the Audit Report to note "Known exception"

### 4. Re-run Audit

- After fixes, re-scan to verify violations are resolved
- Track progress (violations should decrease over time)

---

## Maintenance Rules

### Keep the Test Bed in Sync

When the design language evolves:

1. Update `04-UI-DESIGN-LANGUAGE.md` first (source of truth)
2. Update the corresponding test bed tab to match
3. If a new rule is added, create a new test bed example

### Inline CSS Time-to-Live (TTL)

Experiments in the test bed must not linger indefinitely:

- **Prototype status:** Can stay inline for iteration
- **Approved status:** Must extract within 2 sprints
- **Stale status (> 2 sprints):** Extract or delete

Update the Extraction Tracker regularly to avoid CSS debt.

### Test Bed is Not a Junk Drawer

Do NOT use the test bed for:

- Unrelated experiments (only design system patterns)
- Production code (it's dev-only for a reason)
- Page replicas (test isolated patterns, not full page clones)
- Generic MudBlazor docs (focus on BlazorWebApp-specific rules)

---

## FAQ

### Q: Can I ship test bed inline CSS to production?

**A:** No. The test bed is for **iteration only**. Production pages must use **shared primitives** (tokens, classes, components).

### Q: What if the test bed example doesn't fit my use case?

**A:**

1. Check if it's a valid deviation (read the full design doc rule)
2. If so, prototype in the test bed, discuss with the team
3. If approved, extract and document the new pattern

### Q: How do I add a new pattern to the test bed?

**A:**

1. Identify the correct tab (or propose a new one)
2. Add a section with "Correct" + "Incorrect" examples
3. Link to the relevant design doc rule
4. Add to the Extraction Tracker if using inline CSS

### Q: Can the test bed be accessed in production?

**A:** Not by default. It's dev-only to prevent accidental exposure. If needed for design review, add a `?dev=true` query param guard (Phase 1 decision).

### Q: What if the audit tool flags a false positive?

**A:**

1. Review the violation (is it really correct?)
2. If it's an intentional edge case, document it in the design doc
3. Add a suppression comment (when audit tool supports it)
4. File an issue to improve the audit rule

---

## Related Documentation

- [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md) - Primary design language rules
- [MAIN_PLAN.md](./MAIN_PLAN.md) - Implementation plan for the test bed
- [site.css](../../../BlazorWebApp/wwwroot/site.css) - Global spacing tokens
- [send-to.css](../../../BlazorWebApp/wwwroot/css/send-to.css) - Simple action button styles

---

## Revision History

- **Planning session** - Initial usage guide created
