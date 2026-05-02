---
name: targeted-validation
description: 'Validate changes with the narrowest useful checks in this repo. Use when choosing focused workflow tests, scheduler tests, service tests, build commands, or file-level error checks in a workspace that can have unrelated build and test noise.'
user-invocable: false
---

# Targeted Validation

Use this skill whenever you need to decide how to validate a change efficiently.

## When To Use

- Pick the first validation step after editing
- Choose between a focused test, focused build, or broader build
- Validate workflow, scheduler, service, persistence, or UI changes with minimal unrelated noise

## Procedure

1. Prefer the cheapest check that can falsify the current change:
   - targeted tests for the touched slice
   - file-level diagnostics on the changed files
   - a narrow project build
2. For workflow work, look first at `BlazorWebApp.Tests/Workflows/`.
3. For scheduler work, look first at `BlazorWebApp.Tests/Scheduler/`.
4. For service and repository work, look for the nearest existing test file under `BlazorWebApp.Tests/Services/` or related folders.
5. For UI-only changes with no practical runtime harness, use focused build or diagnostics and call out remaining runtime risk.
6. Use the workspace build task or `dotnet build BlazorWebApp/BlazorWebApp.csproj` only after narrower checks are exhausted or when the touched slice lacks direct tests.
7. Report unrelated repo noise separately so it does not get confused with the change under review.

## Guardrails

- Do not jump straight to a full build when a focused test exists.
- Do not treat `git diff` as sufficient validation if an executable check is available.
- Do not hide unrelated failures; separate them from the changed slice.

## Key Anchors

- `../../../AGENTS.md`
- `../../../BlazorWebApp.Tests/Workflows/`
- `../../../BlazorWebApp.Tests/Scheduler/`
- `../../../BlazorWebApp.Tests/Services/`
- `../../../BlazorWebApp/BlazorWebApp.csproj`