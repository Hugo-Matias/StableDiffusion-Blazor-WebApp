# Phase 12 - Image to Prompt (VL Models)

## Status

**Phase:** 12
**Build Status:** Succeeds (warnings only)
**Phase Status:** [x] Complete

---

## Objective

Enable selecting an image (drag-drop, file pick, or routed in from the Gallery) and receiving a diffusion-prompt produced by an Ollama vision-language model (Qwen2-VL / Qwen3-VL, LLaVA, MiniCPM-V, etc.). Single-image only in this phase. Result is editable, then dispatchable via Send-to-Generate / Workshop / Process or saveable as a `Prompt` preset.

Batch / captioning workflow is split into **Phase 12B** (deferred) - see stub at end of this document.

---

## Context

### Validated environment

User confirmed Qwen3-VL 4B and 8B models work on a GTX 1070 Ollama instance. Native Ollama VL support handles `images: [base64...]` in `/api/chat`. Earlier attempts to stitch an mmproj onto Gemma 4 E4B failed at the llama.cpp level (`unknown model architecture: 'gemma4'`), so the strategy is to use a dedicated VL model selectable per-tool.

### Dependencies on prior phases

- **Phase 1** required (LLM Tools sidebar nav). Complete.
- **Phase 2** (`TagPromptService`) required for tag-normalization mode; degrades gracefully if absent. Complete.
- **Phase 3 Step 3** required for seeded templates (`OllamaService.GetDefaultTemplates()` + idempotent seeder). Complete.

### Existing infrastructure to reuse

- `OllamaService.SendChatMessage(modelName, messages, options, keepAlive, stream)` - extend, do not replace.
- `OllamaService.GetModels()` - already returns `/api/tags`.
- `TagPromptService.BuildAsync` for tag normalization.
- `IDatabaseService.GetImageById` / `Image.Path` for gallery routing.
- `IImageSendToService` for "Send to Generate" routing into the right workflow for the current base.
- `EventService` pub/sub for cross-page signaling.
- Existing source-input pattern from the Generate page (drop + click-to-pick) - reuse the same component if possible for visual consistency.
- `Prompt` entity (`Title`, `Positive`, `Category`, `Tags`) for "Save as Preset".

### Architectural rules

- `OllamaChatMessage.Images` added as an optional property; existing callers unaffected.
- Image base64 encoding happens in `VLModelService`, not in the view.
- Tool-local model selector lives in the view itself (top row), independent from the global LLM model in `LLMSettingsPanel`.
- Streaming is opt-in via a new `StreamChatMessageAsync` method on `OllamaService`; the existing `SendChatMessage` keeps its current signature and behavior.
- Gallery integration is **redirect-only** - both the `ImageCard` quick action and the `AssetInfoPanel` button publish an event + navigate to the LLM Tools view; no per-surface dialogs or duplicated UI.

---

## UX Specification (for Steps 4 / 6)

```
+-------------------------------------------------------------+
| [VL Model: qwen2-vl:8b v]  [Style: Detailed v]      [Run]   |
+-------------------------------------------------------------+
|                                                             |
|  +-------------------------------------------------------+  |
|  |                                                       |  |
|  |             IMAGE PREVIEW (click / drop)              |  |
|  |             (drop-zone styling when empty)            |  |
|  |                                                       |  |
|  |   Replace          Open file...        Clear          |  |
|  +-------------------------------------------------------+  |
|                                                             |
|  Result                                                     |
|  +-------------------------------------------------------+  |
|  | Auto-grow editable textarea                           |  |
|  | (placeholder: "Run to generate a prompt...")          |  |
|  +-------------------------------------------------------+  |
|                                                             |
|  [ ] Normalize to Danbooru tags    (View raw response)      |
|                                                             |
|  [Send to Generate] [Send to Workshop] [Send to Process]    |
|  [Save as Preset]   [Copy]                                  |
+-------------------------------------------------------------+
```

Behavior:

- **VL Model selector**: tool-local. Reads/writes `AppState.Prompts.LLM.ImageToPrompt.LastModel`. Lists all `OllamaService.GetModels()`; non-multimodal entries get a small warning icon but stay selectable.
- **Style selector**: top-row. Reads/writes `AppState.Prompts.LLM.ImageToPrompt.LastStyle`. Six values from `InterrogationStyle` enum.
- **Run button**: disabled when no image or no model. While running, swaps to **Cancel** with an elapsed-seconds counter.
- **Image container**: single component, drop-zone when empty, preview when loaded. Same visual pattern as Generate-page source inputs (consistency requirement). Action row: Replace, Open file, Clear.
- **Result textarea**: editable (auto-grow). Send-to / Save-as-Preset always read the _current_ textarea content, not the raw VL response. If user has edited and clicks Run, prompt to confirm overwrite (only when textarea is dirty vs the last raw response).
- **Normalize toggle**: when on, Run executes the two-pass flow (VL -> `TagPromptService.BuildAsync`) and the textarea shows the normalized version. A small "View raw response" link toggles between raw and normalized.
- **Action row**: Generate, Workshop, Process, Save Preset, Copy. No Mixer or Tag Builder fan-out (Tag Builder is reachable via Normalize toggle).

### Gallery routing (Step 6)

Gallery surfaces never run the VL model themselves. They:

1. Set `AppState.Prompts.LLM.ActiveViewId = "image-to-prompt"`.
2. Save state.
3. Publish `ImageToPromptRequestedEventArgs(string imagePath)`.
4. Navigate to the Prompts page.

`ImageToPromptView` subscribes on `OnInitializedAsync`. When it receives an event, it loads the image and shows an inline chip _"Image received from Gallery"_. **No auto-run** - user picks style and clicks Run.

---

## Execution Checklist

### Step 1: Extend `OllamaService` / `OllamaChatMessage` for multimodal input

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [ ] Extend `BlazorWebApp/Data/Dtos/Ollama/OllamaChatRequest.cs`:

  ```csharp
  public class OllamaChatMessage
  {
      [JsonPropertyName("role")]
      public string Role { get; set; } = string.Empty;

      [JsonPropertyName("content")]
      public string Content { get; set; } = string.Empty;

      // NEW - optional base64-encoded image payloads per Ollama chat API spec.
      [JsonPropertyName("images")]
      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
      public List<string>? Images { get; set; }
  }
  ```

- [ ] Add `OllamaService.EncodeImageBase64Async(string path)` using `File.ReadAllBytesAsync`.
- [ ] Add internal helper `OllamaService.IsLikelyMultimodal(string modelName)` flagging name patterns: `llava`, `bakllava`, `minicpm`, `qwen.*vl`, `llama.*vision`, `moondream`, `internvl`, `cogvlm`, `pixtral`. Used by the view for the warning icon.
- [ ] Smoke-test: existing text-only calls produce identical JSON (the `[JsonIgnore(Condition = WhenWritingNull)]` should drop the field). Inspect `payload_ollama.json` after one generate-page LLM call to confirm.

#### Success Criteria

- Existing text-only chat calls produce identical payloads (no `"images"` field).
- A test request to a known multimodal model with a small image returns a non-empty description.
- `EncodeImageBase64Async` reads files asynchronously (no UI thread block on large images).

---

### Step 2: `VLModelService` orchestration

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [ ] Create `BlazorWebApp/Services/VLModelService.cs`. Inject `OllamaService`, `IDatabaseService`, `TagPromptService?` (optional), `ILogger<VLModelService>`.
- [ ] DTOs in `BlazorWebApp/Models/VLModelModels.cs`:

  ```csharp
  public enum InterrogationStyle { Detailed, Focus, Artistic, Technical, Tags, Simple }

  public class InterrogationRequest
  {
      public string ModelName { get; set; } = string.Empty;
      public InterrogationStyle Style { get; set; } = InterrogationStyle.Detailed;
      public string? ImagePath { get; set; }
      public byte[]? ImageBytes { get; set; }
      public bool NormalizeToDanbooruTags { get; set; } = false;
  }

  public class InterrogationResult
  {
      public string ModelName { get; set; } = string.Empty;
      public InterrogationStyle Style { get; set; }
      public string Prompt { get; set; } = string.Empty;
      public string? NormalizedTagPrompt { get; set; }
      public string RawResponse { get; set; } = string.Empty;
  }
  ```

- [ ] Public method `InterrogateAsync(InterrogationRequest, CancellationToken)`:
  - Loads `SystemPromptTemplate` named `VL.{Style}` from DB.
  - Encodes image to base64 (path or bytes).
  - Builds `List<OllamaChatMessage>` from template, attaching the image to the `user`-role message.
  - Calls `OllamaService.SendChatMessage` (non-streaming path for v1).
  - If `NormalizeToDanbooruTags && _tagPrompts != null`, runs the raw output through `TagPromptService.BuildAsync` with `Verbosity=Standard`, `Preset=Illustrious`, `AllowNsfw=false`.
  - Returns `InterrogationResult` with both raw and normalized text.
- [ ] Register in `Program.cs` as scoped.

#### Success Criteria

- Real image + multimodal model -> non-empty `Prompt`.
- Tag normalization populates `NormalizedTagPrompt` when enabled.
- Cancellation token cancels mid-call cleanly.

---

### Step 3: Seed VL default templates

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [ ] Append six templates to `OllamaService.GetDefaultTemplates()`:
  - `VL.Detailed` - rich diffusion-friendly description (subject, pose, clothing, setting, lighting, mood, style, composition).
  - `VL.Focus` - main subject + one stylistic descriptor only.
  - `VL.Artistic` - style, brushwork, palette, mood emphasis.
  - `VL.Technical` - composition, lighting type, camera angle.
  - `VL.Tags` - Danbooru-style space/underscore tags (natural-language; Step 5 normalize handles the strict version).
  - `VL.Simple` - 1-2 short sentences.
- [ ] All templates: `IsDefault = true`, two messages (system + user), no preamble / no markdown / single comma-separated output instruction.

#### Success Criteria

- All six templates seed on a fresh DB (idempotent).
- Each style yields visibly different output for the same input image.

---

### Step 4: `ImageToPromptView.razor`

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/ImageToPromptView.razor`.
- [ ] Top-row controls:
  - VL model `MudSelect` (label "VL Model"). Items from `OllamaService.GetModels()`. Non-multimodal items show a `Icons.Material.Filled.Warning` adornment via item template. Persists to `AppState.Prompts.LLM.ImageToPrompt.LastModel`.
  - Style `MudSelect` over `InterrogationStyle`. Persists to `AppState.Prompts.LLM.ImageToPrompt.LastStyle`.
  - **Run** button: disabled when image or model missing; swaps to **Cancel** while running with elapsed seconds.
- [ ] Image container: reuse the source-input visual pattern from the Generate page. Drop-zone when empty, preview when set, action row beneath (Replace, Open file, Clear). Accept `image/*`. Track `_imagePath` (file uploads -> temp file or in-memory bytes).
- [ ] Result textarea: `MudTextField` with `AutoGrow`, `Lines="3"`, `MaxLines="0"`. Bound to `_resultText`.
- [ ] Normalize-to-tags `MudSwitch` below textarea (disabled if `TagPromptService` not registered). "View raw response" link toggles `_showRaw` to swap textarea content between `_rawText` and `_normalizedText`.
- [ ] Action row: `Send to Generate`, `Send to Workshop`, `Send to Process`, `Save as Preset`, `Copy`. All read `_resultText`. Disabled when text empty.
- [ ] Run handler:
  - Cancels any prior run.
  - Builds `InterrogationRequest` and calls `VLModelService.InterrogateAsync`.
  - On success, sets `_rawText` and `_normalizedText`, displays whichever the toggle says.
  - On failure, snackbar with the exception message.
- [ ] Dirty-overwrite confirm: if user edited the textarea after a run, Run prompts before overwriting.
- [ ] Send-to wiring:
  - **Generate**: `IImageSendToService` for the current base, default workflow.
  - **Workshop**: `OnSendToWorkshop.InvokeAsync(_resultText)` -> handled by `LLMToolsTab` like other views.
  - **Process**: `OnSendToProcess.InvokeAsync(_resultText)`.
  - **Save Preset**: opens `SavePresetDialog` (Step 4a).

#### Step 4a: `SavePresetDialog`

- [ ] `MudDialog` with fields: Title, Category, Tags (chip input).
- [ ] **Suggest button** next to Title: invokes `OllamaService.SendChatMessage` with a small system prompt asking for a 3-6 word title summarizing the prompt. Pre-fills Title field. Mirrors the Wildcard Forge "suggest collection name" UX. Uses the **globally selected** LLM model (text-only) - not the VL model.
- [ ] On save, creates a `Prompt` row via `IDatabaseService` with `Positive = _resultText`, `Negative = null`, `Title`, `Category`, `Tags`. Snackbar confirmation.

#### Success Criteria

- Drag-drop or file-pick sets the image; preview renders.
- Run produces text in the textarea matching the selected style.
- Style change followed by Run produces visibly different output.
- Normalize toggle produces tag version; raw toggle restores original.
- Each Send-to button routes correctly; Save Preset persists a row visible in the Prompt library.
- Cancel button stops mid-run within ~1s.

---

### Step 5: Streaming response (opt-in)

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [ ] Add `OllamaService.StreamChatMessageAsync(...) -> IAsyncEnumerable<OllamaChatResponse>`:
  - Uses `HttpClient.SendAsync` with `HttpCompletionOption.ResponseHeadersRead`.
  - Reads response body line-by-line via `StreamReader.ReadLineAsync`.
  - Each line is one NDJSON object; deserialize and `yield return`.
  - `[EnumeratorCancellation] CancellationToken ct` parameter; check between reads.
  - Stops when a chunk has `Done == true` or stream closes.
- [ ] Existing `SendChatMessage` untouched.
- [ ] Add `VLModelService.InterrogateStreamingAsync(InterrogationRequest, IProgress<string>, CancellationToken)`:
  - Calls the new streaming method.
  - Aggregates `chunk.Message.Content` into a buffer.
  - Reports buffer to `IProgress<string>` after each chunk.
  - Returns the final `InterrogationResult` once stream completes (then runs tag-normalization if requested - normalization itself stays non-streaming).
- [ ] View update: subscribe to `IProgress<string>` and append into `_resultText`. Throttle `StateHasChanged` to ~10Hz (use a timer or last-update timestamp) to avoid re-render thrash.
- [ ] Add `AppState.Prompts.LLM.ImageToPrompt.UseStreaming` flag, default `true`. Small toggle in the view's overflow menu (or just a checkbox under the textarea, TBD during implementation).

#### Success Criteria

- Streaming on -> textarea fills incrementally as the model generates.
- Cancellation during stream stops the response and leaves whatever text was already received.
- Streaming off -> view falls back to `InterrogateAsync`, behaves identically to Step 4.
- Normalize-to-tags still works (runs after streaming completes).

---

### Step 6: AppState + nav + gallery hooks

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [ ] Add to `AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMImageToPrompt
  {
      public string? LastModel { get; set; }
      public InterrogationStyle LastStyle { get; set; } = InterrogationStyle.Detailed;
      public bool NormalizeToTags { get; set; } = false;
      public bool UseStreaming { get; set; } = true;
  }
  ```
  Reference from `AppStatePromptsLLM`. No migration required (JSON column).
- [ ] Add nav item to `LLMNavMenu`: `new("image-to-prompt", "Image to Prompt", Icons.Material.Filled.ImageSearch)`.
- [ ] Add the view to `LLMToolsTab`'s switch.
- [ ] New event args `Events/ImageToPromptRequestedEventArgs.cs`:
  ```csharp
  public class ImageToPromptRequestedEventArgs : EventArgs
  {
      public string ImagePath { get; init; } = string.Empty;
      public string? SourceLabel { get; init; }   // e.g., "Gallery"
  }
  ```
- [ ] **`ImageCard` quick-action** menu item "Generate Prompt" - visible only when `SendTo.IsLocal(image.Path)`. Handler: set `ActiveViewId`, save state, publish event, navigate to `/prompts`.
- [ ] **`AssetInfoPanel`** button "Generate Prompt" - placed in the existing Send-to action row (or just below it if cramped). Same handler. Visible only for local images.
- [ ] `ImageToPromptView` subscribes to the event in `OnInitializedAsync`, unsubscribes in `Dispose`. On receipt: load image into the preview, show `MudChip` "Image from {SourceLabel}" that auto-dismisses on next user image change. **Does not auto-run.**

#### Success Criteria

- Nav entry shows in sidebar; switching to it persists across reloads.
- Gallery card menu and AssetInfoPanel button route correctly to the LLM tab with the image preloaded.
- URL-based images do not show the action.
- All AppState fields persist and restore on reload.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                       |
| ---- | ------ | ---------- | ------------------------------------------- |
| 1    | [ ]    | 3          | Ollama DTOs + base64 helper + heuristic     |
| 2    | [ ]    | 3          | `VLModelService` orchestration              |
| 3    | [ ]    | 2          | Seed six VL default templates               |
| 4    | [ ]    | 5          | `ImageToPromptView` + `SavePresetDialog`    |
| 5    | [ ]    | 3          | Streaming (`StreamChatMessageAsync` + view) |
| 6    | [ ]    | 2          | AppState + nav + gallery hooks              |

**Total:** 18 points.

---

## Commit Checkpoints

- [ ] After Step 1 (standalone Ollama plumbing change; commit separately for easier rollback)
- [ ] After Step 2
- [ ] After Step 3
- [ ] After Step 4 (single-image MVP - usable end-to-end without streaming or gallery hooks)
- [ ] After Step 5 (streaming layered on)
- [ ] After Step 6 (gallery integration; phase complete)

---

## Open Risks

1. **Ollama multimodal API drift.** `images` field has been stable but versions differ on `data:image/...;base64,` prefix handling. Mitigation: send raw base64 (no data-URI prefix), as Qwen tests confirm this works.
2. **`GetModels()` doesn't reveal multimodal capability.** Heuristic-by-name is fragile - new families surface and don't match. Mitigation: warn-don't-block; user can still try any model.
3. **Streaming + cancellation edge cases.** Aborting an in-flight `HttpClient` stream can leak sockets if not disposed correctly. Mitigation: wrap reader/response in `using` blocks; rely on `[EnumeratorCancellation]` to throw cleanly.
4. **Large images.** A 4K image -> ~5 MB base64; some models time out. Mitigation: out of scope for v1, revisit in Phase 10 settings (auto-resize to 1024px max dim before encode).
5. **Tag normalization latency.** Normalize=on means VL call + 2-pass `TagPromptService` = 3 LLM calls per image. Document this in the info-content; no mitigation in v1.
6. **Suggest-title button blocking.** Calling text-LLM while VL is loaded may cause Ollama to swap models (slow). Acceptable cost; users invoke Suggest manually.

---

## Phase 12B - Batch / Captioning workflow (deferred)

**Status:** Not started. Documented here as forward-looking scope; track as its own phase doc when it gets prioritized.

### Scope

Multi-image processing geared toward dataset captioning rather than ad-hoc prompt generation. Distinct UI affordance from the single-image flow because the action set is different.

### Anticipated features

- **Source modes:**
  - Multi-file upload (drag-drop many).
  - Folder picker (pick a directory, recurse optional).
  - Multi-select from Gallery -> "Generate Prompts (Batch)".
- **Queue UI:** thumbnail strip with per-item status (pending / running / done / failed), live progress, current-item preview pane.
- **Per-item editing:** click a completed item to edit its caption inline before export.
- **Cancellation:** stop button halts after the current item finishes.
- **Sequential by default**, with a single concurrency knob exposed in Phase 10 settings (`max_concurrent_vl_calls`, default 1).
- **Export targets:**
  - Sidecar `.txt` files alongside each image (standard captioning convention).
  - Bulk save to `Prompt` library (one row per image, title from filename).
  - JSON manifest export (`{ image_path, caption, model, style }[]`) for downstream training pipelines.
- **Style + model controls** are batch-wide (set once, applies to all).
- **Resume support:** if cancelled, persist the queue state in `AppState` so reopening the view can pick up where it left off.

### Open questions for 12B

- Does Phase 12B reuse `ImageToPromptView` with a "Batch" toggle, or get its own `BatchCaptionView`? (Leaning toward separate view - the action surfaces are too different.)
- Where do exported sidecar files go - alongside source, or to a configured output dir?
- Does the gallery multi-select integration go through the same `ImageToPromptRequestedEventArgs` (with a list payload) or a dedicated batch event?

### Estimated complexity

~13 points (separate view + queue runner + export + gallery multi-select hook + AppState persistence).

---

## Issues & Resolutions

_None yet._

---

## Phase Summary

_To be filled in on completion._
