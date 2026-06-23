# Phase 9 - Gap Analyzer (Contextual Suggestions)

## Status

**Phase:** 9
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started

---

## Objective

Add a manual-trigger analyzer that inspects a prompt, classifies its detected elements across a fixed taxonomy (subject, style, setting, lighting, mood, composition, quality), identifies which taxonomy slots are missing, and proposes concrete additions. Results render as three chip groups: **Detected** (grayed), **Missing** (red outlined), **Suggestions** (click-to-add). Clicking a suggestion appends it to the prompt; user can then copy, save, or send to Workshop.

No real-time / debounced analysis. Manual trigger only (performance + cost).

---

## Context

### Dependencies on prior phases

- **Phase 1** required.
- **Phase 2** required. The analyzer returns Danbooru-tag suggestions resolved through `TagPromptService.ResolveAsync` so every suggestion chip is a valid CSV tag. If Phase 2 isn't merged, ship a degraded mode that returns raw strings from the LLM and document that tag verification is deferred.
- **Phase 3 Step 3** required (idempotent seeding) to land the new default template.
- **Phase 8** optional (enables "Send to Workshop" button).

### Existing infrastructure to reuse

- `OllamaService.SendChatMessage` for the analysis call.
- `TagPromptService.ResolveAsync` (Phase 2) for turning raw suggestion strings into valid Danbooru tags.
- `OllamaService.GetDefaultTemplates()` extended with `GapAnalyzer`.
- Parent `_selectedModel` + pending-restore for Send-to-Process.

### LLM contract - structured JSON output

The system prompt demands a strict JSON output:

```json
{
  "detected": {
    "subject": ["1girl", "solo"],
    "style": ["anime"],
    "setting": [],
    "lighting": [],
    "mood": [],
    "composition": [],
    "quality": []
  },
  "missing": ["setting", "lighting", "mood", "composition", "quality"],
  "suggestions": {
    "setting": ["cityscape", "forest", "beach"],
    "lighting": ["golden hour", "rim light"],
    "mood": ["serene", "dramatic"],
    "composition": ["rule of thirds", "close-up"],
    "quality": ["masterpiece", "best quality"]
  }
}
```

Parser is resilient - if JSON is malformed, fall back to empty suggestions and surface a snackbar warning rather than crashing.

### Architectural rules

- Manual trigger only (no debounce / real-time).
- Analysis results are ephemeral (not persisted to DB). Only the last-analyzed prompt text + flag is stored in `AppState.Prompts.LLM.GapAnalyzer.LastInput` so the view can restore inputs across reload.
- Chip click → append to the working prompt, comma-separated, deduped.

---

## Execution Checklist

### Step 1: DTOs + service skeleton

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Models/GapAnalyzerModels.cs`:

  ```csharp
  public enum PromptSlot { Subject, Style, Setting, Lighting, Mood, Composition, Quality }

  public class GapAnalysisResult
  {
      public Dictionary<PromptSlot, List<string>> Detected { get; set; } = new();
      public List<PromptSlot> Missing { get; set; } = new();
      public Dictionary<PromptSlot, List<string>> Suggestions { get; set; } = new();
      public string RawResponse { get; set; } = string.Empty;
      public string ModelUsed { get; set; } = string.Empty;
  }
  ```

- [ ] Create `BlazorWebApp/Services/GapAnalyzerService.cs`:

  ```csharp
  public class GapAnalyzerService
  {
      private readonly OllamaService _ollama;
      private readonly IDatabaseService _db;
      private readonly TagPromptService? _tagPrompts; // optional (Phase 2)
      private readonly ILogger<GapAnalyzerService> _log;
      // ctor inject

      public async Task<GapAnalysisResult> AnalyzeAsync(string prompt, string modelName, OllamaOptions? options = null, CancellationToken ct = default)
      {
          var tpl = await LoadTemplateAsync("GapAnalyzer");
          if (tpl == null) throw new InvalidOperationException("GapAnalyzer template missing");
          var messages = tpl.Messages.Select(m => new OllamaChatMessage {
              Role = m.Role,
              Content = m.Content.Replace("{prompt}", prompt)
          }).ToList();
          var response = await _ollama.SendChatMessage(modelName, messages, options ?? new OllamaOptions { Temperature = 0.2f });
          var raw = response?.Message?.Content ?? string.Empty;
          var parsed = ParseJson(raw); // tolerant parse
          parsed.RawResponse = raw;
          parsed.ModelUsed = modelName;

          // Optional: run each suggestion string through TagPromptService.ResolveAsync to ensure it maps
          // to a real Danbooru tag. If it doesn't, keep the raw string but mark it as unverified in UI.
          if (_tagPrompts != null)
              await NormalizeSuggestionsAsync(parsed, ct);

          return parsed;
      }

      private static GapAnalysisResult ParseJson(string raw) { /* tolerant JSON parse; return empty result on failure */ }
  }
  ```

- [ ] Register as scoped in `Program.cs`.

#### Success Criteria

- Build clean.
- Service injectable.

---

### Step 2: Seed `GapAnalyzer` default template

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Append to `OllamaService.GetDefaultTemplates()`:
  ```csharp
  new SystemPromptTemplate
  {
      Name = "GapAnalyzer",
      Description = "Classifies a prompt's detected concepts and suggests additions across a fixed taxonomy.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You are a prompt-analysis assistant for anime/illustration image models. Classify the user's prompt into " +
              "a fixed taxonomy: subject, style, setting, lighting, mood, composition, quality. For each slot, list every " +
              "token from the prompt that falls in it (empty list if none). Then list which slots are EMPTY (the 'missing' " +
              "array). Finally, propose 2-4 short tag-style suggestions per missing slot (Danbooru-friendly vocabulary where " +
              "possible). Respond ONLY with a single JSON object - no prose, no markdown, no code fence. Schema:\n" +
              "{ \"detected\": { <slot>: [strings] }, \"missing\": [slot_names], \"suggestions\": { <slot>: [strings] } }" },
          new() { Role = "user", Content = "Prompt:\n{prompt}\n\nReturn JSON only." }
      }
  }
  ```
- [ ] Depends on Phase 3 Step 3.

#### Success Criteria

- Template seeds.

---

### Step 3: Tolerant JSON parser

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Strip common wrappers (` ```json `, ` ``` `, leading "Here is the analysis:", etc.) before `JsonSerializer.Deserialize`.
- [ ] Map string slot keys → `PromptSlot` enum (case-insensitive).
- [ ] On parse failure, return an empty `GapAnalysisResult` with the raw response preserved for debugging.

#### Success Criteria

- Malformed responses don't crash.
- Valid JSON deserializes into the correct enum-keyed dictionaries.

---

### Step 4: `GapAnalyzerView.razor`

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/GapAnalyzerView.razor`.
- [ ] Layout:
  - **Input panel**: `MudTextField` multi-line for the prompt, plus a `Pull from current prompt` button that reads the current Process-view prompt from `AppState.Prompts.LLM`'s latest history entry or the active Process input (if available).
  - **Analyze** button (primary).
  - **Results panel** (shown after analysis):
    - Three collapsible `MudExpansionPanel`s: "Detected", "Missing", "Suggestions".
    - Detected: `MudChip` per token, grayed, `Close="false"`, grouped by slot (slot label header).
    - Missing: `MudChip` per missing slot name, red outline, clickable to scroll the Suggestions panel to that slot.
    - Suggestions: per-slot chips with `OnClick` that appends the chip label to `_workingPrompt` (dedupe comma-split).
  - **Output panel**: `_workingPrompt` as a `MudTextField` (editable). Same Copy / Save / Send-to-Process / Send-to-Workshop action bar.
- [ ] Append behavior:
  ```csharp
  void AppendTag(string tag)
  {
      var existing = _workingPrompt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Select(t => t.Trim()).ToList();
      if (existing.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))) return;
      existing.Add(tag);
      _workingPrompt = string.Join(", ", existing);
  }
  ```
- [ ] Persist `_inputPrompt` to `AppState.Prompts.LLM.GapAnalyzer.LastInput` on blur.

#### Success Criteria

- Analyze call returns structured results; chips render per taxonomy.
- Clicking a suggestion appends it to the working prompt.
- Duplicate clicks are no-ops.
- Analysis of "1girl, solo, anime" flags setting/lighting/mood/composition/quality as missing.

---

### Step 5: `AppState` + nav + info

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMGapAnalyzer
  {
      public string LastInput { get; set; } = string.Empty;
  }
  ```
- [ ] `public AppStatePromptsLLMGapAnalyzer GapAnalyzer { get; set; } = new();` on `AppStatePromptsLLM`.
- [ ] Nav item: `new("gap-analyzer", "Gap Analyzer", Icons.Material.Filled.FactCheck),`.
- [ ] Switch case in `LLMToolsTab`.
- [ ] Info content: "Manual analysis only - click Analyze to classify your prompt across a fixed taxonomy and see what's missing. Suggestions are click-to-add."

#### Success Criteria

- State persists across reloads.

---

## Progress Tracking

| Step | Status | Complexity | Notes                   |
| ---- | ------ | ---------- | ----------------------- |
| 1    | [ ]    | 2          | DTOs + service skeleton |
| 2    | [ ]    | 1          | Seed template           |
| 3    | [ ]    | 1          | Tolerant JSON parser    |
| 4    | [ ]    | 3          | View + chip UX          |
| 5    | [ ]    | 1          | AppState + nav + info   |

**Total:** 8 points (matches original plan estimate).

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] Step 1 complete
- [ ] Step 2 complete
- [ ] Step 3 complete
- [ ] Step 4 complete
- [ ] Step 5 complete

---

## Open Risks

1. **JSON parse fragility.** Small local models often refuse strict JSON. Mitigation: tolerant parser with regex strip; empty-result fallback; surface a snackbar "Model didn't return valid JSON - try a different model or retry".
2. **Suggestion tokens may not exist in Danbooru CSV.** Mitigation: optional `TagPromptService.ResolveAsync` pass (Phase 2). Mark unverified chips with a warning icon. In degraded mode (Phase 2 not merged), all chips are raw.
3. **Taxonomy is English / anime-centric.** Acceptable v1; Phase 10 can make the taxonomy configurable.
4. **Temperature=0.2** keeps output stable but may produce identical suggestions across runs. Acceptable - predictability over novelty for analysis.

---

## Phase Summary

_To be filled in on completion._
