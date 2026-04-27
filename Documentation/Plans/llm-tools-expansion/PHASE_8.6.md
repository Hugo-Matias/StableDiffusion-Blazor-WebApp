# Phase 8.6 - Workshop UX Overhaul

## Status

**Phase:** 8.6
**Build Status:** Green
**Phase Status:** [x] Complete

---

## Objective

Take the Workshop tool from "functional" to "delightful" by overhauling six pain points: variation control, hierarchy display, preview queueing, session-rail footprint, seed convention, and send-to ergonomics. The end state is a chat-first iterative prompt builder where evolve, preview generation, and session creation feel like natural extensions of the conversation flow.

This phase is a successor to PHASE_8.5 and does not change the schema (only `AppState` JSON additions).

---

## Context

### Why this phase

PHASE_8.5 closed the preview-image loop and made the right panel useful, but real-world usage exposed ergonomic gaps:

- Evolve output is a black box (no intensity/target control).
- The indented list hides hierarchy at a glance and forces a modal for every chat edit.
- Previews are manual one-at-a-time despite ComfyUI's queue.
- The 220px session sidebar steals horizontal space the snapshot tree needs.
- The Lock-Seed switch breaks convention with the Generate page.
- Send-to is buried behind a dropdown when AssetInfoPanel already proves a button array works.

### Direction summary (user, 2026-04-27)

- Combined **structured + freeform** evolve controls; expose the rendered system prompt for transparency.
- Replace center indented list with a **chat-thread layout** that renders evolve children as a horizontal carousel under their parent. Inline composer at the bottom replaces the Chat Edit modal for the common case.
- New session creation integrated into the chat: empty state shows the composer with a hint; first send opens a name dialog, then chat continues normally.
- Auto-queue previews after node creation, fire-and-forget, with snapshotted params (so users can keep tweaking the Generate page).
- "Use Enhancements" switch on the right panel, **off by default**.
- Drop the Lock-Seed toggle; `-1` means random per call (matches Generate page).
- Convert `Send to` dropdown to a button array on the active node bubble; absorb the Copy action into the same row (icon-only).
- Sessions sidebar collapses to an icon rail (matches the existing layout pattern used elsewhere in the app).

### Key files this phase touches

- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor` (largest rewrite)
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeRow.razor` -> replaced by `WorkshopChatBubble.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeDetail.razor` (preview/controls only; prompt+actions move to bubble)
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopComposer.razor` (new)
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopVariationCarousel.razor` (new)
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopEvolveDialog.razor` (expanded)
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopChatDialog.razor` (kept for advanced overrides only)
- `BlazorWebApp/Services/WorkshopService.cs` (evolve control plumbing, snapshot+enhancement-strip in preview)
- `BlazorWebApp/Models/AppState.cs` (`AppStatePromptsLLMWorkshop` additions/removals)
- `BlazorWebApp/Events/WorkshopPreviewGeneratedEventArgs.cs` (add `Status` enum: Queued / Completed / Failed)
- `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor.css` -> consider promoting `.send-to-section` / `.send-to-btn` to a shared file (see Step 6).

### Architectural rules respected

- Pub/sub through `EventService` for the new queued/completed/failed status event.
- No new DB schema. State persisted via existing `AppState` JSON path.
- Reuse `OrchestratorService` / `IPromptSendToService` for send-to writes; do not bypass.

---

## Decisions Locked In

| #   | Decision                                                                                                                                                     | Rationale                                                                                    |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| D1  | Drop `LockSeed` from state and UI; `-1` = random per call                                                                                                    | Convention parity with Generate page.                                                        |
| D2  | Center panel becomes a chat thread; evolve siblings render as a horizontal carousel under their parent                                                       | Hierarchy visible at a glance; chat flow lowers Chat Edit friction; branching preserved.     |
| D3  | Inline composer at bottom; first message in an empty session opens the **New Session** dialog, then continues as a chat child                                | Removes the "Create session before doing anything" friction.                                 |
| D4  | Auto-queue previews after `AddChatChildAsync` / `SpawnVariationsAsync`; fire-and-forget, ComfyUI serializes server-side                                      | Matches user mental model; no client-side queue needed.                                      |
| D5  | Snapshot the cloned `GenerationParameters` per call before kicking off the queue                                                                             | User can keep tweaking the Generate page without affecting in-flight previews.               |
| D6  | "Use Enhancements" toggle on right panel; **off by default**; when off, strip enhancement fragments from the snapshot                                        | Detailer/upscaler/refiner are noise for previews and slow them down.                         |
| D7  | Evolve dialog gains: Intensity (4 stops), Targets (multi-select chips), Preserve list, Length budget, Custom direction, Temp                                 | Combined structured + freeform control. See Step 4 for full list.                            |
| D8  | Send-to becomes a button array on the active node bubble; Copy is an icon button in the same row                                                             | Mirrors AssetInfoPanel; uses the space we have.                                              |
| D9  | Session sidebar collapses to ~56px rail; expanded ~220px; collapse state persisted                                                                           | Same UX pattern used elsewhere in the layout system.                                         |
| D10 | New event status enum (`WorkshopPreviewStatus`: Queued / Completed / Failed) added to existing `WorkshopPreviewGeneratedEventArgs` instead of a second event | Single subscription point per consumer; backwards compatible if we keep the field defaulted. |

---

## Additional Evolve Controls Worth Adding (answer to user question #1)

Beyond Intensity / Targets / Custom direction proposed in chat, three more provide outsized value for the cost:

1. **Preserve tokens** (Step 4): explicit list of phrases/tokens the LLM must keep verbatim. The natural counterpart to Targets (Targets = "what to mutate", Preserve = "what to leave alone"). Pasted as a comma-separated list.
2. **Length budget** (Step 4): `Match base` / `Concise (~50 tokens)` / `Verbose (~120 tokens)`. Useful for keeping prompts within CLIP/T5 budgets across siblings.
3. **Temperature override** (Step 4): per-evolve LLM temperature, default = the model's panel value. Decouples chaos from intensity (low intensity + high temp = creative micro-tweaks; high intensity + low temp = deterministic-but-bold rewrites).

Deferred (recorded under Open Risks for a future phase):

- **Crossover / Reference node**: pick a sibling, ask LLM to merge traits. Genuinely useful but adds UI surface and prompt-engineering complexity disproportionate to this phase.
- **Style anchor presets** (Cinematic / Editorial / Anime / etc.): overlaps heavily with Targets.Style + Custom direction; keep as a possible Phase 9+ refinement.

---

## Execution Checklist

### Step 1: Drop Lock-Seed convention

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Remove `LockSeed` field from `AppStatePromptsLLMWorkshop` (the JSON column tolerates removed fields; no migration needed).
- [x] Remove the `MudSwitch` and `OnLockSeedChanged` handler from `WorkshopNodeDetail.razor`.
- [x] In `OnGeneratePreviewAsync`, stop client-side seed rolling. Always pass `_workshopState.LastSeed` straight through; backend handles `-1`.
- [x] Update the seed field's helper text to "Use -1 for random".

#### Success Criteria

- Seed of -1 produces a different image each preview; any other value reproduces.
- No leftover references to `LockSeed` compile-time or in saved state.

#### Changes Made

- `BlazorWebApp/Models/AppState.cs` - removed `LockSeed` property from `AppStatePromptsLLMWorkshop`.
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeDetail.razor` - replaced the seed row's `MudTooltip`+`MudSwitch` with a single full-width `MudNumericField` carrying `HelperText="Use -1 for random"`; deleted `OnLockSeedChanged`; simplified `OnGeneratePreviewAsync` to pass `_workshopState.LastSeed` straight through (no client-side roll, no post-call seed rewrite).

Build: clean (only pre-existing `Magick.NET-Q16-AnyCPU` NuGet vulnerability warnings, unrelated).

---

### Step 2: Send-to button array + Copy on the bubble

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Promote `.send-to-section` / `.send-to-btn` / `.send-to-buttons` from `AssetInfoPanel.razor.css` into a shared CSS file (`wwwroot/css/send-to.css`). `AssetInfoPanel.razor.css` now points to it via a comment placeholder.
- [x] Render the action row in `WorkshopNodeDetail.razor` for now (the bubble component arrives in Step 3): one `.send-to-btn` per `IPromptSendToService.GetParameterWorkflows()` entry + a small `MudIconButton` (Icon=ContentCopy, Size=Small, Variant=Text) at the end. Step 3 will physically move this row into `WorkshopChatBubble.razor`.
- [x] Remove the `MudMenu Send to` and the standalone Copy button from `WorkshopNodeDetail.razor`.
- [x] Update [04-UI-DESIGN-LANGUAGE.md](../../Architecture/04-UI-DESIGN-LANGUAGE.md) with the `.send-to-btn` style as the canonical look for simple non-primary action buttons (covers future Chat Edit / Spawn Variations migration in Step 3).

#### Success Criteria

- Button array renders only on the **active** node bubble (other bubbles stay clean).
- Clicking a workflow chip triggers `PromptSendToService.SendPromptToWorkflow`; clicking copy puts the prompt on clipboard.
- Visual parity with `AssetInfoPanel`'s send-to row.

#### Changes Made

- **New:** `BlazorWebApp/wwwroot/css/send-to.css` - shared global stylesheet for `.send-to-section`, `.send-to-label`, `.send-to-buttons`, `.send-to-btn` (+ mode-\* hover variants). The `::deep .mud-icon-root` rule from the scoped file became `.send-to-btn .mud-icon-root` since it is now globally scoped.
- **Modified:** `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor.css` - removed the migrated rules; left a pointer comment.
- **Modified:** `BlazorWebApp/Pages/_Layout.cshtml` - added `<link href="/css/send-to.css" rel="stylesheet" />` after `site.css`.
- **Modified:** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeDetail.razor` - swapped the `MudMenu Send to` + standalone `Copy` button for a `.send-to-section` row with one `.send-to-btn` per parameter workflow plus an icon-only `MudIconButton` ContentCopy.
- **Documentation:** `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` - new "Simple action buttons (`.send-to-btn`)" subsection plus two new anti-patterns.

Build: clean (only pre-existing nullable-ref warnings in unrelated files).

#### Step Note

The phase doc originally placed this row inside `WorkshopChatBubble.razor` (Step 3). Since the bubble does not exist yet, the row was added to `WorkshopNodeDetail.razor` to keep Step 2 self-contained and shippable. Step 3 will migrate the same markup into the bubble and remove it from the detail panel.

---

### Step 3: Chat-thread center layout (`WorkshopChatBubble` + `WorkshopVariationCarousel`)

**Complexity:** 8
**Status:** [x] Complete

#### Tasks

- [x] Create `WorkshopChatBubble.razor` that renders a single node:
  - Layout: bubble aligned **left** for `Mode == "root" | "evolve"`, **right** for `Mode == "chat"` (instruction = user message).
  - Bubble content: optional instruction line (small, muted) + prompt body (truncated with "Show more" toggle on long bubbles) + thumbnail strip on the right (current PHASE_8.5 thumb behavior, minus the panel's own preview area).
  - Active highlight: subtle border + the action row (Send-to buttons, Copy icon). Inactive bubbles render content only.
  - Click bubble body -> `OnSelect.InvokeAsync(node)` (same hook the row had).
- [x] Create `WorkshopVariationCarousel.razor`:
  - Renders evolve siblings (parent has 2+ children all `Mode=="evolve"`) as a horizontal scrollable strip of compact cards (thumb + first ~40 chars of prompt).
  - Active card highlighted; clicking promotes that node as current.
  - Left/right scroll buttons appear when overflow.
- [x] Rewrite `WorkshopView.razor` center column:
  - Walk the tree depth-first as today, but emit:
    - A `WorkshopChatBubble` for root + chat nodes.
    - A `WorkshopVariationCarousel` whenever a parent has >=2 evolve children, replacing the per-child bubble emission for those siblings.
    - When evolve children have descendants, recurse into them only if they're on the active branch (the carousel acts as a fork point).
  - Ensure auto-scroll to the active bubble on selection.
- [x] Delete `WorkshopNodeRow.razor`.
- [x] Move the prompt edit `MudTextField` and the `Spawn Variations` / `Chat Edit` action buttons OUT of `WorkshopNodeDetail.razor`. The right panel keeps **only** preview controls (workflow, orientation, seed, "Use Enhancements", Generate Preview button, the preview image itself).

#### Success Criteria

- Linear chat sessions read top-to-bottom like a messaging app.
- A parent with 4 evolve children shows one carousel under the parent, not 4 stacked rows.
- Picking a variation expands its descendants below; switching variations swaps that subtree.
- Right panel retains preview-only controls; bubble carries the prompt + actions.

#### Changes Made

- New `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopChatBubble.razor`. Mode-based alignment, instruction caption, debounced prompt edit on the active bubble, thumb with `WorkshopPreviewGeneratedEventArgs` subscription, send-to button row + Copy icon on the active bubble, show more / show less for prompts > 280 chars.
- New `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopVariationCarousel.razor`. Horizontal strip of 140px cards (3:4 thumb + 2-line text); active card highlighted; click promotes via `OnSelect`; per-card thumb cache subscribes to `WorkshopPreviewGeneratedEventArgs`.
- New `BlazorWebApp/wwwroot/css/workshop-thread.css`. Globally-scoped `.workshop-thread`, `.workshop-bubble*`, `.workshop-carousel*`, plus `.workshop-bubble-chip*` for the prev/next chat-edit switcher. Linked from `Pages/_Layout.cshtml`.
- New `workshopChat.scrollToBottom` helper in `wwwroot/js/Site.js`. Smooth scroll for new bubbles.
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor` rewrite of the center column:
  - `BuildTimeline` walks **only** the active path (computed from `currentNodeId` upward via `ParentId`).
  - Children are split into evolve vs chat. Evolve >= 2 -> carousel + recurse into the active sibling; lone evolve -> recurse only when on path.
  - Chat children: pick the active-path child (or fall back to latest); when there are 2+ chat siblings, the chosen bubble carries the full sibling list so it can render a `Edit k of N` chip with prev/next chevrons.
  - Off-path siblings never render inline as bubbles; the chip / carousel are the only switchers. Resolves the interleaving bug reported on mixed chat+evolve trees with multiple chat siblings under one parent.
  - Header above the thread carries `Chat Edit` / `Spawn Variations` `.send-to-btn` buttons (placeholder until Step 4's composer subsumes them).
  - `OnAfterRenderAsync` fires `workshopChat.scrollToBottom` whenever the active node changes.
- `WorkshopNodeDetail.razor` slimmed to preview controls only: header (mode + model + timestamp), preview image, workflow / orientation / seed / Generate Preview. Prompt textfield, instruction card, send-to row, Chat Edit / Spawn Variations buttons, and `_promptDraft` / `OnPromptDebouncedAsync` / `CopyPromptAsync` removed. `OnChatEditClick` / `OnEvolveClick` parameters dropped.
- `WorkshopNodeRow.razor` deleted.

#### Notes

- The chat-sibling chip uses `@onclick:stopPropagation` so prev/next don't re-trigger the parent bubble's `OnSelect`. Buttons disable at the ends.
- `Walk` ordering for chat siblings is `OrderBy(CreatedAt)`, so the chip's index is stable and matches creation order.
- The `Chat Edit` / `Spawn Variations` header buttons are still wired through to the existing dialogs; Step 4 replaces them with the inline composer and the expanded evolve dialog.

---

### Step 4: Inline composer + new-session integration

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [ ] Create `WorkshopComposer.razor`:
  - `MudTextField` (multiline, AutoGrow) + `MudButton` Send + small "advanced" gear icon that opens the existing `WorkshopChatDialog` (now reduced to depth slider + model override).
  - "Spawn Variations" button next to Send (opens the expanded `WorkshopEvolveDialog` from Step 5).
  - Disabled while `_isProcessing`.
- [ ] Empty-state behavior:
  - When `_activeSession == null` (no session selected, regardless of whether sessions exist) the center column shows: a muted hint "Type your initial prompt to start a new session." + the composer. The session sidebar still works for switching.
  - When a session exists but is empty (defensive; shouldn't happen since CreateSession always seeds a root), same hint.
- [ ] First-send behavior in empty state:
  - Intercept the composer Send.
  - Open the existing `WorkshopSessionDialog` pre-filled with the typed prompt as `Prompt` and an empty `Name`. User confirms or cancels.
  - On confirm: `CreateSessionAsync(name, prompt)`, refresh, select the new session, treat the typed prompt as the **root**, NOT a chat child. Auto-queue preview if the right panel has a workflow selected (Step 6).
  - On cancel: do nothing; composer keeps the text.
- [ ] Composer Send in normal state -> `AddChatChildAsync` against `_activeSession.CurrentNodeId`. After success, set the new child as current and auto-queue its preview (Step 6).
- [ ] `WorkshopChatDialog` UI trim: keep the depth slider + model selector + "Parent only" chip; drop the duplicated instruction textfield (the composer carries it). It opens with the composer's current text and returns `(depth, model)` overrides.
- [ ] Refactor `WorkshopEvolveDialog.razor` to add the new controls:
  - **Intensity** `MudToggleGroup`: Subtle / Moderate / Strong / Wild.
  - **Targets** `MudChipSet T="string" SelectionMode="MultiSelection"`: Subject, Clothing, Pose, Setting, Camera, Lighting, Style, Mood. Empty = freeform.
  - **Preserve** `MudTextField` (comma-separated, optional).
  - **Length** `MudToggleGroup`: Match / Concise / Verbose.
  - **Custom direction** `MudTextField` (multiline, optional).
  - **Temperature** `MudSlider 0.1-1.5` defaulting to the model's panel temp.
  - **Count** existing.
  - Collapsible `<details>` "Show resolved system prompt" rendering the interpolated template (live preview).
- [ ] Wire `WorkshopService.SpawnVariationsAsync` to accept an `EvolveControls` record (new file under `Models/`) with the above fields. Build the system prompt by interpolating into a template; pass `temperature` to `OllamaService.SendChatMessage` (extend the call signature if needed).
- [ ] Persist last-used Intensity / Targets / Length on the session via `AppStatePromptsLLMWorkshop` (new fields). Custom direction + Preserve are NOT persisted (per-call escape hatches).
- [ ] Store the human-readable summary in `Node.Instruction` (e.g. "Subtle, Clothing+Pose, Verbose"). No new column needed.

#### Success Criteria

- With zero sessions, typing in the composer + Send opens the New Session dialog, then renders the new root as a left bubble.
- The Chat Edit modal is no longer the primary chat entry point but remains accessible via the gear button.
- Evolve dialog visibly reflects the new controls; the resolved system prompt updates live as controls change.
- Evolve output is observably more controlled: Subtle + Clothing produces clothing-only tweaks; Wild + empty Targets produces the current-style chaos.

#### Changes Made

- **NEW** `BlazorWebApp/Models/EvolveControls.cs`: defines `EvolveIntensity` (Subtle/Moderate/Strong/Wild), `EvolveLength` (Match/Concise/Verbose), and the `EvolveControls` aggregate (Count, Model, Intensity, Targets, Length, Preserve, CustomDirection, Temperature). Two builders:
  - `BuildInstructionSummary()` -> human-readable label like "Subtle, Clothing+Pose, Verbose" stored in `Node.Instruction`.
  - `BuildSystemPrompt(int count)` -> interpolates intensity/targets/length/preserve/customDirection hints into the LLM system message and appends "Return exactly {count} variations as a numbered list."
- **NEW** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopComposer.razor`: pinned to the bottom of the chat thread (and the empty-state center column). MudTextField (multiline, AutoGrow, Lines=1, MaxLines=6, Variant.Text, Margin.Dense) + actions row with gear icon (`OnAdvanced`), `.send-to-btn` Variations (`OnSpawnVariations`), filled-primary Send (`OnSend`). Enter sends, Shift/Ctrl/Alt+Enter inserts a newline. Public `SetText` / `Clear` / `CurrentText` for parent control. Trims trailing whitespace before invoking `OnSend`.
- **NEW** CSS in `BlazorWebApp/wwwroot/css/workshop-thread.css`:
  - `.workshop-composer` (border-top, dark bg, flex 0 0 auto, align flex-end).
  - `.workshop-empty-state` + `.workshop-empty-state-icon` (centered hint with Awesome icon for empty-session entry).
  - `.workshop-evolve-preview` + `.workshop-evolve-preview-text` (collapsible `<details>` block with monospace pre, max-height 220px scroll).
- **REWRITE** `WorkshopEvolveDialog.razor`: combined structured + freeform controls.
  - Variations `MudNumericField` (1-10) + Model `MudSelect`.
  - Intensity `MudToggleGroup<EvolveIntensity>` (single).
  - Targets `MudChipSet` v6 API (`Filter MultiSelection`, chips with `Default=...` for restore, `SelectedChipsChanged` -> `OnTargetChipsChanged`). Eight chips: Subject, Clothing, Pose, Setting, Camera, Lighting, Style, Mood.
  - Length `MudToggleGroup<EvolveLength>` (single).
  - Temperature `MudSlider 0.1-1.5 step 0.05`.
  - Preserve + CustomDirection MudTextFields (escape hatches; not persisted).
  - Live `<details>` preview computed via `_controls.BuildSystemPrompt(_controls.Count)` directly in markup -> updates on every control change without manual cache.
  - Returns `EvolveControls` directly via `DialogResult.Ok(_controls)` (no nested result type).
- **REWRITE** `WorkshopChatDialog.razor`: trimmed to depth slider + model selector + "Parent only" chip when depth=1. Instruction textfield removed (composer carries the message). Returns `WorkshopChatResult { AncestorDepth, Model }`. Now opened from the composer's gear icon as "Chat Options".
- **MODIFY** `BlazorWebApp/Models/AppState.cs::AppStatePromptsLLMWorkshop`: added `Intensity` (default Moderate), `Targets` (List<string>), `Length` (default Match), `Temperature` (float, default 0.9f). CustomDirection + Preserve intentionally not persisted (per-call escape hatches).
- **MODIFY** `IWorkshopService.SpawnVariationsAsync` signature: `Task<List<PromptWorkshopNode>> SpawnVariationsAsync(int sessionId, int parentId, EvolveControls controls)`. Implementation passes `new OllamaOptions { Temperature = controls.Temperature }` to `OllamaService.SendChatMessage` (no service signature change needed - `OllamaOptions.Temperature` already supported). System prompt comes from `controls.BuildSystemPrompt(count)`. Each child stores `controls.BuildInstructionSummary()` + " (i/n)" in `Node.Instruction`.
- **REWIRE** `WorkshopView.razor`:
  - Header buttons removed; `<WorkshopComposer>` rendered at the bottom of the thread column (and standalone in empty-state).
  - Empty state (`_activeSessionId == 0`): renders `.workshop-empty-state` hint + composer; Send opens `WorkshopSessionDialog` pre-filled with `Prompt=text` and `Name=text.Substring(0, min(40, len))`. Confirm -> `CreateSessionAsync(name, prompt)` -> select session (typed prompt becomes the root, NOT a chat child). Cancel -> composer keeps the text.
  - Normal state: composer Send -> `AddChatChildAsync(session, current, text, model, ancestorDepthOverride)` -> set new child as current. On error, restores composer text via `_composer?.SetText(text)` so typing isn't lost.
  - Composer gear -> opens slim `WorkshopChatDialog` -> persists `AncestorDepth` to `AppState`.
  - Composer Variations -> opens new `WorkshopEvolveDialog` with `Initial` seeded from `AppState` (SpawnCount, Intensity, Targets, Length, Temperature) -> on confirm persists those four back to `AppState` and calls `SpawnVariationsAsync(session, current, controls)`.
  - Removed unused dialog-state fields (`_newSessionName`, `_newSessionPrompt`, `_chatInstruction`, `_evolveCount`).
- **Build status:** Green. No new warnings.

---

### Step 5: Collapsible session rail

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Locate the existing collapsible-rail pattern in the layout system (likely under `Components/Layouts/` - confirm during implementation) and reuse it.
- [x] Wrap the session list in the rail. Collapsed (~56px): show an icon (`Icons.Material.Filled.Forum` or similar) per session with a tooltip carrying the name + node count; the active session has a primary-colored indicator. The "+" button stays at top.
- [x] Expanded (~220px): current behavior.
- [x] Persist `IsSidebarCollapsed` on `AppStatePromptsLLMWorkshop`; default = collapsed.
- [ ] Hover-to-peek (optional, only if the existing pattern provides it cheaply). _Skipped - the existing TwoColumnLayout pattern doesn't expose hover-peek; click-to-expand is the established UX in the app._

#### Success Criteria

- Default load shows the icon rail; sessions are still one click away.
- Clicking the toggle expands the rail in-place without reflowing the chat area.
- Collapse state survives reload.

#### Changes Made

- **MODIFY** `BlazorWebApp/Models/AppState.cs::AppStatePromptsLLMWorkshop`: added `SessionRailCollapsed` (bool, default `true`). The two-column layout system uses `--app-sidebar-rail-width` for its rail; the Workshop view is a custom 3-column layout so it manages its own rail state per-component instead of going through `TwoColumnLayout`.
- **MODIFY** `BlazorWebApp/wwwroot/css/workshop-thread.css`: added `.workshop-session-sidebar` (220px) + `.workshop-session-sidebar.collapsed` (56px) with a 160ms width transition. The `.workshop-session-sidebar-header` flips to a vertical stack in collapsed mode so the chevron/add buttons line up under each other. `.workshop-session-rail-item` centers a 40x40 icon button per session; `.active` highlight uses a translucent primary background.
- **MODIFY** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor`:
  - Sidebar `MudPaper` now carries `workshop-session-sidebar` (+ `collapsed` modifier) instead of inline width styles.
  - Header has 3 buttons when expanded (Deselect / NewDialog / Collapse-chevron) and 3 stacked buttons when collapsed (Expand-chevron / Deselect / NewDialog). The "Sessions" caption is hidden when collapsed via CSS.
  - When collapsed, each session renders as a `MudIconButton` (Forum icon) wrapped in a `MudTooltip` carrying `Name - N node(s) - MMM d`. Active session shows primary color + the `.active` background highlight.
  - When expanded, the original full button list is preserved.
  - New `_sessionRailCollapsed` field seeded from AppState in `OnInitializedAsync`.
  - New `ToggleSessionRailAsync` method flips state and persists via `State.SaveState()`.

**Build status:** Green. No new warnings.

---

### Step 6: Auto-queue previews + snapshot params + Use Enhancements toggle

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Extend `WorkshopPreviewGeneratedEventArgs` with a `Status` property (`Queued | Completed | Failed`). Existing call sites publish `Completed`/`Failed`. Add a new publish at the start of `GeneratePreviewAsync` with `Status=Queued`.
- [x] In `WorkshopService.GeneratePreviewAsync`, take the snapshot **at the very top** of the method (before any awaits that could touch shared state).
- [x] Add `bool useEnhancements` parameter (default false) to `GeneratePreviewAsync`. When false, deactivate enhancement fragments on the snapshot (Detailer, DetailerCore, LoaderDetailer, RefinerSampler, Upscale, UpscaleSeedVR2, SeedVR2, SeedVarianceEnhancer).
  - **Resolved:** Workflow templates already gate enhancement nodes with `fragment?.IsActive == true` (e.g. `ZImageTxt2ImgWorkflow`, `SDTxt2ImgWorkflow`, `WanImg2VidWorkflow`). Setting `IsActive = false` on the cloned snapshot is the canonical "skip" path; removing fragments from the dict is unnecessary.
- [x] Add `UseEnhancements` (bool, default false) and `AutoQueuePreviews` (bool, default true) to `AppStatePromptsLLMWorkshop`; render a `MudSwitch` for `UseEnhancements` on the right panel above Generate Preview.
- [x] Auto-queue hooks in `WorkshopView.razor`:
  - After `AddChatChildAsync` returns -> fire-and-forget `GeneratePreviewAsync(child.Id, ..., useEnhancements: state.UseEnhancements)`.
  - After `SpawnVariationsAsync` returns -> loop and fire-and-forget for each child.
  - After first-send creates a session -> queue a preview for the new root node.
  - Gate auto-queue behind `Workshop.AutoQueuePreviews`; render the toggle on the thread header beside "X node(s) total".
- [x] In `WorkshopChatBubble.razor` and `WorkshopVariationCarousel.razor`, subscribe to the extended event args and render:
  - `Queued` -> `MudProgressCircular` indeterminate spinner over the thumb placeholder.
  - `Completed` -> swap to the real thumb (existing path).
  - `Failed` -> `ErrorOutline` icon with tooltip carrying `args.ErrorMessage`.

#### Success Criteria

- Spawning 5 variations triggers 5 server-queued previews; the carousel shows 5 spinner placeholders that resolve into thumbs over time.
- Toggling the Generate page's resolution while a preview queue is draining does NOT change in-flight previews.
- With Use Enhancements off, the preview workflow runs without the detailer / upscaler nodes (verified by checking server logs or generation time).
- Turning auto-queue off makes the existing manual Generate Preview button the only path.

#### Changes Made

- **MODIFY** `BlazorWebApp/Events/WorkshopPreviewGeneratedEventArgs.cs`:
  - Added `WorkshopPreviewStatus` enum (`Queued = 0`, `Completed = 1`, `Failed = 2`).
  - Added `Status` and `ErrorMessage` properties to the args class.
  - Kept the legacy `(nodeId, imageId, success)` ctor for backwards compatibility (delegates to the new ctor); existing subscribers that read `Success` keep working unchanged because `Success == Status == Completed`.
  - New ctor `(nodeId, imageId, status, errorMessage)` is what the service now publishes.
- **MODIFY** `BlazorWebApp/Services/IWorkshopService.cs`: `GeneratePreviewAsync` signature now `Task<int?> GeneratePreviewAsync(int nodeId, Guid workflowId, PreviewOrientation orientation, long seed, bool useEnhancements = false)`. Default false means callers who do not opt in get the lean preview workflow.
- **MODIFY** `BlazorWebApp/Services/WorkshopService.cs::GeneratePreviewAsync`:
  - Snapshot (`_state.GenerationParameters.Clone()`) now happens as the very first statement in the method, before any awaits or DB calls. The user can keep tweaking the live Generate page while previews drain.
  - Publishes `WorkshopPreviewStatus.Queued` immediately after the snapshot so the UI can render a placeholder before any server round-trip.
  - When `useEnhancements == false`, walks `EnhancementFragmentKeys` and sets `fragment.IsActive = false` on each fragment present in the snapshot.
  - Failure paths now publish `WorkshopPreviewStatus.Failed` with a meaningful `ErrorMessage` (`"Node not found"`, `"Workflow not found"`, `"Generation failed"`); success path publishes `Completed`.
  - New `EnhancementFragmentKeys` static array centralizes the enhancement set: `Detailer, DetailerCore, LoaderDetailer, RefinerSampler, Upscale, UpscaleSeedVR2, SeedVR2, SeedVarianceEnhancer`.
- **MODIFY** `BlazorWebApp/Models/AppState.cs::AppStatePromptsLLMWorkshop`:
  - `UseEnhancements` (bool, default `false`).
  - `AutoQueuePreviews` (bool, default `true`).
- **MODIFY** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeDetail.razor`:
  - New `MudSwitch` for `UseEnhancements` between the seed field and the Generate Preview button.
  - `OnGeneratePreviewAsync` now passes `_workshopState.UseEnhancements` to the service.
  - New `OnUseEnhancementsChanged(bool)` persists via `State.SaveState()`.
- **MODIFY** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor`:
  - Thread-column header gains a small `MudSwitch` (`Auto-queue`) bound to `State.State.Prompts.LLM.Workshop.AutoQueuePreviews`; toggling it fires `OnAutoQueueChanged` which persists via AppState.
  - New `QueuePreviewIfEnabled(int nodeId)` helper: bails out when auto-queue is off or no workflow is selected; otherwise fires `Task.Run` to call `GeneratePreviewAsync` with the current workflow / orientation / seed / useEnhancements snapshot. Failures are silenced because `GeneratePreviewAsync` already publishes `Failed` events.
  - Wired into three creation paths:
    - `OnComposerSendAsync` -> after `AddChatChildAsync` + `SetCurrentAsync(child)`, queue preview for the new child.
    - `OnComposerFirstSendAsync` -> after creating a new session, queue preview for the loaded root node (`session.Nodes.FirstOrDefault(n => n.ParentId == null)`).
    - `ShowEvolveDialog` -> after `SpawnVariationsAsync`, loop the returned children and queue a preview for each.
- **MODIFY** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopChatBubble.razor`:
  - New `_previewStatus` (`WorkshopPreviewStatus?`) + `_previewError` fields.
  - `OnPreviewGenerated` updates them (in addition to the existing thumbnail refresh on `Completed`).
  - Thumb area now branches: image -> indeterminate `MudProgressCircular` (Queued) -> `ErrorOutline` icon with tooltip (Failed) -> placeholder.
- **MODIFY** `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopVariationCarousel.razor`:
  - Per-card `_statuses` + `_errors` dictionaries keyed by node id.
  - Card thumb area renders the same image / spinner / error-icon / placeholder progression as the bubble.

**Build status:** Green. No new warnings.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                                            |
| ---- | ------ | ---------- | ---------------------------------------------------------------- |
| 1    | [x]    | 1          | Remove Lock-Seed                                                 |
| 2    | [x]    | 2          | Send-to button array + Copy icon on bubble                       |
| 3    | [x]    | 8          | Chat bubble + variation carousel; right panel slimmed to preview |
| 4    | [x]    | 5          | Inline composer + new-session flow + expanded evolve dialog      |
| 5    | [x]    | 2          | Collapsible session rail                                         |
| 6    | [x]    | 5          | Auto-queue + param snapshot + Use Enhancements toggle            |

**Total:** 23 points.

---

## Issues & Resolutions

- **Step 6 - Enhancement-fragment skip contract.** Confirmed that workflow templates (`ZImageTxt2ImgWorkflow`, `ZImageTxt2ImgUpscaleWorkflow`, `ZImageImg2ImgWorkflow`, `SDTxt2ImgWorkflow`, `WanImg2VidWorkflow`, etc.) gate enhancement nodes with `fragment?.IsActive == true`. The canonical "skip" path is to set `IsActive = false` on the cloned snapshot's fragments; removing them from the dict is unnecessary and would break any code that re-reads the same snapshot. `WorkshopService.GeneratePreviewAsync` now mutates `IsActive` instead of touching `Fragments.Remove`.

---

## Commit Checkpoints

- [ ] After Step 2 (cleanup wins shipped early)
- [ ] After Step 3 (visual rework solid before composer rewires it)
- [ ] After Step 4 (composer + evolve controls together)
- [ ] After Step 6 (auto-queue closes the loop)

---

## Open Risks

1. **Carousel descendant rendering** - When an evolve child has its own descendants, only the active branch should expand below. Naive recursion will explode the layout. Mitigation: track an `_activeBranchIds` set during tree walk.
2. **Composer focus stealing** - In an empty session the composer must focus on first render; in a populated session focus should stay where the user puts it. Mitigation: only `JSRuntime.FocusAsync` on the empty path.
3. **First-send dialog cancel UX** - If the user cancels the New Session dialog, we must keep the typed prompt in the composer (do not clear). Easy to forget.
4. **Enhancement-fragment removal contract** - As noted in Step 6, the orchestrator's "skip fragment" mechanism may not be `Fragments.Remove`. Confirm before relying on it; fallback is an `IsEnabled` flag if one exists, else feature-flag the toggle off until a follow-up.
5. **Auto-queue with -1 seed** - When 5 evolve siblings auto-queue with seed=-1, each will get a different random seed (good for sibling differentiation). With a fixed seed, all 5 will look prompt-driven only (also intentional). Document in tooltip on the seed field.
6. **Crossover / Reference-node evolve** - Deferred. If user demand returns, plan a Phase 9+ delta that adds a "Reference sibling" picker to `WorkshopEvolveDialog` and a merging system prompt template.
7. **CSS extraction risk for send-to** - Pulling `.send-to-btn` out of `AssetInfoPanel.razor.css` into a shared file means the rules become globally scoped. Audit collisions before committing.

---

## Phase Summary

_To be filled in on completion._
