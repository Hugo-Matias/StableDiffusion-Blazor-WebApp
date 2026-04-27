# Phase 11 - Polish & Settings

## Status

**Phase:** 11
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started

---

## Objective

Consolidate cross-view settings, tighten UX, and handle recurring quality-of-life items that were explicitly deferred out of earlier phases. Specifically:

1. A dedicated "Settings" view for the LLM Tools tab that centralizes options previously spread across views (default starting view, Workshop ancestor depth / spawn count, Tag Builder preset defaults, Roulette weighted default, GapAnalyzer model override, NSFW gate default, etc.).
2. Keyboard shortcuts (Alt+1..9) to switch LLM nav views.
3. Per-view `InfoContent` audit - fill gaps, add screenshots / keyboard hints.
4. Empty-state polish across all views (no selected session, no prompts in library, no wildcards, etc.).
5. (Stretch) Seed default Roulette wildcard collections if missing.
6. (Stretch) Upgrade `MudTreeView` Workshop tree to SVG with parent→child edges.
7. Roll out the `LLMToolPageDescription` header banner + Workshop-style send-to action bar across every LLM tool view.

---

## Context

### Dependencies on prior phases

- **All of Phases 1-9** complete. This phase touches every view.

### Existing infrastructure to reuse

- `IInfoService.SetInfo(InfoContent)` with `Shortcuts` and `Tips` collections.
- `AppStatePromptsLLM` - add a `Settings` nested object for all the cross-view defaults.
- `LLMNavMenu` - settings appears as a gear icon at the bottom (not in the main view list).

### Design decisions locked for this phase

- Settings live in an `AppStatePromptsLLMSettings` nested class, persisted with the rest of the LLM state.
- Shortcuts are bound via a root-level `@onkeydown` on the `LLMToolsTab` container; `preventDefault` on matches.
- Empty states use `MudAlert` with a friendly hint and a primary CTA (e.g., "Create a wildcard collection").

---

## Execution Checklist

### Step 1: `AppStatePromptsLLMSettings` + view skeleton

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMSettings
  {
      public string DefaultViewId { get; set; } = "process";
      public int WorkshopAncestorDepth { get; set; } = 3;
      public int WorkshopSpawnCount { get; set; } = 5;
      public bool WorkshopAutoRender { get; set; } = true;
      public TagModelPreset TagBuilderDefaultPreset { get; set; } = TagModelPreset.Illustrious;
      public TagVerbosity TagBuilderDefaultVerbosity { get; set; } = TagVerbosity.Standard;
      public bool DefaultAllowNsfw { get; set; } = false;
      public bool RouletteDefaultWeighted { get; set; } = true;
      public string? GapAnalyzerModelOverride { get; set; } // null = use shared _selectedModel
      public bool TagNamespacingEnabled { get; set; } = true; // formalizes "style:", "env:", etc. library tags
  }
  ```
- [ ] `public AppStatePromptsLLMSettings Settings { get; set; } = new();` on `AppStatePromptsLLM`.
- [ ] Update each view's initialization to honor the relevant setting as the default when its own sub-state is fresh.
- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/SettingsView.razor` with vertically grouped sections:
  - "General": default starting view, tag namespacing toggle.
  - "Tag Builder": preset, verbosity, NSFW gate.
  - "Workshop": ancestor depth, spawn count, auto-render.
  - "Inspiration / Roulette": weighted default, "Seed default collections" button (stretch step).
  - "Gap Analyzer": model override (dropdown of `OllamaService.GetModels()`).
- [ ] Gear icon button at the bottom of `LLMNavMenu` opens the Settings view. Not part of the nav `Items` list - treated specially.

#### Success Criteria

- All settings persist.
- View renders all groups.
- Changing a default propagates to the respective sub-view on next open.

---

### Step 2: Keyboard shortcuts

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `LLMToolsTab.razor`, attach `@onkeydown="HandleKeyDown"` on a focused root `div` (or use a global JS handler injected via `IJSRuntime`).
- [ ] Handler:
  ```csharp
  void HandleKeyDown(KeyboardEventArgs e)
  {
      if (!e.AltKey) return;
      var index = e.Key switch {
          "1" => 0, "2" => 1, "3" => 2, "4" => 3, "5" => 4,
          "6" => 5, "7" => 6, "8" => 7, "9" => 8,
          _ => -1
      };
      if (index < 0 || index >= _navItems.Count) return;
      _ = HandleViewSelected(_navItems[index].Id);
  }
  ```
- [ ] Ensure focus stays on the tab container so shortcuts fire. If focus leaves (e.g., into a `MudTextField`), Alt+N still works inside a text field because Alt-modified keys don't generate characters in most layouts; verify behavior.
- [ ] Register shortcuts in each view's `InfoContent.Shortcuts` collection.

#### Success Criteria

- `Alt+1` switches to Process from anywhere in the LLM tab.
- `Alt+9` switches to the 9th nav entry (if it exists).
- Shortcut list visible in the info panel.

---

### Step 3: InfoContent audit

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] For every view (Process, SystemPrompts, History, TagBuilder, Mixer, Inspiration, SceneBuilder, TemplateBuilder, Remixer, Workshop, GapAnalyzer, Settings): populate `InfoContent.Title`, `Overview` (1-2 sentences), `Shortcuts` (at minimum the `Alt+N` switch to that view), `Tips` (3-5 bullets), and optionally `Sections` (for longer views like Workshop).
- [ ] Consistency pass: every view's Overview explains the primary action; tips emphasize the gotchas documented in each phase's Open Risks.

#### Success Criteria

- No view has a generic or empty info panel.
- Navigating through all views never shows stale info from a previous view.

---

### Step 4: Empty-state polish

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] **History**: "No history yet - run Process once to populate."
- [ ] **Tag Builder**: show candidate count 0 as "No tags matched - try broader concepts or disable category filters."
- [ ] **Remixer**: "No saved prompts in library - save one from any LLM view first." with a CTA that switches to Process.
- [ ] **TemplateBuilder**: "No templates yet - click + to create one." with inline example: `A [subject] at [[roulette/location]] during [time]`.
- [ ] **Workshop**: "No sessions. Run any LLM view and click 'Send to Workshop' to seed one."
- [ ] **Inspiration Roulette**: when wildcard collections are missing, show an info banner "Using built-in fallbacks - set up Roulette collections in Settings → Seed default collections for richer variety."
- [ ] **GapAnalyzer**: no empty state (input is always editable). Show a hint when analysis returns no suggestions.

#### Success Criteria

- No raw empty lists or silent blanks.
- Every CTA navigates to a useful destination.

---

### Step 5: Seed default Roulette wildcard collections (stretch)

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `SettingsView`, add a "Seed default Roulette collections" button. Calls a new `WildcardService.SeedDefaultRouletteCollectionsAsync()` method that creates `roulette/subject`, `roulette/style`, `roulette/lighting`, `roulette/mood`, `roulette/location` from a curated hard-coded list of 15-25 entries each (source the same lists embedded in `InspirationView`'s fallback map + expand).
- [ ] Idempotent: skip collections that already exist.
- [ ] Snackbar confirms how many collections + entries were created.

#### Success Criteria

- Clicking the button once seeds all five collections.
- Subsequent clicks are no-ops.
- Roulette spins now pull from the seeded collections.

---

### Step 6: Workshop SVG tree upgrade (stretch)**Complexity:** 5

**Status:** [ ] Not Started

#### Tasks

- [ ] Research lightweight Blazor SVG tree libraries or implement a minimal custom renderer that lays out nodes top-down with edges drawn between parent and child.
- [ ] Replace `MudTreeView` in `WorkshopView`. Keep the same click-to-activate behavior.
- [ ] Preserve icon semantics (root / chat / evolve).
- [ ] Ensure large trees (50+ nodes) remain usable - add zoom/pan if needed.

#### Success Criteria

- Tree visually communicates parent→child relationships at a glance.
- No regression in click / select behavior.
- Trees with 50+ nodes remain responsive.

---

### Step 7: LLMToolPageDescription + Workshop send-to bar rollout

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add a `<LLMToolPageDescription Title=... Icon=... Description=... />` header to every LLM tool view: ProcessView, SystemPromptsView, HistoryView, MixerView, InspirationView, SceneBuilderView, TemplateBuilderView, RemixerView, WorkshopView, GapAnalyzerView, SettingsView. Use the component at `BlazorWebApp/Components/Prompts/LLM/Shared/LLMToolPageDescription.razor` (originally introduced for Tag Builder).
- [ ] Replace ad-hoc `Send to Process` / `Copy` buttons in every result view with the Workshop-style send-to action bar (`send-to-section` + `send-to-btn`). Wire a `Copy` icon button, `Send to Process`, `Send to Workshop` (creates a new session via `LLMToolsTab.HandleSendToWorkshop`), and per-workflow buttons sourced from `IPromptSendToService.GetParameterWorkflows()`.
- [ ] Add `OnSendToWorkshop` `EventCallback<string>` parameters to MixerView / InspirationView / SceneBuilderView / TemplateBuilderView / RemixerView / GapAnalyzerView and wire them in `LLMToolsTab.razor`.
- [ ] Consider extracting an `LLMSendToBar.razor` shared component (prompt + EventCallbacks) so the markup stops being duplicated.
- [ ] Audit each header description for tone and length: 1-2 sentences, action oriented.

#### Success Criteria

- Every LLM tool view opens with a distinctive description banner (not `MudAlert`).
- Every result panel exposes the same Copy / Send-to-Process / Send-to-Workshop / per-Workflow set of actions.
- No regression in existing send-to wiring.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                       |
| ---- | ------ | ---------- | ------------------------------------------- |
| 1    | [ ]    | 2          | Settings sub-state + view                   |
| 2    | [ ]    | 2          | Keyboard shortcuts                          |
| 3    | [ ]    | 2          | InfoContent audit                           |
| 4    | [ ]    | 2          | Empty-state polish                          |
| 5    | [ ]    | 2          | Seed default Roulette collections (stretch) |
| 6    | [ ]    | 5          | SVG tree (stretch)                          |
| 7    | [ ]    | 2          | LLMToolPageDescription + send-to rollout    |

**Total:** 17 points if both stretch items shipped; 10 points for required scope.

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] After Step 1
- [ ] After Step 2
- [ ] After Step 3
- [ ] After Step 4
- [ ] After Step 5 (if pursued)
- [ ] After Step 6 (if pursued)

---

## Open Risks

1. **Alt+N doesn't fire inside `MudTextField`.** Browser behavior varies. Mitigation: consider `Ctrl+Alt+N` or a JS interop keydown hook on `document`.
2. **Setting drift with view-local state.** If a user toggles a Tag Builder preset in the main view, the Settings view shows the "default", not the current view state. Document clearly: Settings = defaults for new inputs, not live override.
3. **SVG tree lib choice.** No established Blazor SVG tree library surveyed yet. Mitigation: defer to a stretch item; if no good fit, keep `MudTreeView` and polish its visual density.

---

## Phase Summary

_To be filled in on completion._
