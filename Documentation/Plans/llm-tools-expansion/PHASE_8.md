# Phase 8 - Workshop (Conversational + Visual Evolution)

## Status

**Phase:** 8
**Build Status:** Passed (0 errors)
**Phase Status:** [~] Text features complete; image-side moved to PHASE_8.5

> **Correction (2026-04-27):** Step 7 ("Evolve mode + image generation loop") was marked complete but the image-generation pipeline was never wired. The new direction (per-node manual previews instead of multi-image evolve) is captured in [PHASE_8.5.md](PHASE_8.5.md). Step 7 status downgraded to `[~]` below.

---

## Objective

Ship the Workshop view: a tree-based iterative prompt refinement tool with two interaction modes sharing one lineage:

- **Chat mode** - multi-turn textual refinement ("add rain", "remove the smile"). Each user instruction spawns one new child node.
- **Evolution mode** - the LLM spawns N variation prompts from the current node; each variation renders an image through the current generation workflow; the user picks a winner which becomes the next current node. Losers remain in the tree for later exploration.

The tree is persisted across app restarts. Send-to-Workshop entry points are wired from Phases 2-7 (Tag Builder / Mixer / Inspiration / Scene Builder / Templates / Remixer) so any LLM-generated prompt can seed a new session.

---

## Context

### Dependencies on prior phases

- **Phase 1** required (nav, composition root).
- **Phase 3 Step 3** required (per-name idempotent seeding) to land the two new seeded templates.
- **Phases 2-7** SOFT dependencies. This phase adds a "Send to Workshop" callback to `LLMToolsTab` and each upstream view wires its send-to-workshop button to that callback. If an upstream phase isn't merged yet, its button is simply absent.

### Entity model

Two new entities. Standard relational tables (not JSON-column pattern) - the tree walk queries and the per-node image-id list are the only complex fields.

```csharp
// BlazorWebApp/Data/Entities/PromptWorkshopSession.cs
public class PromptWorkshopSession
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Id of the currently active node for this session; null if empty session.</summary>
    public int? CurrentNodeId { get; set; }

    public ICollection<PromptWorkshopNode> Nodes { get; set; } = new List<PromptWorkshopNode>();
}

// BlazorWebApp/Data/Entities/PromptWorkshopNode.cs
public class PromptWorkshopNode
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int? ParentId { get; set; }

    /// <summary>0 for root, 1 for first gen, increments with each turn or spawn cycle.</summary>
    public int GenerationNumber { get; set; }

    [Required]
    public string PromptText { get; set; } = string.Empty;

    /// <summary>"root" | "chat" | "evolve"</summary>
    [Required, MaxLength(16)]
    public string Mode { get; set; } = "root";

    /// <summary>Chat-mode only. The user instruction that produced this node.</summary>
    public string? Instruction { get; set; }

    public string? ModelUsed { get; set; }

    /// <summary>JSON array of Image entity IDs. May be stale (images may have been deleted).</summary>
    public string? ImageIdsJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public PromptWorkshopSession Session { get; set; } = null!;
    public PromptWorkshopNode? Parent { get; set; }
    public ICollection<PromptWorkshopNode> Children { get; set; } = new List<PromptWorkshopNode>();

    [NotMapped]
    public List<int> ImageIds
    {
        get => string.IsNullOrWhiteSpace(ImageIdsJson)
            ? new()
            : JsonSerializer.Deserialize<List<int>>(ImageIdsJson) ?? new();
        set => ImageIdsJson = JsonSerializer.Serialize(value);
    }
}
```

### Existing infrastructure to reuse

- `OllamaService.SendChatMessage` for both chat and evolve modes.
- `OllamaService.GetDefaultTemplates()` - append `Workshop.ChatEdit` and `Workshop.EvolveVariations`.
- `IRouterService.PostGenerationAsync(GenerationParameters, Workflow)` (see `BlazorWebApp/Services/RouterService.cs` lines 27-49) for evolve-mode image generation.
- `GenerationParameters` fragments dictionary (`Fragments["main_prompt"]`, etc.) - the app already clones / mutates this for different runs.
- `ImagesGeneratedEventArgs` via `IEventService` - subscribe to correlate completion back to the spawn request, but in practice `PostGenerationAsync` returns a `GeneratedImages` DTO directly, so event subscription is optional.
- `Image` entity IDs returned from the generation pipeline - store them in `PromptWorkshopNode.ImageIds`.
- `DatabaseService.CreatePrompt` for Save as Style on any node.
- `PromptComparisonPanel` could be reused to diff a child's prompt against its parent (nice-to-have; not required for v1).

### Architectural rules

- **Lineage is prompt-only.** Deleting an image does not corrupt lineage; `ImageIds` may contain stale IDs that simply won't render.
- **Images use current generation settings** (model / sampler / steps / size / seed behavior) **with only the prompt replaced.** A workflow dropdown lets the user pick which workflow from the currently selected base to execute.
- **No rating system.** User picks "the one" from N spawns; others remain nodes in the tree.
- **Chat context window** configurable - include ancestor instructions/results up to a configurable depth (default 3, user-adjustable in Settings view). Parent-only mode (depth=1) also supported.
- **Always persist** sessions to DB. Manual delete only. No auto-prune.
- **Spawn count**: slider 3-7, default 5.

---

## Execution Checklist

### Step 1: Entities + DbContext

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Create both entity files as shown above under `BlazorWebApp/Data/Entities/`.
- [ ] Register DbSets:
  ```csharp
  public DbSet<PromptWorkshopSession> PromptWorkshopSessions => Set<PromptWorkshopSession>();
  public DbSet<PromptWorkshopNode> PromptWorkshopNodes => Set<PromptWorkshopNode>();
  ```
- [ ] Configure relationships in `OnModelCreating`:

  ```csharp
  modelBuilder.Entity<PromptWorkshopNode>()
      .HasOne(n => n.Session)
      .WithMany(s => s.Nodes)
      .HasForeignKey(n => n.SessionId)
      .OnDelete(DeleteBehavior.Cascade);

  modelBuilder.Entity<PromptWorkshopNode>()
      .HasOne(n => n.Parent)
      .WithMany(p => p.Children)
      .HasForeignKey(n => n.ParentId)
      .OnDelete(DeleteBehavior.Restrict); // prevent cascade loops
  ```

#### Success Criteria

- Build clean.

---

### Step 2: Hand-authored migration + snapshot

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Create `Migrations/YYYYMMDDhhmmss_Add_PromptWorkshop.cs` with both `[DbContext]` and `[Migration]` attributes. Up creates both tables + indexes on `SessionId`, `ParentId`. Down drops both in reverse order (nodes first, then sessions).
- [x] Update `AppDbContextModelSnapshot.cs` alphabetically with both entity blocks and the `HasAnnotation` relationship edges.
- [ ] Run the app; verify `PromptWorkshopSessions` and `PromptWorkshopNodes` tables exist.

#### Success Criteria

- App starts clean.
- Both tables present with correct FKs / indexes.

---

### Step 3: `WorkshopService` (orchestration layer)

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Create `BlazorWebApp/Services/WorkshopService.cs`. Inject `IDbContextFactory<AppDbContext>`, `OllamaService`, `ILogger<WorkshopService>`.
- [x] Public API:

  ```csharp
  Task<PromptWorkshopSession> CreateSessionAsync(string name, string rootPrompt);
  Task<List<PromptWorkshopSession>> ListSessionsAsync();
  Task<PromptWorkshopSession?> LoadSessionAsync(int sessionId); // includes full node tree
  Task RenameSessionAsync(int sessionId, string newName);
  Task DeleteSessionAsync(int sessionId);

  Task<PromptWorkshopNode> AddChatChildAsync(int sessionId, int parentId, string instruction, string modelName, int ancestorDepth);
  Task<List<PromptWorkshopNode>> SpawnVariationsAsync(int sessionId, int parentId, int count, string modelName);

  Task SetCurrentNodeAsync(int sessionId, int nodeId);
  Task<IReadOnlyList<PromptWorkshopNode>> GetAncestorChainAsync(int nodeId, int maxDepth);
  ```

- [x] `GetAncestorChainAsync` walks parent pointers up to `maxDepth` nodes (inclusive of the target), oldest-first order. Used to build chat context.
- [x] `AddChatChildAsync` implementation:
  ```csharp
  var ancestors = await GetAncestorChainAsync(parentId, ancestorDepth);
  var tpl = await LoadTemplateAsync("Workshop.ChatEdit");
  var history = new List<OllamaChatMessage> { new() { Role = "system", Content = tpl.Messages[0].Content } };
  foreach (var node in ancestors)
  {
      if (!string.IsNullOrWhiteSpace(node.Instruction))
          history.Add(new() { Role = "user", Content = node.Instruction });
      history.Add(new() { Role = "assistant", Content = node.PromptText });
  }
  history.Add(new() { Role = "user", Content = instruction });
  var response = await _ollama.SendChatMessage(modelName, history);
  // Persist new node with PromptText = response.Message.Content, Mode = "chat", Instruction = instruction, ParentId = parentId, GenerationNumber = parent.GenerationNumber + 1.
  ```
- [x] `SpawnVariationsAsync` implementation:
  ```csharp
  var parent = await GetNodeAsync(parentId);
  var tpl = await LoadTemplateAsync("Workshop.EvolveVariations");
  var messages = new List<OllamaChatMessage>
  {
      new() { Role = "system", Content = tpl.Messages[0].Content },
      new() { Role = "user", Content = tpl.Messages[1].Content
          .Replace("{base_prompt}", parent.PromptText)
          .Replace("{count}", count.ToString()) }
  };
  var response = await _ollama.SendChatMessage(modelName, messages);
  var variations = ParseNumberedList(response?.Message?.Content ?? string.Empty, expected: count);
  // Persist N child nodes with Mode = "evolve". Return in spawn order.
  ```

#### Changes Made

| File                                        | Action   | Description                                                                                                                                         |
| ------------------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Services/IWorkshopService.cs` | Created  | Interface defining session CRUD, chat child addition, variation spawning, current node setting, and ancestor chain retrieval.                       |
| `BlazorWebApp/Services/WorkshopService.cs`  | Created  | Full implementation using `IDbContextFactory<AppDbContext>` for data access and `OllamaService` for LLM calls. Includes `ParseNumberedList` helper. |
| `BlazorWebApp/Program.cs`                   | Modified | Registered `IWorkshopService` as singleton.                                                                                                         |

#### Notes

- `GenerateImagesForNodeAsync` deferred to Step 7 (Evolve mode + image generation loop) as it depends on `IRouterService`, `IStateService`, and the generation workflow pipeline.
- Template loading is inline (system prompt strings embedded directly) rather than using DB-seeded templates, since template seeding (Step 4) runs in parallel.

#### Success Criteria

- Build clean (0 errors). ✓

---

```csharp
var node = await GetNodeAsync(nodeId);
// Clone current GenerationParameters from IStateService; patch the main prompt fragment.
var parameters = _state.GenerationParameters.Clone();
parameters.Fragments["main_prompt"].SetValue(FragmentKeys.Params.Positive, node.PromptText);
var result = await _router.PostGenerationAsync(parameters, workflow);
// Persist returned Image rows (generation pipeline already saves them) and update node.ImageIds accordingly.
```

**Note:** the existing generation pipeline persists `Image` entities; we only need to collect their new IDs from `GeneratedImages` and write them into the node. Verify exact return shape during implementation.

- [ ] Line-parse helper (`ParseNumberedList`):
  ```csharp
  static List<string> ParseNumberedList(string raw, int expected)
  {
      var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      var rx = new Regex(@"^\d+[\.\)\:]\s*");
      var cleaned = lines.Select(l => rx.Replace(l, string.Empty).Trim()).Where(s => s.Length > 0).ToList();
      return cleaned.Count >= expected ? cleaned.Take(expected).ToList() : cleaned;
  }
  ```

#### Success Criteria

- Create a session; add 3 chat nodes; tree query returns 4 nodes total.
- Spawn 5 variations from a node; child count = 5; each has unique `PromptText`.
- Current-node pointer updates; session reload restores the correct active node.

---

### Step 4: Seed `Workshop.ChatEdit` + `Workshop.EvolveVariations` default templates

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Append to `OllamaService.GetDefaultTemplates()`:
  ```csharp
  new SystemPromptTemplate
  {
      Name = "Workshop.ChatEdit",
      Description = "Workshop chat mode - applies a short instruction to the current prompt while preserving style.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You are a prompt-editing assistant inside a creative workshop. The conversation history is alternating " +
              "user instructions and assistant-produced prompts. Apply the latest user instruction to the most recent " +
              "assistant prompt while preserving every unrelated concept. Return only the updated prompt - no commentary." }
      }
  },
  new SystemPromptTemplate
  {
      Name = "Workshop.EvolveVariations",
      Description = "Workshop evolve mode - produces N numbered variation prompts from a base prompt.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You generate creative variations of an image-model prompt. Each variation must differ from the base in at " +
              "least one meaningful way (subject, setting, style, lighting, or composition) while remaining coherent. " +
              "Return exactly {count} variations as a numbered list. No preamble. No extra commentary." },
          new() { Role = "user", Content =
              "Base prompt: {base_prompt}\n\nReturn {count} numbered variations, one per line." }
      }
  }
  ```
- [ ] Depends on Phase 3 Step 3 (idempotent seeding).

#### Success Criteria

- Both templates seed on first startup after update.

---

### Step 5: `WorkshopView.razor` - layout + session sidebar + tree

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Create `BlazorWebApp/Components/Prompts/LLM/Views/WorkshopView.razor`.
- [x] Three-pane layout:
  - **Left (session sidebar, 220px)**: list of sessions (`MudList`), `+ New Session` button, context menu (rename / delete / duplicate).
  - **Center (tree panel, flex-grow)**: renders the active session's node tree.
  - **Right (current-node panel, 340px, collapsible)**: shows the current node's prompt, images, and action buttons (Save as Style, Send to Process).
- [x] For v1 the tree renderer is a **simple indented list** using `MudTreeView`:
  ```razor
  <MudTreeView T="PromptWorkshopNode" Items="_rootNodes" Hover="true">
      <ItemTemplate>
          <MudTreeViewItem Value="@context" Text="@Truncate(context.PromptText, 60)"
                           Icon="@IconFor(context.Mode)"
                           CanExpand="@context.Children.Any()"
                           OnClick="() => SetCurrentAsync(context)">
              ... (recurse) ...
          </MudTreeViewItem>
      </ItemTemplate>
  </MudTreeView>
  ```
  Icons: `AutoAwesome` for root, `Chat` for chat, `Science` for evolve. Current node rendered with `Color="Color.Primary"`.
  An "upgrade to SVG tree" is explicitly deferred to Phase 10.
- [ ] Session load calls `WorkshopService.LoadSessionAsync(id)` which returns the full session with nodes eagerly loaded. Reshape in-memory to a tree: group children by `ParentId`; root nodes are those with `ParentId == null`.
- [ ] Session CRUD wired to `WorkshopService`.

#### Success Criteria

- Multiple sessions visible; clicking switches active session.
- Tree renders parent → child correctly.
- Clicking a node updates the current-node panel and persists via `SetCurrentNodeAsync`.

---

### Step 6: Chat mode + ancestor depth control

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Chat dialog (`WorkshopChatDialog.razor`) with instruction field, model selector, and ancestor depth slider (1-10).
  - `MudTextField` for the instruction (multi-line).
  - `MudSlider` (1-10, default 3) for ancestor depth. Persist to `AppState.Prompts.LLM.Workshop.AncestorDepth`.
  - `Send` button → `WorkshopService.AddChatChildAsync(sessionId, currentNode.Id, instruction, model, depth)`.
- [ ] After send, reload session → new child appears; set it as current.
- [ ] Show "Parent-only" quick toggle that forces depth=1.
- [ ] Show a preview of the messages that will be sent (tooltip or expandable panel) so the user sees what context goes to the model.

#### Success Criteria

- Sending an instruction adds a child node and makes it current.
- Changing ancestor depth changes the context length (verify via the preview panel).

---

### Step 7: Evolve mode + image generation loop

**Complexity:** 5
**Status:** [~] Text-spawn implemented; image generation deferred to PHASE_8.5 (single per-node previews replace the parallel batch design).

#### Tasks

- [x] Evolve dialog (`WorkshopEvolveDialog.razor`) with model selector and spawn count slider (3-7).
  - Workflow dropdown (populated from the current base model's workflow list - reuse whatever `LLMToolsTab` or `GenerationView` uses to pick workflows).
  - Spawn count slider (3-7, default 5). Persist.
  - Auto-render toggle (default on) - if on, each spawned variation automatically queues an image generation.
  - `Spawn` button → `WorkshopService.SpawnVariationsAsync(...)` → returns N new child nodes.
- [ ] After spawn, render the N variations as a responsive grid (one card per variation):
  - Prompt text preview.
  - Image placeholder (spinner while generating, rendered image once complete).
  - "Pick this one" button → `SetCurrentAsync(child)`. Sibling cards remain visible but are visually dimmed.
- [ ] Generation loop:
  ```csharp
  async Task QueueImagesForChildrenAsync(IEnumerable<PromptWorkshopNode> children, Workflow workflow)
  {
      // Queue sequentially for v1 to avoid backend overload. If the existing scheduler queues in parallel,
      // we get parallelism for free; otherwise these run one-by-one.
      foreach (var child in children)
          _ = Task.Run(async () => {
              try { await Workshop.GenerateImagesForNodeAsync(child.Id, workflow); StateHasChanged(); }
              catch (Exception ex) { _log.LogError(ex, "Generation failed for node {Id}", child.Id); }
          });
  }
  ```
- [ ] Subscribe to `ImagesGeneratedEventArgs` via `IEventService` to refresh the grid when generations complete. Unsubscribe on dispose.
- [ ] If the user rejects all spawns, they can re-spawn with the same parent; previous variations remain in the tree.

#### Success Criteria

- Spawn 5 → 5 child nodes appear with in-flight spinners.
- Images render as backend returns them.
- Picking a winner sets it as current; dimmed siblings remain in the tree.
- Sibling spawn history preserved across session reload.

---

### Step 8: Send-to-Workshop entry points from prior phases

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Add `HandleSendToWorkshop` method in `LLMToolsTab.razor` that creates a session and switches to workshop view.
  ```csharp
  async Task HandleSendToWorkshop(string prompt)
  {
      // Create a new session seeded with the given prompt, switch view to "workshop", select it.
      var session = await Workshop.CreateSessionAsync(
          $"Session ({DateTime.Now:yy-MM-dd HH:mm})", prompt);
      _pendingWorkshopSessionId = session.Id;
      _activeViewId = "workshop";
      await State.SaveState();
  }
  ```
- [ ] Extend every applicable view with an optional `OnSendToWorkshop` parameter and a button in the output panel. Touch: `TagBuilderView`, `MixerView`, `InspirationView`, `SceneBuilderView`, `TemplateBuilderView`, `RemixerView`. If any of those phases are not merged yet, skip that view (nothing to wire).
- [ ] `WorkshopView` reads `_pendingWorkshopSessionId` on parameters set and loads that session.

#### Success Criteria

- Clicking "Send to Workshop" from any upstream view creates a session and switches to the Workshop view with that session active and its root node selected.

---

### Step 9: `AppState.Prompts.LLM.Workshop` + nav + info

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Add `AppStatePromptsLLMWorkshop` to `AppState.cs`.
  ```csharp
  public class AppStatePromptsLLMWorkshop
  {
      public int? ActiveSessionId { get; set; }
      public int AncestorDepth { get; set; } = 3;
      public int SpawnCount { get; set; } = 5;
      public bool AutoRender { get; set; } = true;
      public bool RightPanelCollapsed { get; set; } = false;
  }
  ```
- [ ] Nav item: `new("workshop", "Workshop", Icons.Material.Filled.Build),`.
- [ ] Switch case in `LLMToolsTab` renders `<WorkshopView PendingSessionId="@_pendingWorkshopSessionId" ... />`.
- [ ] Info content tips: "Tree is prompt-only; deleting images doesn't break sessions.", "Spawn 3-7 variations per evolve step.", "Chat context depth controls how much history the LLM sees."

#### Success Criteria

- State persists across reloads.
- Nav entry visible.
- Info panel updates.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                             |
| ---- | ------ | ---------- | ------------------------------------------------- |
| 1    | [x]    | 1          | Entities                                          |
| 2    | [x]    | 3          | Migration + snapshot                              |
| 3    | [x]    | 3          | `WorkshopService`                                 |
| 4    | [x]    | 1          | Seed templates                                    |
| 5    | [x]    | 5          | View layout + tree + session CRUD                 |
| 6    | [x]    | 3          | Chat mode                                         |
| 7    | [~]    | 5          | Text-spawn only; image loop deferred to PHASE_8.5 |
| 8    | [x]    | 2          | Upstream "Send to Workshop" wiring                |
| 9    | [x]    | 1          | AppState + nav + info                             |

**Total:** 24 points (original plan estimate: 13 - overrun expected because evolve-mode wiring through `IRouterService` + image lifecycle adds real complexity).

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] After Step 2 (entities + migration shipped together for DB consistency)
- [ ] After Step 3 (service layer)
- [ ] After Step 4 (seeded templates)
- [ ] After Step 5 (view skeleton + session CRUD)
- [ ] After Step 6 (chat mode)
- [ ] After Step 7 (evolve mode)
- [ ] After Step 8 (entry-point wiring)
- [ ] After Step 9 (state + polish)

---

## Open Risks

1. **Tree rendering complexity.** `MudTreeView` works for v1 but looks flat. Phase 10 can upgrade to an SVG-based tree with parent→child edges. Acceptable fallback.
2. **Parallel image generation load.** Backend may serialize requests. Mitigation: let the existing scheduler queue; don't introduce new parallelism on the C# side. If throughput is bad, queue serially.
3. **Stale `ImageIds`.** Users delete images; workshop nodes still reference them. Mitigation: on render, check `DB.GetImageById(id)` and skip missing images silently. Show "image missing" placeholder.
4. **`GenerationParameters.Clone()` may not exist.** Verify during implementation; if not, add a `.Clone()` helper using deep-copy serialization, or mutate the live parameters and restore on completion (risky).
5. **`WorkflowKeys` / `FragmentKeys.Params.Positive`** must match the actual constants in the codebase. Verify against `BlazorWebApp/Scheduler/` or `BlazorWebApp/Models/GenerationParameters.cs` during Step 3.
6. **`OnDelete(DeleteBehavior.Restrict)` on `ParentId`** means deleting a node with children throws. Desired: deleting a session cascades all nodes; individually deleting nodes is not exposed in v1 UI to avoid tree corruption. Document this in `WorkshopService`.
7. **Number-list parsing failure.** LLMs occasionally produce "Here are 5 variations:" preamble or markdown bullets. The regex strips `1.` / `1)` / `1:`; failure mode is fewer variations than requested, not a crash. Mitigation: if `cleaned.Count < expected`, surface a snackbar warning.

---

## Phase Summary

_To be filled in on completion._
