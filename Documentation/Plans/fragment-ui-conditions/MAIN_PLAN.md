# Fragment UI Conditions - Implementation Plan

## Status

**Current Phase:** Planning

---

## Implementation Guidelines

**Primary planning contract:** `Documentation/Plans/IMPLEMENTATION_GUIDE.md`

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to the next step until testing is complete.
   - User must explicitly approve before updating phase documents.
   - Build runs only after user requests or after completing all file edits for a step.

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)

- **1**: Trivial
- **2**: Simple
- **3**: Moderate
- **5**: Medium
- **8**: Complex
- **13**: Very complex
- **21+**: Epic, should be split

### Key Rules

- Keep the Generate page dynamic. It must not know template-specific source IDs such as `base_image`.
- Workflow and fragment metadata must declare conditional UI behavior.
- Designed fragment components should consume generic resolved UI state, not workflow-specific rules.
- Preserve existing fragment behavior by default. No current workflow should change unless it opts into a condition.
- Follow `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` for form density, variants, and flush component rendering.

---

## Problem Statement

Some workflows have fragment controls that are valid only in certain source states. The immediate example is `Flux2 Klein Grid Ref`: when the optional base image is supplied, the workflow sizes the latent from `GetImageSize` on the base image, so the latent width and height controls no longer affect generation. When the base image is absent, those same controls are meaningful because the workflow uses faux image-to-image generation with explicit latent width and height.

The current Generate page renders sources and fragment forms independently. A quick fix could hardcode `base_image` behavior into `Generate.razor`, but that would violate the dynamic Generate-page principle. The page should render workflow-declared metadata, not encode one template's branching logic.

---

## Proposed Solution

Add a generic, workflow-declared fragment UI condition layer. Workflows can attach conditions to fragment parameters, such as disabling a parameter when a specific source has data. The Generate page will evaluate these rules generically against the current `GenerationParameters` and pass resolved UI state into fragment renderers. Fragment components like `LatentForm` and `ResolutionPanel` can then disable controls without knowing which workflow or source triggered the state.

### Example Target Shape

```csharp
new FragmentParameter
{
    Name = "width",
    Label = "Width",
    Type = ParameterType.Slider,
    DefaultValue = 1024,
    UiState = new FragmentParameterUiState
    {
        DisabledWhen =
        [
            new FragmentUiCondition
            {
                SourceId = "base_image",
                Kind = FragmentUiConditionKind.SourceHasData,
                Reason = "Base image controls output size"
            }
        ]
    }
}
```

The exact names may change during implementation, but the key concept should remain: condition definitions live in workflow/fragment metadata and are evaluated by shared UI infrastructure.

---

## Key Decisions

| Decision                                                                 | Rationale                                                                                       |
| ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------- |
| Do not hardcode source IDs in `Generate.razor`                           | Keeps Generate dynamic and avoids template-specific coupling                                    |
| Attach conditions to fragment parameter metadata                         | The workflow owns the rule for when a parameter is meaningful                                   |
| Resolve conditions outside individual forms                              | Forms should receive generic disabled/read-only state, not inspect workflow sources directly    |
| Start with source-presence conditions only                               | Covers the grid-reference need while keeping the first version small                            |
| Treat dynamic field rendering as a future beneficiary, not a requirement | Current forms are designed components, so the first implementation should support them directly |
| Leave existing workflows unchanged by default                            | Backward compatibility and low rollout risk                                                     |

---

## Current Architecture Notes

Relevant current flow:

1. `WorkflowMetadata` exposes workflow assets and sources.
2. `IWorkflowBuilder.GetFragments()` exposes `FragmentMetadata` and `FragmentParameter` definitions.
3. `GenerationParameterService.BuildSchemaFromMetadata(...)` maps `FragmentParameter` values into `FragmentSchema.Parameters` as `ParameterConstraints`.
4. `Generate.razor` renders sources through `SourcesPanel` and fragment forms through `FragmentRenderer`.
5. `FragmentRenderer` passes only `Fragment`, `OnChanged`, and optional sampler `UseGuidance` into designed components.
6. `LatentForm` renders `ResolutionPanel` for width and height and a batch-size slider.
7. `SourceAsset.HasData` already provides the generic signal needed for source-presence conditions.

Important files:

- `BlazorWebApp/Workflows/Models/WorkflowMetadata.cs`
- `BlazorWebApp/Models/FragmentSchema.cs`
- `BlazorWebApp/Models/FragmentReference.cs`
- `BlazorWebApp/Services/GenerationParameterService.cs`
- `BlazorWebApp/Services/WorkflowService.cs`
- `BlazorWebApp/Pages/Generate.razor`
- `BlazorWebApp/Pages/Generate.razor.cs`
- `BlazorWebApp/Components/Shared/Generation/FragmentRenderer.razor`
- `BlazorWebApp/Components/Shared/Generation/Fragments/LatentForm.razor`
- `BlazorWebApp/Components/Shared/Generation/ResolutionPanel.razor`
- `BlazorWebApp/Workflows/Templates/flux/Flux2KleinGridRefWorkflow.cs`

---

## Proposed Model

### Metadata Types

Add a small set of metadata models near the workflow fragment metadata types:

```csharp
public record FragmentParameterUiState
{
    public IReadOnlyList<FragmentUiCondition> DisabledWhen { get; init; } = [];
}

public record FragmentUiCondition
{
    public FragmentUiConditionKind Kind { get; init; }
    public string? SourceId { get; init; }
    public string? FragmentId { get; init; }
    public string? Parameter { get; init; }
    public object? Value { get; init; }
    public string? Reason { get; init; }
}

public enum FragmentUiConditionKind
{
    SourceHasData,
    SourceMissingData,
    FragmentValueEquals,
    FragmentValueNotEquals,
    FragmentIsActive,
    FragmentIsInactive
}
```

Initial implementation can support only `SourceHasData` and `SourceMissingData`; the other enum values can be deferred if they add too much surface area.

### Runtime State

Add a generic resolved state object passed into fragment components:

```csharp
public sealed class FragmentRenderContext
{
    public IReadOnlyDictionary<string, FragmentParameterRenderState> Parameters { get; init; }
        = new Dictionary<string, FragmentParameterRenderState>();

    public bool IsDisabled(string parameterName) =>
        Parameters.TryGetValue(parameterName, out var state) && state.Disabled;

    public string? GetDisabledReason(string parameterName) =>
        Parameters.TryGetValue(parameterName, out var state) ? state.DisabledReason : null;
}

public sealed class FragmentParameterRenderState
{
    public bool Disabled { get; init; }
    public string? DisabledReason { get; init; }
}
```

The exact type name can be adjusted, but a single render-context parameter is preferred over many one-off booleans.

---

## Implementation Phases

### Phase 1: Schema And Condition Model

**Objective:** Add metadata and schema support for parameter-level UI conditions without changing any UI behavior.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add UI-state condition models to the workflow metadata layer.
- [ ] Extend `FragmentParameter` with an optional UI-state property.
- [ ] Extend `ParameterConstraints` or a sibling schema model to carry UI-state metadata.
- [ ] Update both schema conversion paths in `GenerationParameterService` and `WorkflowService` so fluent metadata flows into `FragmentSchema`.
- [ ] Add focused tests for metadata-to-schema propagation.

#### Success Criteria

- Existing workflows compile without any metadata changes.
- Fragment schemas preserve condition metadata when a fragment opts in.
- No Generate-page or component behavior changes yet.

---

### Phase 2: Generic Condition Evaluation

**Objective:** Evaluate parameter UI conditions generically from `GenerationParameters` and workflow source state.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add a small evaluator service or helper, for example `FragmentUiConditionEvaluator`.
- [ ] Implement `SourceHasData` and `SourceMissingData` against `GenerationParameters.Sources` and `SourceAsset.HasData`.
- [ ] Produce a `FragmentRenderContext` for a given `FragmentReference`.
- [ ] Ensure evaluation is pure and has no side effects on fragment values.
- [ ] Add focused unit tests for source-present and source-missing conditions.

#### Success Criteria

- The evaluator returns disabled state and reason for matching parameter rules.
- Missing source IDs fail closed as not disabled unless the condition explicitly means missing data.
- No workflow-specific source ID is present in Generate-page logic.

---

### Phase 3: FragmentRenderer And Designed Components

**Objective:** Pass generic render context through `FragmentRenderer` and teach existing components to honor disabled parameter state.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Update `FragmentRenderer` to build and pass a `RenderContext` parameter to designed fragment components.
- [ ] Add optional `[Parameter] public FragmentRenderContext? RenderContext { get; set; }` to `LatentForm`.
- [ ] Update `ResolutionPanel` with `DisableWidth`, `DisableHeight`, or a compact disabled-state API for resolution controls.
- [ ] Disable quick-resolution buttons, scale/rotate/lock buttons, and width/height sliders when either affected dimension is disabled.
- [ ] Keep batch size enabled unless a workflow explicitly adds a condition for `batch_size`.
- [ ] Add diagnostics/build validation for Razor component changes.

#### Success Criteria

- Designed components that do not declare `RenderContext` continue to render normally.
- `LatentForm` disables only controls indicated by generic parameter state.
- The UI remains dense and flush with existing Generate-page form patterns.

---

### Phase 4: Flux2 Klein Grid Ref Opt-In

**Objective:** Use the new metadata in the grid-reference workflow to disable faux-resolution controls when the optional base image is present.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps

- [ ] Add `DisabledWhen SourceHasData(base_image)` metadata to the grid-reference latent width and height parameters.
- [ ] Consider whether `batch_size` should also be disabled, since base-image mode currently forces batch size to `1`.
- [ ] Add focused tests proving the grid-reference fragment metadata declares the expected conditions.
- [ ] Verify the workflow graph remains unchanged.

#### Success Criteria

- The grid-reference template opts into the generic rule without Generate-page special cases.
- When `base_image` has data, resolution controls are disabled and explain why.
- When `base_image` is empty, width and height controls remain enabled.

---

### Phase 5: Documentation And Follow-Up Decisions

**Objective:** Document the new fragment UI-condition pattern for future workflow authors.
**Complexity:** 2 points
**Status:** [ ] Not Started

#### Steps

- [ ] Update workflow UI conversion documentation with the condition metadata pattern.
- [ ] Add examples for source-driven disabling.
- [ ] Document deferred condition kinds if only source-presence conditions ship initially.
- [ ] Record validation notes and any UX caveats in the phase document.

#### Success Criteria

- Workflow authors know how to opt into disabled controls without touching Generate-page logic.
- Documentation distinguishes workflow graph behavior from UI hints.
- Future templates can reuse the pattern.

---

## Stress Points And Risks

| Risk                                                               | Mitigation                                                                                                                       | Complexity |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Dynamic Generate page becomes coupled to one template              | Keep conditions in fragment metadata and evaluate generically                                                                    | 5          |
| Designed components may ignore unknown DynamicComponent parameters | Pass render context only to components that can accept it, or update shared target components before passing                     | 5          |
| Two schema-build paths diverge                                     | Update both `GenerationParameterService.BuildSchemaFromMetadata` and `WorkflowService.BuildSchemaFromMetadata` in the same phase | 3          |
| UI condition metadata grows into a scripting language              | Start with source-presence conditions; add other condition kinds only after a concrete workflow needs them                       | 5          |
| Disabled controls confuse users because saved values still exist   | Provide an optional disabled reason and keep values unchanged; the workflow graph remains authoritative                          | 3          |
| Batch-size behavior is ambiguous for base-image grid reference     | Decide during Phase 4 whether to disable `batch_size` or leave it enabled but ignored                                            | 2          |

---

## Validation Strategy

- `get_errors` on touched C# and Razor files after each phase.
- Focused unit tests for metadata propagation and condition evaluation.
- Focused workflow test for `Flux2KleinGridRefWorkflow` metadata opt-in.
- Build `BlazorWebApp/BlazorWebApp.csproj` after Razor component changes.
- Manual Generate-page smoke test when available:
  - no base image: resolution controls enabled,
  - base image present: resolution controls disabled,
  - base image cleared: resolution controls enabled again.

---

## Deferred Options

- Visibility conditions, not just disabled conditions.
- Conditions based on asset selection, backend availability, or dynamic option lists.
- Dynamic-field renderer support for the same condition metadata.
- A tooltip or inline caption system for disabled reasons across all fragment controls.
- A workflow-level alias such as `DrivenBySource = "base_image"` for common resolution-lock cases. This should be avoided until multiple workflows need the same shorthand.

---

## Changelog

| Phase    | Changes                                                         |
| -------- | --------------------------------------------------------------- |
| Planning | Initial plan created for metadata-driven fragment UI conditions |

---

## References

- `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- `BlazorWebApp/Workflows/Models/WorkflowMetadata.cs`
- `BlazorWebApp/Models/FragmentSchema.cs`
- `BlazorWebApp/Services/GenerationParameterService.cs`
- `BlazorWebApp/Services/WorkflowService.cs`
- `BlazorWebApp/Components/Shared/Generation/FragmentRenderer.razor`
- `BlazorWebApp/Components/Shared/Generation/Fragments/LatentForm.razor`
- `BlazorWebApp/Components/Shared/Generation/ResolutionPanel.razor`
- `BlazorWebApp/Workflows/Templates/flux/Flux2KleinGridRefWorkflow.cs`
