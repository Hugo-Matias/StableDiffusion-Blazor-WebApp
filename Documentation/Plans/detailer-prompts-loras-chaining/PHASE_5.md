# Phase 5 - Documentation

## Status
**Phase:** 5
**Build Status:** N/A (docs only)

## Objective
Capture the new Detailer conventions in long-lived guides so future workflow conversions stay consistent.

## Steps
- [x] Update `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md` with:
    - Indexed detailer scope convention (`detailer_` for pass 0, `detailer_{i}_` for passes >= 1).
    - Required LoRA loops per pass.
    - Prompt-fallback-on-empty rule.
    - Chained detailer diagram.
- [x] Update `.github/prompts/workflow-conversion.prompt.md` with a Detailer checklist.
- [x] Archive the outdated `Documentation/Plans/dynamic-generation-refactor/DETAILER_PROMPTS_LORA_PLAN.md` with a redirect note.
- [x] Update `MAIN_PLAN.md` with final status and lessons learned.
