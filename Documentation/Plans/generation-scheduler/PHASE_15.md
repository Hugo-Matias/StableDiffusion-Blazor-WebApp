# Phase 15 - Dual-Mode Editor (Form + JSON) and Context-Aware Help

## Status

**Phase:** 15 - NOT STARTED
**Complexity:** 16 points (15a: 3, 15b: 8, 15c: 5)
**Depends on:** Phases 1-9 (delivered). Benefits from Phase 12 (base-params summary) and Phase 13 (selectors) for the Target picker experience. Phase 14 is independent but recommended first.

---

## Objective

Make Directive / Variation composition accessible via a **typed form editor as the primary entry point**, while preserving **raw JSON editing as an advanced fallback**. Ship a **context-aware Scheduler section in the existing `InfoDrawer`** with structured (non-markdown) content.

## Background

Current editor: a single `SchedulerJsonEditorDialog` that exposes raw JSON for every Directive / Variation. Powerful but opaque, especially given the polymorphic `$type` discriminator and per-subtype schemas. Users need discoverability and guidance without losing the escape hatch.

## Design Summary

- **Form mode (default):** `DirectiveEditorDialog` + `VariationEditorDialog` each host a type selector + a dynamic per-subtype form rendered by a small factory. A shared `ParameterTargetPicker` handles all `ParameterTarget` input.
- **JSON mode (advanced):** existing `SchedulerJsonEditorDialog`, promoted to validate on blur, demoted in the UI to an \"Advanced > Edit JSON\" overflow entry on each Directive/Variation card. A \"View JSON\" toggle inside the form dialog shows the live serialized value (read-only) so users can learn the format.
- **Context-aware help:** extend `InfoContent` with `Sections` (structured, non-markdown). Populate Scheduler content covering Jobs, Directives (per subtype), Variations (per subtype), Targets, Limits, Permutation order, Output overrides, Advanced JSON tips. The `SchedulerEditorTab` sets Scheduler info on mount; opening a form expands the matching section.

## Phase 15a - Structured Info Sections & Scheduler Help Content [3 pts]

### Steps

- [ ] Step 1 - Extend `IInfoService` models (backwards compatible) [2 pts]
  - Add `public record InfoSection(string Title, string? Icon, List<InfoItem> Items);`
  - Add `public record InfoItem(string? Label, string Text);`
  - Add optional `List<InfoSection>? Sections` to `InfoContent`.

- [ ] Step 2 - Render sections in `InfoDrawer.razor` [2 pts]
  - Below existing Overview / Shortcuts / Tips blocks, render each `InfoSection` as a `MudExpansionPanel` titled with the section's Title, icon prefix if provided.
  - Items render as two-column rows: `Label` (bold, optional) + `Text` (`MudText Typo=\"Typo.body2\"`).
  - No markdown. Line breaks preserved via `white-space: pre-wrap`.

- [ ] Step 3 - Author Scheduler help seed [3 pts]
  - New static class `SchedulerInfoContent` returning the preconfigured `InfoContent` (Overview, Tips, Shortcuts, Sections) for the Scheduler page.
  - Sections:
    - \"Jobs\" - what, lifecycle, base params snapshot, output config.
    - \"Directives\" - one InfoSection per subtype (SetValue, AppendPrompt, ReplacePrompt, AddLora, RemoveLora, ToggleLora, AddPromptStyle, SwapAsset, SetOutput) with Purpose / Required fields / Example value.
    - \"Variations\" - one per subtype (List, Range, Random, Wildcard, LLM, SearchReplace, Toggle).
    - \"Targets\" - Fragment / LoRA / Asset / Prompt / Output target semantics.
    - \"Limit & Cartesian Size\" - cap behaviour, relationship to total.
    - \"Permutation Order\" - Sequential vs Random + seed semantics.
    - \"Advanced: JSON Mode\" - `$type` discriminator, polymorphic gotchas, when to prefer JSON.

- [ ] Step 4 - Context-awareness wiring [2 pts]
  - `SchedulerEditorTab.OnInitializedAsync` -> `InfoService.SetInfo(SchedulerInfoContent.Build())`.
  - On dispose -> `InfoService.ClearInfo()`.
  - When a Directive/Variation dialog opens, publish the section id (e.g., via a parameter on `InfoDrawer` or a small `InfoService.HighlightSection(string)`) so the drawer auto-expands the matching panel.

## Phase 15b - Form-Mode Editor (priority subtypes) [8 pts]

Priority subtypes selected for first delivery (most common workflows):

- Directives: `SetValue`, `AppendPrompt`, `AddLora`, `ToggleLora`.
- Variations: `List`, `Range`, `Wildcard`, `SearchReplace`.

### Steps

- [ ] Step 1 - `ParameterTargetPicker.razor` [5 pts]
  - Parameters: `ParameterTarget? Value`, `EventCallback<ParameterTarget> ValueChanged`, `Guid WorkflowId`, `Job Job` (for LoRA stack context), plus optional `IEnumerable<TargetKind> AllowedKinds` to restrict to valid kinds per Directive/Variation type.
  - Cascading selects: TargetKind -> concrete identifier.
    - `FragmentTarget`: pick FragmentId from `WorkflowMetadata`, then ParamKey filtered to that fragment's declared params.
    - `LoraTarget`: pick from `Job.BaseParameters.Loras` names.
    - `AssetTarget`: pick from `AssetKey` enum (Model, Vae, Clip, ...).
    - `PromptTarget`: toggle `IsNegative`.
    - `OutputTarget`: pick `OutputField` (Project | Folder).
  - Emits a concrete subclass on change.

- [ ] Step 2 - `DirectiveEditorDialog.razor` + `DirectiveFormFactory` [5 pts]
  - Dialog header: type selector (`MudSelect<string>` of directive kind), Label field, Enabled toggle.
  - Body: renders the per-subtype form component resolved by the factory.
  - Footer: Cancel / OK, plus a \"View JSON\" expander showing the read-only serialized form.
  - Per-subtype forms for priority set:
    - `SetValueDirectiveForm` - `ParameterTargetPicker` + `Value` input (type inferred from selected target: numeric / string / bool).
    - `AppendPromptDirectiveForm` - `Text`, `IsPrefix`, `IsNegative`, `Separator`.
    - `AddLoraDirectiveForm` - LoRA picker from available local resources + `StrengthModel`, `StrengthClip`.
    - `ToggleLoraDirectiveForm` - LoRA name select (from Base Params) + `Enable` toggle.

- [ ] Step 3 - `VariationEditorDialog.razor` + `VariationFormFactory` [5 pts]
  - Same shell structure as the Directive dialog.
  - Per-subtype forms for priority set:
    - `ListVariationForm` - repeatable value rows matching target type; live count preview.
    - `RangeVariationForm` - Start / End / Step / IsInteger; live count + first-5 values preview.
    - `WildcardVariationForm` - CollectionName select from `IWildcardService`, optional Count, AllowRepeats, Weighted.
    - `SearchReplaceVariationForm` - Search text + list of Replacements; IsNegative, CaseSensitive.
  - Each form exposes a live `GetCount()` display next to OK.

- [ ] Step 4 - \"View JSON\" toggle inside both dialogs [1 pt]
  - Read-only `MudTextField` monospace showing `JsonSerializer.Serialize(value, SchedulerJsonOptions.Default)`.

- [ ] Step 5 - Promote JSON editor to \"Advanced\" [1 pt]
  - Each Directive/Variation card gets an overflow `MudMenu` with \"Edit\" (form) as primary and \"Advanced > Edit JSON\" opening the existing `SchedulerJsonEditorDialog`.
  - \"Add Directive\" / \"Add Variation\" buttons default to form mode; a sibling \"Add via JSON\" option remains for power users.

## Phase 15c - Remaining Subtypes & JSON Polish [5 pts]

### Steps

- [ ] Step 1 - Remaining Directive forms [5 pts]
  - `ReplacePromptDirectiveForm` - Search / Replace / IsNegative / CaseSensitive.
  - `RemoveLoraDirectiveForm` - LoRA name select.
  - `AddPromptStyleDirectiveForm` - multi-select of existing `PromptStyle` names.
  - `SwapAssetDirectiveForm` - AssetKey select + value select (filtered to that asset kind).
  - `SetOutputDirectiveForm` - reuse `ProjectFolderSelector` (Phase 13).

- [ ] Step 2 - Remaining Variation forms [3 pts]
  - `RandomVariationForm` - Min / Max / Count / IsInteger / Seed.
  - `LlmVariationForm` - ModelName select from Ollama, BasePrompt, SystemPrompt, Count, IsNegative.
  - `ToggleVariationForm` - OnValue / OffValue inputs typed to target.

- [ ] Step 3 - `SchedulerJsonEditorDialog` polish [2 pts]
  - Parse-on-blur via `JsonSerializer.Deserialize<...>(text, SchedulerJsonOptions.Default)`; show inline `MudAlert` with the error and line on failure; OK disabled while invalid.
  - \"Format\" button reserializes with `WriteIndented = true`.

- [ ] Step 4 - Extend `SchedulerInfoContent` seed with any added subtypes [1 pt]

## Success Criteria

- Opening a Directive/Variation defaults to the typed form dialog.
- All 9 Directive and 7 Variation subtypes are creatable and editable via form.
- `ParameterTargetPicker` never offers invalid FragmentId / ParamKey combinations for the current workflow.
- Raw JSON reachable from each card via \"Advanced\" overflow; JSON dialog validates on blur and rejects invalid submissions.
- `InfoDrawer` shows a Scheduler-specific section set; opening a form auto-expands the matching panel; no markdown is rendered.
- Existing `SchedulerServiceTests` remain green; new UI is covered by minimal component-level tests where they add value.

## Verification

- Manual: create a job entirely via form mode covering every subtype.
- Manual: round-trip a job through \"Edit\" form then \"Advanced > Edit JSON\" and back; no data loss.
- Manual: open InfoDrawer, verify sections, verify context auto-expansion.
- Build: green. Scheduler tests: 92/92.

## Open Questions

- Type-inferred value inputs for `SetValueDirective`: rely on the selected target's declared type metadata (FragmentParameter schema). Confirm coverage across workflows during Phase 15b Step 2.
- LLM form: expose prompt template presets from `SystemPromptTemplate` table? Deferred unless requested.

## Out of Scope

- Drag-and-drop reordering (tracked separately).
- Live preview of a single iteration's resolved parameters (tracked separately).
