# Design Language Tightening - Implementation Plan

## Status
**Current Phase:** Planning

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits

### Progress Tracking Symbols
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)
- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic (split)

### Key Rules
- Each step is a commitable checkpoint.
- No time/date references - complexity points only.
- Detours are acceptable after discussion - append to main plan.
- Phase documents must contain enough context to resume in a new session.
- Minimal, focused changes - avoid over-engineering.
- User permission required before moving to next phase.
- **Every accepted design decision is recorded in `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` in the same change set as the code change.** This is the contract; the doc must never lag the code.

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase.
- Update phase document after each step completion.
- Document all issues, blockers, and resolutions.
- Track commit checkpoints throughout execution.

---

## Problem Statement

A read-only design audit of `BlazorWebApp` against [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md) surfaced ~15 CRITICAL contract breaks, ~50 MAJOR deviations, ~30 MINOR magic-number / inline-style smells, and 9 GAPs in the design language doc itself. The full inventory is in [FINDINGS.md](FINDINGS.md) - it must not be lost across phases. The largest single lever is lifting a small set of shared primitives (`<AppOutlinedSurface>`, `<EmptyState>`, a subtle-bg token) which collapses many MAJORs and MINORs into one clean change set.

The goal of this plan is to bring the codebase back into alignment with the design language **and** to fill the documented GAPs (nested tabs, vertical tabs, app-shell clamp ownership, MudButtonGroup stance, non-tabbed pages, empty states, subtle-bg token, muted/disabled states, send-to-group primitive) so future work has clear rules to follow.

---

## Proposed Solution

Sequence the work so that **doc updates and shared primitives land first**, then individual offenders are migrated onto the new primitives. This avoids fixing the same recipe by hand in 6+ files.

### Key Decisions

| Decision | Rationale |
|---|---|
| Doc updates ship in the same change set as the code fix | Prevents silent drift; agent and humans rely on the doc as contract. |
| Lift shared primitives before sweeping consumers | One refactor wave instead of N hand fixes; consistent surface guaranteed. |
| Audit-only output is preserved as `FINDINGS.md` | Source of truth for completeness; phases reference it; nothing gets dropped. |
| GAP resolutions are explicit phase steps, not side-effects | Forces a discussion + doc update for each rule the doc is currently silent on. |
| `MainLayout`'s `MudContainer MaxWidth=...` is treated as a CRITICAL architectural fix, done first | It blocks `--app-shell-max-width` from ever taking effect downstream; every other layout fix is moot until this is done. |
| `Index.razor` and `Generate.razor` are exempt-or-migrate decisions, not silent | Currently undocumented exceptions; must be either migrated to `ContentOnlyLayout` or formally exempted in the doc. |

### Conventions

- All UI work in this plan obeys the rules in [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md), [design-language.instructions.md](../../../BlazorWebApp/.github/instructions/design-language.instructions.md), and [tokens.instructions.md](../../../BlazorWebApp/.github/instructions/tokens.instructions.md).
- New shared CSS classes live under `BlazorWebApp/wwwroot/css/`.
- New shared components live under `BlazorWebApp/Components/Shared/`.
- New tokens are declared in `BlazorWebApp/wwwroot/site.css` `:root` and added to the token table in the design language doc.
- Page-local CSS variables follow the `--<page>-<concern>` pattern.
- No inline `style="..."` for static styling; inline style is reserved for values bound from C# state.

---

## Implementation Phases

### Phase 1: Doc gaps + app-shell clamp
**Objective:** Close the rule-level GAPs in the design language doc and fix the architectural blocker that prevents `--app-shell-max-width` from being honored. No consumer code is touched yet beyond `MainLayout`.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Resolve and document the 9 GAPs (nested tabs, vertical tabs, app-shell clamp, MudButtonGroup, non-tabbed pages, empty states, subtle-bg token, muted/disabled states, send-to-group). 5 points.
- [ ] Step 2 - Replace `MudContainer MaxWidth="MaxWidth.ExtraLarge"` in [MainLayout.razor](../../../BlazorWebApp/Components/Shared/MainLayout.razor) with a CSS shell class driven by `--app-shell-max-width`. 2 points.

#### Success Criteria
- Each GAP listed in `FINDINGS.md` has a paragraph or rule entry in `04-UI-DESIGN-LANGUAGE.md`.
- `MainLayout` no longer drives content width via MudBlazor; the shell clamp comes from the CSS token.
- Visual smoke test on every page confirms no regression in shell width.

---

### Phase 2: Shared primitives + tokens
**Objective:** Add the small set of primitives that the rest of the plan reuses. Each primitive lands with its doc update.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Add `--app-surface-subtle-bg` token to `site.css` (theme-aware) and document it. 1 point.
- [ ] Step 2 - Add `.app-outlined-surface` shared class (or `<AppOutlinedSurface>` component) replacing the `MudPaper Elevation="0" Outlined Class="pa-3"` recipe. Document. 3 points.
- [ ] Step 3 - Add `<EmptyState>` shared component for "no results" / "create project to get started" surfaces. Document. 3 points.
- [ ] Step 4 - Add `.is-muted` utility class (replaces `Style="opacity:0.5"` and `text-decoration:line-through` recipes). Document. 1 point.
- [ ] Step 5 - Decide on `.send-to-group` (promote to `send-to.css` or rename out of the family). Document. 2 points.

#### Success Criteria
- All new primitives exist, are referenced from the design language doc, and are linked from the relevant CSS instruction files.
- No consumer migrations yet; primitives validated in isolation.

---

### Phase 3: CRITICAL slot-child wrappers + duplicated send-to block
**Objective:** Eliminate every contract-breaking root `MudPaper` / `pa-*` / `Elevation` on a layout-slot child and remove the duplicated `.send-to-*` definitions.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Delete the duplicated `.send-to-*` block in [ImageViewer.razor.css](../../../BlazorWebApp/Components/Shared/Image/ImageViewer.razor.css). Verify `send-to.css` still applies. 1 point.
- [ ] Step 2 - Make Scheduler tabs render flush: [SchedulerRunsTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerRunsTab.razor), [SchedulerResultsTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerResultsTab.razor). 3 points.
- [ ] Step 3 - Make generation forms flush: [PromptsFormNew.razor](../../../BlazorWebApp/Components/Shared/Generation/PromptsFormNew.razor), [DoubleSamplerForm.razor](../../../BlazorWebApp/Components/Shared/Generation/Fragments/DoubleSamplerForm.razor), [LLMPromptEnhancerForm.razor](../../../BlazorWebApp/Components/Shared/Generation/LLMPromptEnhancerForm.razor), [LoraCard.razor](../../../BlazorWebApp/Components/Shared/Generation/LoraCard.razor). 3 points.
- [ ] Step 4 - Audit [EntryManager.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/EntryManager.razor) papers - keep only the deliberate focus surface, document it as the canonical exception. 2 points.
- [ ] Step 5 - Audit [PromptStyleTable.razor](../../../BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor), [ArtistBrowserPanel.razor](../../../BlazorWebApp/Components/Prompts/ArtistBrowserPanel.razor) - flush root, focus surfaces only where intentional. 2 points.

#### Success Criteria
- Every CRITICAL slot-child wrapper from `FINDINGS.md` is either flush or documented as a focus-surface exception.
- `.send-to-*` rules exist in exactly one CSS file.

---

### Phase 4: Nested-tabs sweep
**Objective:** Bring nested `MudTabs` (dialogs, sidebars) into compliance with the rule established in Phase 1.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Strip `PanelClass` and align elevation/centering in [CivitaiModelInfoDialog.razor](../../../BlazorWebApp/Components/Resources/CivitaiModelInfoDialog.razor) and [LoadResourceDialog.razor](../../../BlazorWebApp/Components/Resources/LoadResourceDialog.razor). 2 points.
- [ ] Step 2 - Decide vertical-tabs stance for [CivitaiModelVersionInfoPanel.razor](../../../BlazorWebApp/Components/Resources/CivitaiModelVersionInfoPanel.razor): allow as documented exception, or migrate. 3 points.
- [ ] Step 3 - Decide gallery folder tabs in [GallerySettings.razor](../../../BlazorWebApp/Components/Gallery/GallerySettings.razor): make them honor nested-tabs rule, or replace with a different control. 3 points.

#### Success Criteria
- Every `MudTabs` instance in the codebase either lives inside `TabbedPageShell` or matches the new nested-tabs rule.
- No `PanelClass` usages remain.

---

### Phase 5: AppOutlinedSurface migration
**Objective:** Replace the `MudPaper Elevation="0" Outlined Class="pa-*"` recipe with the new shared primitive across all consumers.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Migrate Scheduler summaries: [JobBaseParamsSummary.razor](../../../BlazorWebApp/Components/Scheduler/JobBaseParamsSummary.razor), [JobActionsSummary.razor](../../../BlazorWebApp/Components/Scheduler/JobActionsSummary.razor), nested papers in [SchedulerEditorTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor). 3 points.
- [ ] Step 2 - Migrate LLM panels: [PromptComparisonPanel.razor](../../../BlazorWebApp/Components/Prompts/LLM/PromptComparisonPanel.razor), [SystemPromptEditor.razor](../../../BlazorWebApp/Components/Prompts/LLM/SystemPromptEditor.razor). 2 points.
- [ ] Step 3 - Replace inline `Style="gap: 8px;"` on flex-row papers with a CSS class. 1 point.

#### Success Criteria
- The outlined-paper recipe exists in zero consumer files.
- Spacing inside outlined surfaces is consistent (one canonical density).

---

### Phase 6: Form variant sweep (Outlined -> Text)
**Objective:** Bring form controls back to `Variant.Text`. The LLM Views family is the largest cluster.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - LLM Views: [MixerView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/MixerView.razor), [SceneBuilderView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/SceneBuilderView.razor), [TagBuilderView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor), [InspirationView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/InspirationView.razor). 3 points.
- [ ] Step 2 - Generation forms: [PromptsFormNew.razor](../../../BlazorWebApp/Components/Shared/Generation/PromptsFormNew.razor), [LLMPromptEnhancerForm.razor](../../../BlazorWebApp/Components/Shared/Generation/LLMPromptEnhancerForm.razor), [DynamicField.razor](../../../BlazorWebApp/Components/Shared/Generation/DynamicField.razor). 3 points.
- [ ] Step 3 - Artist + misc: [ArtistBrowserPanel.razor](../../../BlazorWebApp/Components/Prompts/ArtistBrowserPanel.razor), [Img2Vid/GeneratedVideoTabs.razor](../../../BlazorWebApp/Components/Img2Vid/GeneratedVideoTabs.razor), [CivitaiFileButton.razor](../../../BlazorWebApp/Components/Resources/CivitaiFileButton.razor) (also drop `Variant.Filled` from its `MudSelect`). 2 points.

#### Success Criteria
- Form `MudTextField` / `MudSelect` / `MudNumericField` / `MudAutocomplete` use `Variant.Text` unless an emphasis exception is documented at the call site.
- Density and look match the Generate page baseline.

---

### Phase 7: Send-to-btn sweep (Outlined buttons -> .send-to-btn)
**Objective:** Replace `MudButton Variant="Outlined"` flat-row actions with `.send-to-btn`.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Scheduler editor row buttons: [SchedulerEditorTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor) lines 40, 107, 109, 153. 2 points.
- [ ] Step 2 - LLM panels and dialogs: [PromptComparisonPanel.razor](../../../BlazorWebApp/Components/Prompts/LLM/PromptComparisonPanel.razor), [SystemPromptEditor.razor](../../../BlazorWebApp/Components/Prompts/LLM/SystemPromptEditor.razor), [SceneBuilderView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/SceneBuilderView.razor), [ForgeSaveDialog.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/ForgeSaveDialog.razor). 2 points.
- [ ] Step 3 - Misc: [ContextualNavActions.razor](../../../BlazorWebApp/Components/Shared/ContextualNavActions.razor), [CivitaiModelsPanel.razor](../../../BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor) "Load More", [PromptStyleCompactCard.razor](../../../BlazorWebApp/Components/Prompts/Styles/PromptStyleCompactCard.razor), [ImageEditorModal.razor](../../../BlazorWebApp/Components/ImageEditor/ImageEditorModal.razor) lines 428, 434. 2 points.

#### Success Criteria
- Flat rows use `.send-to-btn`; `MudButton Variant="Outlined"` only appears where an emphasis exception is documented.
- `MudButtonGroup Variant="Variant.Outlined"` keeps the stance decided in Phase 1.

---

### Phase 8: EmptyState + inline-style cleanup
**Objective:** Migrate empty-state surfaces and remove the long tail of static inline styles flagged in `FINDINGS.md`.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Migrate empty states: [Index.razor](../../../BlazorWebApp/Pages/Index.razor) "create project" + "Empty Project" blocks; [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor) "No results"; [WildcardsTab.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardsTab.razor) empty stacks. 3 points.
- [ ] Step 2 - Inline-style cleanup: [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor) flex/min-width inline styles, [ProgressContainer.razor](../../../BlazorWebApp/Components/Shared/ProgressContainer.razor) hard-coded margins, [ToolbarResourceFilters.razor](../../../BlazorWebApp/Components/Shared/Toolbar/ToolbarResourceFilters.razor) muted styles, [DynamicField.razor](../../../BlazorWebApp/Components/Shared/Generation/DynamicField.razor) raw rgba border, [DynamicFragmentForm.razor](../../../BlazorWebApp/Components/Shared/Generation/DynamicFragmentForm.razor) background, subtle-bg `rgba(0,0,0,0.02)` recipe in [PromptDialog.razor](../../../BlazorWebApp/Components/Prompts/PromptDialog.razor) and the LLM panels. 2 points.
- [ ] Step 3 - Replace inline `Style="border: 1px solid var(--mud-palette-lines-default)"` in [WildcardImportDialog.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardImportDialog.razor) and [WildcardExportDialog.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardExportDialog.razor) with the shared outlined surface. 1 point.

#### Success Criteria
- Static inline `style="..."` is gone from the files in `FINDINGS.md`. Remaining inline styles are dynamic (bound from C#) and justified.
- Empty states share one component.

---

### Phase 9: Index + Generate decisions
**Objective:** Resolve the two non-tabbed pages that currently bypass the shell.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Decide on [Index.razor](../../../BlazorWebApp/Pages/Index.razor): migrate to `ContentOnlyLayout`, or formally exempt in the doc. 3 points.
- [ ] Step 2 - Decide on [Generate.razor](../../../BlazorWebApp/Pages/Generate.razor): align with shell + `--app-gutter-inner`, or formally exempt. 3 points.

#### Success Criteria
- Each decision is recorded in `04-UI-DESIGN-LANGUAGE.md` with rationale.
- The two pages either honor the shell tokens or are explicitly listed as exempt.

---

### Phase 10: Final pass + doc consolidation
**Objective:** Final sweep, ensure nothing in `FINDINGS.md` is unaddressed, consolidate the design language doc.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Run `/design-review all` (the new prompt) to confirm the audit is clean. 1 point.
- [ ] Step 2 - Reconcile `FINDINGS.md` against final state - mark each entry resolved / deferred / declined with rationale. 1 point.
- [ ] Step 3 - Final pass on `04-UI-DESIGN-LANGUAGE.md` - reorder, add cross-links, ensure token table and primitives table are exhaustive. 1 point.

#### Success Criteria
- `/design-review all` returns no CRITICAL or MAJOR.
- `FINDINGS.md` is fully reconciled.
- Design language doc is the single source for every rule introduced by this plan.

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|---|---|---|
| `MainLayout` shell-clamp change shifts every page width | Visual smoke test on each tabbed page right after Phase 1; tune the token, not the consumer | 2 |
| `<AppOutlinedSurface>` ends up too rigid for one consumer | Allow override slot for padding density; document the override as the only approved knob | 3 |
| Form variant sweep changes vertical rhythm on dense pages | Take before/after screenshots per file; if rhythm regresses, tune `Dense` rather than reverting variant | 5 |
| Vertical-tabs decision in Phase 4 affects external links into model info | Coordinate the decision with the Resources team-of-one (the user) before code changes | 3 |
| Generate page is the doc baseline but doesn't use the shell | Phase 9 may decide to exempt it - this must be explicit, not silent | 5 |
| `EntryManager` already cited as the focus-surface exemplar - tightening it could weaken the example in the doc | Before changing it, lock the new exemplar (or keep `EntryManager` as the canonical one) | 3 |
| LLM Views are heavily Outlined as a stylistic choice (not by accident) | Confirm with the user before sweeping; if the look is desired, document an LLM-Views exception zone | 5 |

---

## Changelog

| Phase | Changes |
|---|---|
| Planning | Initial plan created from design audit. Findings preserved in `FINDINGS.md`. |

---

## References

- Findings inventory: [FINDINGS.md](FINDINGS.md)
- Design language: [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md)
- Tokens: [site.css](../../../BlazorWebApp/wwwroot/site.css)
- Layouts: [Components/Layouts/](../../../BlazorWebApp/Components/Layouts/)
- Send-to canonical style: [send-to.css](../../../BlazorWebApp/wwwroot/css/send-to.css)
- Agent: [.github/agents/ui-design.agent.md](../../../BlazorWebApp/.github/agents/ui-design.agent.md)
- Audit prompt: [.github/prompts/design-review.prompt.md](../../../BlazorWebApp/.github/prompts/design-review.prompt.md)
- Instructions: [design-language.instructions.md](../../../BlazorWebApp/.github/instructions/design-language.instructions.md), [tokens.instructions.md](../../../BlazorWebApp/.github/instructions/tokens.instructions.md)
