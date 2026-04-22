# Phase 12 - Base Parameters Read-Only Summary

## Status

**Phase:** 12 - NOT STARTED
**Complexity:** 3 points
**Depends on:** Phases 1-9 (delivered)

---

## Objective

Surface the concrete baseline `GenerationParameters` snapshot each job is built on top of, in both the Editor and Runs detail panes. Two presentations:

1. **Curated** (default) - plain-text, opinionated core fields in a fixed order.
2. **Show all** (toggle) - scrollable fixed-height `JsonTreeView` over the full `BaseParameters`.

No mutation, no bindings - pure readonly component.

## Scope

### Curated fields (in order)

1. Prompt (positive)
2. Prompt (negative)
3. Seed
4. Sampler / Scheduler (rendered on one line: `sampler - scheduler`)
5. Steps
6. CFG
7. Size (`width x height`)
8. LoRAs (list: `name : strength_model / strength_clip`)

Fields not resolvable for the current workflow render as `-` to keep layout stable across workflow families (Flux, Wan, LTX, Qwen, Chroma, etc.). Edge-case formatting (video-specific params, multi-sampler workflows) is out of scope for this phase and tracked as a later review.

### Placement

- `SchedulerEditorTab.razor` - collapsible panel at the top of the right pane (expanded by default).
- Runs detail view (inside `SchedulerRunsTab.razor`) - collapsible panel (collapsed by default).

## Steps

- [ ] Step 1 - Create `BlazorWebApp/Components/Scheduler/JobBaseParamsSummary.razor` [2 pts]
  - `[Parameter] public GenerationParameters Parameters { get; set; }`
  - `[Parameter] public Guid? WorkflowId { get; set; }` (for future workflow-specific formatting; unused in v1)
  - `[Parameter] public bool DefaultExpanded { get; set; } = true;`
  - Internal state `_showAll = false`.
  - Uses a small private helper to extract the curated fields from `Parameters.Fragments` / `Parameters.Assets` / `Parameters.Loras` / `Parameters.Prompt` - return `"-"` for missing values.

- [ ] Step 2 - \"Show all\" toggle swaps to `<JsonTreeView Data=\"Parameters\" />` inside a `<div style=\"max-height: 360px; overflow-y: auto;\">` [2 pts]

- [ ] Step 3 - Embed the component:
  - `SchedulerEditorTab.razor`: above the action list, inside a `MudCollapse` header labelled \"Base parameters\".
  - `SchedulerRunsTab.razor` (detail pane): same component, `DefaultExpanded=false`.

- [ ] Step 4 - Curated-field resolver robustness: iterate `FragmentKeys.Params` defensively; if a key/param combination is missing on the fragment, output `-`.

## Success Criteria

- Curated summary renders the seven fields in the agreed order with multi-line prompts wrapped cleanly.
- Toggling \"Show all\" reveals a tree inside a fixed-height scroll container; page length does not grow.
- Non-matching workflows still render all rows (placeholders for missing fields).

## Verification

- Manual: open a Job for each supported workflow family and confirm curated rows render.
- Manual: toggle \"Show all\" and scroll through a large params blob.
- No new unit tests required (pure presentation); existing Scheduler tests must remain green.

## Open Questions

- Later review: how to surface workflow-specific salient fields (e.g., Wan frame count, LTX denoise schedule) - deferred.
