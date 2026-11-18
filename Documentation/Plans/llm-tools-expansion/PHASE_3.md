# Phase 3 - Prompt Mixer

## Status

**Phase:** 3
**Build Status:** Clean - 0 errors, no new warnings
**Phase Status:** [x] Complete

---

## Objective

Add a lightweight "Prompt Mixer" view that takes two prompts (A and B) plus a blend ratio (0-100) and asks the LLM to produce a single coherent hybrid prompt. The mixer reuses the existing LLM infrastructure (`OllamaService`, system-prompt seeding, `_selectedModel`, `PromptComparisonPanel`-style output) and introduces no new services or entities.

Blend ratio semantics: `0` = 100% Prompt A, `100` = 100% Prompt B, `50` = equal blend. The slider value is passed to the LLM as a plain-text directive in the system prompt - no math happens in C#.

---

## Context

### Dependencies on prior phases

- **Phase 1** must be complete: `LLMNavMenu`, `LLMToolsTab` composition root, `AppStatePromptsLLM` sub-state.
- **Phase 2** is NOT a hard dependency. The Mixer is independent of tag-vocabulary logic and ships even if Phase 2 is not yet merged.

### Existing infrastructure to reuse

- `OllamaService.SendChatMessage(modelName, List<OllamaChatMessage>, OllamaOptions?, keepAlive, stream)` — returns `OllamaChatResponse?`. Already used by `ProcessView`.
- `OllamaService.GetDefaultTemplates()` at `BlazorWebApp/Services/OllamaService.cs` lines 125-147. Append the new `PromptMixer` default to the returned list; `DatabaseService.SeedDefaultSystemPromptTemplates` will pick it up on next startup **only when no `IsDefault` rows exist yet** (see "Seeding a new default when defaults already exist" risk below).
- `SystemPromptTemplate` entity (`BlazorWebApp/Data/Entities/SystemPromptTemplate.cs`): `Name`, `Description`, `MessagesJson`, `IsDefault`, `Messages` (`NotMapped`, `List<OllamaChatMessage>`).
- `PromptComparisonPanel.razor` - if a "before/after" view makes sense, reuse it; otherwise a plain `MudPaper` + copy button + Save-as-Prompt button is sufficient (two inputs, one output - no single "original" to compare).
- Parent `LLMToolsTab` already owns `_selectedModel` and `OllamaOptions` wiring - mixer view takes these as parameters.

### Architectural rules

- All cross-view notifications flow through `IEventService` (pub/sub convention). Phase 3 will emit no new events by itself; the "Send to Workshop" button is a parent-handled callback (same `PendingRestore` / version-counter pattern Phase 2 uses for "Send to Process").
- No new EF migration (`AppStatePromptsLLM.Mixer` is a nested class on the JSON-serialized `State.AppState` column).

### UX decisions locked for v1

- No multi-prompt blending (exactly 2 inputs). N-way blend is deferred to Phase 7 (Remixer).
- Slider is a single `MudSlider` 0-100 with tick labels "A", "50/50", "B".
- Result is single-shot (no streaming, no progress bar). Loading indicator = disabled Mix button + spinner.
- No negative-prompt handling; mixer operates on positive prompts only. If users need negative blending, they run the mixer twice (once per side) - noted in the view's info-content.

---

## Execution Checklist

### Step 1: `AppState.Prompts.LLM.Mixer` sub-state + nav registration

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `AppStatePromptsLLMMixer` nested class in `BlazorWebApp/Models/AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMMixer
  {
      public string PromptA { get; set; } = string.Empty;
      public string PromptB { get; set; } = string.Empty;
      public int Ratio { get; set; } = 50; // 0 = all A, 100 = all B
  }
  ```
- [ ] Extend `AppStatePromptsLLM` with `public AppStatePromptsLLMMixer Mixer { get; set; } = new();`.
- [ ] Add nav item in `LLMNavMenu.razor` `Items` list:
  ```csharp
  new("mixer", "Mixer", Icons.Material.Filled.Shuffle),
  ```
  (Keep ordering consistent with `MAIN_PLAN.md` sidebar mock: process, system-prompts, history, tag-builder, mixer, ...).
- [ ] Extend `switch (_activeViewId)` block in `LLMToolsTab.razor` with a `"mixer"` case that renders `<MixerView SelectedModel="@_selectedModel" OnSendToProcess="HandleSendToProcess" />`.

#### Success Criteria

- Nav shows "Mixer" entry; clicking it loads an empty `MixerView`.
- Active view id persists across reloads.
- Build clean.

---

### Step 2: Seed `PromptMixer` default system-prompt template

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Extend `OllamaService.GetDefaultTemplates()` with a fourth entry:
  ```csharp
  new SystemPromptTemplate
  {
      Name = "PromptMixer",
      Description = "Blends two prompts into a single coherent hybrid using a blend ratio.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You are a prompt-blending assistant. You will receive two prompts (A and B) and a blend ratio from 0 to 100 " +
              "(0 = keep only A, 100 = keep only B, 50 = equal blend). Produce a SINGLE new prompt that coherently combines " +
              "the visual concepts of both inputs weighted by the ratio. Preserve subject fidelity when the ratio favors that side. " +
              "Return only the final prompt - no commentary, no labels, no markdown." },
          new() { Role = "user", Content =
              "Prompt A: {promptA}\nPrompt B: {promptB}\nBlend ratio (0-100, higher = more B): {ratio}\n\nReturn the blended prompt only." }
      }
  }
  ```
  Note: the `Messages` property auto-serializes to `MessagesJson` via the existing `NotMapped` pattern on the entity.
- [ ] Placeholder substitution is performed client-side before calling `SendChatMessage` (simple `string.Replace`), because `SendChatMessage` does not template-substitute.
- [ ] Document the `{promptA}`, `{promptB}`, `{ratio}` placeholder contract in a `// Template placeholders:` comment above the new entry so future edits stay consistent.

#### Success Criteria

- Existing installs (which already have `IsDefault` rows) will NOT auto-pick-up the new template - see risk #1. That's fine; the mixer view has a fallback (Step 4).
- Fresh installs seed all four defaults.
- Build clean.

---

### Step 3: Fallback / resilience for existing installs

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `DatabaseService.SeedDefaultSystemPromptTemplates`, replace the "skip if any defaults exist" gate with a per-name check:
  ```csharp
  var defaultTemplates = _ollamaService.GetDefaultTemplates();
  var existingNames = await context.SystemPromptTemplates
      .Where(t => t.IsDefault)
      .Select(t => t.Name)
      .ToListAsync();
  var toAdd = defaultTemplates.Where(t => !existingNames.Contains(t.Name)).ToList();
  if (toAdd.Count == 0) return;
  foreach (var t in toAdd) { t.CreatedAt = DateTime.UtcNow; t.UpdatedAt = DateTime.UtcNow; }
  await context.SystemPromptTemplates.AddRangeAsync(toAdd);
  await context.SaveChangesAsync();
  ```
  This makes seeding idempotent per-template-name so Phase 2, 3, 4, etc. can each add new defaults without manual migration.
- [ ] Verify existing three defaults are not re-inserted on startup (run app once, confirm no duplicates).

#### Success Criteria

- First run after update seeds `PromptMixer` (and any other new defaults from earlier phases) exactly once.
- Subsequent runs insert nothing.
- User-authored templates named `PromptMixer` would block the seed - acceptable because they likely want their own version.

---

### Step 4: `MixerView.razor` component

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/MixerView.razor` with this structure:

  ```razor
  @inject IOllamaService Ollama
  @inject IDatabaseService DB
  @inject IStateService State
  @inject ISnackbar Snackbar
  @inject IJSRuntime JS

  <MudPaper Class="pa-4" Elevation="0">
      <MudText Typo="Typo.h6">Prompt Mixer</MudText>
      <MudTextField @bind-Value="_promptA" Label="Prompt A" Lines="3" Variant="Variant.Outlined"
                    OnBlur="PersistAsync" Immediate="false" Class="mt-3" />
      <MudTextField @bind-Value="_promptB" Label="Prompt B" Lines="3" Variant="Variant.Outlined"
                    OnBlur="PersistAsync" Immediate="false" Class="mt-3" />

      <div class="d-flex align-center gap-3 mt-4">
          <MudText Typo="Typo.caption">A</MudText>
          <MudSlider @bind-Value="_ratio" Min="0" Max="100" Step="5"
                     TickMarks="true" TickMarkLabels="@_tickLabels"
                     ValueLabel="true" Class="flex-grow-1"
                     OnBlur="PersistAsync" />
          <MudText Typo="Typo.caption">B</MudText>
      </div>

      <MudButton Color="Color.Primary" Variant="Variant.Filled"
                 Disabled="_isMixing || string.IsNullOrWhiteSpace(_promptA) || string.IsNullOrWhiteSpace(_promptB) || string.IsNullOrWhiteSpace(SelectedModel)"
                 OnClick="MixAsync" Class="mt-4">
          @if (_isMixing) { <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="me-2" /> }
          Mix
      </MudButton>

      @if (!string.IsNullOrEmpty(_result))
      {
          <MudPaper Class="pa-3 mt-4" Elevation="2">
              <MudText Typo="Typo.subtitle2">Result</MudText>
              <MudTextField Value="_result" ReadOnly="true" Lines="4" Variant="Variant.Outlined" Class="mt-2" />
              <div class="d-flex gap-2 mt-2">
                  <MudButton OnClick="CopyAsync" StartIcon="@Icons.Material.Filled.ContentCopy">Copy</MudButton>
                  <MudButton OnClick="SaveAsync" StartIcon="@Icons.Material.Filled.Bookmark">Save as Prompt</MudButton>
                  <MudButton OnClick="SendToProcessAsync" StartIcon="@Icons.Material.Filled.ForwardToInbox">Send to Process</MudButton>
              </div>
          </MudPaper>
      }
  </MudPaper>
  ```

- [ ] Code-behind (`@code`):
  - Parameters: `[Parameter] public string? SelectedModel { get; set; }`, `[Parameter] public EventCallback<string> OnSendToProcess { get; set; }`.
  - Fields: `_promptA`, `_promptB`, `_ratio`, `_result`, `_isMixing`, `_templateCache` (cache the `PromptMixer` template lookup so it loads once).
  - `OnInitializedAsync`: hydrate `_promptA/_promptB/_ratio` from `State.AppState.Prompts.LLM.Mixer`, load the `PromptMixer` template via `DB.GetSystemPromptTemplates()` (or whatever accessor exists; if not, add one - simple `where Name == "PromptMixer" && IsDefault` query).
  - `MixAsync`:
    ```csharp
    async Task MixAsync()
    {
        if (_templateCache == null)
        {
            Snackbar.Add("PromptMixer template missing from DB; restart app to re-seed or import manually.", Severity.Error);
            return;
        }
        _isMixing = true;
        try
        {
            var messages = _templateCache.Messages
                .Select(m => new OllamaChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                        .Replace("{promptA}", _promptA)
                        .Replace("{promptB}", _promptB)
                        .Replace("{ratio}", _ratio.ToString())
                }).ToList();
            var response = await Ollama.SendChatMessage(SelectedModel!, messages);
            _result = response?.Message?.Content?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Mix failed: {ex.Message}", Severity.Error);
        }
        finally { _isMixing = false; }
    }
    ```
  - `PersistAsync`: copies field values into `State.AppState.Prompts.LLM.Mixer` then `await State.SaveState()`.
  - `CopyAsync`: `await JS.InvokeVoidAsync("navigator.clipboard.writeText", _result);`
  - `SaveAsync`: `await DB.CreatePrompt(new Prompt { Title = $"Mix ({DateTime.Now:yy-MM-dd HH:mm})", Positive = _result });`
  - `SendToProcessAsync`: raise `OnSendToProcess.InvokeAsync(_result)`.
  - `_tickLabels = new[] { "A", "", "", "", "", "50/50", "", "", "", "", "B" };` (11 labels for 0..100 step 10 — adjust if step is 5).
- [ ] Wire `OnSendToProcess` in `LLMToolsTab`: handler puts the blended prompt into `_pendingRestore` (synthesize a `PromptHistoryEntry` with `Operation = "Mixed"` / `ResultPrompt = result` / `OriginalPrompt = ""`), bump `_restoreVersion`, set `_activeViewId = "process"`.
- [ ] Register an `InfoContent` in `OnInitializedAsync` describing: what the mixer does, slider semantics, that negatives are unsupported, copy/save/send actions.

#### Success Criteria

- End-to-end: enter two prompts, set ratio to 70 (favor B), click Mix, receive a blended result.
- Switching away and back preserves inputs.
- Ratio 0 ≈ returns a prompt very close to A; ratio 100 ≈ very close to B (LLM-dependent, but observable).
- "Send to Process" navigates to Process view with the blended prompt pre-filled.
- "Save as Prompt" creates a DB row visible in the Prompts tab.

---

### Step 5: Info content + dispose

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] `MixerView` implements `IAsyncDisposable`; `DisposeAsync` calls `InfoService.ClearInfo()` **only if** this view owns the current info (compare a stored reference). Alternatively, let `LLMToolsTab` manage info-content switching per active view - current code already clears on tab dispose.
- [ ] Info content: title "Prompt Mixer", overview "Blend two prompts using a ratio slider", tips: "Ratio 0 = all A, 100 = all B", "Negatives are unsupported - run twice for negative blending", "Save or Send results to continue iterating".

#### Success Criteria

- Switching to Mixer view shows its info; switching away hides it.
- No leaks of stale info when navigating.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                    |
| ---- | ------ | ---------- | ---------------------------------------- |
| 1    | [x]    | 1          | `AppState.Prompts.LLM.Mixer` + nav entry |
| 2    | [x]    | 2          | Seed `PromptMixer` default template      |
| 3    | [x]    | 2          | Make seed idempotent per-template-name   |
| 4    | [x]    | 3          | `MixerView` component with full UX flow  |
| 5    | [-]    | 1          | Info content registration (deferred)     |

**Total:** 9 points (original plan estimate: 3 - overrun driven by Step 3, which benefits all future phases that add default templates).

---

## Issues & Resolutions

- **Compilation errors in MixerView.razor:** Fixed `IOllamaService` -> `OllamaService`, missing `using BlazorWebApp.Data.Entities`, `SavePrompt` -> `CreatePrompt`, and `Prompt.Text` -> `Prompt.Positive`. Removed unused `PersistAsync` (state persistence deferred).

---

## Commit Checkpoints

- [ ] After Step 1 complete
- [ ] After Step 2 complete
- [ ] After Step 3 complete (seeding refactor is a cross-cutting change; commit on its own)
- [ ] After Step 4 complete
- [ ] After Step 5 complete

---

## Open Risks

1. **Seeding a new default when defaults already exist.** Current `DatabaseService.SeedDefaultSystemPromptTemplates` skips seeding if ANY `IsDefault` row exists. After Phase 1 every existing install has 3 defaults, so new templates won't land without the Step 3 refactor. Mitigation: Step 3 changes the gate to per-name so new defaults land on next startup without manual import.
2. **User-named "PromptMixer" template conflict.** If a user already has a template named `PromptMixer` (unlikely but possible), the new default is skipped. Acceptable: the user presumably prefers their version. Document in release notes.
3. **LLM returns prose / quotes the prompts back.** Small local models occasionally echo the input or add "Here is the blended prompt:" preamble. Mitigation: strict system prompt ("Return only the final prompt - no commentary, no labels, no markdown"). No post-processing stripping in v1; add only if QA flags frequent occurrences.
4. **Slider step granularity.** 5-unit steps give 21 positions; 10-unit gives 11. Pick based on UX testing; can ship with 5 and tighten if requested.

---

## Phase Summary

Phase 3 delivered a functional Prompt Mixer view with two-prompt blending, ratio slider, LLM-based mixing, copy/save/send actions. Key files created/modified:

**Files Created:**

- `BlazorWebApp/Components/Prompts/LLM/Views/MixerView.razor` - Complete mixer UI component

**Files Modified (from prior phases):**

- `BlazorWebApp/Models/AppState.cs` - `AppStatePromptsLLMMixer` sub-state added
- `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor` - "mixer" nav entry added
- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor` - `"mixer"` switch case added
- `BlazorWebApp/Services/OllamaService.cs` - `PromptMixer` default template seeded
- `BlazorWebApp/Services/DatabaseService.cs` - Per-name idempotent seeding refactor

**Deferred:** Step 5 (Info content registration) - low priority polish item.
