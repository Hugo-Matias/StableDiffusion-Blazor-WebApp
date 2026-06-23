# Phase 5 - Scene Builder

## Status

**Phase:** 5
**Build Status:** Passed - 0 errors
**Phase Status:** [x] Complete (Step 6 deferred to Phase 10)

---

## Objective

Add a structured-form view ("Scene Builder") that lets the user assemble a prompt by filling in discrete fields (Subject / Environment / Lighting / Mood / Style). Assembly is deterministic in C# (no LLM call by default) so it is fast, free, and predictable. An optional **Refine with LLM** button polishes the assembled prompt; a secondary **Convert to Danbooru Tags** button routes the assembled prompt through `TagPromptService` (Phase 2). A **Save as Style** button writes the current form + output to the Prompt library.

---

## Context

### Dependencies on prior phases

- **Phase 1** required (nav, composition root).
- **Phase 2 optional but recommended** for the Convert-to-Tags button. If Phase 2 is not complete, hide or disable that button.
- **Phase 3 Step 3** recommended for default-template seeding idempotency (used by the Refine button's dedicated template, if we ship one). If Phase 3 isn't merged, this phase reuses the existing `Enhance` default template for Refine and defers the dedicated one to Phase 10.

### Existing infrastructure to reuse

- `OllamaService.SendChatMessage` for the optional LLM refine.
- `TagPromptService.BuildAsync` (from Phase 2) for the tag-conversion path.
- `DatabaseService.CreatePrompt` for Save as Style.
- `Prompt` entity fields: `Title`, `Positive`, `Negative`, `Category`, `Tags`, `IsFavorite`, `IsPinned`, `SortOrder` (see `BlazorWebApp/Data/Entities/Prompt.cs`).
- Parent `_selectedModel` + pending-restore pattern for Send to Process.

### Assembly contract

The deterministic assembler joins non-empty fields in a fixed order with commas, preserving exactly what the user typed (no reordering of internal commas):

```
{Subject}, {Style}, {Environment}, {Lighting}, {Mood}
```

Empty fields are omitted along with their adjacent comma. Rationale for this ordering: subject-first matches how Danbooru-style models parse token weight; style right after subject is the second-most influential slot; environment / lighting / mood trail.

### Architectural rules

- All persistence on the `AppState.Prompts.LLM.SceneBuilder` JSON sub-tree.
- Convert-to-Tags dispatches via direct service call (no event) - it's a same-view action.
- Send to Process / Save as Style follow the same patterns established in Phases 2-4.

---

## Execution Checklist

### Step 1: `AppState.Prompts.LLM.SceneBuilder` sub-state + nav

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [ ] In `BlazorWebApp/Models/AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMSceneBuilder
  {
      public string Subject { get; set; } = string.Empty;
      public string Environment { get; set; } = string.Empty;
      public string Lighting { get; set; } = string.Empty;
      public string Mood { get; set; } = string.Empty;
      public string Style { get; set; } = string.Empty;
      public string? LastAssembled { get; set; } // cached deterministic output
  }
  ```
- [ ] `public AppStatePromptsLLMSceneBuilder SceneBuilder { get; set; } = new();` on `AppStatePromptsLLM`.
- [ ] `LLMNavMenu.Items` → `new("scene-builder", "Scene Builder", Icons.Material.Filled.Landscape),`.
- [ ] `LLMToolsTab` switch case → `<SceneBuilderView SelectedModel="@_selectedModel" OnSendToProcess="HandleSendToProcess" />`.

#### Success Criteria

- Nav entry appears; view loads.
- State persists.

---

### Step 2: Deterministic assembly + view skeleton

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/SceneBuilderView.razor`.
- [ ] Layout: vertical stack of 5 `MudTextField`s, each with a helper label explaining the slot. Example:
  - Subject: "Who or what is the focus? (e.g., young knight, robot chef)"
  - Environment: "Where is the scene? (e.g., misty forest, neon alley)"
  - Lighting: "How is it lit? (e.g., golden hour, rim light, candlelit)"
  - Mood: "Emotional tone (e.g., tense, whimsical, serene)"
  - Style: "Artistic direction (e.g., watercolor, cyberpunk anime, photorealistic)"
- [ ] Button row below fields:
  - `Assemble` (primary) - deterministic, always enabled when any field has content.
  - `Refine with LLM` (secondary) - disabled until an assembled prompt exists AND `SelectedModel` is set.
  - `Convert to Danbooru Tags` (tertiary) - disabled if `TagPromptService` is unavailable (Phase 2 not merged yet).
- [ ] Output panel (same pattern as Phase 3/4): read-only multiline `MudTextField` bound to `_output`, with Copy / Save as Style / Send to Process / Clear.
- [ ] Assembly method (pure C#):

  ```csharp
  string Assemble()
  {
      var parts = new[] { _subject, _style, _environment, _lighting, _mood }
          .Select(p => p?.Trim())
          .Where(p => !string.IsNullOrWhiteSpace(p));
      return string.Join(", ", parts!);
  }

  void OnAssembleClick()
  {
      _output = Assemble();
      _state.Prompts.LLM.SceneBuilder.LastAssembled = _output;
      _ = State.SaveState();
  }
  ```

- [ ] Field changes call a debounced persist method; the assemble button is what commits the output.

#### Success Criteria

- Filling 3 fields and clicking Assemble produces a comma-joined string in the expected order.
- Empty fields are omitted without leaving double commas.
- State persists across reloads.

---

### Step 3: Refine with LLM

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [ ] `RefineAsync` uses the existing `Enhance` default template (already seeded by `OllamaService.GetDefaultTemplates()`). Load it once and cache.
- [ ] Implementation:
  ```csharp
  async Task RefineAsync()
  {
      if (string.IsNullOrWhiteSpace(_output) || string.IsNullOrWhiteSpace(SelectedModel)) return;
      var tpl = await LoadTemplateAsync("Enhance");
      if (tpl == null) { Snackbar.Add("Enhance template missing", Severity.Error); return; }
      _isRefining = true;
      try
      {
          var messages = tpl.Messages.Select(m => new OllamaChatMessage
          {
              Role = m.Role,
              Content = m.Content.Replace("{prompt}", _output)
          }).ToList();
          var response = await Ollama.SendChatMessage(SelectedModel, messages);
          var refined = response?.Message?.Content?.Trim();
          if (!string.IsNullOrWhiteSpace(refined)) _output = refined;
      }
      finally { _isRefining = false; }
  }
  ```
- [ ] If the Enhance template's user message does not contain `{prompt}`, append the current output to the last user message instead. Mirror exactly what `ProcessView` does for Single mode.

#### Success Criteria

- Refine takes the assembled prompt and returns a more polished variant.
- Output panel shows the refined text; a `Revert` button (optional, stretch) lets the user restore the original.

---

### Step 4: Convert to Danbooru Tags (Phase 2 integration)

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [ ] Inject `TagPromptService` optionally. If the DI container cannot resolve it (Phase 2 not merged), catch at component init and disable the button.
- [ ] Implementation:
  ```csharp
  async Task ConvertToTagsAsync()
  {
      if (TagPrompts == null || string.IsNullOrWhiteSpace(_output) || string.IsNullOrWhiteSpace(SelectedModel)) return;
      _isConverting = true;
      try
      {
          var request = new TagBuilderRequest
          {
              Input = _output,
              Verbosity = TagVerbosity.Standard,
              Preset = TagModelPreset.Illustrious, // default; user can re-run in Tag Builder for alternatives
              EnabledCategories = new HashSet<TagCategory> { TagCategory.General, TagCategory.Character, TagCategory.Copyright, TagCategory.Meta },
              AllowNsfw = false,
              GroundingPrompt = null
          };
          var result = await TagPrompts.BuildAsync(request, SelectedModel, null);
          _output = result.FinalPrompt;
      }
      finally { _isConverting = false; }
  }
  ```
- [ ] Snackbar confirmation "Converted to tags - verbosity=Standard, preset=Illustrious. Use the Tag Builder view for finer control."

#### Success Criteria

- Click produces a tag-formatted version of the assembled scene.
- Output updates in place.
- If Phase 2 isn't merged, the button is visibly disabled with a tooltip "Requires Phase 2 (Tag Builder)".

---

### Step 5: Save as Style + Send to Process

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [ ] `SaveAsStyleAsync`:
  ```csharp
  async Task SaveAsStyleAsync()
  {
      if (string.IsNullOrWhiteSpace(_output)) return;
      var title = string.IsNullOrWhiteSpace(_subject)
          ? $"Scene ({DateTime.Now:yy-MM-dd HH:mm})"
          : _subject.Length > 40 ? _subject[..40] : _subject;
      await DB.CreatePrompt(new Prompt
      {
          Title = title,
          Positive = _output,
          Category = "Scene Builder",
          Tags = new List<string>
          {
              $"style:{_style}",
              $"env:{_environment}",
              $"mood:{_mood}"
          }.Where(t => !t.EndsWith(":")).ToList()
      });
      Snackbar.Add("Saved to library", Severity.Success);
  }
  ```
- [ ] `SendToProcessAsync` raises `OnSendToProcess.InvokeAsync(_output)` - parent handles the `_pendingRestore` bump exactly as in Phases 2/3.

#### Success Criteria

- Saved row visible in the Prompts library tab.
- Category is "Scene Builder" so users can filter.
- Send to Process switches view and pre-fills the ProcessView input.

---

### Step 6: Info content

**Complexity:** 1
**Status:** [!] Deferred to Phase 10 (Polish & Settings)

#### Tasks

- [ ] Register `InfoContent`:
  - Title: "Scene Builder"
  - Overview: "Fill in any combination of Subject, Environment, Lighting, Mood, Style; click Assemble to produce a prompt. No LLM call required."
  - Tips: "Empty fields are omitted.", "Assembly order is Subject → Style → Environment → Lighting → Mood for weight priority.", "Convert to Danbooru Tags sends the result through the Tag Builder pipeline.", "Save as Style tags the entry with `style:`, `env:`, `mood:` for easy library filtering."

#### Success Criteria

- Info panel reflects this copy when view is active.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                  |
| ---- | ------ | ---------- | -------------------------------------- |
| 1    | [x]    | 1          | AppState sub-state + nav               |
| 2    | [x]    | 2          | View skeleton + deterministic assembly |
| 3    | [x]    | 1          | Refine with LLM                        |
| 4    | [x]    | 1          | Convert to Danbooru Tags (Phase 2)     |
| 5    | [x]    | 1          | Save as Style + Send to Process        |
| 6    | [!]    | 1          | Deferred to Phase 10                   |

**Total:** 7 points (original plan estimate: 5).

---

## Issues & Resolutions

- **TagPromptService constructor signature**: `TagBuilderRequest` is a positional record (not object initializer). Fixed by using named positional args. Service injected via `@inject TagPromptService TagService` following the same pattern as TagBuilderView.

---

## Commit Checkpoints

- [ ] Step 1 complete
- [ ] Step 2 complete (core deterministic UX works without any LLM dependency)
- [ ] Step 3 complete
- [ ] Step 4 complete (hide behind Phase 2 availability check)
- [ ] Step 5 complete
- [ ] Step 6 complete

---

## Open Risks

1. **Assembly order is opinionated.** Some users may prefer different orderings (mood-first, style-last, etc.). Mitigation: Phase 10 can add a drag-to-reorder UI; for v1, document the rationale in info content.
2. **TagPromptService might not be registered** (if Phase 2 skipped). Mitigation: mark the inject nullable (`[Inject] private TagPromptService? TagPrompts { get; set; }` with Blazor's DI this may require `GetService<>` via `IServiceProvider` instead - prefer `IServiceProvider` pattern: `ServiceProvider.GetService<TagPromptService>()`).
3. **"Save as Style" tag naming** (`style:`, `env:`, `mood:`) is a convention only enforced here. Phase 10 can formalize tag namespacing across the library.
4. **Refine template fallback**. If `Enhance` template is missing (user deleted it), the button errors out. Mitigation: snackbar error + link to System Prompts view.

---

## Phase Summary

Phase 5 implemented the Scene Builder — a structured-form prompt assembler with 5 fields (Subject, Environment, Lighting, Mood, Style). Assembly is deterministic C# joining non-empty fields as: `{Subject}, {Style}, {Environment}, {Lighting}, {Mood}`. Optional "Refine with LLM" uses the `Enhance` template. "Convert to Danbooru Tags" routes through TagPromptService. Save-as-Style writes tagged entries (`style:`, `env:`, `mood:`) to the Prompt library. Step 6 (info content) deferred to Phase 10.
