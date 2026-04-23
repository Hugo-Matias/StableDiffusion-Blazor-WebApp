# Phase 4 - Random Inspiration + Roulette

## Status

**Phase:** 4
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started

---

## Objective

Add a creative-exploration view offering two sub-modes under a single nav entry ("Inspiration"):

1. **Inspire Me** (LLM-driven): The user picks a few loose constraints (Genre / Mood / Complexity) and the LLM generates a novel prompt.
2. **Roulette** (deterministic): Spin animation; the app randomly combines one entry from each of several wildcard collections (subject, style, lighting, mood, location) into a prompt. No LLM call. Optional "Refine with LLM" secondary action.

Both modes share the same output pane and the same "Copy / Save / Send to Process" actions used by Phase 3.

---

## Context

### Dependencies on prior phases

- **Phase 1** required (nav, composition root, AppState LLM sub-state).
- **Phase 3** strongly recommended, specifically the **idempotent-seeding refactor in Step 3 of Phase 3**. If Phase 3 hasn't merged, replicate that seeding change here as a prerequisite step or the new default template will not seed on existing installs.

### Existing infrastructure to reuse

- `WildcardService` (`BlazorWebApp/Services/WildcardService.cs`):
  - `Task<WildcardEntry?> GetRandomEntry(string collectionName)` - equal-probability.
  - `Task<WildcardEntry?> GetRandomEntryWeighted(string collectionName)` - weighted.
  - `Task<List<string>> GetAllEntryValues(string collectionName)` - full list.
  - Collection resolution supports `"category/collection"` or `"collection"` (see `ResolveCollection`, lines 80-108).
  - Underlying data: `WildcardCollection` + `WildcardEntry` EF entities populated via existing wildcard tab.
- `OllamaService.SendChatMessage` for Inspire Me.
- Seed pattern: append to `OllamaService.GetDefaultTemplates()`.
- Parent `_selectedModel` + `OllamaOptions` passed as parameters.
- Same copy/save/send-to-process wiring used by Phases 2 and 3.

### Roulette collection naming contract

The Roulette sub-mode expects these wildcard collection names to exist. If missing, the spin falls back to a hard-coded default list embedded in the view so the feature always works out-of-the-box.

| Slot     | Collection name (primary) | Fallback hard-coded examples (subset)               |
| -------- | ------------------------- | --------------------------------------------------- |
| Subject  | `roulette/subject`        | "lone warrior", "young mage", "cybernetic wanderer" |
| Style    | `roulette/style`          | "oil painting", "cyberpunk anime", "impressionist"  |
| Lighting | `roulette/lighting`       | "golden hour", "neon glow", "overcast"              |
| Mood     | `roulette/mood`           | "melancholic", "triumphant", "uneasy"               |
| Location | `roulette/location`       | "abandoned station", "mountain peak", "deep forest" |

The view does not auto-create these collections; it only consults them and falls back silently. Phase 10 "Polish & Settings" can add a one-click "Create default Roulette collections" action.

### Architectural rules

- Roulette is pure C# randomness; no LLM. This keeps the feature snappy and free.
- "Refine with LLM" is a separate explicit button (not automatic).
- All cross-view notifications through `IEventService`. Send-to-Process uses the same `PendingRestore` pattern as Phases 2/3.
- No EF migration (`AppState.Prompts.LLM.Inspiration` is JSON-persisted).

---

## Execution Checklist

### Step 1: `AppState.Prompts.LLM.Inspiration` sub-state + nav registration

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `BlazorWebApp/Models/AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMInspiration
  {
      public string Mode { get; set; } = "inspire"; // "inspire" | "roulette"
      public string Genre { get; set; } = string.Empty;
      public string Mood { get; set; } = string.Empty;
      public string Complexity { get; set; } = "Standard"; // "Simple" | "Standard" | "Rich"
      public bool WeightedRoulette { get; set; } = true;
  }
  ```
- [ ] Add `public AppStatePromptsLLMInspiration Inspiration { get; set; } = new();` on `AppStatePromptsLLM`.
- [ ] In `LLMNavMenu.razor` `Items`:
  ```csharp
  new("inspiration", "Inspiration", Icons.Material.Filled.Casino),
  ```
- [ ] Extend `switch (_activeViewId)` in `LLMToolsTab.razor` with `"inspiration"` → `<InspirationView SelectedModel="@_selectedModel" OnSendToProcess="HandleSendToProcess" />`.

#### Success Criteria

- Nav entry appears; empty view loads.
- Active view persists.

---

### Step 2: Seed `Inspiration` default system-prompt template

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] **Pre-check**: confirm Phase 3 Step 3 (per-name idempotent seeding) is merged. If not, replicate that change here before adding new defaults.
- [ ] Append to `OllamaService.GetDefaultTemplates()`:
  ```csharp
  new SystemPromptTemplate
  {
      Name = "Inspiration",
      Description = "Generates novel, creative prompts from loose genre/mood/complexity constraints.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You are a creative prompt generator for image-generation models. Given loose constraints, produce ONE " +
              "novel, evocative prompt. Prioritize unusual combinations and concrete visual detail over abstract adjectives. " +
              "Complexity guide: Simple = one clear subject + 2-3 modifiers; Standard = subject + setting + mood + style; " +
              "Rich = dense layered scene with atmosphere, lighting, and composition cues. " +
              "Return only the prompt - no commentary, no labels." },
          new() { Role = "user", Content =
              "Genre: {genre}\nMood: {mood}\nComplexity: {complexity}\n\nReturn the prompt only." }
      }
  }
  ```
- [ ] Placeholder substitution client-side (same `string.Replace` pattern as Phase 3).

#### Success Criteria

- Fresh install seeds the `Inspiration` template.
- Existing install picks it up once the per-name seeding from Phase 3 is active.

---

### Step 3: `InspirationView.razor` skeleton + Inspire Me mode

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/InspirationView.razor`.
- [ ] Parameters: `[Parameter] public string? SelectedModel { get; set; }`, `[Parameter] public EventCallback<string> OnSendToProcess { get; set; }`.
- [ ] Injects: `IOllamaService Ollama`, `IWildcardService Wildcards`, `IDatabaseService DB`, `IStateService State`, `ISnackbar Snackbar`, `IJSRuntime JS`, `IInfoService InfoService`.
- [ ] Top-level segmented control (`MudToggleGroup` or `MudTabs`) switches `_mode` between "inspire" and "roulette"; persisted to `AppState.Prompts.LLM.Inspiration.Mode`.
- [ ] **Inspire Me panel** (`_mode == "inspire"`):
  - `MudSelect` bound to `_genre`: suggested values ("Any", "Fantasy", "Sci-Fi", "Horror", "Slice of Life", "Cyberpunk", "Historical"). User can type custom (`Clearable="true"`, `Strict="false"`).
  - `MudSelect` bound to `_mood`: ("Any", "Serene", "Tense", "Melancholic", "Playful", "Ominous", "Heroic").
  - `MudToggleGroup` bound to `_complexity`: "Simple" | "Standard" | "Rich".
  - `Inspire Me` button → calls `InspireAsync`:
    ```csharp
    async Task InspireAsync()
    {
        var tpl = await LoadTemplateAsync("Inspiration");
        if (tpl == null || string.IsNullOrWhiteSpace(SelectedModel)) { Snackbar.Add("Missing template or model", Severity.Error); return; }
        _isWorking = true;
        try
        {
            var messages = tpl.Messages.Select(m => new OllamaChatMessage
            {
                Role = m.Role,
                Content = m.Content
                    .Replace("{genre}", string.IsNullOrWhiteSpace(_genre) ? "Any" : _genre)
                    .Replace("{mood}", string.IsNullOrWhiteSpace(_mood) ? "Any" : _mood)
                    .Replace("{complexity}", _complexity)
            }).ToList();
            var options = new OllamaOptions { Temperature = 1.1f, TopP = 0.95f }; // nudge creativity
            var response = await Ollama.SendChatMessage(SelectedModel, messages, options);
            _result = response?.Message?.Content?.Trim() ?? string.Empty;
        }
        finally { _isWorking = false; }
    }
    ```
- [ ] Result panel identical to Phase 3 (readonly text + Copy / Save as Prompt / Send to Process).

#### Success Criteria

- Clicking Inspire produces distinct prompts across clicks (varies with seed / temperature).
- Genre/Mood/Complexity actually influence output (spot-check "Horror + Melancholic + Rich" vs "Sci-Fi + Playful + Simple").

---

### Step 4: Roulette sub-mode

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] `_mode == "roulette"` renders:
  - A row of 5 "slot boxes" (subject / style / lighting / mood / location) each showing its current value as a `MudChip` with an icon.
  - A `MudSwitch` for weighted vs equal random (`_weighted`).
  - A **Spin** button.
  - Below: final assembled prompt in a `MudTextField` (read-only, multiline, 2 rows).
  - Actions bar: Copy / Save / Send to Process / **Refine with LLM** (disabled until a spin has happened).
- [ ] Spin algorithm:

  ```csharp
  async Task SpinAsync()
  {
      _isSpinning = true;
      try
      {
          // Animation: cycle random values N times before settling (pure UX)
          var slots = new[] { "subject", "style", "lighting", "mood", "location" };
          for (int cycle = 0; cycle < 8; cycle++) // ~400ms of flicker
          {
              foreach (var s in slots) _slotValues[s] = await PickAsync($"roulette/{s}", _weighted) ?? PickFromFallback(s);
              StateHasChanged();
              await Task.Delay(50);
          }
          // Final landing values (last iteration is kept)
          _result = string.Join(", ", _slotValues.Values.Where(v => !string.IsNullOrWhiteSpace(v)));
      }
      finally { _isSpinning = false; }
  }

  async Task<string?> PickAsync(string collection, bool weighted)
  {
      try
      {
          var entry = weighted
              ? await Wildcards.GetRandomEntryWeighted(collection)
              : await Wildcards.GetRandomEntry(collection);
          return entry?.Value;
      }
      catch { return null; }
  }

  static readonly Dictionary<string, string[]> Fallbacks = new()
  {
      ["subject"]  = new[] { "lone warrior", "young mage", "cybernetic wanderer", "celestial dancer" },
      ["style"]    = new[] { "oil painting", "cyberpunk anime", "impressionist", "photorealistic" },
      ["lighting"] = new[] { "golden hour", "neon glow", "overcast", "candlelight" },
      ["mood"]     = new[] { "melancholic", "triumphant", "uneasy", "serene" },
      ["location"] = new[] { "abandoned station", "mountain peak", "deep forest", "floating city" },
  };
  string PickFromFallback(string slot) => Fallbacks[slot][Random.Shared.Next(Fallbacks[slot].Length)];
  ```

- [ ] **Refine with LLM** button (only visible after a spin): takes `_result`, sends to Ollama with the `Enhance` default template (existing), replaces `_result` with the refined version. Reuses `OllamaService.ExpandPrompt` if signatures allow, otherwise mirrors `ProcessView`'s logic.

#### Success Criteria

- Spin produces 5 distinct slot values; final prompt is a comma-joined string.
- Flicker animation visible but quick (<500ms total).
- Missing wildcard collections fall back silently without errors in the console.
- Refine button produces a more polished variant of the spun output.

---

### Step 5: Info content + state persistence hooks

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Persist `_mode`, `_genre`, `_mood`, `_complexity`, `_weighted` to `AppState.Prompts.LLM.Inspiration` on `OnBlur` / `ValueChanged` handlers; debounce only the text-ish fields (Genre / Mood allow typing) by calling `State.SaveState()` on blur.
- [ ] Register `InfoContent`:
  - Title: "Inspiration"
  - Overview: "Generate creative prompts either from loose constraints (Inspire Me) or from random wildcard spins (Roulette)."
  - Tips: "Roulette uses `roulette/subject`, `roulette/style`, `roulette/lighting`, `roulette/mood`, `roulette/location` collections if they exist.", "Use 'Refine with LLM' to polish a roulette result.", "Complexity affects Inspire Me verbosity."

#### Success Criteria

- All selectors persist across reloads.
- Info panel shows the relevant copy while the view is active.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                       |
| ---- | ------ | ---------- | ------------------------------------------- |
| 1    | [ ]    | 1          | AppState sub-state + nav entry              |
| 2    | [ ]    | 1          | Seed `Inspiration` default template         |
| 3    | [ ]    | 2          | View skeleton + Inspire Me mode             |
| 4    | [ ]    | 3          | Roulette sub-mode with wildcard integration |
| 5    | [ ]    | 1          | Persistence + info content                  |

**Total:** 8 points (original plan estimate: 3 - overrun from Roulette animation + fallback complexity).

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

1. **Wildcard collections don't exist out-of-the-box.** Users will see the fallback list on first use. Acceptable - documented. Phase 10 can add a "Seed default Roulette collections" one-click action.
2. **LLM with `Temperature=1.1` can produce incoherent outputs.** Mitigation: expose a "Creativity" slider in Phase 10 polish pass; for now, value is hard-coded as a starting point.
3. **Roulette animation jank on slow machines.** 8 × 50ms delays are cheap; if Blazor re-render cost is high, reduce cycles or drop animation entirely.
4. **Fallback list is hard-coded and English-only.** Acceptable; users with i18n needs would populate the wildcard collections.

---

## Phase Summary

_To be filled in on completion._
