# Phase 3: External Image Gating & Bookmark CTA

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md#phase-3-external-image-gating--bookmark-cta)
> **Status:** [ ] Not Started
> **Complexity:** 5 points
> **Depends on:** Phase 1 artifacts - `SendTo.IsLocal` and external metadata guard; Phase 2 artifacts - `ImageViewer` now renders the shared `AssetInfoPanel`.
> **Unblocks:** Phase 4 - cleanup and documentation can describe one shared panel plus the external-image bookmark rule.

---

## 1. Objective

Implement the external-image experience for Danbooru search results. After this phase, non-bookmarked remote images hide the "Send image to:" workflow-input section, still allow prompt-parameter send, show a bookmark call-to-action, and can flip to local-file behavior in place after a successful save without closing and reopening the viewer.

---

## 2. Context & Background

The main plan deliberately keeps workflow-image send local-file-only because `ImageSendToService` depends on base64 content loaded from disk. Danbooru search results are remote CDN URLs, while Danbooru library items are local `/files/danbooru/{FilePath}` routes. The user-visible rule for this phase is therefore not "Danbooru vs Gallery" but "external vs local path".

Inherited constraints from the main plan:

- `Local-file-only for "Send image to workflow"`.
- `External images keep parameter-send enabled (prompt only)`.
- `Show a bookmark CTA on external images`.
- `After clicking "Bookmark", the viewer flips to the local-file UI in place (no dialog reopen required).`

Current code facts that matter:

- `Image(DanbooruPost)` sets `Path = post.Url`, `Prompt = string.Join(", ", post.Tags)`, and width/height only.
- `Danbooru.razor` currently opens the viewer with `_viewerImages = Posts.Select(p => new Image(p)).ToList();` and saves from cards with `LibraryService.SaveAsync(post)`.
- `IDanbooruLibraryService.SaveAsync(DanbooruPost post, ...)` returns `DanbooruSaveResult` only; it does not return the persisted entity.
- `DanbooruLibraryService` publishes `DanbooruMediaSavedEventArgs(entity)` after repository persistence, and `Danbooru.razor` already subscribes to that event for list refresh.

This phase adds the caller-specific Danbooru save hook while keeping the workflow-send gating inside `AssetInfoPanel`, where the send UI already lives.

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` is the sole info-panel implementation.
  - `BlazorWebApp/Services/IImageSendToService.IsLocal` exists and is used by the panel.
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - owner of the send sections that must be gated.
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor` - viewer wrapper that forwards the bookmark request.
  - `BlazorWebApp/Pages/Danbooru.razor` - caller that owns the posts list, viewer image list, and library save messaging.
  - `BlazorWebApp/Data/Dtos/DanbooruPost.cs` - available URL fields: `Url`, `SampleUrl`, and `PreviewUrl`.
  - `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs` - confirms local route shape uses `FilePath`.
  - `BlazorWebApp/Services/IDanbooruLibraryService.cs` - save contract returns only `DanbooruSaveResult`.
  - `BlazorWebApp/Services/DanbooruLibraryService.cs` - confirms the existing service publishes `DanbooruMediaSavedEventArgs`.
  - `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` - current bookmark action path and URL usage on search cards.
- **External references:** _Not applicable for this phase._

---

## 4. Files Inventory

### To Create

| Path                             | Purpose |
| -------------------------------- | ------- |
| _Not applicable for this phase._ |         |

### To Modify

| Path                                                        | Change                                                                                                                         |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` | Hide image-send UI for remote paths, show info `MudAlert`, and expose `OnRequestBookmark`                                      |
| `BlazorWebApp/Components/Shared/Image/ImageViewer.razor`    | Forward `OnRequestBookmark` to the shared panel                                                                                |
| `BlazorWebApp/Pages/Danbooru.razor`                         | Build the viewer bookmark lookup, handle save-from-viewer, and mutate the in-memory `Image.Path` to the local route after save |

### To Leave Untouched (but referenced)

| Path                                                        | Why it matters                                                       |
| ----------------------------------------------------------- | -------------------------------------------------------------------- |
| `BlazorWebApp/Services/DanbooruLibraryService.cs`           | Existing save + publish behavior should be reused, not replaced      |
| `BlazorWebApp/Data/Entities/Image.cs`                       | Confirms current `Image.Path` population for Danbooru search results |
| `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` | Existing save UX and snackbar text are the baseline to mirror        |

---

## 5. Step-by-Step Execution

### Step 3.1: Gate remote-image workflow send UI inside `AssetInfoPanel`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] When `!SendTo.IsLocal(Asset.Path)`, hide the entire "Send image to:" section.
- [ ] Keep the "Send parameters to:" section available so prompt send still works.
- [ ] Render a `MudAlert` with info severity and the exact plan text: `Bookmark this image to enable workflow send. Only prompt parameters are available for external images.`
- [ ] Add `EventCallback OnRequestBookmark`; when provided, show a button inside the alert that invokes it.

#### Implementation Notes

The CTA belongs in `AssetInfoPanel` because the panel already owns the send UI and path classification. Keep the alert conditional on external paths only. Do not disable parameter-send buttons globally for external images; the plan explicitly keeps prompt send available.

#### Code Sketch

```razor
@if (!IsVideo)
{
    if (SendTo.IsLocal(Asset?.Path))
    {
        <div class="send-to-section">
            <span class="send-to-label">Send image to:</span>
            <!-- existing workflow buttons -->
        </div>
    }
    else
    {
        <MudAlert Severity="Severity.Info" Dense="true" Variant="Variant.Outlined">
            <div>Bookmark this image to enable workflow send. Only prompt parameters are available for external images.</div>
            @if (OnRequestBookmark.HasDelegate)
            {
                <MudButton Size="Size.Small" Variant="Variant.Text" OnClick="OnRequestBookmark">
                    Bookmark
                </MudButton>
            }
        </MudAlert>
    }
}
```

#### Conventions to Respect

- `External images keep parameter-send enabled (prompt only)`.
- Keep the gating rule based on `SendTo.IsLocal(Asset.Path)`, not caller-specific flags.

#### Validation

- Open a remote Danbooru search result and confirm the alert is shown while the image-send buttons are not.
- Open a local Gallery or Danbooru library asset and confirm the image-send section still renders.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 3.2: Thread the bookmark callback through `ImageViewer`

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `EventCallback<Image> OnRequestBookmark` to `ImageViewer`.
- [ ] Pass the current image to the callback when `AssetInfoPanel` requests bookmarking.
- [ ] Keep the parameter optional so existing non-Danbooru callers remain unaffected.

#### Implementation Notes

This is a local component callback chain, not a replacement for the app-wide pub/sub event. The domain-level refresh still comes from `DanbooruMediaSavedEventArgs`; the callback here is only the viewer-to-page request to start the save operation.

#### Code Sketch

```razor
<AssetInfoPanel Asset="CurrentImage"
                IsVideo="IsCurrentVideo"
                ShowAiMetadata="ShowAiMetadata"
                ShowKeyboardShortcuts="true"
                SelectedParams="_selectedParams"
                SelectedParamsChanged="HandleSelectedParamsChanged"
                OnRequestBookmark="RequestBookmark"
                OnClose="ToggleInfo" />
```

```csharp
[Parameter] public EventCallback<Image> OnRequestBookmark { get; set; }

private Task RequestBookmark()
{
    return CurrentImage is null
        ? Task.CompletedTask
        : OnRequestBookmark.InvokeAsync(CurrentImage);
}
```

#### Conventions to Respect

- Keep the new parameter additive.
- Do not push Danbooru-specific service dependencies into `ImageViewer`.

#### Validation

- Confirm non-Danbooru `ImageViewer` consumers compile without providing the new parameter.
- Confirm the callback fires only when the alert button is rendered and clicked.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 3.3: Implement bookmark-from-viewer flow in `Danbooru.razor`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Build a deterministic lookup from each viewer image's full `DanbooruPost.Url` value back to its source `DanbooruPost`.
- [ ] Implement `HandleBookmarkFromViewer(Image img)` to call `LibraryService.SaveAsync(post)` and reuse the existing snackbar messages.
- [ ] After a successful save, update the in-memory `img.Path` to `/files/danbooru/{FilePath}` so the shared panel re-evaluates the asset as local.
- [ ] Rely on the existing `DanbooruMediaSavedEventArgs` subscription for list refresh rather than manually reloading the library.

#### Implementation Notes

`IDanbooruLibraryService.SaveAsync` does not return the saved entity, so the viewer-side in-memory path flip must obtain the local `FilePath` from data already available after save. The cleanest local approach is:

1. Keep a dictionary keyed by the full `DanbooruPost.Url` value copied into `Image.Path` for request routing.
2. On `DanbooruSaveResult.Saved`, locate the saved media from the event-updated library collection or a targeted query path already available in the page state, then update `img.Path`.
3. Re-render the page so `AssetInfoPanel` recomputes `SendTo.IsLocal(img.Path)`.

Do not bypass the existing event-driven list refresh with direct page-level reloads. Use the full file URL, not `SampleUrl` or `PreviewUrl`, because `new Image(post)` preserves `post.Url` as the viewer image path today.

#### Code Sketch

```csharp
private readonly Dictionary<string, DanbooruPost> _viewerPostsByFullUrl = new(StringComparer.Ordinal);

private void ShowImageViewerDialog(DanbooruPost post)
{
    _viewerImages = Posts.Select(p => new Image(p)).ToList();
  _viewerPostsByFullUrl.Clear();
    foreach (var currentPost in Posts)
    {
    _viewerPostsByFullUrl[currentPost.Url] = currentPost;
    }

    _viewerStartIndex = Posts.IndexOf(post);
    _showViewer = true;
}

private async Task HandleBookmarkFromViewer(Image img)
{
  if (!_viewerPostsByFullUrl.TryGetValue(img.Path, out var post))
        return;

    var result = await LibraryService.SaveAsync(post);
    switch (result)
    {
        case DanbooruSaveResult.Saved:
            Snackbar.Add("Saved to library", Severity.Success);
            break;
        case DanbooruSaveResult.AlreadySaved:
            Snackbar.Add("Already saved to library", Severity.Info);
            break;
        default:
            Snackbar.Add("Failed to save to library", Severity.Error);
            return;
    }

    var saved = _libraryItems.FirstOrDefault(m => m.DanbooruPostId == post.Id);
    if (saved != null)
    {
        img.Path = $"/files/danbooru/{saved.FilePath}";
        await InvokeAsync(StateHasChanged);
    }
}
```

#### Conventions to Respect

- `All new event hooks ... route through IEventService pub/sub` applies to data refresh. Keep the service-published `DanbooruMediaSavedEventArgs` as the source of library synchronization.
- Reuse the existing snackbar message strings already present in `SaveToLibrary(DanbooruPost post)`.

#### Validation

- Open a remote search result in the viewer and confirm the CTA is visible.
- Click `Bookmark`, confirm the existing snackbar message appears, and confirm the alert disappears in place after `img.Path` switches to `/files/danbooru/{FilePath}`.
- Confirm library items still open with the full local workflow-send UI.

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:** none.
- **Events to publish / subscribe:**
  - Reuse existing `DanbooruMediaSavedEventArgs` published by `DanbooruLibraryService`.
  - No new app-level event type is required for this phase.
- **Configuration bindings:** none.
- **Startup side-effects:** none.

---

## 7. Testing Strategy

- **Automated tests to add/update:** _Not applicable by default; the current value is primarily in manual viewer flow verification unless a natural page-level test seam already exists in the session._
- **Manual verification checklist:**
  1. Search-tab Danbooru image shows bookmark CTA instead of image-send buttons.
  2. Prompt parameter send still works for that same remote image.
  3. Clicking `Bookmark` shows the same snackbar text as the card save action.
  4. After save, the viewer flips to the local workflow-send UI without reopening.
  5. Already-local library images never show the CTA.
- **Regression watch-list:**
  - Existing Danbooru card save behavior.
  - `DanbooruMediaSavedEventArgs` list refresh.
  - Shared `AssetInfoPanel` behavior in Gallery.

---

## 8. Stress Points Specific to This Phase

- **Remote images accidentally still try to send file data**
  - Failure mode: hidden buttons are incomplete, and some path still reaches `_io.GetBase64FromFile(asset.Path)` with `https://...`.
  - Mitigation: gate the entire "Send image to:" section on `SendTo.IsLocal(Asset.Path)` inside `AssetInfoPanel`.
- **Viewer cannot map the clicked image back to the source post**
  - Failure mode: bookmark CTA appears, but the page cannot find the `DanbooruPost` to save.
  - Mitigation: build the lookup at `ShowImageViewerDialog` time from the same posts collection that produced `_viewerImages`, keyed by the full `DanbooruPost.Url` value copied into `Image.Path`.
- **UI does not flip to local mode after save**
  - Failure mode: snackbar says save succeeded, but the viewer still behaves like the image is remote.
  - Mitigation: update the in-memory `Image.Path` to `/files/danbooru/{FilePath}` after save and re-render the page.
- **Backend unavailable hides both workflow lists**
  - Failure mode: executor mistakes an empty parameter-workflow section for a CTA bug.
  - Mitigation: validate CTA presence separately from backend-dependent workflow enumeration.

---

## 9. Resolved Assumptions

- **Callback vs pub/sub split:** The `OnRequestBookmark` component callback is acceptable because it is an in-component request path; the app-level state refresh still relies on `DanbooruMediaSavedEventArgs` via `EventService`.
- **Snackbar reuse:** The viewer save action should reuse the exact messages already emitted by `SaveToLibrary(DanbooruPost post)` to keep UX consistent.
- **Viewer lookup key:** The in-memory lookup should be keyed by the full `DanbooruPost.Url` value because `new Image(post)` copies that exact full URL into `Image.Path`; `SampleUrl` and `PreviewUrl` are not preserved in the viewer image list.
- **Local-route shape:** `/files/danbooru/{FilePath}` remains the authoritative local path because `ShowLibraryViewer` already uses that route shape today.

---

## 10. Open Clarifications

_None - phase is fully specified._

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes                                             |
| ---- | ------ | ---------- | ------------------------------------------------- |
| 3.1  | [ ]    | 2          |                                                   |
| 3.2  | [ ]    | 1          |                                                   |
| 3.3  | [ ]    | 2          | Includes in-place path flip after successful save |

---

## 12. Issues & Resolutions

_Populated during execution._

### Issue: _Not applicable for this phase yet._

- **Impact:** Pending
- **Resolution:** Pending

---

## 13. Commit Checkpoints

- [ ] Step 3.1 complete
- [ ] Step 3.2 complete
- [ ] Step 3.3 complete
- [ ] Phase build green

---

## 14. Phase Summary

_To be filled in after the phase is complete._

- **Accomplishments:**
- **Deferred to later phase:**
- **Lessons learned:**

---

## 15. Cross-References

- Main plan section: [Phase 3: External Image Gating & Bookmark CTA](./MAIN_PLAN.md#phase-3-external-image-gating--bookmark-cta)
- Prior phase: [PHASE_2.md](./PHASE_2.md)
- Next phase: [PHASE_4.md](./PHASE_4.md)
- Related plans / docs:
  - `BlazorWebApp/Pages/Danbooru.razor`
  - `BlazorWebApp/Data/Dtos/DanbooruPost.cs`
  - `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs`
  - `BlazorWebApp/Services/DanbooruLibraryService.cs`
