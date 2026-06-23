# Phase 2 - Danbooru Tag Builder

## Status

**Phase:** 2
**Build Status:** Clean (0 errors)
**Phase Status:** [x] Complete

---

## Objective

Generate coherent prompts in the Danbooru tag vocabulary from natural-language input, using the existing CSV-backed tag catalog (`CsvService`) and Ollama LLMs. Two-pass retrieval architecture keeps the LLM context small, guarantees every output tag exists in the vocabulary, and respects per-model conventions (Pony / Illustrious / NoobAI / Anima).

---

## Context

### Dependencies on Phase 1

- `LLMNavMenu` items list (3 nav items) must be extended with a `tag-builder` entry.
- `AppStatePromptsLLM` must be extended with a `TagBuilder` sub-state.
- New view file sits in `Components/Prompts/LLM/Views/TagBuilderView.razor` following the pattern established by `ProcessView` / `HistoryView`.

### Existing infrastructure to reuse

- `CsvService.SearchTags(string, bool)` - fuzzy + alias search against `danbooru.csv`, returns `IEnumerable<Tag>` sorted by recency / local use / fuzzy score / global popularity. `Tag.Color` carries the Danbooru category code (0 general, 1 artist, 3 copyright, 4 character, 5 meta).
- `OllamaService.SendChatMessage(...)` - raw chat call; already used by `ProcessView`.
- Model selection, Ollama options, and `_selectedModel` are already shared across all LLM views via the parent `LLMToolsTab`.

### Architectural Pattern - Two-Pass Retrieval (Option B from `MAIN_PLAN.md`)

```
User NL input -> [LLM Pass 1: concept extraction] -> List<Concept>
                     |
                     v
         [TagPromptService.Resolve] -> List<ResolvedConcept(Concept, List<Tag candidates>)>
                     |
                     v
   [LLM Pass 2: assembly + model preset + verbosity] -> Final ordered tag prompt
```

- Pass 1 is a small, deterministic-ish extraction (JSON output preferred, but we'll parse line-based text as fallback).
- Resolution is pure C#: loops through extracted concepts, calls `CsvService.SearchTags`, takes top-K candidates per concept.
- Pass 2 is given (a) the resolved tag list grouped by category, (b) the verbosity level, (c) the model preset's vocabulary / quality-tag convention, and produces the final comma-separated tag prompt.

### Persistence Conventions

- `AppStatePromptsLLM.TagBuilder` is a new nested class on `AppState`. Auto-serialized via the existing JSON ValueConverter on `State.AppState`; no EF migration required.

---

## Execution Checklist

### Step 1: Core DTOs + `TagPromptService` skeleton

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Create `BlazorWebApp/Models/TagPromptModels.cs` containing:
  - `TagCategory` enum mapped to CSV color codes (`General = 0`, `Artist = 1`, `Copyright = 3`, `Character = 4`, `Meta = 5`).
  - `TagModelPreset` enum (`Pony`, `Illustrious`, `NoobAI`, `Anima`).
  - `TagVerbosity` enum (`Minimal`, `Standard`, `Detailed`, `Exhaustive`).
  - `TagBuilderRequest` record (user input, verbosity, preset, category toggles, NSFW gate, optional grounding-prompt).
  - `ExtractedConcept` record (`Text`, optional `Category`).
  - `ResolvedConcept` record (`Concept` + `IReadOnlyList<Tag> Candidates`).
  - `TagBuilderResult` record (`FinalPrompt`, `ResolvedConcepts`, `ModelUsed`, `Preset`, `Verbosity`, `RawPass1`, `RawPass2`).
- [x] Create `BlazorWebApp/Services/TagPromptService.cs` with constructor injecting `CsvService`, `OllamaService`, `IDatabaseService`, `ILogger<TagPromptService>`. Public methods (skeleton only - implementations in Steps 2-4):
  - `Task<List<ExtractedConcept>> ExtractConceptsAsync(string userInput, string modelName, OllamaOptions? options, CancellationToken ct = default)`
  - `Task<List<ResolvedConcept>> ResolveAsync(IEnumerable<ExtractedConcept> concepts, TagBuilderRequest request, CancellationToken ct = default)`
  - `Task<string> AssemblePromptAsync(IEnumerable<ResolvedConcept> resolved, TagBuilderRequest request, string modelName, OllamaOptions? options, CancellationToken ct = default)`
  - `Task<TagBuilderResult> BuildAsync(TagBuilderRequest request, string modelName, OllamaOptions? options, CancellationToken ct = default)` - orchestrates the three steps.
- [x] Register `TagPromptService` in `Program.cs` as singleton.

#### Success Criteria

- Build passes.
- Service is injectable from a Razor component.
- No business logic yet - just contracts.

---

### Step 2: Pass 1 - concept extraction

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Implement `ExtractConceptsAsync`:
  - Build `List<OllamaChatMessage>` with a strict system prompt instructing the model to output one concept per line, optionally prefixed with `[category]` (e.g., `[subject] woman at beach`).
  - Call `OllamaService.SendChatMessage`.
  - Parse line-based response into `ExtractedConcept` list; tolerate JSON output by trying JSON parse first, falling back to line parsing.
  - Deduplicate, trim, drop empty lines.

#### Success Criteria

- Input "a cyberpunk woman at the beach at sunset" yields ~4-6 concepts touching subject / setting / time-of-day / style.
- If model returns prose, parser still extracts the dominant concept or returns empty without throwing.

---

### Step 3: Resolution layer

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Implement `ResolveAsync`:
  - For each `ExtractedConcept`, call `CsvService.SearchTags(concept.Text, enableFuzzy: true)`.
  - Filter candidates by `request.CategoryToggles` (map `Tag.Color` int back to `TagCategory`).
  - Take top-K candidates (K = 5 per concept by default).
  - Return `ResolvedConcept` with trimmed candidate list.
- [x] Add a static `DanbooruCategory.FromColor(int)` helper in `TagPromptModels.cs`.

#### Notes

- NSFW gate deferred — resolution layer does not filter NSFW tags (noted as open risk #3).

#### Success Criteria

- Concept "woman" resolves to `1girl` / `solo` / `looking_at_viewer` candidates.
- Concept "beach" returns setting-category tags.
- Category toggles actually filter output.

---

### Step 4: Pass 2 - tag assembly + model presets

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Implement `AssemblePromptAsync`:
  - Build a prompt payload with: the full list of resolved candidates grouped by category, the preset name, the verbosity level, and an optional grounding-prompt (if `request.GroundingPrompt` is non-empty, tell the model "keep elements from the user's existing prompt where compatible").
  - Post-process: split on `,`, trim, dedupe preserving order, clamp length per verbosity (Minimal <= 15 tags, Standard <= 30, Detailed <= 50, Exhaustive <= 80).
- [x] Implement preset conventions table (static dict in service) - quality-tag prefixes / suffixes per preset:
  - Pony: `score_9, score_8_up, score_7_up, ...`
  - Illustrious: `masterpiece, best quality, amazing quality, ...`
  - NoobAI: similar to Illustrious baseline.
  - Anima: `masterpiece, best quality, very aesthetic, absurdres`.
- [x] Implement `BuildAsync` orchestration method: Pass 1 -> Resolve -> Pass 2 -> assemble `TagBuilderResult`. Store `RawPass1` and `RawPass2` so the view can show traceability.

#### Success Criteria

- "woman at the beach" at Detailed verbosity + Pony preset produces a tag prompt starting with Pony quality prefix, containing subject / setting / atmosphere tags, all comma-separated.
- Switching preset changes quality tags only (core tags stable across presets for the same input).
- Exhaustive yields more tags than Minimal for the same input.

---

### Step 5: `AppState.Prompts.LLM.TagBuilder` sub-state + nav registration

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Extend `AppStatePromptsLLM` with `TagBuilder = new AppStatePromptsLLMTagBuilder();`.
- [x] Create `AppStatePromptsLLMTagBuilder` nested class storing: last input, `TagVerbosity`, `TagModelPreset`, category toggles dictionary.
- [x] Extend `LLMNavMenu.Items` with tag-builder nav entry using `Icons.Material.Filled.Build`.
- [x] Extend the `switch (_activeViewId)` block in `LLMToolsTab.razor` with the `tag-builder` case.

#### Success Criteria

- Nav shows the new entry; clicking it loads the view (which will be empty until Step 6).
- Active view id persists across reloads.

---

### Step 6: `TagBuilderView.razor`

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Create `BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor` with:
  - Natural-language input `MudTextField` (multi-line).
  - Verbosity slider (`MudSlider` 0-3 -> `TagVerbosity`).
  - Model-preset `MudSelect`.
  - Category toggles (General / Character / Copyright / Artist / Meta) via `MudCheckBox`.
  - "Ground in current prompt" `MudCheckBox` with grounding prompt input.
  - Primary `Build` button.
  - Resolution preview panel (expandable `MudExpandPanel` showing concept -> top-K tag chips).
  - Final output panel: read-only text + copy-to-clipboard / "Send to Process" buttons.
- [x] Parameter on the view: `SelectedModel`.
- [x] Persist all selector state via `AppState.Prompts.LLM.TagBuilder` + `State.SaveState()`.
- [x] Single-phase UX: Build runs Pass 1 -> Resolve -> Pass 2 in one call (simpler than two-phase for v1).
- [x] "Send to Process" publishes `LLMViewChangedEventArgs` via `EventService` and sets prompt on `AppState.Generation.LLM.Prompt`.

#### Notes

- NSFW gate omitted from initial implementation (deferred per open risk #3).
- Two-phase UX simplified to single Build button for v1.

#### Success Criteria

- End-to-end flow: type concept -> Build -> see resolution preview + final prompt -> copy / send-to-process.
- Preset / verbosity / toggles persist across reloads.
- Every final tag appears in `danbooru.csv` (spot-check).
- No duplication of `CsvService` search logic inside the view.

---

## Progress Tracking

| Step | Status | Complexity | Notes                      |
| ---- | ------ | ---------- | -------------------------- |
| 1    | [x]    | 3          | DTOs + service skeleton    |
| 2    | [x]    | 5          | Pass 1 concept extraction  |
| 3    | [x]    | 3          | CSV resolution layer       |
| 4    | [x]    | 5          | Pass 2 assembly + presets  |
| 5    | [x]    | 2          | State + nav registration   |
| 6    | [x]    | 5          | `TagBuilderView` component |

**Total:** 23 points delivered.

---

## Issues & Resolutions

1. **NSFW gate deferred** — Resolution layer does not filter NSFW tags. The deny-list approach is intentionally non-exhaustive and can be added in Phase 10 (Polish).
2. **Two-phase UX simplified** — Build runs all three passes (extract -> resolve -> assemble) in a single call rather than the originally planned two-step flow. Resolution preview still displayed after build for traceability.
3. **`CsvService.SearchTags` performance** — Each concept triggers a full CSV scan. Documented as open risk #4; mitigate with in-memory index if profiling shows bottleneck.

---

## Commit Checkpoints

- [x] After Step 1 complete — DTOs, service skeleton, DI registration
- [x] After Step 2 complete — Concept extraction with JSON-first / line-parse fallback
- [x] After Step 3 complete — CSV resolution with category filtering and top-K candidates
- [x] After Step 4 complete — Tag assembly with model presets, verbosity clamping, BuildAsync orchestration
- [x] After Step 5 complete — AppState sub-state + nav menu entry + view switch wiring
- [x] After Step 6 complete — Full TagBuilderView with input, settings, resolution preview, result panel

---

## Open Risks

1. **Model preset vocabulary drift** - Danbooru-derived models (Pony / Illustrious / NoobAI / Anima) evolve quality-tag conventions frequently. Mitigation: isolate preset constants in one dictionary with a "verify at implementation time" comment; user can override via system-prompt edits.
2. **Pass 1 output parsing fragility** - small local models often return prose. Mitigation: JSON-first, line-parse fallback, tolerate empty results without throwing.
3. **NSFW gate is non-exhaustive** - A deny-list cannot catch every NSFW tag. Deferred to Phase 10.
4. **Performance** - `CsvService.SearchTags` re-reads `danbooru.csv` on every call. If Pass 1 yields 10 concepts, that's 10 full passes. Mitigation: if profiling shows this is a problem, add an in-memory tag index to `CsvService` as a detour (new phase 2.5) rather than bloating this phase.

---

## Phase Summary

Phase 2 delivered a functional Danbooru tag builder using the two-pass retrieval architecture. Natural-language descriptions are extracted into visual concepts by an LLM, resolved against the CSV-backed `danbooru.csv` catalog via `CsvService.SearchTags`, and assembled into model-preset-compliant tag prompts with verbosity-controlled length clamping.

### Files Created

- `BlazorWebApp/Models/TagPromptModels.cs` — Enums (`TagCategory`, `TagModelPreset`, `TagVerbosity`), helper (`DanbooruCategory.FromColor`), DTOs (`TagBuilderRequest`, `ExtractedConcept`, `ResolvedConcept`, `TagBuilderResult`)
- `BlazorWebApp/Services/TagPromptService.cs` — Two-pass pipeline service with concept extraction, CSV resolution, tag assembly, and orchestration
- `BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor` — Full UI component

### Files Modified

- `BlazorWebApp/Program.cs` — Registered `TagPromptService` as singleton
- `BlazorWebApp/Models/AppState.cs` — Added `AppStatePromptsLLMTagBuilder` sub-state
- `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor` — Added tag-builder nav entry
- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor` — Wired TagBuilderView into view switch

### Deferred Items

- NSFW gate filtering (open risk #3)
- Two-phase Build UX (simplified to single button for v1)
- In-memory CSV index for performance (open risk #4)
