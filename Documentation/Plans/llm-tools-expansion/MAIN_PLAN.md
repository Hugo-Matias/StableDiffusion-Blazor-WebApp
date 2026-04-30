# LLM Tools Expansion - Implementation Plan

## Status

**Current Phase:** Phase 8.6 complete (Workshop UX Overhaul - all 6 steps shipped); Phase 10 (Wildcard Forge) in progress
**Total Complexity:** ~136 points across 13 phases (Phase 8.6 adds 23, Phase 10 adds 36 - renumbered)
**Related Plans:** Builds on features delivered in `prompt-page-enhancement` Phases 1-5. Supersedes the never-started Phase 5.5 (LLM Creative Tools) and Phase 6 (VL Models) of that plan.

---

## Implementation Guidelines

See `Documentation/Plans/IMPLEMENTATION_GUIDE.md` for the canonical conventions. Summary:

### Execution Workflow (per step)

1. Initial Code Writing -> 2. Test and Debug -> 3. Discuss Improvements -> 4. Update Phase Document
   - Do NOT proceed until testing is complete
   - User must explicitly approve before updating the phase document
   - Build only after user request or after completing all file edits for a step

### Progress Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked / needs discussion

### Complexity (Fibonacci)

1 Trivial | 2 Simple | 3 Moderate | 5 Medium | 8 Complex | 13 Very Complex | 21+ Epic

### Key Rules

- Each step is a commitable checkpoint
- Minimal focused changes only - no unrelated refactors
- All events use pub/sub via `EventService`
- User permission required before moving to next phase
- Each phase document must contain enough context to resume in a new session

---

## Problem Statement

The current `LLM Tools` tab in the Prompts page is functional but bare-bones: Process, System Prompts, History. As more LLM-driven features ship, the inner `MudTabs` pattern will not scale. At the same time, several high-value LLM capabilities remain unbuilt:

1. No way to build prompts from the Danbooru tag vocabulary that modern anime/illustration models were actually trained on.
2. No iterative refinement UX - every operation is single-shot.
3. No image-in-the-loop workflow to evaluate prompt quality visually.
4. No lightweight creative tools (mixer, scene builder, roulette) despite the underlying infrastructure being ready.

---

## Proposed Solution

### Architectural Pivot (Phase 1)

Replace the inner `MudTabs` inside `LLMMainPanel` with a collapsible navigation menu on the sidebar, placed above the existing `LLMSettingsPanel` (model selector + sampling). Each "view" becomes its own component under `Components/Prompts/LLM/Views/`.

```
+---------------------------------------------------+
| [<] Sidebar (icon-only when collapsed) | Content  |
|                                        |          |
|  [Icon] Process                        |          |
|  [Icon] System Prompts                 |  Active  |
|  [Icon] History                        |  view    |
|  [Icon] Tag Builder       (Phase 2)    |  renders |
|  [Icon] Mixer             (Phase 3)    |  here    |
|  [Icon] Inspiration       (Phase 4)    |          |
|  [Icon] Scene Builder     (Phase 5)    |          |
|  [Icon] Templates         (Phase 6)    |          |
|  [Icon] Remixer           (Phase 7)    |          |
|  [Icon] Workshop          (Phase 8)    |          |
|  [Icon] Gap Analyzer      (Phase 9)    |          |
|  [Icon] Wildcard Forge    (Phase 10)   |          |
|  [Icon] Image -> Prompt   (Phase 12)   |          |
|                                        |          |
|  ---------------------------           |          |
|  Model: [select]                       |          |
|  Temperature, Top-P, ...               |          |
+---------------------------------------------------+
```

Design decisions:

- Plain `MudNavMenu` (no collapsible paper for v1).
- Icon-only rail when collapsed (~56px); expanded shows icon + label.
- Active view and collapsed flag persisted in `AppState.Prompts.LLM` (new sub-state).
- Model + sampling remain shared across all views.
- All view-switching events flow through `EventService`.

### High-Value LLM Capabilities

- **Tag Builder** (Phase 2): Natural language -> canonical Danbooru tags via two-pass retrieval, reusing `CsvService`.
- **Workshop** (Phase 8): Unified text/visual iterative refinement with a tree-based lineage. Two modes:
  - **Chat**: user types edits, LLM rewrites.
  - **Evolution**: LLM spawns N variations, each renders an image, user picks the winner.

### Development Conventions

- View components live in `BlazorWebApp/Components/Prompts/LLM/Views/`.
- Each view exposes an `InfoContent` via `IInfoService` on init.
- Tag-resolution logic is centralized in a new `TagPromptService` (reused by Workshop and VL in later phases).
- New DB entities follow the single-complex-JSON-column pattern only where it simplifies persistence; otherwise regular relational tables.
- No new EF migrations should be authored without the full checklist from `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`.

---

## Implementation Phases

### Phase 1: Sidebar Nav Refactor

**Objective:** Replace inner `MudTabs` with a sidebar nav menu; extract the three existing views; persist nav state in `AppState`.
**Complexity:** 8 points (actual: 17)
**Status:** [x] Complete - see `PHASE_1.md`

#### Steps

1. Add `LLMState` sub-state to `AppState.Prompts` with `ActiveViewId` (string) and `IsNavCollapsed` (bool), persisted via existing `State` entity.
2. Create `Views/ProcessView.razor`, `Views/SystemPromptsView.razor`, `Views/HistoryView.razor` by extracting the three panels from `LLMMainPanel`.
3. Create `LLMNavMenu.razor` with `MudNavMenu`, icon-only / expanded modes, collapse toggle.
4. Rebuild `LLMToolsTab.razor` to compose `LLMNavMenu` + `LLMSettingsPanel` in the sidebar and render the active view component in content.
5. Publish view-switch events through `EventService` (new `LLMViewChangedEventArgs`).
6. Verify History and System Prompts still work; confirm Process pipeline unchanged.

#### Success Criteria

- Clicking any nav entry switches views without full page reload.
- Collapse toggle persists across reloads.
- No behavioral regression in Process / System Prompts / History.
- Build clean, no new compiler warnings.

#### Files Affected

- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor` (recompose)
- `BlazorWebApp/Components/Prompts/LLM/LLMMainPanel.razor` (decompose and delete once extracted)
- New: `BlazorWebApp/Components/Prompts/LLM/LLMNavMenu.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/Views/ProcessView.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/Views/SystemPromptsView.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/Views/HistoryView.razor`
- New: `BlazorWebApp/Events/LLMViewChangedEventArgs.cs`
- Extend: `BlazorWebApp/Models/AppState.cs` (Prompts.LLM sub-state)
- Extend: `BlazorWebApp/Services/EventService.cs` (new event)

---

### Phase 2: Danbooru Tag Builder

**Objective:** Generate coherent prompts in the Danbooru tag vocabulary from natural-language input, using the existing CSV-backed tag catalog.
**Complexity:** 13 points (actual: 23)
**Status:** [x] Complete — see `PHASE_2.md`

#### Approach (Option B - Two-Pass Retrieval)

1. **Pass 1 (concept extraction)**: LLM receives the user request and outputs a structured list of visual concepts (subject, setting, lighting, clothing, mood, etc.) as plain text or JSON.
2. **Resolution**: `TagPromptService` matches each concept against the Danbooru CSV via `CsvService.SearchTags` (fuzzy + aliases). Produces canonical tags grouped by category.
3. **Pass 2 (assembly)**: LLM receives the resolved tags plus the user's verbosity/model-preset choice and produces a final, ordered, deduplicated tag prompt.

This keeps the LLM context small, guarantees every output tag exists in the vocabulary, and respects per-model conventions.

#### Steps

1. Create `TagPromptService` (constructor injects `CsvService`, `OllamaService`).
2. Define `TagCategory` enum mapped to CSV color codes (0 general, 1 artist, 3 copyright, 4 character, 5 meta).
3. Implement Pass 1: concept extraction system prompt + parser.
4. Implement resolution: concept -> list of candidate tags with confidence score.
5. Implement Pass 2: tag assembly per model preset with quality-prefix conventions.
6. Add model presets: Pony, Illustrious, NoobAI, Anima (preview - note to verify dialect at implementation time).
7. Build `TagBuilderView.razor`: input field, verbosity slider (minimal / standard / detailed / exhaustive), model-preset dropdown, category toggles, NSFW gate, "ground in current prompt" checkbox.
8. Show resolution preview (which concepts resolved to which tags) before final assembly.
9. Persist Tag Builder settings in `AppState.Prompts.LLM.TagBuilder`.
10. Seed default system prompts for Pass 1 and Pass 2 in `SystemPromptTemplate` table (marked `IsDefault`, category = TagBuilder).

#### Success Criteria

- "woman at the beach" at detailed verbosity produces a coherent tag prompt with subject + setting + atmosphere + quality tags.
- Model-preset switch changes quality-tag placement and vocabulary.
- Every output tag is a valid Danbooru tag (verifiable against CSV).
- Resolution preview shows traceability from concept to tag.
- No duplication with `CsvService` search logic.

#### Files Affected

- New: `BlazorWebApp/Services/TagPromptService.cs`
- New: `BlazorWebApp/Models/TagPromptModels.cs` (enums, request/result DTOs)
- New: `BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor.css`
- Extend: `BlazorWebApp/Services/DatabaseService.cs` (seed TagBuilder system prompts if missing)
- Extend: `BlazorWebApp/Models/AppState.cs` (TagBuilder sub-state)

---

### Phase 3: Prompt Mixer

**Objective:** Blend two prompts into a cohesive hybrid using an LLM.
**Complexity:** 3 points
**Status:** [x] Complete - see `PHASE_3.md`

#### Steps

1. Create `MixerView.razor`: two input fields, blend-ratio slider (0-100), Mix button.
2. Seed a `PromptMixer` default `SystemPromptTemplate` with `{promptA}`, `{promptB}`, `{ratio}` placeholders.
3. Reuse existing `ProcessView` result pipeline for display / Save to Library / Send to Workshop.

#### Success Criteria

- Mixing "cyberpunk city" and "underwater world" at 50/50 yields a coherent blended prompt.
- Send-to-Workshop button appears (wires up in Phase 8).

---

### Phase 4: Random Inspiration + Roulette

**Objective:** Generate prompts from random or filtered creative constraints.
**Complexity:** 3 points
**Status:** [x] Complete - see `PHASE_4.md` (Step 5 deferred to Phase 10)

#### Steps

1. `InspirationView.razor`: Genre / Mood / Complexity selectors + "Inspire Me" button.
2. Roulette sub-mode: spin animation, randomly combines subject + style + lighting + mood + location from wildcard collections.
3. Seed `Inspiration` default system prompt template.
4. Reuse result pipeline.

#### Success Criteria

- "Inspire Me" produces novel prompts on each click.
- Roulette spin pulls valid items from existing wildcard collections.

---

### Phase 5: Scene Builder

**Objective:** Structured-form prompt assembly.
**Complexity:** 5 points
**Status:** [x] Complete - see `PHASE_5.md` (Step 6 deferred to Phase 10)

#### Steps

1. `SceneBuilderView.razor`: Subject / Environment / Lighting / Mood / Style form.
2. Deterministic assembly logic (no LLM call) + optional "Refine with LLM" pass.
3. Optional "Convert to Danbooru tags" button -> calls `TagPromptService` from Phase 2.
4. Save-as-Style integration.

#### Success Criteria

- All fields optional; empty ones omitted from output.
- "Convert to tags" produces matching tag prompt.

---

### Phase 6: Template Engine (Mad Libs)

**Objective:** Reusable prompt templates with variable slots, integrated with wildcards.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

1. New entity `PromptTemplate` with `Template` (text with `[variable]` placeholders) and `VariableOptions` (JSON dictionary).
2. EF migration (follow persistence checklist).
3. `TemplateBuilderView.razor`: template editor + fill-in panel + Fill Random button.
4. Wildcard integration: `[[collection_name]]` slot resolves via `WildcardService`.
5. Save filled template as `Prompt` style.

#### Success Criteria

- User can author and save reusable templates.
- Wildcard slots resolve randomly on fill.
- Manual overrides persist until explicitly re-randomized.

---

### Phase 7: Prompt Remixer

**Objective:** Multi-select from saved styles; shuffle / combine / LLM-mix.

**NOTE (2026-04-26):** This feature should be integrated into the existing **Prompt Mixer** tool (Phase 3) rather than a standalone view. The integration should follow the same Single/Batch logic pattern used by the Process view — i.e., the user can either:

- **Single mode**: Pick one saved prompt and remix it with LLM guidance.
- **Batch mode**: Multi-select several saved prompts and combine/shuffle/LLM-mix them together.

This keeps the Mixer as the single entry point for all prompt-blending operations and avoids feature fragmentation across tools. Revisit Phase 3 (MixerView) when implementing this integration.

**Complexity:** 3 points
**Status:** [ ] Not Started — deferred for integration into Phase 3 (Prompt Mixer)

#### Steps

1. Extend `MixerView.razor` with prompt multi-select grid (replacing or augmenting the two-input fields).
2. Method selector: Shuffle elements / Combine parts / LLM intelligent mix.
3. Shuffle and Combine are deterministic; LLM-mix uses a dedicated system prompt.

#### Success Criteria

- Selecting 3 styles and picking LLM mix produces a coherent hybrid.
- Shuffle mode preserves all tags but randomizes order.

---

### Phase 8: Workshop (Conversational + Visual Evolution)

**Objective:** Iterative prompt refinement with two interaction modes sharing one tree-based lineage:

- **Chat mode**: multi-turn textual refinement ("add rain", "remove the smile").
- **Evolution mode**: LLM spawns N variations; each renders an image via the current workflow; user picks the winner which becomes the next parent.

**Complexity:** 24 points
**Status:** [x] Complete - see `PHASE_8.md`

#### Design Decisions

- **Tree-based lineage** (not flat timeline). Any node is clickable -> becomes current; user can branch freely.
- **Images are NOT lineage edges.** Lineage links prompts only, so deleting images doesn't corrupt sessions.
- **Image generation** uses the current generation settings (model, sampler, steps, size, etc.) with only the prompt replaced. A workflow dropdown lets the user pick which workflow from the currently selected base to execute.
- **No rating system.** User selects "the one" from spawned variations; other branches remain in the tree for later exploration.
- **Chat multi-turn context**: include ancestor instructions/results up to a configurable depth (default 3, user-adjustable in Settings view). Parent-only mode also supported.
- **Always persist** sessions to DB. Manual delete only; no auto-pruning.
- **Spawn count**: slider 3-7, default 5.

#### Data Model

```csharp
public class PromptWorkshopSession
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CurrentNodeId { get; set; }
    public ICollection<PromptWorkshopNode> Nodes { get; set; } = new List<PromptWorkshopNode>();
}

public class PromptWorkshopNode
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int? ParentId { get; set; }
    public int GenerationNumber { get; set; }
    public string PromptText { get; set; } = "";
    public string Mode { get; set; } = "";       // "chat" | "evolve" | "root"
    public string? Instruction { get; set; }     // chat mode only
    public string? ModelUsed { get; set; }
    public List<int>? ImageIds { get; set; }     // optional link to generated Image entities; may be stale
    public DateTime CreatedAt { get; set; }
    public PromptWorkshopSession Session { get; set; } = null!;
    public PromptWorkshopNode? Parent { get; set; }
    public ICollection<PromptWorkshopNode> Children { get; set; } = new List<PromptWorkshopNode>();
}
```

#### Steps

1. Entities + EF migration (follow persistence checklist).
2. `WorkshopService` with session/node CRUD, branching helpers, ancestor-context builder.
3. `WorkshopView.razor` layout: session sidebar (list of sessions) + tree panel + current-node panel.
4. Tree renderer (custom SVG / CSS grid component; research if a MudBlazor component or lightweight Blazor lib fits).
5. Chat mode: input field + turn pipeline with configurable ancestor depth; adds one child node on send.
6. Evolution mode: spawn slider + workflow dropdown + Spawn button -> LLM generates N variation prompts -> parallel image generation via `IRouterService` -> result grid.
7. Image-winner selection promotes the chosen variation to current node and dims siblings (they stay in the tree).
8. "Send to Workshop" entry points from Tag Builder, Mixer, Inspiration, Scene Builder, Templates, Remixer.
9. Save-as-Style action on any node writes to the `Prompt` library.
10. Seed dedicated system prompts (`Workshop.ChatEdit`, `Workshop.EvolveVariations`) as defaults.
11. Session rename / delete UI.

#### Success Criteria

- Can start a session from Tag Builder output, chat-refine three turns, then switch to evolve mode and visually pick a winner.
- Tree renders parent/child relationships correctly, even after branching.
- Deleting an image does not break the session.
- Spawn-5 in evolve mode issues 5 parallel generations with the current settings.
- Chat context respects the ancestor-depth setting.

#### Open Risks

- Tree rendering complexity - mitigate by shipping a simple indented list first if needed, upgrading to proper tree if visual lib chosen.
- Parallel image generation load - may need queueing if backend serializes requests; lean on existing scheduler pipeline.

---

### Phase 9: Contextual Suggestions (Gap Analyzer)

**Objective:** Manual-trigger analysis of a prompt: detect missing elements (lighting, mood, composition, quality) and suggest additions.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

1. `GapAnalyzerView.razor`: paste prompt / pull from current -> Analyze button.
2. System prompt produces structured JSON output: `detected`, `missing`, `suggestions`.
3. Render detected tags as chips, suggestions as one-click adders.
4. Apply changes back to the prompt; optional Send-to-Workshop.
5. No real-time / debounced mode (performance concern) - manual trigger only.

#### Success Criteria

- Analyzer reliably detects subject/style/setting.
- Suggestions are actionable chips, not prose.

---

### Phase 10: Polish & Settings

**Objective:** UX refinements and per-view settings consolidation.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps

1. LLM Tools settings section: default view on open, Workshop ancestor depth, spawn count default, NSFW gate default.
2. Keyboard shortcuts (Alt+1-9 to switch views).
3. Per-view `InfoContent` pass for tooltips and quick guides.
4. Empty-state polish across all views.

#### Success Criteria

- Settings persist across sessions.
- All shortcuts documented in Info panel.

---

### Phase 12: Image -> Prompt (VL Models) - Nice-to-Have

**Objective:** Upload an image; Ollama multimodal model produces a prompt.
**Complexity:** 13 points
**Status:** [ ] Not Started (deferred as nice-to-have)

#### Steps

1. Research current Ollama multimodal support (LLaVA, MiniCPM-V, Qwen2-VL) and best fit.
2. Extend `OllamaService` to accept image inputs (base64).
3. `VLModelService` for image -> prompt orchestration.
4. `ImageToPromptView.razor`: drag-drop upload, interrogation style selector, result panel.
5. Interrogation styles: Detailed / Focus / Artistic / Technical / Tags / Simple.
6. "Normalize to Danbooru tags" post-process via `TagPromptService` (reuses Phase 2).
7. Gallery integration: send gallery image to analysis.
8. Batch mode (multiple images).

#### Success Criteria

- Uploading an image returns a useful prompt.
- Tag normalization produces valid Danbooru tags.

---

### Phase 13: Workshop Wizard (Button-Based Prompt Builder)

**Objective:** Add a gamified, button-driven wizard panel above the Workshop composer that lets users build and iterate on a prompt without typing. A hardcoded intro questionnaire seeds a baseline prompt; from there the LLM returns JSON-formatted suggestion buttons that drive the next turn. The wizard always emits a draft into the composer textbox - it does NOT auto-create nodes in the session tree.

**Complexity:** 21 points
**Status:** [ ] Not Started

#### Problem Recap

Today's Workshop assumes the user types meaningful free-text instructions ("add rain", "make it cyberpunk"). For users who don't know how to express what they want, this is a blank-page problem. The wizard reframes prompt-building as a "creative chess game" of ask/answer turns where the LLM proposes the next plausible directions and the user just clicks.

#### Design Decisions

| Decision                                                                               | Rationale                                                                                                               |
| -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Coexists with chat/evolve; does not replace either                                     | Wizard is a builder for the composer; chat/evolve still operate on the tree                                             |
| Wizard runs **independently of any active session** for its flow                       | Users can build a draft before picking/creating a session; Commit just writes text into the composer                    |
| Wizard **state is per-session** for restoration (nullable `SessionId`)                 | One in-progress wizard per Workshop session, plus one "unbound" wizard slot when no session is selected                 |
| Intro **sections** are hardcoded; **all option labels are LLM-generated**              | Predictable scaffolding (Subject -> Scenery -> Lighting -> Mood -> Style) but every list of buttons is a fresh surprise |
| Every option turn includes a **More...** button to re-roll without repeating           | Drives the "creative chess game" feel and avoids stale catalogs                                                         |
| **JSON-mode** Ollama responses with a fixed schema in the system prompt                | Reliable parsing; no regex/free-text fallback path                                                                      |
| Context budget: **current draft + last 1-2 Q/A pairs only**                            | Honors the constraint of low-compute setups; avoids sending full history every turn                                     |
| Persistent action verbs: Improve, Change, Add, Remove, Surprise me, Commit -> Composer | Hardcoded set always rendered; LLM-suggested buttons rendered alongside per turn                                        |
| **Undo is deterministic** (local turn-history stack, no LLM round-trip)                | Reverts last turn from stored history; cheap and predictable                                                            |
| Panel **always visible above the composer**, collapsible header                        | Wizard is the gamified hub; user can collapse to free vertical space                                                    |
| Wizard does NOT generate images, branches, or nodes                                    | Strict separation of concerns: wizard builds text, composer/Workshop handles execution                                  |

#### Data Model

```csharp
public class WorkshopWizardSession
{
    public int Id { get; set; }

    /// <summary>Optional FK. Null = the unbound wizard slot used when no Workshop session is active.</summary>
    public int? SessionId { get; set; }
    public PromptWorkshopSession? Session { get; set; }

    /// <summary>JSON-serialized WizardBody (single complex column, JSON-backed entity pattern).</summary>
    public string Body { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Serialized into Body via ValueConverter, mirroring SchedulerDraft / State conventions.
public sealed class WizardBody
{
    public WizardStage Stage { get; set; }              // Intro | Iteration | Committed
    public int IntroStepIndex { get; set; }             // 0..N for hardcoded intro questions
    public string CurrentDraft { get; set; } = "";     // The current prompt being built
    public List<WizardTurn> History { get; set; } = new(); // For deterministic undo + last-N context
    public List<WizardOption> PendingOptions { get; set; } = new(); // Buttons currently rendered
    public string? LastActionVerb { get; set; }         // e.g. "Add", "Change"
    public string? LastUserChoice { get; set; }
}

public sealed class WizardTurn
{
    public string Question { get; set; } = "";
    public string Choice { get; set; } = "";
    public string? ActionVerb { get; set; }
    public string DraftBefore { get; set; } = "";
    public string DraftAfter { get; set; } = "";
}

public sealed class WizardOption
{
    public string Label { get; set; } = "";    // Button text
    public string? Hint { get; set; }          // Optional secondary text / tooltip
    public string? Payload { get; set; }       // Sent back to LLM as the choice (often == Label)
}
```

#### LLM Contract (JSON Mode)

Single response shape requested from Ollama (`format: "json"`):

```json
{
  "question": "What should we focus on next?",
  "options": [
    { "label": "Add atmospheric fog", "hint": "evening mist, cool palette" },
    { "label": "Specify camera angle", "hint": "low-angle, dutch tilt..." }
  ]
}
```

Schema constraints documented in the system prompt: 3-6 options per turn, each label <= 60 chars, hints optional and <= 80 chars. Parser fails closed (re-prompt once, then surface a snackbar and let the user click a hardcoded verb).

#### UI Placement

```
+-------------------------------------------------------------+
| WorkshopWizardPanel  [collapse ^]                           |
|   Stage: Iteration   Turn 4   Draft: "..."                  |
|                                                             |
|   Question: "What should we focus on next?"                 |
|   [Add fog] [Camera angle] [Time of day] [Outfit detail]    |
|                                                             |
|   --- Always-visible verbs ---                              |
|   [Improve] [Change] [Add] [Remove] [Surprise] [Undo]       |
|   [Commit -> Composer]                                       |
+-------------------------------------------------------------+
| WorkshopComposer (unchanged)                                |
|   <textarea>     [Tune] [Variations] [Send]                 |
+-------------------------------------------------------------+
```

#### Steps

1. **Entity + migration** - `WorkshopWizardSession` with JSON-backed `Body` (follow `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` checklist). Update `AppDbContextModelSnapshot.cs`.
2. **DTOs + JSON contract** - `WizardBody`, `WizardTurn`, `WizardOption`, `WizardStage`, `WizardLLMResponse`. Add `WizardJsonOptions` mirroring `SchedulerJsonOptions.Compact`.
3. **`WorkshopWizardService`** - load/save by `SessionId?`, advance turn (intro vs iteration paths), undo, reset, commit. Encapsulates the context-trimming policy (current draft + last 1-2 turns only).
4. **System prompts (seeded)** - `Workshop.WizardIntroOptions`, `Workshop.WizardIterate`, `Workshop.WizardActionVerb` (one prompt per persistent verb that needs LLM expansion: Improve / Change / Add / Remove / Surprise). Marked `IsDefault`.
5. **Hardcoded intro sections only** - `BlazorWebApp/Data/wizard_intro.json` defines the ordered section list (id, title, prompt-fragment shown to the LLM, optional 1-3 reply examples used as few-shot guidance). NO option labels are stored; every step calls the LLM for fresh options. The schema response is the same `{ question, options[] }` contract used in iteration.
6. **`WorkshopWizardPanel.razor`** - collapsible card above composer; renders question, dynamic option grid (`MudButton` / `.send-to-btn`), persistent verb row, Undo (local), Commit. Uses spacing tokens from `wwwroot/site.css`.
7. **Composer integration** - `WorkshopWizardPanel` lives inside `WorkshopView.razor`'s composer slot, sits above existing `WorkshopComposer`. Commit calls `_composer.SetText(draft)`; user presses Send manually. No changes to `WorkshopComposer` internals.
8. **State persistence** - on every turn, `WorkshopWizardService.SaveAsync()` updates `Body`. On `WorkshopView` mount, hydrate via `SessionId` (or null slot when no session). `IsModified` flag set per persistence convention.
9. **EventService events** - `WizardTurnAdvancedEventArgs`, `WizardCommittedEventArgs`, `WizardResetEventArgs` via the pub/sub pattern.
10. **AppState** - `AppState.Prompts.LLM.Workshop.WizardCollapsed` (bool) for header collapse persistence.
11. **Failure handling** - on JSON parse failure, retry once with a stricter system prompt; on second failure show snackbar "Suggestions unavailable - use the action buttons" and keep the persistent verbs functional.
12. **Telemetry / dev affordance** - log raw LLM JSON to console in DEBUG only for tuning the schema.

#### Success Criteria

- Cold start (no session, empty wizard): clicking through the 5-step intro produces a coherent baseline draft visible in the panel.
- Clicking any LLM-suggested button or persistent verb advances the draft and renders new options without typing anything.
- Commit -> Composer writes the draft into the composer textbox; pressing Send creates the session/node exactly as today.
- Undo reverts the previous turn deterministically (no LLM call); the option grid shows the prior turn's options.
- Navigating away and back restores the wizard's stage, draft, and pending options for the active session (and for the unbound slot).
- A sample turn payload sent to Ollama contains only: current draft, last 1-2 Q/A pairs, the action verb / chosen option. No full session history is sent.
- JSON-mode parse failures retry once and degrade gracefully.

#### Files Affected

- New: `BlazorWebApp/Data/Entities/WorkshopWizardSession.cs`
- New: `BlazorWebApp/Migrations/{timestamp}_Add_WorkshopWizardSession.cs` + snapshot update
- New: `BlazorWebApp/Models/WizardModels.cs` (DTOs + enum)
- New: `BlazorWebApp/Services/WorkshopWizardService.cs` + interface
- New: `BlazorWebApp/Data/wizard_intro.json`
- New: `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopWizardPanel.razor` (+ `.razor.css`)
- New: `BlazorWebApp/Events/WizardTurnAdvancedEventArgs.cs`, `WizardCommittedEventArgs.cs`, `WizardResetEventArgs.cs`
- Extend: `BlazorWebApp/Data/AppDbContext.cs` (DbSet + JSON converter wiring)
- Extend: `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor` (mount panel above composer; hydrate on session change)
- Extend: `BlazorWebApp/Models/AppState.cs` (`Workshop.WizardCollapsed`)
- Extend: `BlazorWebApp/Services/DatabaseService.cs` (seed wizard system-prompt templates if missing)
- Extend: `BlazorWebApp/Program.cs` (register `IWorkshopWizardService`)

#### Stress Points & Risks

| Risk                                                          | Mitigation                                                                                          | Complexity |
| ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- | ---------- |
| Small models produce malformed JSON                           | Strict schema in system prompt, JSON mode, single retry, snackbar fallback                          | 3          |
| Context creep across turns inflates payloads                  | Hard-coded sliding window of last 1-2 turns + draft only; service-level enforcement                 | 2          |
| Per-session FK + nullable unbound slot creates two code paths | Single load helper `LoadOrCreate(int? sessionId)`; one row per non-null FK enforced by unique index | 2          |
| Panel collisions with existing composer keyboard handlers     | Panel uses buttons only; no global key listeners                                                    | 1          |
| Seeded system prompts drift from schema                       | Version-stamp each template; `DatabaseService` re-seeds when version mismatches                     | 2          |
| Deterministic Undo conflicts with LLM continuity              | Undo restores `Body.PendingOptions` from `History`; LLM is not re-invoked                           | 1          |

#### Resolved Assumptions

- **Surprise me** is pure-LLM with no wildcard constraints - the goal is genuine surprise.
- **Turn cap**: hard limit 50 turns; soft snackbar nudge at 30. Counter resets on Reset / Commit.
- **System-prompt versioning**: each seeded wizard template carries a version stamp; `DatabaseService` re-seeds entries whose version is older than the embedded constant.
- **More...** button calls the same iteration endpoint with an explicit `excludeLabels` hint listing the labels just shown so the model rotates suggestions instead of repeating.
- Auto-Commit is **not** triggered at intro completion; the user always clicks Commit -> Composer.

---

## Deferred Items

Recorded here for future planning. When picked up, use this plan as reference context.

### Tag Builder Tab (Visual Explorer)

**Status:** Deferred (distinct from Phase 2 LLM tag generator).
**Scope:** Visual browser over the full Danbooru taxonomy with category nav, weight sliders, conflict detection, combination presets.
**Rationale for deferral:** Pure-UI feature; doesn't need LLM plumbing; can ship independently once LLM expansion is stable.
**Reference:** `prompt-page-enhancement` plan Phase 7.

### Prompt Analytics Tab

**Status:** Deferred.
**Scope:** Per-prompt usage tracking, tag co-occurrence, historical browser, ratings.
**Rationale for deferral:** Requires image-rating infrastructure and analytics DB tables unrelated to the current LLM focus.
**Reference:** `prompt-page-enhancement` FEATURE_IDEAS.md ideas 6-9.

---

## Phase Summary Table

| #   | Phase                         | Complexity  | Depends On                         | Status |
| --- | ----------------------------- | ----------- | ---------------------------------- | ------ |
| 1   | Sidebar Nav Refactor          | 8           | -                                  | [x]    |
| 2   | Danbooru Tag Builder          | 13 (act:23) | 1                                  | [x]    |
| 3   | Prompt Mixer                  | 3           | 1                                  | [x]    |
| 4   | Random Inspiration + Roulette | 8           | 1                                  | [x]    |
| 5   | Scene Builder                 | 8           | 1, (optional 2)                    | [x]    |
| 6   | Template Engine               | 5           | 1                                  | [ ]    |
| 7   | Prompt Remixer                | 3           | 1                                  | [ ]    |
| 8   | Workshop                      | 13          | 1, (send-to-Workshop hooks in 2-7) | [ ]    |
| 9   | Gap Analyzer                  | 8           | 1, 2                               | [ ]    |
| 10  | Wildcard Forge                | 36          | 1, 2                               | [~]    |
| 11  | Polish & Settings             | 3           | 1-10                               | [ ]    |
| 12  | Image -> Prompt (VL)          | 13          | 1, 2                               | [ ]    |
| 13  | Workshop Wizard               | 21          | 8                                  | [ ]    |

**Total:** ~134 complexity points.

---

## Next Steps

1. ~~Review and approve this plan.~~ Done.
2. ~~Create `PHASE_1.md` and begin Phase 1 Step 1.~~ Done - Phase 1 complete.
3. On user approval, create `PHASE_2.md` and begin Phase 2 (Danbooru Tag Builder).
