# Phase 16 - Editor Tab UX Refinements

## Status

**Phase:** 16 - IN PROGRESS
**Complexity:** 21 points (16a..16g)
**Depends on:** Phases 1-9, 12, 13, 14, 15 (all delivered).

---

## Objective

Refine the Scheduler Editor tab based on post-revamp review:

- Align form styling with the Generate page (baseline design language).
- Restrict `ParameterTargetPicker` kinds per Directive / Variation type.
- Consolidate output-routing handling around `SetOutputDirective` only.
- Data-bind LoRA, Asset, Wildcard and LLM pickers to their existing services.
- Display wildcard folder/category paths in the collection selector.
- Back the LLM "system prompt" field by the Prompts page templates (DB rows).

## Design References

- Baseline design rules: `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.
- Generate-page LoRA loader: `BlazorWebApp/Components/Shared/Generation/LoraForm.razor`.
- Asset resolution: `IAssetResolverService.GetAssetOptions(AssetType)`.
- Wildcard collections: `IWildcardService.SearchCollections(query, maxResults)`
  returns `WildcardCollection` with `Name` + `Category`; displayed as `Category/Name`.
- System prompt templates: `IDatabaseService.GetSystemPromptTemplates()`.

---

## 16a - Design-language sweep [2 pts]

- [x] Replace `Variant.Outlined` with `Variant.Text` across
      `BlazorWebApp/Components/Scheduler/**/*.razor` form controls
      (MudSelect, MudTextField, MudNumericField, MudAutocomplete).
- [x] Keep `Variant.Outlined` on emphasis chips (warnings) where it already is; only sweep input variants.

### Success criteria

- Scheduler Editor inputs visually match the Generate page.

## 16b - ParameterTargetPicker: AllowedKinds + value-type gating [3 pts]

- [x] Extend `ParameterTargetPicker` with an optional `FragmentValueFilter`
      (`Any | Numeric | String | Boolean`) so numeric variations
      (Range / Random) only list numeric fragment params.
- [x] Pass `AllowedKinds` per directive / variation:
  - `SetValueDirective`: `Fragment, Lora, Asset, Prompt` (no Output).
  - `ListVariation`: `Fragment, Lora, Asset, Prompt`.
  - `RangeVariation` / `RandomVariation`: `Fragment, Lora`; numeric filter on fragment.
  - `WildcardVariation`: `Fragment, Prompt`; string filter on fragment.
  - `LlmVariation`: `Prompt` only (already in place).
  - `ToggleVariation`: `Fragment, Lora`; boolean filter preferred.
- [x] When the current `Value.Kind` is not in `AllowedKinds`, coerce to the first allowed kind on mount.

## 16c - Consolidate output routing [5 pts]

- [x] Remove `FolderName` from `SetOutputDirective` (no saved jobs to migrate).
- [x] Update `DirectiveExecutor` and tests accordingly.
- [x] `SetOutputDirectiveForm` uses `ProjectFolderSelector`; Folder is a filter only
      (empty = show all projects), Project is the persisted target.
- [x] Drop `TargetKind.Output` from `SetValueDirective`'s AllowedKinds (picker still supports it
      for backwards wiring but the directive no longer exposes it).

## 16d - LoRA + Asset pickers [5 pts]

- [x] `AddLoraDirectiveForm`: `MudAutocomplete` backed by `IRouterService.SearchLoras`.
      Selection sets both `Name` (filename stem) and `Path` (full).
      No base-model filter (defer to a later revision).
- [x] `SwapAssetDirectiveForm`: AssetKey `MudSelect` (from workflow), Asset Value becomes a
      `MudAutocomplete` backed by `IAssetResolverService.GetAssetOptions` mapped through
      the workflow's `WorkflowAsset.Type`.

## 16e - Wildcard collection dropdown shows folders [2 pts]

- [x] `WildcardVariationForm` autocomplete displays `Category/Name` (e.g. `clothes/shoes`).

## 16f - LLM system prompt dropdown [3 pts]

- [x] Replace `LlmVariation.SystemPrompt` (string) with `SystemPromptTemplateId` (int?).
- [x] `LlmVariationForm`: template `MudSelect` populated from
      `IDatabaseService.GetSystemPromptTemplates()`; show `(none)` + template names
      with a default chip for `IsDefault`.
- [x] `VariationMaterializer.MaterializeLlmAsync`: when `SystemPromptTemplateId` is set,
      load the template, clone its messages, replace `{prompt}` with `BasePrompt`, and
      call `OllamaService.SendChatMessage`. Fallback to existing `ExpandPrompt` path otherwise.

## 16g - Variation-type-aware fragment filtering [1 pt]

Folded into 16b via `FragmentValueFilter`.

---

## Risks

| Risk | Mitigation |
| ---- | ---------- |
| `AllowedKinds` coercion drops pre-existing targets | On coercion, emit `null` so the user is forced to re-pick; never silently mutate to a different kind of target. |
| `SystemPromptTemplateId` referring to a deleted template | Materializer falls back to default expand path and logs a warning. |
| `IRouterService.SearchLoras` unavailable in test | Keep the manual Name/Path fallback by letting the autocomplete accept free text (`CoerceValue="true"`). |
