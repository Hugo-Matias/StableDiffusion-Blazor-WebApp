# Phase 10 - Wildcard Forge (LLM x Wildcards Integration)

## Status

**Phase:** 10
**Build Status:** Clean (steps 1-9 implemented; smoke test pending user run)
**Phase Status:** [~] Awaiting user smoke test

---

## Objective

Add a new LLM-Tools view, **Wildcard Forge**, where users generate, expand, refine, convert, and describe wildcard collections via prompt-driven LLM calls. Output flows through the existing `WildcardCollection` / `WildcardEntry` pipeline so collections show up everywhere wildcards already work (`[[name]]` parsing, Inspiration roulette, etc.).

The Forge is intentionally NOT a replacement for the Wildcards tab. Manual editing (per-entry add/edit/delete, weight tuning, import/export) stays in `WildcardsTab`. The Forge is the AI-assisted authoring surface where prompting is the primary interaction.

---

## Background

The repository already contains a high-quality knowledge base under `Documentation/Wildcards/`:

- `WILDCARD_GENERATION_GUIDE.md` - quality rules, verbosity levels, do's and don'ts
- `WILDCARD_CREATION_WIZARD.md` - 5-phase questionnaire designed for LLM-guided creation
- `LLM_PROMPTS.json` - 5 tested prompt templates (basic_generation, themed_expansion, quality_enhancement, category_focused, diversity_focused)
- `THEME_CATALOG.json` - 10 themed categories with subcategories, keywords, verbosity examples, sample collections, best practices, avoid lists
- `WILDCARD_TEMPLATE.json` - JSON structure reference for imports

The Forge integrates this knowledge base as a runtime knowledge layer. The wizard markdown is treated as design reference (not LLM payload). The two JSON files are loaded into typed POCOs at startup and used by a `PromptComposer` to build slim, targeted system prompts per operation.

---

## Token Budget Strategy

Target: 8k context window (limit reported by user's local model).

| Bucket                                   | Reserve    |
| ---------------------------------------- | ---------- |
| Output (e.g. 25 verbose entries as JSON) | ~2,000     |
| Safety margin / response variance        | ~500       |
| System-prompt scaffolding                | ~500       |
| **Available for context per call**       | **~5,000** |

Within the 5k context budget the composer slots:

- 1 distilled `CategoryCard` (~250-400 tokens) - only the user's chosen category, never all 10.
- Verbosity rubric slice (~150 tokens) - the four levels with examples drawn from that category.
- Wizard answer summary as a compact key/value block (~150 tokens).
- User free-text instruction (~100-500 tokens).
- Existing entries for Expand/Refine (~300-800 tokens for 25-50 entries).

Hard caps:

- Refine processes max 25 entries per call. Larger collections auto-chunk into multiple sequential calls; UI shows progress.
- Expand respects the same chunking on the "do not repeat" reference set, summarized between chunks.

If the catalog card for a single category exceeds the soft target the composer emits a "compact card" variant that drops `verbosity_examples` (verbosity is served separately) and trims `keywords` and `sample_collections`.

---

## Design Decisions

### Operations (v1 ships all five)

| Op       | Backed by template                                                                                               | Notes                                                                                                     |
| -------- | ---------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Generate | `basic_generation` (or `category_focused` when subcategory chosen, `diversity_focused` when diversity toggle on) | Simple or Advanced (wizard) input modes.                                                                  |
| Expand   | `themed_expansion`                                                                                               | Requires existing collection; auto-chunks if reference set > 25 entries.                                  |
| Refine   | `quality_enhancement`                                                                                            | Side-by-side diff (old -> new) per entry; bulk Accept All.                                                |
| Convert  | `TagPromptService` (Phase 2)                                                                                     | Output replaces or augments depending on user choice.                                                     |
| Describe | small custom prompt (~150 tokens in / ~80 out)                                                                   | Utility op; infers theme + suggested name + category for an unnamed draft and auto-fills the Save dialog. |

### Wildcard Forge Wizard (Advanced mode)

Adapts `WILDCARD_CREATION_WIZARD.md` to a 4-step inline form (collapsed accordion, not modal). The doc's Phase 4 (QA) and Phase 5 (Export) collapse into the post-generation **Review** UI shared by all operations.

| Step                   | Inputs                                                                                                   | LLM cost          |
| ---------------------- | -------------------------------------------------------------------------------------------------------- | ----------------- |
| 1. Theme & Category    | free-text purpose, category dropdown (10 categories), subcategory dropdown (auto-populated from catalog) | 0 (deterministic) |
| 2. Scope & Count       | focused/moderate/broad pills, count slider (5-50), diversity toggle                                      | 0                 |
| 3. Verbosity & Style   | 4 verbosity radio cards with live examples pulled from the chosen category's `verbosity_examples`        | 0                 |
| 4. Examples (optional) | 0-5 user-provided seed entries                                                                           | 0                 |
| **Submit**             | composer assembles slim prompt -> single LLM call                                                        | 1                 |

Each wizard step gets an opt-in `[Suggest with AI]` button that sends only the running summary (~300 tokens in, ~50 out) and fills the field. Strictly opt-in.

Simple mode is one text box + count + Run. A deterministic preprocessor classifies the description into category + verbosity using keyword matching against `THEME_CATALOG.keywords` (no LLM call). When confidence is low it surfaces a one-line clarifier with the top two suggested category buttons; otherwise it falls back to a "general" permissive system prompt.

### State and Persistence

`AppState.Prompts.LLM.WildcardForge` (new sub-state) holds:

- Mode (`simple` | `advanced`)
- Last theme, count, verbosity, selected category and subcategory
- Diversity toggle, scope choice
- 0-5 seed examples
- The current unsaved `ForgeDraft` (entries, status flags, target collection if Expand/Refine, op type)

LocalStorage is **not** used. AppState already JSON-persists via the `State` entity; the draft survives tab navigation and reloads. Heavy mutation pressure during streaming is mitigated by buffering in component state and committing to AppState only on stream-end or explicit user actions (accept/reject).

### Knowledge Layer

- `WildcardForgeKnowledge` (singleton, loaded at startup) parses `THEME_CATALOG.json` and `LLM_PROMPTS.json` into typed POCOs (`CategoryCard`, `LlmTemplate`, `VerbosityLevelInfo`).
- No hot-reload. Files are stable at runtime.
- Catalog card lookup: by category id or name (case-insensitive). Subcategory list comes from the loaded card.
- Verbosity rubric lookup: per category, the composer slices `verbosity_examples` to the user's chosen level plus one neighbor for contrast.

### `PromptComposer`

Single class responsible for turning `(operation, ForgeRequest, CategoryCard?, draft state)` into `List<OllamaChatMessage>`. Substitutes `{variables}` from the loaded template. Hard-budgets each section by token estimate (chars/4 heuristic) and trims gracefully if the estimate exceeds the per-call ceiling.

### Save Flow

Three save paths:

- **New collection**: prompts for name + category + description (Describe op autofills these). Calls `IDatabaseService.CreateWildcardCollection` then `CreateWildcardEntry` per accepted draft entry.
- **Append**: existing collection. Adds accepted entries; preserves existing.
- **Replace**: existing collection. Wipes existing entries and replaces with accepted draft. Confirmation dialog required.

All entries default to `Weight = 1.0` regardless of LLM output - confirmed UX choice. `SortOrder` is the position in the accepted draft.

### Send-to-Workshop

Each draft entry gets a small "Send to Workshop" affordance that composes a sample prompt using the entry plus a generic context (`"a portrait of {entry}, photorealistic"`) and routes through the existing Workshop entry-point pattern (Phase 8 send-to plumbing).

---

## Data Model

No new EF entities. No new migration. AppState additions only.

```csharp
public class AppStatePromptsLLMWildcardForge
{
    public string Mode { get; set; } = "simple"; // "simple" | "advanced"
    public string LastTheme { get; set; } = string.Empty;
    public int Count { get; set; } = 20;
    public string Verbosity { get; set; } = "balanced"; // minimal | balanced | detailed | verbose
    public string? CategoryId { get; set; }
    public string? Subcategory { get; set; }
    public string Scope { get; set; } = "focused"; // focused | moderate | broad
    public bool DiversityMode { get; set; } = false;
    public List<string> SeedExamples { get; set; } = new();
    public ForgeDraftDto? CurrentDraft { get; set; }
}

public class ForgeDraftDto
{
    public string Operation { get; set; } = "Generate";
    public int? TargetCollectionId { get; set; }
    public string? TargetCollectionName { get; set; }
    public string? SuggestedName { get; set; }
    public string? SuggestedCategory { get; set; }
    public string? SuggestedDescription { get; set; }
    public List<ForgeDraftEntryDto> Entries { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class ForgeDraftEntryDto
{
    public string Value { get; set; } = string.Empty;
    public string Status { get; set; } = "New"; // New | Kept | Modified | Rejected
    public string? OriginalValue { get; set; }
    public bool Accepted { get; set; } = true;
}
```

---

## Files Affected

New:

- `BlazorWebApp/Components/Prompts/LLM/Views/WildcardForgeView.razor` (+ `.razor.css`)
- `BlazorWebApp/Components/Prompts/LLM/Views/Forge/ForgeSimpleInput.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/Forge/ForgeAdvancedWizard.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/Forge/ForgeReviewPanel.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/Forge/ForgeSaveDialog.razor`
- `BlazorWebApp/Services/WildcardForge/IWildcardForgeService.cs`
- `BlazorWebApp/Services/WildcardForge/WildcardForgeService.cs`
- `BlazorWebApp/Services/WildcardForge/WildcardForgeKnowledge.cs`
- `BlazorWebApp/Services/WildcardForge/PromptComposer.cs`
- `BlazorWebApp/Services/WildcardForge/Models/CategoryCard.cs`
- `BlazorWebApp/Services/WildcardForge/Models/LlmTemplate.cs`
- `BlazorWebApp/Services/WildcardForge/Models/VerbosityLevelInfo.cs`
- `BlazorWebApp/Services/WildcardForge/Models/ForgeRequest.cs`
- `BlazorWebApp/Services/WildcardForge/Models/ForgeOperation.cs`

Extend:

- `BlazorWebApp/Models/AppState.cs` (+ `WildcardForge` sub-state and DTOs)
- `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor` (+ `wildcard-forge` nav item)
- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor` (+ switch case)
- `BlazorWebApp/Program.cs` (DI registrations)

No changes:

- `WildcardService` / `IWildcardService` (write APIs already on `IDatabaseService`).
- `OllamaService` (existing chat interface is sufficient).
- EF migrations / `AppDbContext`.

---

## Execution Checklist

### Step 1: AppState sub-state + DTOs

**Complexity:** 2
**Status:** [x] Complete

- [ ] Add `AppStatePromptsLLMWildcardForge`, `ForgeDraftDto`, `ForgeDraftEntryDto` to `AppState.cs`.
- [ ] Reference from `AppStatePromptsLLM.WildcardForge`.
- [ ] Build clean.

### Step 2: Knowledge layer

**Complexity:** 5
**Status:** [x] Complete

- [ ] Create `Models/CategoryCard.cs`, `LlmTemplate.cs`, `VerbosityLevelInfo.cs`.
- [ ] `WildcardForgeKnowledge` singleton: load `Documentation/Wildcards/THEME_CATALOG.json` and `LLM_PROMPTS.json` once on construction. Throw a clear error if files missing.
- [ ] Public surface: `IReadOnlyList<CategoryCard> Categories`, `CategoryCard? FindCategory(string idOrName)`, `LlmTemplate GetTemplate(ForgeOperation op)`.
- [ ] Register in `Program.cs` as singleton.

### Step 3: PromptComposer

**Complexity:** 5
**Status:** [x] Complete

- [ ] `ForgeRequest` DTO captures op + theme + count + verbosity + category + subcategory + diversity + seeds + (optional) existing entries + (optional) target collection.
- [ ] `PromptComposer.Compose(ForgeRequest) -> List<OllamaChatMessage>`.
- [ ] Implements per-op slot filling for the 5 templates loaded from `LLM_PROMPTS.json`.
- [ ] Char/4 token heuristic with graceful trim of `verbosity_examples` and `sample_collections` if oversized.
- [ ] Unit tests deferred (smoke-test by view interaction in Step 7).

### Step 4: WildcardForgeService

**Complexity:** 5
**Status:** [x] Complete (Convert uses inline LLM prompt; TagPromptService bridge deferred to follow-up)

- [ ] Orchestrates: Compose -> `OllamaService.SendChatMessage` -> JSON parse -> dedup against target / self -> emit `ForgeDraft`.
- [ ] Tolerant JSON parser: handles model output wrapped in markdown code fences, strips leading/trailing prose, falls back to line-by-line extraction if JSON invalid.
- [ ] Dedup helper: lowercase + trim + collapse whitespace + strip surrounding quotes.
- [ ] Wraps existing entries in fenced delimiters (`<<entry>>...<</entry>>`) inside Refine/Expand prompts to mitigate prompt injection.
- [ ] Convert op delegates to `TagPromptService` and adapts the result.
- [ ] Describe op uses a tiny inline prompt (no template).

### Step 5: View - Simple mode

**Complexity:** 3
**Status:** [x] Complete

- [ ] `WildcardForgeView.razor` shell with op pills (Generate active by default).
- [ ] `ForgeSimpleInput`: theme textarea + count slider + Run.
- [ ] Deterministic classifier (keyword match against `Categories.SelectMany(c => c.Keywords)`) -> picks category.
- [ ] On low confidence (no match >= 1 keyword) surface a clarifier chip row with the top 2 candidates.
- [ ] Wires to `WildcardForgeService.GenerateAsync`.

### Step 6: View - Advanced wizard

**Complexity:** 5
**Status:** [x] Complete (Suggest-with-AI per-step deferred to follow-up)

- [ ] `ForgeAdvancedWizard` 4-step accordion.
- [ ] Step 1: free-text purpose + category dropdown + subcategory dropdown.
- [ ] Step 2: scope pills, count slider, diversity toggle.
- [ ] Step 3: 4 verbosity radio cards with live category examples.
- [ ] Step 4: 0-5 seed example entries (chip-style add).
- [ ] Each step: optional `[Suggest with AI]` mini-call that fills the field.
- [ ] Final Submit assembles `ForgeRequest` -> `WildcardForgeService`.

### Step 7: View - Review panel

**Complexity:** 5
**Status:** [x] Complete (inlined in WildcardForgeView rather than a separate ForgeReviewPanel component)

- [ ] `ForgeReviewPanel` shows draft entries with Accept toggle, edit-in-place, trash.
- [ ] Refine: side-by-side diff column (Original | Refined).
- [ ] Filters: hide rejected, hide duplicates of target.
- [ ] Bulk actions: Accept All, Reject All, Re-run.
- [ ] Send-to-Workshop per entry.

### Step 8: View - Save flow

**Complexity:** 3
**Status:** [x] Complete

- [ ] `ForgeSaveDialog`: New / Append / Replace radio.
- [ ] New: name + category + description (auto-filled by Describe op when available, or by clicking `[Suggest]`).
- [ ] Append/Replace: target collection picker (uses existing `GetAllWildcardCollections`).
- [ ] Replace path requires confirmation (`MudMessageBox`).
- [ ] Persists via `IDatabaseService.CreateWildcardCollection` + `CreateWildcardEntry` (Weight=1.0, SortOrder=index).

### Step 9: Wire nav + LLMToolsTab

**Complexity:** 1
**Status:** [x] Complete

- [ ] Add `("wildcard-forge", "Wildcard Forge", Icons.Material.Filled.AutoAwesomeMosaic)` to `LLMNavMenu.Items` between Tag Builder and Inspiration.
- [ ] Add switch case in `LLMToolsTab.razor` rendering `<WildcardForgeView SelectedModel="@_selectedModel" />`.

### Step 10: Smoke test + build

**Complexity:** 2
**Status:** [~] Build clean; manual smoke test pending

- [ ] Build clean.
- [ ] Manual smoke flow: Simple Generate ("dramatic lighting", count 15) -> draft renders -> save as new -> appears in `WildcardsTab`.
- [ ] Verify draft persists across tab nav (AppState).

---

## Success Criteria

- All five operations function end-to-end against the local Ollama model.
- Generated entries land in `WildcardCollection` and are usable via `[[name]]` parsing without a restart.
- Token usage stays under 8k for Generate count<=30 and Refine chunked at 25.
- Advanced wizard works without any LLM call until Submit (zero-cost path preserved).
- AppState round-trips the draft across page reloads.

---

## Open Risks

1. **Tolerant JSON parsing.** Local 8k models occasionally emit malformed JSON. The line-by-line fallback should still produce usable entries; flagged with a "parsed leniently" notice.
2. **Streaming vs single-shot.** v1 is single-shot (spinner during call). Streaming with progressive entry emission is a follow-up if the wait UX is poor on slow models.
3. **Catalog drift.** If users edit `THEME_CATALOG.json` while the app is running, changes are not picked up until restart (per direction).
4. **Suggest-with-AI cost.** Four optional mini-calls per wizard pass. Documented in the view InfoContent so users with very small models know to skip them.
5. **Diversity toggle semantics.** Currently a single boolean -> swaps template. Future: expose the percentage knobs from `diversity_focused`.

---

## Resolved Assumptions

- Renumbering: this is the new Phase 10. Old Phase 10 (Polish) -> Phase 11. Old Phase 11 (VL) -> Phase 12.
- Weights: always `1.0` from generation; users tune later in `WildcardsTab`.
- Side-nav label: "Wildcard Forge".
- Hot-reload of knowledge files: not in v1.
- Per-step Suggest-with-AI: in v1.
- Persistence: AppState only (no DB-backed draft entity).
