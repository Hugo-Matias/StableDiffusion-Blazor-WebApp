# Phase 7 - Prompt Remixer

## Status

**Phase:** 7
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started — deferred for integration into Phase 3 (Prompt Mixer)

---

## Objective

Multi-select from the existing saved `Prompt` library and combine the selected entries into one output prompt via one of three methods:

1. **Shuffle Elements** - tokenize every selected prompt by comma, flatten, shuffle, rejoin. Pure C#, no LLM.
2. **Combine Parts** - concatenate all selected positives in selection order, dedupe exact tokens. Pure C#.
3. **LLM Intelligent Mix** - send all selected prompts to the LLM under the `PromptRemixer` system prompt and receive a single coherent hybrid.

The output panel reuses the familiar Copy / Save / Send-to-Process actions.

**NOTE (2026-04-26):** This feature will be integrated into the existing **Prompt Mixer** tool (Phase 3) rather than implemented as a standalone `RemixerView`. The integration follows the same Single/Batch logic pattern used by the Process view:

- **Single mode**: Pick one saved prompt and remix it with LLM guidance.
- **Batch mode**: Multi-select several saved prompts and combine/shuffle/LLM-mix them together.

This keeps the Mixer as the single entry point for all prompt-blending operations and avoids feature fragmentation across tools. When implementing, extend `MixerView.razor` directly rather than creating a new view component.

---

## Context

### Dependencies on prior phases

- **Phase 1** required.
- **Phase 3 Step 3** (idempotent seeding) required for the LLM-mix default template to land on existing installs.
- **Phase 6** NOT required; the remixer operates on `Prompt` library entries, not on templates.

### Existing infrastructure to reuse

- `IDatabaseService.GetPrompts()` (or whatever the canonical list-prompts accessor is - verify during implementation; `IDatabaseService` has prompt CRUD per the context dump).
- `OllamaService.SendChatMessage` for the LLM method.
- `OllamaService.GetDefaultTemplates()` extended with `PromptRemixer`.
- `PromptHistoryEntry` pattern (via `LLMToolsTab._pendingRestore`) for Send-to-Process.

### Tokenization contract (Shuffle / Combine)

- Split on `,`; trim whitespace; drop empties.
- Deduplicate case-insensitively but preserve the first-seen casing.
- Preserve weight syntax tokens (e.g., `(beautiful:1.2)`) as single indivisible units - do NOT split on parentheses.

### Architectural rules

- No new entity.
- `AppState.Prompts.LLM.Remixer` holds last selection IDs + method.
- All view-switching via `IEventService`.

---

## Execution Checklist

### Step 1: `AppState.Prompts.LLM.Remixer` + nav

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMRemixer
  {
      public List<int> SelectedPromptIds { get; set; } = new();
      public string Method { get; set; } = "shuffle"; // "shuffle" | "combine" | "llm"
  }
  ```
  Attach to `AppStatePromptsLLM`.
- [ ] Nav item: `new("remixer", "Remixer", Icons.Material.Filled.Blender),`.
- [ ] Switch case in `LLMToolsTab`.

#### Success Criteria

- Nav entry appears; view loads empty.
- Last selection + method persist.

---

### Step 2: Seed `PromptRemixer` default template

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Append to `OllamaService.GetDefaultTemplates()`:
  ```csharp
  new SystemPromptTemplate
  {
      Name = "PromptRemixer",
      Description = "Intelligently combines multiple saved prompts into one cohesive output.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You are a prompt-blending assistant. You will receive an arbitrary number of prompts. Produce a SINGLE new " +
              "prompt that coherently combines their visual concepts. Avoid literal concatenation. Prefer concrete, " +
              "image-model-friendly phrasing. Resolve conflicts by picking the dominant theme. Return only the final " +
              "prompt - no commentary, no labels, no markdown." },
          new() { Role = "user", Content = "{prompts_block}" }
      }
  }
  ```
- [ ] Placeholder `{prompts_block}` is replaced client-side with:
  ```
  Prompt 1: <positive1>
  Prompt 2: <positive2>
  ...
  Prompt N: <positiveN>
  ```

#### Success Criteria

- Template seeds correctly (requires Phase 3 Step 3 refactor).

---

### Step 3: `RemixerView.razor` - selection grid + methods

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/RemixerView.razor`.
- [ ] Layout:
  - **Top bar**: search `MudTextField` (filter by `Title`/`Category`), `MudSelect` for Category filter, `Select All` / `Clear` buttons, selection counter badge.
  - **Selection grid**: `MudTable<Prompt>` or `MudSimpleTable` with selectable rows (checkbox column). Show `Title`, `Category`, first 60 chars of `Positive`. Persist selected IDs to `AppState.Prompts.LLM.Remixer.SelectedPromptIds`.
  - **Method selector**: `MudToggleGroup` for "Shuffle Elements" | "Combine Parts" | "LLM Mix".
  - **Run** button (enabled when ≥ 2 prompts selected; disabled for LLM mode if `SelectedModel` unset).
  - **Output panel**: same as Phases 2-5 (readonly text + Copy / Save as Prompt / Send to Process).
- [ ] Tokenization helper (private static):

  ```csharp
  static List<string> Tokenize(string s)
  {
      // Split on commas that are NOT inside parentheses.
      var tokens = new List<string>();
      int depth = 0; var buf = new StringBuilder();
      foreach (var ch in s)
      {
          if (ch == '(') depth++;
          else if (ch == ')') depth = Math.Max(0, depth - 1);
          if (ch == ',' && depth == 0)
          {
              var t = buf.ToString().Trim();
              if (!string.IsNullOrEmpty(t)) tokens.Add(t);
              buf.Clear();
          }
          else buf.Append(ch);
      }
      var tail = buf.ToString().Trim();
      if (!string.IsNullOrEmpty(tail)) tokens.Add(tail);
      return tokens;
  }

  static List<string> DedupePreserveFirst(IEnumerable<string> items)
  {
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var result = new List<string>();
      foreach (var i in items) if (seen.Add(i)) result.Add(i);
      return result;
  }
  ```

- [ ] Shuffle method:
  ```csharp
  string ShuffleRun(IEnumerable<Prompt> selected)
  {
      var tokens = selected.SelectMany(p => Tokenize(p.Positive ?? "")).ToList();
      var deduped = DedupePreserveFirst(tokens);
      for (int i = deduped.Count - 1; i > 0; i--)
      {
          int j = Random.Shared.Next(i + 1);
          (deduped[i], deduped[j]) = (deduped[j], deduped[i]);
      }
      return string.Join(", ", deduped);
  }
  ```
- [ ] Combine method:
  ```csharp
  string CombineRun(IEnumerable<Prompt> selected)
  {
      var tokens = selected.SelectMany(p => Tokenize(p.Positive ?? ""));
      return string.Join(", ", DedupePreserveFirst(tokens));
  }
  ```
- [ ] LLM method:
  ```csharp
  async Task<string> LlmRunAsync(IList<Prompt> selected)
  {
      var tpl = await LoadTemplateAsync("PromptRemixer");
      if (tpl == null || string.IsNullOrWhiteSpace(SelectedModel)) return string.Empty;
      var block = string.Join("\n", selected.Select((p, i) => $"Prompt {i + 1}: {p.Positive}"));
      var messages = tpl.Messages.Select(m => new OllamaChatMessage
      {
          Role = m.Role,
          Content = m.Content.Replace("{prompts_block}", block)
      }).ToList();
      var response = await Ollama.SendChatMessage(SelectedModel, messages);
      return response?.Message?.Content?.Trim() ?? string.Empty;
  }
  ```
- [ ] Run dispatcher:
  ```csharp
  async Task RunAsync()
  {
      var selected = _prompts.Where(p => _selectedIds.Contains(p.Id)).ToList();
      if (selected.Count < 2) { Snackbar.Add("Select at least 2 prompts.", Severity.Warning); return; }
      _isRunning = true;
      try
      {
          _output = _method switch
          {
              "shuffle" => ShuffleRun(selected),
              "combine" => CombineRun(selected),
              "llm"     => await LlmRunAsync(selected),
              _ => string.Empty
          };
      }
      finally { _isRunning = false; }
  }
  ```

#### Success Criteria

- Selecting 2 prompts + Shuffle yields a randomized flat list with no duplicates.
- Combine method preserves order per prompt (not shuffled).
- LLM Mix returns a coherent narrative output (model-dependent).
- Empty / whitespace-only positives in selection are skipped without error.

---

### Step 4: Info content + polish

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Register `InfoContent`:
  - Title: "Remixer"
  - Overview: "Pick multiple saved prompts and combine them by shuffle, concat, or LLM mix."
  - Tips: "Shuffle is great for breaking out of ruts.", "Combine respects selection order.", "LLM mix needs a loaded model.", "Tokens inside parentheses (e.g., `(detail:1.2)`) stay intact."
- [ ] Loading state: disable Run button, show `MudProgressCircular`.
- [ ] Empty state (no prompts in library): show `MudAlert` with a link to create one.

#### Success Criteria

- Info visible; empty / loading states render correctly.

---

## Progress Tracking

| Step | Status | Complexity | Notes                |
| ---- | ------ | ---------- | -------------------- |
| 1    | [ ]    | 1          | AppState + nav       |
| 2    | [ ]    | 1          | Seed `PromptRemixer` |
| 3    | [ ]    | 3          | View + three methods |
| 4    | [ ]    | 1          | Info + polish        |

**Total:** 6 points (original plan estimate: 3).

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] After Step 1
- [ ] After Step 2
- [ ] After Step 3 (main implementation)
- [ ] After Step 4

---

## Open Risks

1. **Paren-aware tokenizer edge cases.** Nested parens or unmatched parens could confuse the depth counter. Mitigation: the `Math.Max(0, depth - 1)` guard prevents underflow; malformed input degrades to "treat as plain text" which is acceptable.
2. **Library can be huge.** If a user has thousands of prompts, selection grid performance may suffer. Mitigation: paginate via `MudTable`'s built-in pagination; filter by search/category.
3. **LLM mix hallucinations.** Model can invent concepts not in any selected prompt. Acceptable - it's the "intelligent mix" expectation.
4. **Dedup is case-insensitive.** `BEAUTIFUL` and `beautiful` collapse. First occurrence wins. Documented in info panel.

---

## Phase Summary

_To be filled in on completion._
