# Phase 8 - Variant.Outlined -> Variant.Text sweep

## Status
**Phase:** 8 - Complete
**Build Status:** Passing

---

## Objective

Enforce the design-language rule ("form controls default to `Variant.Text`; `Variant.Outlined` is reserved for emphasis") across every page and component touched in Phases 3-7 plus any stragglers discovered during the sweep.

---

## Execution

### Strategy

A one-pass PowerShell regex replacement that **only** targets standalone attribute lines:

```
^(\s*)Variant="Variant\.Outlined"(\s*/?>?)$
```

This pattern matches multi-line attribute declarations on `MudTextField` / `MudSelect` / `MudAutocomplete` / `MudNumericField`. It does NOT match:

- Inline usages on `<MudButton ...>`, `<MudChip ...>`, `<MudAlert ...>` (attribute sits on the same line as the opening tag).
- Ternary expressions like `Variant="@(... ? Variant.Filled : Variant.Outlined)"` (quote pairing differs).
- `<MudButtonGroup Variant="Variant.Outlined">` (inline on opening tag).

### Replacements (29 total)

| File | Hits |
|---|---|
| `Components/Prompts/LLM/LLMMainPanel.razor` | 2 |
| `Components/Prompts/LLM/LLMSettingsPanel.razor` | 6 |
| `Components/Prompts/LLM/LLMModelSelector.razor` | 1 |
| `Components/Prompts/LLM/PromptComparisonPanel.razor` | 1 |
| `Components/Prompts/LLM/SystemPromptEditor.razor` | 4 |
| `Components/Prompts/LLM/TemplateInfoDialog.razor` | 2 |
| `Components/Prompts/Styles/PromptStyleTable.razor` | 3 |
| `Components/Prompts/Wildcards/CollectionBrowser.razor` | 1 |
| `Components/Prompts/Wildcards/EntryManager.razor` | 2 |
| `Components/Prompts/Wildcards/CollectionEditorDialog.razor` | 3 |
| `Components/Prompts/Wildcards/EntryEditorDialog.razor` | 1 |
| `Components/Prompts/Wildcards/WildcardImportDialog.razor` | 3 |

### Side effect (one ambiguous case caught)

`SystemPromptEditor.razor:63-64` was a multi-line `<MudButton>` with `Size="Size.Small"` on line 63 and `Variant="Variant.Outlined"` on its own line 64 (an "Add Message" button). The regex matched and flipped it to `Variant.Text`. Text variant is a reasonable default for a small inline button, so it was left as-is rather than reverted. Flagged here so the user can revert if desired.

---

## Intentional Retentions (explicit emphasis - NOT swept)

| Location | Reason |
|---|---|
| `LLMMainPanel.razor` 21, 28 | Button toggle state (`Variant.Filled` when active, `Variant.Outlined` when inactive) - deliberate visual contrast |
| `LLMMainPanel.razor` 140 | `MudAlert` severity=Error - warning emphasis |
| `PromptComparisonPanel.razor` 102, 108 | `MudButton` secondary actions - intentional emphasis over `Variant.Text` |
| `SystemPromptEditor.razor` 171 | `MudButton` secondary action |
| `SystemPromptEditor.razor` 180 | `MudButtonGroup` wrapper - outlined group styling |
| `PromptStyleCard.razor` 55 | `MudChip` tag visual style |
| `PromptStyleCompactCard.razor` 106 | `MudChip` tag visual style |
| `PromptStyleCompactCard.razor` 127 | `MudButton` card action |
| `WildcardExportDialog.razor` 49 | `MudAlert` Error |
| `WildcardImportDialog.razor` 120 | `MudAlert` Warning |
| `WildcardImportDialog.razor` 130 | `MudAlert` Error |
| `Scheduler/Forms/RangeVariationForm.razor` 57 | `MudChip` info tag |

These align with the rule: `Variant.Outlined` is permitted for non-form-control emphasis (alerts, chips, deliberate button accents, toggle state).

---

## Phase Summary

29 form-control instances normalized to `Variant.Text` across 12 files; 14 intentional-emphasis usages left untouched. Build passes. One ambiguous button (SystemPromptEditor "Add Message") flagged in the side-effect section.

**Phase Status:** Complete [x]
