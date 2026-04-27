# Phase 12 - Image to Prompt (VL Models)

## Status

**Phase:** 12
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started (flagged as nice-to-have in `MAIN_PLAN.md`)

---

## Objective

Enable uploading (or selecting from the gallery) an image and receiving a prompt produced by an Ollama vision-language model (LLaVA, MiniCPM-V, Qwen2-VL, etc.). Support interrogation styles (Detailed / Focus / Artistic / Technical / Tags / Simple), optional normalization to Danbooru tags via `TagPromptService` (Phase 2), and batch mode for multiple images.

---

## Context

### Dependencies on prior phases

- **Phase 1** required.
- **Phase 2** required for tag-normalization mode; degrades gracefully if absent.
- **Phase 3 Step 3** required for seeded templates.

### Ollama multimodal support - scaffolding required

Per the workspace audit (see PHASE_2 / session notes), `OllamaService` currently has **no multimodal plumbing**. `OllamaChatMessage` has only `Role` + `Content`. The Ollama `/api/chat` endpoint accepts an `images: [base64strings]` field per message when the target model is multimodal.

This phase therefore adds the scaffolding as Step 1 before building the view.

### Existing infrastructure to reuse

- `OllamaService.SendChatMessage` (to be extended).
- `OllamaService.GetModels()` (already exists) - we need a way to flag multimodal models (by name pattern: `llava`, `minicpm`, `qwen2-vl`, `bakllava`, etc.) or rely on user selection.
- `TagPromptService.BuildAsync` (Phase 2) for tag normalization.
- `IDatabaseService.GetImageById` / gallery integration for "send from gallery" flow.
- `Image` entity with `Path` (absolute file path) for disk reads.

### Architectural rules

- `OllamaChatMessage.Images` added as an optional property; existing callers unaffected.
- Image encoding happens in `VLModelService`, not in the view.
- Batch mode runs sequentially by default to avoid blowing up local GPU memory; a parallelism cap can be exposed in Phase 10 Settings.

---

## Execution Checklist

### Step 1: Extend `OllamaService` / `OllamaChatMessage` for multimodal input

**Complexity:** 3
**Status:** [ ] Not Started

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

- [ ] Smoke-test: existing calls with `Images == null` omit the field in the JSON payload.
- [ ] `OllamaService.SendChatMessage` requires no signature change - the new property rides along. Verify the `System.Text.Json` serializer honors `[JsonIgnore(Condition = WhenWritingNull)]` so pre-existing text-only calls send identical payloads.
- [ ] Add a helper `OllamaService.EncodeImageBase64Async(string path)`:
  ```csharp
  public async Task<string> EncodeImageBase64Async(string path)
  {
      var bytes = await File.ReadAllBytesAsync(path);
      return Convert.ToBase64String(bytes);
  }
  ```
- [ ] Document multimodal model-name patterns in a comment (or an internal `IsLikelyMultimodal(string model)` heuristic that flags `llava`, `minicpm`, `qwen.*vl`, `bakllava`, `llama3.2-vision`, `moondream`).

#### Success Criteria

- Existing text-only chat calls unchanged (payload regression test: compare serialized JSON before/after).
- Sending a test request with a known multimodal model + a small image returns a description.
- `EncodeImageBase64Async` handles large files without blocking the UI thread (use `File.ReadAllBytesAsync`).

---

### Step 2: `VLModelService` orchestration

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Services/VLModelService.cs`. Inject `OllamaService`, `IDatabaseService`, `TagPromptService?` (optional), `ILogger<VLModelService>`.
- [ ] DTOs in `BlazorWebApp/Models/VLModelModels.cs`:

  ```csharp
  public enum InterrogationStyle { Detailed, Focus, Artistic, Technical, Tags, Simple }

  public class InterrogationRequest
  {
      public string ModelName { get; set; } = string.Empty;
      public InterrogationStyle Style { get; set; } = InterrogationStyle.Detailed;
      public string? ImagePath { get; set; } // file path
      public byte[]? ImageBytes { get; set; } // alternative to path (for uploads)
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

- [ ] Public method:

  ```csharp
  public async Task<InterrogationResult> InterrogateAsync(InterrogationRequest request, CancellationToken ct = default)
  {
      var tpl = await LoadTemplateAsync($"VL.{request.Style}");
      if (tpl == null) throw new InvalidOperationException($"Template VL.{request.Style} missing");

      var base64 = request.ImageBytes is { Length: > 0 }
          ? Convert.ToBase64String(request.ImageBytes)
          : await _ollama.EncodeImageBase64Async(request.ImagePath!);

      var messages = tpl.Messages.Select(m => new OllamaChatMessage {
          Role = m.Role,
          Content = m.Content,
          Images = m.Role == "user" ? new List<string> { base64 } : null
      }).ToList();

      var response = await _ollama.SendChatMessage(request.ModelName, messages);
      var raw = response?.Message?.Content?.Trim() ?? string.Empty;

      string? tagPrompt = null;
      if (request.NormalizeToDanbooruTags && _tagPrompts != null)
      {
          var tagRequest = new TagBuilderRequest {
              Input = raw,
              Verbosity = TagVerbosity.Standard,
              Preset = TagModelPreset.Illustrious,
              EnabledCategories = new HashSet<TagCategory> { TagCategory.General, TagCategory.Character, TagCategory.Copyright, TagCategory.Meta },
              AllowNsfw = false
          };
          var built = await _tagPrompts.BuildAsync(tagRequest, request.ModelName, null, ct);
          tagPrompt = built.FinalPrompt;
      }

      return new InterrogationResult {
          ModelName = request.ModelName,
          Style = request.Style,
          Prompt = raw,
          NormalizedTagPrompt = tagPrompt,
          RawResponse = raw
      };
  }
  ```

- [ ] Register in `Program.cs` as scoped.

#### Success Criteria

- Given a real image + multimodal model, returns a prompt description.
- Tag normalization returns a Danbooru-style version when enabled.

---

### Step 3: Seed VL default templates

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Append six templates to `OllamaService.GetDefaultTemplates()`, one per style: `VL.Detailed`, `VL.Focus`, `VL.Artistic`, `VL.Technical`, `VL.Tags`, `VL.Simple`. Example for Detailed:
  ```csharp
  new SystemPromptTemplate
  {
      Name = "VL.Detailed",
      Description = "Produces a rich, image-model-friendly description of a provided image.",
      IsDefault = true,
      Messages = new List<OllamaChatMessage>
      {
          new() { Role = "system", Content =
              "You are an image captioner for diffusion prompts. Describe the provided image in detail covering: " +
              "subject, pose, clothing, setting, lighting, mood, style, composition. Avoid speculation about the " +
              "artist or the model. Output a single comma-separated prompt suitable for an image-generation model. " +
              "No preamble, no bullet points, no markdown." },
          new() { Role = "user", Content = "Describe this image." }
      }
  }
  ```
- [ ] Variants:
  - `VL.Focus`: emphasize the main subject + one stylistic descriptor only.
  - `VL.Artistic`: emphasize style, brushwork / rendering, palette, mood.
  - `VL.Technical`: emphasize composition, lighting type, camera angle.
  - `VL.Tags`: output Danbooru-style space/underscore tags (still natural-language until Step 2 normalization).
  - `VL.Simple`: 1-2 short sentences.
- [ ] Depends on Phase 3 Step 3 (idempotent seeding).

#### Success Criteria

- All six templates seed.
- Each style produces visibly different output for the same image.

---

### Step 4: `ImageToPromptView.razor`

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/ImageToPromptView.razor`.
- [ ] Layout:
  - **Input section**:
    - `MudFileUpload` drag-drop zone (accept `image/*`).
    - "Or pick from gallery" button opens a `MudDialog` showing recent `Image` entities with thumbnails (pull top 50 via `IDatabaseService.GetImages(top: 50, orderByDate: true)` or whatever the gallery uses).
    - Selected image preview (small thumbnail).
  - **Options**:
    - Model dropdown - `MudSelect` populated from `OllamaService.GetModels()` with multimodal heuristics flag (non-multimodal entries still selectable but show a warning icon).
    - Style dropdown (`InterrogationStyle` enum).
    - Normalize-to-tags `MudSwitch` (disabled if Phase 2 unavailable).
  - **Run** button.
  - **Result panel**: raw prompt + normalized tag prompt (when generated). Copy / Save / Send to Process / Send to Workshop actions.
- [ ] Wire model detection: if the selected model isn't flagged multimodal by the heuristic, show a warning snackbar but still allow the call.
- [ ] Persist last selected model / style / normalize flag to `AppState.Prompts.LLM.ImageToPrompt`.

#### Success Criteria

- Drag-drop or gallery pick sets the input image.
- Clicking Run invokes `VLModelService.InterrogateAsync` and populates the result panel.
- Style change regenerates different output.
- Normalize toggle produces the tag version.

---

### Step 5: Batch mode

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add a "Batch" toggle at the top of the view. When enabled:
  - File upload accepts multiple files.
  - Input list shows thumbnails of queued images.
  - Run iterates sequentially, showing progress (`N / M` + current image preview).
  - Results render as a list with per-image prompt + copy action.
  - "Save all to library" button creates one `Prompt` per image with `Title = filename` and `Positive = resulting prompt`.
- [ ] Cancellation: `CancellationTokenSource` exposed via a Cancel button.

#### Success Criteria

- Queuing 10 images runs through all 10; cancel stops mid-run cleanly.
- Save-all creates 10 library rows.

---

### Step 6: `AppState` + nav + info

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMImageToPrompt
  {
      public string? LastModel { get; set; }
      public InterrogationStyle LastStyle { get; set; } = InterrogationStyle.Detailed;
      public bool NormalizeToTags { get; set; } = false;
      public bool BatchMode { get; set; } = false;
  }
  ```
- [ ] Nav item: `new("image-to-prompt", "Image → Prompt", Icons.Material.Filled.ImageSearch),`.
- [ ] Switch case in `LLMToolsTab`.
- [ ] Info content: explain multimodal-model requirement; list known-supported model name patterns; warn that big images slow down calls; suggest `llava:13b` / `minicpm-v` as starting points.

#### Success Criteria

- State persists.
- Nav entry present.

---

## Progress Tracking

| Step | Status | Complexity | Notes                               |
| ---- | ------ | ---------- | ----------------------------------- |
| 1    | [ ]    | 3          | Extend Ollama DTOs + base64 helper  |
| 2    | [ ]    | 3          | `VLModelService` orchestration      |
| 3    | [ ]    | 2          | Seed six VL default templates       |
| 4    | [ ]    | 3          | `ImageToPromptView` single-image UX |
| 5    | [ ]    | 2          | Batch mode                          |
| 6    | [ ]    | 1          | AppState + nav + info               |

**Total:** 14 points (original plan estimate: 13).

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] After Step 1 (standalone Ollama plumbing change; commit separately for easier rollback)
- [ ] After Step 2
- [ ] After Step 3
- [ ] After Step 4
- [ ] After Step 5
- [ ] After Step 6

---

## Open Risks

1. **Ollama multimodal API drift.** The `images` field has been stable but different versions accept different encodings (raw base64 vs data-uri). Mitigation: test against the current deployed Ollama; strip `data:image/...;base64,` prefix defensively.
2. **`GetModels()` doesn't reveal multimodal capability.** Heuristic-by-name is fragile - new vision model families will surface and not match the regex. Mitigation: allow user to force any model; only warn, don't block.
3. **Large images blow up local memory.** A 4K image encodes to ~5MB base64; some models time out. Mitigation: optional auto-resize to 768px max dimension before sending (add in Phase 10 as a setting).
4. **Tag normalization double-call.** Currently `VLModelService` routes the raw prompt through `TagPromptService.BuildAsync` which itself runs a two-pass LLM. That's three LLM calls per image in normalize mode. Document latency; no mitigation in v1.
5. **Gallery dialog performance.** Pulling all images is expensive. Mitigation: limit to 50 most recent; add search later.
6. **Send-to-Workshop** reuses Phase 8 infrastructure; ensure `LLMToolsTab.HandleSendToWorkshop` handler is still present (if Phase 8 was removed or refactored, fall back to Send-to-Process).

---

## Phase Summary

_To be filled in on completion._
