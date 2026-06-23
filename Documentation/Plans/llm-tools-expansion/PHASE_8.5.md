# Phase 8.5 - Workshop Preview & Polish

## Status

**Phase:** 8.5
**Build Status:** Green (0 errors)
**Phase Status:** [x] Complete

---

## Objective

Replace the (never-shipped) multi-image evolve generation loop with a per-node, manually-triggered **preview image** workflow, and tighten the Workshop UX based on real-use feedback. Each prompt snapshot can produce **one** preview image at a time, kept isolated from the main gallery via a generic `IsHidden` flag on the `Image` entity.

This phase also corrects PHASE_8 overstatements (Step 7 was marked complete but the image-generation loop was never implemented).

---

## Context

### Why this is not part of PHASE_8

PHASE_8 closed the text-side of Workshop (sessions, nodes, chat, evolve text-spawn, persistence). The image side was scoped but never executed, and the design has since changed (single per-node preview > parallel batch) so a clean phase boundary is appropriate.

### Direction summary (from user, 2026-04-26)

- Drop multi-image evolve generation. Replace with manual single-image previews on any node.
- Card layout: prompt + instruction stacked left, thumbnail/placeholder right.
- Prompt textfield is editable with auto-save (debounced).
- Add a `Send to ▾` action that targets workflows on the current base (prompts only — preview images never get a send-to).
- Generation params come from `IStateService.GenerationParameters`; only positive prompt, batch size (forced 1), resolution (orientation toggle), and seed (per-session, default 42) are overridden.
- Workflow selector on the right panel; changes are remembered per session.
- Previews are real `Image` rows but flagged `IsHidden = true` and assigned `ProjectId = 0` so they never appear in galleries.
- Chat dialog copy fixes; session-button caption color fix; tree-shape fix.

### Key files this phase touches

- `BlazorWebApp/Data/Entities/Image.cs`
- `BlazorWebApp/Data/Entities/PromptWorkshopNode.cs`
- `BlazorWebApp/Data/AppDbContext.cs` (model snapshot side-effects)
- `BlazorWebApp/Migrations/` (two new hand-authored migrations)
- `BlazorWebApp/Services/WorkshopService.cs` (+ `IWorkshopService.cs`)
- `BlazorWebApp/Services/DatabaseService.cs` (filter `IsHidden` everywhere)
- `BlazorWebApp/Services/ImageService.cs` (skip accumulator for hidden images, if applicable)
- `BlazorWebApp/Events/WorkshopPreviewGeneratedEventArgs.cs` (new)
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeRow.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopNodeDetail.razor`
- `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopChatDialog.razor`
- `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor`
- `BlazorWebApp/Models/AppState.cs` (per-session preview prefs: `LastWorkflowId`, `LastSeed`, `Orientation`)

### Architectural rules respected

- Pub/sub through `EventService` for the new preview event.
- Hand-authored migrations carry both `[DbContext(typeof(AppDbContext))]` and `[Migration("...")]` and update `AppDbContextModelSnapshot.cs`.
- UI follows the layout language already used by the existing Workshop panes (no hard-coded shell padding, `Variant.Text` for inputs).

---

## Decisions Locked In

| #   | Decision                                                                                                    | Rationale                                                                                                                                                                                                                 |
| --- | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D1  | Use generic `IsHidden` (bool) on `Image` entity, not a workshop-specific flag                               | Reusable for future "scratch" generations.                                                                                                                                                                                |
| D2  | No back-compat for `ImageIdsJson`                                                                           | Dev DB; user accepted. Drop column outright.                                                                                                                                                                              |
| D3  | Default seed value `42`, `-1` still works as random                                                         | Generation pipeline already handles `-1` after clone.                                                                                                                                                                     |
| D4  | Per-session seed (with optional Lock toggle)                                                                | Sibling comparison fairness.                                                                                                                                                                                              |
| D5  | Send-to only sends the **prompt text** to a target workflow's positive field                                | Images are throwaway.                                                                                                                                                                                                     |
| D6  | Hidden images keep the **current** `ProjectId` (not 0)                                                      | `Image.ProjectId` is a required FK to `Project` with cascade delete (verified in `AppDbContextModelSnapshot.cs` line 811-815). `IsHidden` filtering is sufficient isolation; project=0 would break referential integrity. |
| D7  | No "Show hidden" gallery filter for now                                                                     | Avoids confusing users about preview origins.                                                                                                                                                                             |
| D8  | Preview previews use a separate event (`WorkshopPreviewGeneratedEventArgs`), not `ImagesGeneratedEventArgs` | Keeps Results gallery untouched on every preview render.                                                                                                                                                                  |
| D9  | Editable prompt uses **debounced auto-save** (~500 ms)                                                      | More reliable than blur in Blazor; avoids per-keystroke DB writes.                                                                                                                                                        |

---

## Execution Checklist

### Step 1: `Image` entity - add `IsHidden`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `public bool IsHidden { get; set; }` to `Image.cs` (default `false`).
- [ ] Hand-author migration `YYYYMMDDhhmmss_Add_Image_IsHidden.cs` with both required attributes. Up adds `IsHidden` column with default 0; Down drops it.
- [ ] Update `AppDbContextModelSnapshot.cs` `Image` block.
- [ ] Update **every** existing `context.Images` query in `DatabaseService.cs` (paged, sorted, random) to filter `i => !i.IsHidden`. By-id lookups stay unfiltered (workshop and asset panel rely on direct id access).
  - Methods to touch (from grep): `GetPagedImages` (3 overloads), `GetSortedImages`, `GetRandomImages`, `GetRandomFavoriteImage`, project-id list aggregation in constructor (`Select(i => i.ProjectId)` queries on lines ~99-102).

#### Success Criteria

- App boots; existing rows back-fill `IsHidden = false`.
- Existing gallery shows everything as before.

---

### Step 2: `PromptWorkshopNode` - swap `ImageIdsJson` for `PreviewImageId`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `PromptWorkshopNode.cs`: remove `ImageIdsJson`, the `[NotMapped] ImageIds` helper, and the `using System.Text.Json` if no longer needed. Add `public int? PreviewImageId { get; set; }`.
- [ ] Hand-author migration `YYYYMMDDhhmmss_Update_WorkshopNode_PreviewImage.cs`. Up: `DropColumn("ImageIdsJson")`, `AddColumn<int?>("PreviewImageId")`. Down: reverse.
- [ ] Update `AppDbContextModelSnapshot.cs` `PromptWorkshopNode` block.

#### Success Criteria

- App boots, schema updated, no orphaned references compile.

---

### Step 3: `WorkshopService.GeneratePreviewAsync` + state-driven param prep

**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks

- [ ] Inject `IStateService`, `IRouterService`, `IBackendService`, `IIOService`, `IEventService` into `WorkshopService`.
- [ ] Add to `IWorkshopService`:
  ```csharp
  Task<int?> GeneratePreviewAsync(int nodeId, int workflowId, PreviewOrientation orientation, long seed);
  ```
- [ ] Define `enum PreviewOrientation { Portrait, Landscape }`.
- [ ] Implementation outline (single image, hidden, project 0):

  ```csharp
  // Resolve workflow from state.
  var workflow = _state.State.Generation.Workflows.FirstOrDefault(w => w.Id == workflowId);
  if (workflow == null) return null;

  // Clone live params; only override the four locked fields.
  var prepared = _state.GenerationParameters.Clone();
  prepared.WorkflowId = workflow.Id;

  if (prepared.Fragments.TryGetValue(FragmentKeys.Fragments.Prompts, out var prompts))
      prompts.SetValue(FragmentKeys.Params.Positive, node.PromptText);

  if (prepared.Fragments.TryGetValue(FragmentKeys.Fragments.Latent, out var latent))
  {
      // Pick a sensible long-edge based on the workflow base (SDXL ~1024, SD15 ~768, Flux ~1024).
      var (w, h) = ResolvePreviewResolution(workflow, orientation);
      latent.SetValue(FragmentKeys.Params.Width, w);
      latent.SetValue(FragmentKeys.Params.Height, h);
      latent.SetValue(FragmentKeys.Params.BatchSize, 1);
  }

  if (prepared.Fragments.TryGetValue(FragmentKeys.Fragments.MainSampler, out var sampler))
      sampler.SetValue(FragmentKeys.Params.Seed, seed); // -1 stays valid; pipeline rerolls.

  // Run generation through the router.
  var result = await _router.PostGenerationAsync(prepared, workflow);

  // Persist returned bytes via IIOService following the same path the gallery uses,
  // create an Image entity with IsHidden = true, ProjectId = 0, set Workshop node's PreviewImageId.
  ```

- [ ] Add a `ResolvePreviewResolution(Workflow, orientation)` helper. Default table:
      | Base | Long edge |
      |---|---|
      | SD 1.5 | 768 |
      | SDXL / Pony / Illustrious | 1024 |
      | Flux | 1024 |
      | Other | fall back to current state's W/H |
      Portrait => width = short, height = long; Landscape inverted. Snap to multiples of 64.
- [ ] After save, publish `WorkshopPreviewGeneratedEventArgs(nodeId, imageId)`.
- [ ] If a previous preview existed for the node, delete its row + file (best-effort).

#### Success Criteria

- Calling `GeneratePreviewAsync` produces a single 1024x... image, saved with `IsHidden = true, ProjectId = 0`.
- Image is **not** visible in the main gallery.
- Calling again replaces the preview cleanly.

---

### Step 4: `WorkshopPreviewGeneratedEventArgs` + Results gallery left untouched

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Events/WorkshopPreviewGeneratedEventArgs.cs`:
  ```csharp
  public class WorkshopPreviewGeneratedEventArgs : EventArgs
  {
      public int NodeId { get; }
      public int ImageId { get; }
      public WorkshopPreviewGeneratedEventArgs(int nodeId, int imageId) { ... }
  }
  ```
- [ ] Confirm `ImageService.GenerateImagesAsync` is **not** invoked by `WorkshopService` (we call `IRouterService.PostGenerationAsync` directly), so no `ImagesGeneratedEventArgs` fires and the `_batchImages` accumulator stays clean.
- [ ] If we end up routing through `ImageService` for any reason, add an `isHidden` overload that skips the accumulator append and the event publish.

#### Success Criteria

- Generating previews does not refresh / mutate the Results tab.

---

### Step 5: Redesign `WorkshopNodeRow`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] New layout (left content + right thumbnail):
  ```
  [icon]  Prompt (1-2 lines, ellipsed)              [thumb 48x72]
          Instruction (caption, muted, 1 line)      placeholder if null
  ```
- [ ] Thumb resolves via `IIOService.GetImageStaticFile(image.Path)` when `Node.PreviewImageId` is set; otherwise show a muted placeholder icon (`Icons.Material.Filled.ImageNotSupported`).
- [ ] Keep current selected highlight; keep generation-number indent.
- [ ] Inject `IDatabaseService` to fetch the preview image (paged-friendly: load lookup once per session render, not per row).

#### Success Criteria

- Rows visually consistent regardless of preview presence.
- Long prompts don't push the thumbnail off-screen.

---

### Step 6: Redesign `WorkshopNodeDetail` right panel

**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks

- [ ] New layout (top-down):
  - Header: mode icon + label + model badge + Gen N / date.
  - **Editable** `MudTextField` for the prompt (`ReadOnly="false"`, `Immediate="true"`, debounced auto-save via timer ~500 ms after last keystroke). Show small "Saved" / spinner affordance.
  - Instruction (read-only, only when present).
  - Preview image area (~320 wide, aspect honored). Placeholder when missing.
  - Preview controls block:
    - Workflow `MudSelect` (sourced from current base; persisted in `AppState.Prompts.LLM.Workshop.LastWorkflowId`).
    - Orientation `MudToggleGroup` (Portrait default / Landscape).
    - Seed `MudNumericField<long>` (default 42, allow -1).
    - "Lock seed across session" `MudSwitch` (persisted on the session row in state).
    - `Generate Preview` `MudButton` with spinner while in flight.
  - Actions row:
    - `Copy` (existing).
    - `Send to ▾` `MudMenu` listing image-capable workflows for the current base; click writes the prompt to that workflow's positive fragment via `IOrchestratorService` (see existing pattern in `OrchestratorService.cs` line 478).
    - `Chat Edit`, `Spawn Variations` (existing).
- [ ] Wire `Generate Preview` to `WorkshopService.GeneratePreviewAsync(...)`. Show snackbar on completion / error.
- [ ] Subscribe to `WorkshopPreviewGeneratedEventArgs` for the active node and refresh the image.

#### Success Criteria

- Editing the prompt updates the DB after debounce.
- Generate Preview produces an image visible in this panel only.
- Send-to dropdown lists Txt2Img/Img2Img workflows for the active base; selecting one updates that workflow's positive prompt and snackbars success.
- All controls retain their values when switching nodes within the same session.

---

### Step 7: Send-to dropdown for prompts

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `IPromptSendToService` (mirrors `IImageSendToService` shape):
  ```csharp
  List<Workflow> GetPromptTargetWorkflows();          // Txt2Img + Img2Img on current base
  Task SendPromptToWorkflowAsync(string promptText, Workflow workflow);
  ```
- [ ] Implementation reuses the orchestrator path: `OrchestratorService.ApplyOrQueue(Fragments.Prompts, Params.Positive, prompt, queue)`.
- [ ] Register singleton in `Program.cs`.
- [ ] Use it from `WorkshopNodeDetail`.

#### Success Criteria

- Send-to writes the prompt and snackbars; navigating to the target workflow shows the prompt populated.

---

### Step 8: Fix `Send to Workshop` landing

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `WorkshopView.razor`, on `OnInitializedAsync`, after `RefreshSessionsAsync`, check `State.State.Prompts.LLM.Workshop.ActiveSessionId`. If non-zero and present in `_sessions`, call `SelectSessionAsync` for it.

#### Success Criteria

- Clicking "Send to Workshop" from any upstream view lands on the new session with its root node selected.

---

### Step 9: Polish

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] **Session button caption color**: drop `text-primary` for the active session caption; keep it `text-disabled` so only the title carries the color.
- [ ] **Chat dialog**:
  - Replace the top `InfoMessage` with a plain `<MudText Typo="Typo.body2" Class="text-disabled">Edit prompt through conversation with the LLM.</MudText>`.
  - Remove `NoIcon="true"` from the depth `InfoMessage`.
  - Add a small "Parent only" `MudChip` quick action that sets depth to 1.
- [ ] **Lock seed**: persisted on session (state-side) — when on, `Generate Preview` reuses the saved seed regardless of node.

#### Success Criteria

- Visual diffs match the user requests.

---

### Step 10: Tree shape - group children by parent

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `WorkshopView.razor`, replace `_allNodes = session.Nodes.OrderBy(n => n.CreatedAt)` with a depth-first traversal that walks roots (`ParentId == null`), then recurses into children ordered by `CreatedAt`.
- [ ] Adjust `WorkshopNodeRow` indent to use **tree depth** (computed during traversal) instead of `GenerationNumber`.

#### Success Criteria

- Evolve siblings render as children of the parent, not interleaved with chat descendants.

---

## Progress Tracking

| Step | Status | Complexity | Notes                    |
| ---- | ------ | ---------- | ------------------------ |
| 1    | [ ]    | 2          | Image.IsHidden + filters |
| 2    | [ ]    | 2          | Node.PreviewImageId      |
| 3    | [ ]    | 5          | GeneratePreviewAsync     |
| 4    | [ ]    | 2          | Preview event            |
| 5    | [ ]    | 2          | NodeRow redesign         |
| 6    | [ ]    | 5          | NodeDetail redesign      |
| 7    | [ ]    | 3          | PromptSendToService      |
| 8    | [ ]    | 1          | Landing fix              |
| 9    | [ ]    | 2          | Polish                   |
| 10   | [ ]    | 2          | Tree grouping            |

**Total:** 26 points.

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] After Step 2 (schema settled together)
- [ ] After Step 4 (service + event)
- [ ] After Step 7 (UI redesign + send-to)
- [ ] After Step 10 (polish + tree fix)

---

## Open Risks

1. **`IsHidden` filter coverage** - missing one query path means hidden images leak into a gallery view. Mitigation: grep all `context.Images` usages and audit each one in Step 1.
2. ~~**`ProjectId = 0` foreign key**~~ - Resolved as D6: required FK with cascade; keep current ProjectId, rely on `IsHidden` filter alone.
3. **Workflow base resolution** - `Workflow.Base` matching for "SD 1.5" vs "SDXL" varies; the resolution helper must be defensive (fall back to state W/H).
4. **Debounced auto-save** - need to dispose the timer on node switch / panel disposal to avoid stale writes overwriting newer node prompts.
5. **Send-to from a node with no preview** - confirmed: send-to operates on prompt text only, no preview required.

---

## Phase Summary

_To be filled in on completion._
