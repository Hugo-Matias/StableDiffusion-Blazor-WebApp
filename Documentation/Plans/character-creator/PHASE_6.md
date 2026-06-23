# Phase 6 - LLM Assisted Authoring

## Status

Complete. User approved proceeding after Phase 5 completion, and Phase 6 implementation plus validation finished in this pass.

## Objective

Add LLM helpers that turn terse selections into polished descriptions and suggest missing traits.

## Scope

Phase 6 adds structured LLM authoring operations and a preview/apply/reject workflow in the Character Creator editor. It does not add image interrogation or prompt send-to workflow routing; those remain later phases.

## Step Checklist

- [x] Step 1: Add character-specific system prompt templates. `[2 pts]`
- [x] Step 2: Add service methods for expand, summarize, rewrite, and fill-missing operations. `[3 pts]`
- [x] Step 3: Ensure LLM suggestions return structured changes, not only prose. `[3 pts]`
- [x] Step 4: Let users preview/accept/reject proposed trait updates. `[3 pts]`
- [x] Step 5: Add tests around response parsing and fallback behavior. `[3 pts]`

## Implementation Notes

- The LLM authoring service uses shared `State.Generation.LLM` settings and Ollama JSON mode.
- Suggestions are structured drafts and are never applied without user acceptance.
- Applying suggestions skips existing manual or locked trait values.
- The Character Creator workspace exposes three reviewable actions: expand summary, rewrite region, and fill missing.
- The service also exposes `SummarizeAsync` for later UI/workflow reuse.

## Validation Plan

Completed:

- File diagnostics for touched service, model, page, component, and test files: no errors.
- `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterLlmAuthoringServiceTests --no-restore`: 4 passed.
- `process: build`: passed.

Known unrelated warning noise:

- `NU1603` for `Microsoft.Bcl.AsyncInterfaces` version resolution.
- `NU1901`/`NU1902`/`NU1903` advisories for `Magick.NET-Q16-AnyCPU`.

## Open Issues / Blockers

- Runtime quality depends on the selected local Ollama model; tests cover parsing and safety behavior, not live model quality.

## Change Log

- Created Phase 6 document and began implementation after user approval.
- Added `CharacterLlmAuthoringService` and `ICharacterLlmAuthoringService` for structured JSON suggestions.
- Added `CharacterLlmAuthoringModels` for operations, suggestions, and apply results.
- Wired the Character Creator workspace to preview, accept, and reject LLM suggestions.
- Added focused tests for parsing, malformed fallback, safe apply behavior, and Ollama request options.
