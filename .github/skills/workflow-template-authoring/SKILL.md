---
name: workflow-template-authoring
description: 'Author or modify fluent workflow templates and fragments. Use when converting raw ComfyUI JSON, creating an IWorkflowBuilder, implementing an IFragmentBuilder, wiring NodeRegistry outputs, defining workflow assets, scopes, sources, or adding workflow tests.'
argument-hint: 'Workflow base, mode, or fragment to add or change'
---

# Workflow Template Authoring

Use this skill for workflow-template implementation work in the fluent C# system.

## When To Use

- Convert a raw ComfyUI workflow into the app's template system
- Add a new `IWorkflowBuilder`
- Add or edit an `IFragmentBuilder`
- Define workflow assets, sources, descriptions, scope behavior, or output naming
- Update workflow tests after template changes

## Procedure

1. Start from the closest existing workflow under `BlazorWebApp/Workflows/Templates/<Base>/`.
2. If the request is a full conversion that requires planning and user approval, use the existing workflow-conversion prompt workflow before implementation.
3. Define or update `WorkflowMetadata` first:
   - `Title`
   - `Description`
   - `Base`
   - `Mode`
   - `Assets`
   - `Sources`
   - `CompatibleResourceBaseModels`
4. Keep workflow descriptions user-facing. They belong in the Info drawer, not as a banner on the page.
5. Reuse existing fragments where the parameter shape already matches.
6. Hidden utility fragments should stay out of `GetFragments()`.
7. Follow the repo's scope and output conventions:
   - scoped loader fragments write scoped outputs
   - processing fragments read from the requested scope
   - pipeline outputs such as `image_output` and `latent_output` stay on the main pipeline
8. Use `NodeRegistry` typed references instead of ad hoc node id strings where the existing patterns support them.
9. For optional passes, overwrite the main pipeline output instead of creating side-channel save logic.
10. If a user-visible fragment changes, pair the workflow work with the workflow UI skill.
11. Add or update the narrowest workflow tests under `BlazorWebApp.Tests/Workflows/`.

## Guardrails

- Do not invent new output naming schemes.
- Do not hardcode CivitAI base-model strings without checking `Data/CivitAI/basemodels.json`.
- Do not leave a workflow without a `Description`.
- Do not rely on metadata-only dynamic rendering for fluent fragments that need a real form component.

## Key Anchors

- `../../../BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`
- `../../../BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`
- `../../../BlazorWebApp/Workflows/WORKFLOW_UI_CONVERSION_GUIDE.md`
- `../../../BlazorWebApp/Workflows/Models/IWorkflowBuilder.cs`
- `../../../BlazorWebApp/Workflows/Models/IFragmentBuilder.cs`
- `../../../BlazorWebApp/Workflows/Models/WorkflowMetadata.cs`
- `../../../BlazorWebApp/Services/WorkflowService.cs`
- `../../../BlazorWebApp.Tests/Workflows/`