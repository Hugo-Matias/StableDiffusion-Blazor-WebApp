# Design Review Findings (Audit Snapshot)

This document is the raw, unedited inventory from the audit pass that produced
[MAIN_PLAN.md](MAIN_PLAN.md). It is preserved as a supporting doc so that nothing is
lost during phase execution. Each phase should reconcile its target items against
this list and mark them resolved / deferred / declined in Phase 10.

Audit scope: app-wide. Source of truth: [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md).

---

## Severity Scale

- **CRITICAL** - breaks the contract (raw `MudTabs` in a page, root `MudPaper` on a slot child, hard-coded shell spacing).
- **MAJOR** - clear deviation (wrong variant, free-text where data-bound exists, `.send-to-*` redefined).
- **MINOR** - magic number that should be a token, missing `Dense`, inconsistent spacing.
- **GAP** - the design language doc is silent on this case; flag for a doc update with a proposed rule.

---

## CRITICAL Findings

### Raw `MudTabs` rendered inside pages / dialogs (not via `TabbedPageShell`)

| File | Line | Note |
|---|---|---|
| [Components/Gallery/GallerySettings.razor](../../../BlazorWebApp/Components/Gallery/GallerySettings.razor) | 12 | `MudTabs Elevation="3"` for Folder selection inside the gallery sidebar. Folder tabs are primary navigation, currently bypassing shell elevation/centering rules. |
| [Components/Resources/CivitaiModelInfoDialog.razor](../../../BlazorWebApp/Components/Resources/CivitaiModelInfoDialog.razor) | 56 | `MudTabs PanelClass="py-4"` - `PanelClass` explicitly forbidden by doc. |
| [Components/Resources/LoadResourceDialog.razor](../../../BlazorWebApp/Components/Resources/LoadResourceDialog.razor) | 36 | `MudTabs Elevation="0" PanelClass="mt-4"` - same. |
| [Components/Resources/CivitaiModelVersionInfoPanel.razor](../../../BlazorWebApp/Components/Resources/CivitaiModelVersionInfoPanel.razor) | 13 | Free-form `MudTabs Class="tabs" Position="Position.Start"` - vertical tabs unique to this panel; new variant never documented. |

GAP partner: doc currently scopes the `TabbedPageShell` rule to pages; nested tabs in dialogs/sidebars have no rule.

### Slot-child re-wraps itself in `MudPaper` + `pa-*`

| File | Line | Issue |
|---|---|---|
| [Components/Scheduler/SchedulerRunsTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerRunsTab.razor) | 22 | `<MudPaper Class="pa-6 d-flex flex-column align-center" Elevation="0">` rendered directly inside `ContentOnlyLayout`. Double padding. |
| [Components/Scheduler/SchedulerResultsTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerResultsTab.razor) | 65 | `<MudPaper Class="pa-3 mb-3" Elevation="1">` as the root inside its slot. |
| [Components/Prompts/ArtistBrowserPanel.razor](../../../BlazorWebApp/Components/Prompts/ArtistBrowserPanel.razor) | 93 | `<MudPaper Class="pa-3" Elevation="2">` - may be focus surface, sits next to free content; intent unclear. |
| [Components/Prompts/Wildcards/EntryManager.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/EntryManager.razor) | 8, 42, 77, 207, 290 | Multiple `MudPaper Elevation="1/2"` with `pa-*`. Doc names this file as the canonical focus-surface exception, but the current file uses 4+ such papers - the exception has become the rule. |
| [Components/Prompts/Styles/PromptStyleTable.razor](../../../BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor) | 13, 38, 219 | Stacked `MudPaper Elevation="1/2" pa-3` and a `pa-8 text-center` empty-state paper. |
| [Components/Shared/Generation/PromptsFormNew.razor](../../../BlazorWebApp/Components/Shared/Generation/PromptsFormNew.razor) | 11 | Root `<MudPaper Class="pa-4" Elevation="2">`. |
| [Components/Shared/Generation/Fragments/DoubleSamplerForm.razor](../../../BlazorWebApp/Components/Shared/Generation/Fragments/DoubleSamplerForm.razor) | 17 | Root `<MudPaper Class="pa-4 mb-4">`. |
| [Components/Shared/Generation/LLMPromptEnhancerForm.razor](../../../BlazorWebApp/Components/Shared/Generation/LLMPromptEnhancerForm.razor) | 9 | Root `<MudPaper Class="pa-4 mb-4">`. |
| [Components/Shared/Generation/LoraCard.razor](../../../BlazorWebApp/Components/Shared/Generation/LoraCard.razor) | 1 | Root `<MudPaper Class="p-1 mb-1">`. |

### `.send-to-*` rules redeclared in component-scoped CSS

- [Components/Shared/Image/ImageViewer.razor.css](../../../BlazorWebApp/Components/Shared/Image/ImageViewer.razor.css) lines 340-380 - full `.send-to-section`, `.send-to-label`, `.send-to-buttons`, `.send-to-btn`, `.send-to-btn:hover`, `.send-to-btn ::deep .mud-icon-root` definitions. Verbatim duplicate of the global stylesheet.
- [Components/Prompts/LLM/Views/TagBuilderView.razor.css](../../../BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor.css) lines 7-31 - overrides `.send-to-btn` and adds new `.send-to-group` / `.send-to-group-label` classes. **GAP partner:** "send-to-group" is a new primitive that is not documented anywhere.

### `MudContainer MaxWidth=...` driving page width

- [Components/Shared/MainLayout.razor](../../../BlazorWebApp/Components/Shared/MainLayout.razor) line 102 - `<MudContainer Class="mt-5" MaxWidth="MaxWidth.ExtraLarge">`. Architectural blocker: every shell variant downstream is constrained by this and cannot honor `--app-shell-max-width`. **GAP partner:** the doc never spells out who owns the app-shell clamp.

---

## MAJOR Findings

### `Variant.Outlined` on form controls (rule violated)

Doc: form controls use `Variant.Text`. `Variant.Outlined` is for emphasis only.

- [Components/Shared/Generation/PromptsFormNew.razor](../../../BlazorWebApp/Components/Shared/Generation/PromptsFormNew.razor) lines 19, 32
- [Components/Shared/Generation/LLMPromptEnhancerForm.razor](../../../BlazorWebApp/Components/Shared/Generation/LLMPromptEnhancerForm.razor) lines 14, 30, 40, 63, 104, 111, 118, 127
- [Components/Shared/Generation/DynamicField.razor](../../../BlazorWebApp/Components/Shared/Generation/DynamicField.razor) lines 80, 89
- [Components/Prompts/ArtistBrowserPanel.razor](../../../BlazorWebApp/Components/Prompts/ArtistBrowserPanel.razor) lines 15, 34, 52, 76, 122 (whole panel uses Outlined throughout)
- [Components/Prompts/LLM/Views/MixerView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/MixerView.razor) lines 17, 18, 47
- [Components/Prompts/LLM/Views/SceneBuilderView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/SceneBuilderView.razor) lines 20, 23, 25, 27, 29, 65
- [Components/Prompts/LLM/Views/TagBuilderView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor) lines 137, 201, 209
- [Components/Prompts/LLM/Views/InspirationView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/InspirationView.razor) line 96
- [Components/Img2Vid/GeneratedVideoTabs.razor](../../../BlazorWebApp/Components/Img2Vid/GeneratedVideoTabs.razor) line 85

The whole **LLM Views family** is uniformly Outlined and will need a coordinated lift.

### `Variant.Filled` used in form controls

- [Components/Resources/CivitaiFileButton.razor](../../../BlazorWebApp/Components/Resources/CivitaiFileButton.razor) line 10 - `MudSelect Variant="Variant.Filled"`. The `MudButton Variant="Filled"` on lines 17-18 is fine (primary action).

### `MudButton Variant="Outlined"` for simple non-primary actions (should be `.send-to-btn`)

- [Components/Shared/ContextualNavActions.razor](../../../BlazorWebApp/Components/Shared/ContextualNavActions.razor) line 28
- [Components/Scheduler/SchedulerEditorTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor) lines 40, 107, 109, 153
- [Components/Resources/CivitaiModelsPanel.razor](../../../BlazorWebApp/Components/Resources/CivitaiModelsPanel.razor) line 172 - "Load More" outlined button
- [Components/Prompts/Styles/PromptStyleCompactCard.razor](../../../BlazorWebApp/Components/Prompts/Styles/PromptStyleCompactCard.razor) line 127
- [Components/Prompts/LLM/PromptComparisonPanel.razor](../../../BlazorWebApp/Components/Prompts/LLM/PromptComparisonPanel.razor) lines 102, 108
- [Components/Prompts/LLM/SystemPromptEditor.razor](../../../BlazorWebApp/Components/Prompts/LLM/SystemPromptEditor.razor) lines 171, 180
- [Components/Prompts/LLM/Views/SceneBuilderView.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/SceneBuilderView.razor) lines 37, 46
- [Components/Prompts/LLM/Views/ForgeSaveDialog.razor](../../../BlazorWebApp/Components/Prompts/LLM/Views/ForgeSaveDialog.razor) line 27
- [Components/ImageEditor/ImageEditorModal.razor](../../../BlazorWebApp/Components/ImageEditor/ImageEditorModal.razor) lines 428, 434

GAP partner: `MudButtonGroup Variant="Variant.Outlined"` (heavily used in `ImageEditorModal` lines 49, 80, 119, 144, 221, 261, 306) has no governing rule. Toolbar-style segmented controls are a legitimate need.

### Pages bypass the shell entirely

- [Pages/Index.razor](../../../BlazorWebApp/Pages/Index.razor) lines 22-29, 51, 61, 65 - no `TabbedPageShell` / `ContentOnlyLayout`; renders directly into `MudContainer`, plus inline `Style="height: 50vh"`, `Style="text-align:center;"`, `Style="margin-bottom: 2rem; margin-top: 2rem;"`, `Style="opacity: 0.4; text-align: center; margin-top: 10rem;"`.
- [Pages/Generate.razor](../../../BlazorWebApp/Pages/Generate.razor) lines 24+ - full-bleed with `MudGrid Spacing="8"` driving the two-column split, ignoring `TwoColumnLayout` and `--app-gutter-inner`. Doc names Generate as the *baseline* for spacing, but the page itself doesn't use any of the shell primitives.

GAP partner: doc has no rule for non-tabbed root pages.

---

## MINOR Findings

### Inline `style="..."` for static styling

- [Pages/Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor) lines 25, 71, 83, 85, 92 - `flex-grow`, `min-width`, `opacity` hard-coded inline.
- [Pages/Index.razor](../../../BlazorWebApp/Pages/Index.razor) lines 24, 51, 61, 65 - large block of inline styles.
- [Components/Shared/ProgressContainer.razor](../../../BlazorWebApp/Components/Shared/ProgressContainer.razor) line 4 - `position: absolute; top: 0; gap: 0.2rem !important; width: 100%; margin: 0 -24px;`. The `-24px` matches `--app-surface-padding`; should reference the token.
- [Components/Shared/Toolbar/ToolbarResourceFilters.razor](../../../BlazorWebApp/Components/Shared/Toolbar/ToolbarResourceFilters.razor) lines 18, 30 - `opacity:0.5`, `text-decoration:line-through` repeated; promote to `.is-muted` / `.is-blacklisted`.
- [Components/Prompts/Wildcards/WildcardImportDialog.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardImportDialog.razor) lines 24, 62 - `border: 1px solid var(--mud-palette-lines-default)`. Same outlined-paper recipe in [WildcardExportDialog.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardExportDialog.razor) line 40 - candidate for `.app-outlined-surface`.
- [Components/Prompts/Wildcards/WildcardImportDialog.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardImportDialog.razor) lines 98, 101, 110 - `background-color: transparent` on `MudList` / `MudListItem` to defeat default styling; should be a class.
- [Components/Prompts/Wildcards/WildcardsTab.razor](../../../BlazorWebApp/Components/Prompts/Wildcards/WildcardsTab.razor) lines 36, 62, 75 - `Style="height: 100%;"` for empty-state stacks.
- [Components/Prompts/PromptDialog.razor](../../../BlazorWebApp/Components/Prompts/PromptDialog.razor) line 78 - `Style="background-color: rgba(0,0,0,0.02);"`. Repeated in [PromptComparisonPanel.razor](../../../BlazorWebApp/Components/Prompts/LLM/PromptComparisonPanel.razor) lines 14, 49 and [SystemPromptEditor.razor](../../../BlazorWebApp/Components/Prompts/LLM/SystemPromptEditor.razor) line 200. **Token candidate:** `--app-surface-subtle-bg`.
- [Components/Shared/Generation/DynamicField.razor](../../../BlazorWebApp/Components/Shared/Generation/DynamicField.razor) line 149 - `border: 1px solid rgba(255,255,255,0.1);` raw hex (theme-incompatible).
- [Components/Shared/Generation/DynamicFragmentForm.razor](../../../BlazorWebApp/Components/Shared/Generation/DynamicFragmentForm.razor) line 34 - `background: var(--mud-palette-background-grey)` on a slot child.
- [Components/Scheduler/JobActionsSummary.razor](../../../BlazorWebApp/Components/Scheduler/JobActionsSummary.razor) lines 43, 62 and [SchedulerEditorTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor) lines 222, 254 - `Style="gap: 8px;"` on a `MudPaper` flex row.
- [Components/Shared/Image/ImageInput.razor](../../../BlazorWebApp/Components/Shared/Image/ImageInput.razor) line 5 - `Style="@Style"` passed-through, fine, but the parent callers should be audited.

### Repeated "outlined paper" recipe (`MudPaper Elevation="0" Outlined Class="pa-3"`)

- [Components/Scheduler/JobBaseParamsSummary.razor](../../../BlazorWebApp/Components/Scheduler/JobBaseParamsSummary.razor) line 11
- [Components/Scheduler/JobActionsSummary.razor](../../../BlazorWebApp/Components/Scheduler/JobActionsSummary.razor) line 12
- [Components/Scheduler/SchedulerEditorTab.razor](../../../BlazorWebApp/Components/Scheduler/SchedulerEditorTab.razor) line 161
- [Components/Prompts/LLM/PromptComparisonPanel.razor](../../../BlazorWebApp/Components/Prompts/LLM/PromptComparisonPanel.razor) line 6
- [Components/Prompts/LLM/SystemPromptEditor.razor](../../../BlazorWebApp/Components/Prompts/LLM/SystemPromptEditor.razor) line 79

**Primitive proposal:** `<AppOutlinedSurface>` or `.app-outlined-surface` CSS class. Density varies (`pa-2` / `pa-3` / `pa-4`).

### Other

- [BlazorWebApp/wwwroot/css/workshop-thread.css](../../../BlazorWebApp/wwwroot/css/workshop-thread.css) - empty file; remove or fill.
- [Components/Resources/DanbooruSearchesDrawer.razor](../../../BlazorWebApp/Components/Resources/DanbooruSearchesDrawer.razor) line 11 - `MudPaper Class="pa-2 ma-4" Elevation="4"` - high elevation drawer item; verify intentional.

---

## GAPs in the Design Language Doc

These came up repeatedly and have no governing rule:

1. **Nested `MudTabs` inside dialogs and sidebars.** Need an explicit rule (likely: same elevation/centering as `TabbedPageShell`, but no shell wrapper).
2. **Vertical tabs.** Used by `CivitaiModelVersionInfoPanel` (`Position="Position.Start"`). Either a fourth allowed variant or a banned one.
3. **`MudButtonGroup` styling.** Heavily used in `ImageEditorModal`. Either a documented "segmented control" component or scoped to ImageEditor with justification.
4. **App-shell content clamp.** `MainLayout` currently uses `MudContainer MaxWidth.ExtraLarge`. Doc says use `--app-shell-max-width`. Resolve: `MainLayout` honors the token via a CSS shell class.
5. **Index/Generate as non-tabbed pages.** No documented contract. Should they wrap in `ContentOnlyLayout`?
6. **Empty-state component.** `Index.razor`, `Danbooru.razor`, `WildcardsTab.razor` reinvent empty states with inline styles. Worth a shared `<EmptyState>` component.
7. **Subtle-surface background.** `rgba(0,0,0,0.02)` recipe should be a token (`--app-surface-subtle-bg`) and theme-aware.
8. **Disabled / muted chips and buttons.** `opacity:0.5` and `text-decoration:line-through` repeat enough to deserve a class.
9. **`.send-to-group` in TagBuilderView.** Either promote to `send-to.css` or rename. Currently a stealth deviation.

---

## Token / Shared-Primitive Proposals

| Proposal | Rationale | Where it lifts from |
|---|---|---|
| `--app-surface-subtle-bg` | Stop hard-coding `rgba(0,0,0,0.02)` | PromptDialog, PromptComparisonPanel, SystemPromptEditor |
| Reuse `--mud-palette-lines-default` | Replace inline `border: 1px solid ...` repetitions | Wildcard import/export dialogs, DynamicField |
| `.app-outlined-surface` class (or `<AppOutlinedSurface>`) | Replace `MudPaper Elevation="0" Outlined Class="pa-3"` recipe | Scheduler summaries + LLM panels |
| `<EmptyState>` component | Replace inline-styled "no results" + "create project" blocks | Index, Danbooru, WildcardsTab |
| `.is-muted` class | Replace `opacity:0.5` + `line-through` inline | ToolbarResourceFilters |
| Document or rename `.send-to-group` | Ad-hoc primitive in TagBuilderView | TagBuilderView.razor.css |

---

## Summary Counts (Approximate)

| Category | Count |
|---|---|
| CRITICAL | ~15 (4 raw `MudTabs`, ~9 slot-child paper wrappers, 1 duplicated `.send-to-*`, 1 `MudContainer` clamp) |
| MAJOR | ~50+ (LLM Views Outlined, Outlined buttons app-wide, Index/Generate not in shell) |
| MINOR | ~30 (inline styles, magic colors/spacings, missing tokens) |
| GAP | 9 (rules the doc is silent on) |

---

## Reconciliation Tracker (Filled During Phase 10)

| Finding | Status | Notes |
|---|---|---|
| | | |

(Phase 10 will populate this table by walking every CRITICAL / MAJOR / MINOR / GAP above and marking it Resolved / Deferred / Declined with rationale.)
