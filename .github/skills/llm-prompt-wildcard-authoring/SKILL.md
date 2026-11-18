---
name: llm-prompt-wildcard-authoring
description: "Extend prompt tooling, wildcard generation, and LLM template flows. Use when editing llm_prompts.json, theme_catalog.json, WildcardForge services, WildcardService behavior, prompt send-to actions, or LLM-oriented prompt authoring views."
argument-hint: "Prompt, wildcard, or LLM tooling change"
---

# LLM Prompt Wildcard Authoring

Use this skill for prompt-generation and wildcard-related features.

## When To Use

- Add or update prompt templates in `llm_prompts.json`
- Extend wildcard generation or wildcard forge behavior
- Change prompt send-to flows
- Add or revise LLM-oriented prompt-authoring UI

## Procedure

1. Decide which layer owns the change:
   - data template files in `BlazorWebApp/Data/`
   - orchestration services in `BlazorWebApp/Services/WildcardForge/`
   - runtime wildcard behavior in `BlazorWebApp/Services/WildcardService.cs`
   - prompt-routing UX in `PromptSendToService` and prompt views
2. For template-file changes, preserve the schema expected by `WildcardForgeKnowledge`.
3. Do not add a new template key or shape without checking the loader and the enum or operation mapping that consumes it.
4. For UI actions that send content into generation flows, reuse `PromptSendToService` and the shared `.send-to-btn` conventions.
5. Keep prompt-authoring views aligned with the design-language rules for tabs, spacing, and form controls.
6. Add or update narrow service tests when the change affects prompt composition or wildcard transformations.
7. If runtime validation is limited, call out the remaining LLM-integration risk explicitly.

## Guardrails

- Do not silently change template semantics in `llm_prompts.json` without checking the services that interpret them.
- Do not bypass the existing send-to routing service from UI code.
- Do not mix prompt-view styling with one-off button patterns when the shared send-to styles already fit.

## Key Anchors

- `../../../BlazorWebApp/Data/llm_prompts.json`
- `../../../BlazorWebApp/Data/theme_catalog.json`
- `../../../BlazorWebApp/Services/WildcardService.cs`
- `../../../BlazorWebApp/Services/WildcardForge/WildcardForgeService.cs`
- `../../../BlazorWebApp/Services/WildcardForge/WildcardForgeKnowledge.cs`
- `../../../BlazorWebApp/Services/PromptSendToService.cs`
- `../../../BlazorWebApp/Components/Prompts/LLM/`
