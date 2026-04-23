# Phase 2 - Danbooru Tag Builder

## Status

**Phase:** 2
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started

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
- `SystemPromptTemplate` entity + `DatabaseService.SeedDefaultSystemPromptTemplates` - default templates are sourced from `OllamaService.GetDefaultTemplates()` and only seeded when zero `IsDefault` rows exist. We will extend that seeding so `Pass1_ConceptExtraction` and `Pass2_TagAssembly` defaults ship with the DB.
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
- Seeded system prompts for Pass 1 and Pass 2 go in `SystemPromptTemplate` rows with `IsDefault = true` and a category hint in the `Description` (there's no category column yet; the Phase 10 "Polish & Settings" phase may introduce one).
- The two new default templates are only added when absent - check by `Name` inside the existing seed method.

### Architectural Rules (from `.github/copilot-instructions.md`)

- Any cross-view notification (e.g., "send these tags to Process view") must go through `EventService` pub/sub. Phase 2 does **not** introduce cross-view sends yet - that hook lands in Phase 8 (Workshop).
- No new entity = no migration checklist needed for Phase 2.

---

## Execution Checklist

### Step 1: Core DTOs + `TagPromptService` skeleton

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Models/TagPromptModels.cs` containing:
  - `TagCategory` enum mapped to CSV color codes (`General = 0`, `Artist = 1`, `Copyright = 3`, `Character = 4`, `Meta = 5`).
  - `TagModelPreset` enum (`Pony`, `Illustrious`, `NoobAI`, `Anima`).
  - `TagVerbosity` enum (`Minimal`, `Standard`, `Detailed`, `Exhaustive`).
  - `TagBuilderRequest` record (user input, verbosity, preset, category toggles, NSFW gate, optional grounding-prompt).
  - `ExtractedConcept` record (`Text`, optional `Category`).
  - `ResolvedConcept` record (`Concept` + `IReadOnlyList<Tag> Candidates`).
  - `TagBuilderResult` record (`FinalPrompt`, `ResolvedConcepts`, `ModelUsed`, `Preset`, `Verbosity`, `RawPass1`, `RawPass2`).
- [ ] Create `BlazorWebApp/Services/TagPromptService.cs` with constructor injecting `CsvService`, `OllamaService`, `IDatabaseService`, `ILogger<TagPromptService>`. Public methods (skeleton only - implementations in Steps 2-4):
  - `Task<List<ExtractedConcept>> ExtractConceptsAsync(string userInput, string modelName, OllamaOptions? options, CancellationToken ct = default)`
  - `Task<List<ResolvedConcept>> ResolveAsync(IEnumerable<ExtractedConcept> concepts, TagBuilderRequest request, CancellationToken ct = default)`
  - `Task<string> AssemblePromptAsync(IEnumerable<ResolvedConcept> resolved, TagBuilderRequest request, string modelName, OllamaOptions? options, CancellationToken ct = default)`
  - `Task<TagBuilderResult> BuildAsync(TagBuilderRequest request, string modelName, OllamaOptions? options, CancellationToken ct = default)` - orchestrates the three steps.
- [ ] Register `TagPromptService` in `Program.cs` as scoped.

#### Success Criteria

- Build passes.
- Service is injectable from a Razor component.
- No business logic yet - just contracts.

---

### Step 2: Pass 1 - concept extraction

**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks

- [ ] Implement `ExtractConceptsAsync`:
  - Build `List<OllamaChatMessage>` with a strict system prompt instructing the model to output one concept per line, optionally prefixed with `[category]` (e.g., `[subject] woman at beach`).
  - Call `OllamaService.SendChatMessage`.
  - Parse line-based response into `ExtractedConcept` list; tolerate JSON output by trying JSON parse first, falling back to line parsing.
  - Deduplicate, trim, drop empty lines.
- [ ] Seed the Pass 1 `SystemPromptTemplate` with `Name = "TagBuilder.ConceptExtraction"`, `IsDefault = true`. Extend `OllamaService.GetDefaultTemplates()` to include it; existing seed method picks it up automatically on next startup.

#### Success Criteria

- Input "a cyberpunk woman at the beach at sunset" yields ~4-6 concepts touching subject / setting / time-of-day / style.
- If model returns prose, parser still extracts the dominant concept or returns empty without throwing.

---

### Step 3: Resolution layer

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Implement `ResolveAsync`:
  - For each `ExtractedConcept`, call `CsvService.SearchTags(concept.Text, enableFuzzy: true)`.
  - Filter candidates by `request.CategoryToggles` (map `Tag.Color` int back to `TagCategory`).
  - If `request.AllowNsfw == false`, exclude well-known NSFW tags (use a conservative deny-list OR rely on tag category; start with a simple hard-coded deny-list in the service - document that this is intentionally non-exhaustive).
  - Take top-K candidates (K = 5 per concept by default).
  - Return `ResolvedConcept` with trimmed candidate list.
- [ ] Add a static `DanbooruCategory.FromColor(int)` helper in `TagPromptModels.cs`.

#### Success Criteria

- Concept "woman" resolves to `1girl` / `solo` / `looking_at_viewer` candidates.
- Concept "beach" returns setting-category tags.
- Category toggles actually filter output.

---

### Step 4: Pass 2 - tag assembly + model presets

**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks

- [ ] Implement `AssemblePromptAsync`:
  - Build a prompt payload with: the full list of resolved candidates grouped by category, the preset name, the verbosity level, and an optional grounding-prompt (if `request.GroundingPrompt` is non-empty, tell the model "keep elements from the user's existing prompt where compatible").
  - Use the `TagBuilder.TagAssembly` default system prompt (seeded in this step).
  - Post-process: split on `,`, trim, dedupe preserving order, clamp length per verbosity (Minimal <= 15 tags, Standard <= 30, Detailed <= 50, Exhaustive <= 80).
- [ ] Implement preset conventions table (static dict in service) - quality-tag prefixes / suffixes per preset:
  - Pony: `score_9, score_8_up, score_7_up, ...`
  - Illustrious: `masterpiece, best quality, amazing quality, ...`
  - NoobAI: similar to Illustrious baseline (document uncertainty).
  - Anima: `masterpiece, best quality, very aesthetic, absurdres`.
  - Mark the constants with a comment: "verify dialect at implementation time - conventions drift".
- [ ] Implement `BuildAsync` orchestration method: Pass 1 -> Resolve -> Pass 2 -> assemble `TagBuilderResult`. Store `RawPass1` and `RawPass2` so the view can show traceability.
- [ ] Seed `TagBuilder.TagAssembly` default `SystemPromptTemplate` via `OllamaService.GetDefaultTemplates()`.

#### Success Criteria

- "woman at the beach" at Detailed verbosity + Pony preset produces a tag prompt starting with Pony quality prefix, containing subject / setting / atmosphere tags, all comma-separated.
- Switching preset changes quality tags only (core tags stable across presets for the same input).
- Exhaustive yields more tags than Minimal for the same input.

---

### Step 5: `AppState.Prompts.LLM.TagBuilder` sub-state + nav registration

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Extend `AppStatePromptsLLM` with `TagBuilder = new AppStatePromptsLLMTagBuilder();`.
- [ ] Create `AppStatePromptsLLMTagBuilder` nested class storing: last input, `TagVerbosity`, `TagModelPreset`, category toggles dictionary, `AllowNsfw`, `GroundInCurrentPrompt`.
- [ ] Extend `LLMNavMenu.Items` with `new("tag-builder", "Tag Builder", Icons.Material.Filled.LocalOffer)`.
- [ ] Extend the `switch (_activeViewId)` block in `LLMToolsTab.razor` with the `tag-builder` case.

#### Success Criteria

- Nav shows the new entry; clicking it loads the view (which will be empty until Step 6).
- Active view id persists across reloads.

---

### Step 6: `TagBuilderView.razor`

**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor` with:
  - Natural-language input `MudTextField` (multi-line, immediate).
  - Verbosity slider (`MudSlider` 0-3 -> `TagVerbosity`).
  - Model-preset `MudSelect`.
  - Category toggles `MudChipSet` (General / Character / Copyright / Artist / Meta).
  - NSFW gate `MudSwitch`.
  - "Ground in current prompt" `MudSwitch` (stubbed - actual current-prompt wiring defers to Phase 8).
  - Primary `Build` button.
  - Resolution preview panel (concept -> top-K tag chips with fuzzy score tooltip) - visible after Pass 1+Resolve, before Pass 2 runs.
  - Final output panel: `PromptComparisonPanel`-like read-only text + copy / save-to-library / "send to Process view" (last one: parent raises a restore-style event like History does).
- [ ] Parameters on the view: `SelectedModel`, `OnSendToProcess(string prompt)` callback.
- [ ] Persist all selector state via `AppState.Prompts.LLM.TagBuilder` + `State.SaveState()` (debounced on input change, immediate on selector changes).
- [ ] Two-phase UX: pressing Build first runs Pass 1 + Resolve; shows preview; a secondary "Assemble Final Prompt" button triggers Pass 2. This gives the user a chance to eyeball resolution before committing.
- [ ] Wire the optional `OnSendToProcess` to `LLMToolsTab` by reusing the `PendingRestore` pattern (put the assembled prompt into `_pendingRestore` with a synthetic `PromptHistoryEntry`, bump `_restoreVersion`, switch to `"process"`).

#### Success Criteria

- End-to-end flow: type concept -> Build -> see resolution preview -> assemble final -> copy / save / send-to-process.
- Preset / verbosity / toggles persist across reloads.
- Every final tag appears in `danbooru.csv` (spot-check).
- No duplication of `CsvService` search logic inside the view.

---

## Progress Tracking

| Step | Status | Complexity | Notes                      |
| ---- | ------ | ---------- | -------------------------- |
| 1    | [ ]    | 3          | DTOs + service skeleton    |
| 2    | [ ]    | 5          | Pass 1 concept extraction  |
| 3    | [ ]    | 3          | CSV resolution layer       |
| 4    | [ ]    | 5          | Pass 2 assembly + presets  |
| 5    | [ ]    | 2          | State + nav registration   |
| 6    | [ ]    | 5          | `TagBuilderView` component |

**Total:** 23 points (original plan estimate: 13 - overrun expected, driven by the two-phase UX and preset research).

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] After Step 1 complete
- [ ] After Step 2 complete
- [ ] After Step 3 complete
- [ ] After Step 4 complete
- [ ] After Step 5 complete
- [ ] After Step 6 complete

---

## Open Risks

1. **Model preset vocabulary drift** - Danbooru-derived models (Pony / Illustrious / NoobAI / Anima) evolve quality-tag conventions frequently. Mitigation: isolate preset constants in one dictionary with a "verify at implementation time" comment; user can override via system-prompt edits.
2. **Pass 1 output parsing fragility** - small local models often return prose. Mitigation: JSON-first, line-parse fallback, tolerate empty results without throwing.
3. **NSFW gate is non-exhaustive** - a deny-list cannot catch every NSFW tag. Mitigation: document the limitation in the view's info-content; Phase 10 can introduce a category-tagging column on `SystemPromptTemplate` / tag metadata if needed.
4. **Performance** - `CsvService.SearchTags` re-reads `danbooru.csv` on every call. If Pass 1 yields 10 concepts, that's 10 full passes. Mitigation: if profiling shows this is a problem, add an in-memory tag index to `CsvService` as a detour (new phase 2.5) rather than bloating this phase.

---

## Phase Summary

_To be filled in on completion._
