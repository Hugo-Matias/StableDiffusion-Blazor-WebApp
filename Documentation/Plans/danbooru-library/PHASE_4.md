# Phase 4: Save UX (Search tab)

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 3 points
> **Depends on:** `IDanbooruLibraryService` (Phase 3)
> **Unblocks:** Phase 5 relies on `DanbooruMediaSavedEventArgs` being raised by end-user saves so the Library tab auto-refreshes.

---

## 1. Objective

Expose a "Save to Library" action on every `DanbooruImageCard` on the Search tab and wire it to `IDanbooruLibraryService.SaveAsync`, showing `Snackbar` feedback for the three result states (`Saved`, `AlreadySaved`, `Failed`) and disabling the button while a download is in flight to avoid double-clicks.

---

## 2. Context & Background

`BlazorWebApp/Components/Resources/DanbooruImageCard.razor` already has a hover overlay with three action buttons (Copy Tags, Send to Img2Img, Open Fullscreen). The Save action is a fourth button in the same `.quick-actions` container. `BlazorWebApp/Pages/Danbooru.razor` wires existing per-card callbacks through `EventCallback` parameters - follow the same pattern (`OnSave`) so the card stays agnostic of the service.

The page already injects `IOrchestratorService M`, `IStateService State`, `ISettingsService Settings`, `IDialogService DialogService`, `IJSRuntime JS`. For this phase it also needs `IDanbooruLibraryService` and `ISnackbar`.

Inherited conventions (verbatim from `MAIN_PLAN.md`):

- "Subsequent clicks on the same post show an 'Already saved' info toast."
- "Error path (network / disk) surfaces a clear error toast and logs."
- "Disable button briefly during download."

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `IDanbooruLibraryService.SaveAsync(DanbooruPost)` and `DanbooruSaveResult` (Phase 3)
  - `DanbooruMediaSavedEventArgs` (Phase 3) - fires automatically; Phase 4 does not subscribe, but Phase 5 does.
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` - overlay layout and existing `OnSendTags` / `OnShowImage` callbacks
  - `BlazorWebApp/Components/Resources/DanbooruImageCard.razor.css` - `.quick-actions`, `.action-btn`, `.action-btn.primary` classes
  - `BlazorWebApp/Pages/Danbooru.razor` - injection and Search-tab wiring for `WriteDanbooruTags`, `ShowImageViewerDialog`
- **External references:** MudBlazor `ISnackbar` severity conventions already used across the codebase (see `BlazorWebApp/Pages/Resources.razor`).

---

## 4. Files Inventory

### To Create

_None - this phase is component + page modifications only._

### To Modify

| Path                                                            | Change                                                                                                                                                                                                                           |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Components/Resources/DanbooruImageCard.razor`     | Add `OnSave` `EventCallback`, `IsSaving` parameter, `IsInLibrary` parameter; new overlay button                                                                                                                                  |
| `BlazorWebApp/Components/Resources/DanbooruImageCard.razor.css` | Style for the new action icon state (saved / saving)                                                                                                                                                                             |
| `BlazorWebApp/Pages/Danbooru.razor`                             | Inject `IDanbooruLibraryService Library` and `ISnackbar Snackbar`; implement `SavePostAsync(DanbooruPost)`; track per-post `IsSaving`/`IsInLibrary`; wire `OnSave` on the card; preload "already saved" ids for the current page |

### To Leave Untouched (but referenced)

| Path                                              | Why it matters                                           |
| ------------------------------------------------- | -------------------------------------------------------- |
| `BlazorWebApp/Services/DanbooruLibraryService.cs` | The behaviour the UI relies on must not be extended here |

---

## 5. Step-by-Step Execution

### Step 4.1: Extend `DanbooruImageCard` with the Save action

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add parameters: `[Parameter] public EventCallback OnSave { get; set; }`, `[Parameter] public bool IsSaving { get; set; }`, `[Parameter] public bool IsInLibrary { get; set; }`.
- [ ] Insert a new `<button>` inside `.quick-actions` between Send-to-Img2Img and Expand.
- [ ] Icon states:
  - `IsSaving` -> `fa-solid fa-spinner fa-spin`, button `disabled`
  - `IsInLibrary` -> `fa-solid fa-bookmark` (solid), `title="Saved to Library"`, button `disabled`
  - Otherwise -> `fa-regular fa-bookmark`, `title="Save to Library"`
- [ ] No CSS restructuring required; reuse `.action-btn`.

#### Code Sketch

```razor
@* BlazorWebApp/Components/Resources/DanbooruImageCard.razor (inside .quick-actions) *@
<button class="action-btn" @onclick="@(() => SendTags(false))" @onclick:stopPropagation title="Copy Tags">
    <i class="fa-solid fa-file-lines"></i>
</button>
<button class="action-btn" @onclick="@(() => SendTags(true))" @onclick:stopPropagation title="Send to Img2Img">
    <i class="fa-solid fa-file-image"></i>
</button>
<button class="action-btn"
        @onclick="HandleSave"
        @onclick:stopPropagation
        disabled="@(IsSaving || IsInLibrary)"
        title="@SaveTooltip()">
    <i class="@SaveIconClass()"></i>
</button>
<button class="action-btn primary" @onclick="ShowImage" @onclick:stopPropagation title="View Fullscreen">
    <i class="fa-solid fa-expand"></i>
</button>
```

```csharp
@* ... in the @code block ... *@
[Parameter] public EventCallback OnSave { get; set; }
[Parameter] public bool IsSaving { get; set; }
[Parameter] public bool IsInLibrary { get; set; }

private async Task HandleSave()
{
    if (IsSaving || IsInLibrary) return;
    await OnSave.InvokeAsync();
}

private string SaveIconClass() =>
    IsSaving     ? "fa-solid fa-spinner fa-spin" :
    IsInLibrary  ? "fa-solid fa-bookmark" :
                   "fa-regular fa-bookmark";

private string SaveTooltip() =>
    IsSaving     ? "Saving..." :
    IsInLibrary  ? "Saved to Library" :
                   "Save to Library";
```

#### Conventions to Respect

- Icon font is FontAwesome (matches existing card).
- `@onclick:stopPropagation` on every action button (pattern used for the other three).

#### Validation

- Card renders the new button and toggles between three visual states as `IsSaving` / `IsInLibrary` change.
- `dotnet build` passes.

---

### Step 4.2: Wire page-level Save flow

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] In `BlazorWebApp/Pages/Danbooru.razor` add `@inject IDanbooruLibraryService Library` and `@inject ISnackbar Snackbar`.
- [ ] Add private state:
  - `HashSet<int> _savingIds = new();`
  - `HashSet<int> _savedPostIds = new();`
- [ ] On initial `LoadInitialData` (and after each `LoadNextPage`), query the repository for which of the just-loaded post ids are already in the library and populate `_savedPostIds`.
- [ ] Pass `IsSaving="_savingIds.Contains(post.Id)"` and `IsInLibrary="_savedPostIds.Contains(post.Id)"` into every `DanbooruImageCard`.
- [ ] Implement `SavePostAsync(DanbooruPost post)`:
  ```csharp
  if (!_savingIds.Add(post.Id)) return;
  try
  {
      var result = await Library.SaveAsync(post);
      switch (result.Status)
      {
          case DanbooruSaveStatus.Saved:
              _savedPostIds.Add(post.Id);
              Snackbar.Add($"Saved post #{post.Id} to library.", Severity.Success);
              break;
          case DanbooruSaveStatus.AlreadySaved:
              _savedPostIds.Add(post.Id);
              Snackbar.Add("Already saved to library.", Severity.Info);
              break;
          case DanbooruSaveStatus.Failed:
              Snackbar.Add($"Failed to save: {result.ErrorMessage}", Severity.Error);
              break;
      }
  }
  finally
  {
      _savingIds.Remove(post.Id);
      StateHasChanged();
  }
  ```
- [ ] Wire on the card: `OnSave="@(() => SavePostAsync(post))"`.
- [ ] For the "preload already-saved" lookup, add a dependency on `ISavedDanbooruMediaRepository` as well (or add a `GetExistingPostIdsAsync(IEnumerable<int>)` helper on `IDanbooruLibraryService` - see Open Clarifications). Proposed default: extend the service with a single bulk helper to keep the page dependency-list thin.

#### Implementation Notes

- `_savingIds` is per-circuit, so concurrent saves on two cards work. `_savedPostIds` is cleared on search reset (new search string).
- `StateHasChanged()` is needed inside `SavePostAsync` because the await spans a circuit-level async boundary.
- Preload query: call the bulk helper once per page load / `LoadNextPage` with `Posts.Select(p => p.Id).ToList()`.

#### Code Sketch (page excerpt)

```razor
@inject IDanbooruLibraryService Library
@inject ISnackbar Snackbar

<DanbooruImageCard Post="post"
                   IsSaving="_savingIds.Contains(post.Id)"
                   IsInLibrary="_savedPostIds.Contains(post.Id)"
                   OnSendTags="@(isImg2Img => WriteDanbooruTags(post, isImg2Img))"
                   OnShowImage="@(() => ShowImageViewerDialog(post))"
                   OnSave="@(() => SavePostAsync(post))" />
```

#### Conventions to Respect

- `Severity.Success` / `Info` / `Error` usage matches `BlazorWebApp/Pages/Resources.razor`.
- All events must continue to flow through `EventService`. `SavePostAsync` does not raise events directly - the library service does (Phase 3).

#### Validation

- Click Save on a new post: spinner -> bookmark (solid) + success toast; file on disk; DB row present.
- Click Save again on the same post: info toast "Already saved"; icon already solid; no second file.
- Disconnect network and click Save on a new post: spinner -> error toast; icon reverts to outline bookmark.
- Navigate away and back: bookmark-solid state reflects DB (preload query works).

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:** None added in this phase; `IDanbooruLibraryService` was registered in Phase 3. If the bulk-preload helper is added, no additional DI line is required (it becomes a method on the same interface).
- **Events to publish / subscribe:**
  - Publishes (indirectly, via the service): `DanbooruMediaSavedEventArgs` - Phase 5 listens to this to refresh the Library grid.
- **Configuration bindings:** Reads `DanbooruOptions.SavedMediaPath` indirectly through the service.
- **Startup side-effects:** None.

---

## 7. Testing Strategy

- **Automated tests to add/update:** Component-level tests are out of repo pattern for this page; rely on manual coverage.
- **Manual verification checklist:**
  1. Save on image post -> toast "Saved post #N to library"; card shows solid bookmark.
  2. Save on same post -> toast "Already saved"; card remains solid.
  3. Save on video post (mp4) -> slower; spinner visible for the duration; success toast.
  4. Force failure (disconnect network / wrong `SavedMediaPath`) -> red error toast with message.
  5. Search for a new term; posts already in library show solid bookmark immediately.
- **Regression watch-list:**
  - Infinite scroll (`DanbooruMasonry.js`) unchanged.
  - Existing overlay actions (Copy Tags, Send to Img2Img, Expand) still trigger from the card.

---

## 8. Stress Points Specific to This Phase

| Risk                                                       | Mitigation                                                                                                          |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| Double-click triggers two saves                            | `_savingIds.Add` returns false on duplicate; button is also `disabled` while saving.                                |
| UI freezes during large video download                     | Awaited async path; spinner shown; does not block Blazor circuit.                                                   |
| `_savedPostIds` drifts out of sync after manual DB changes | Cheap to rebuild: repopulated on each search / infinite-scroll page. Not a user-facing concern in v1.               |
| Preload N+1 query on every scroll page                     | Bulk helper that does `Where(e => ids.Contains(e.DanbooruPostId)).Select(e => e.DanbooruPostId)` in a single query. |

---

## 9. Resolved Assumptions

- **Bookmark iconography:** Regular bookmark for "not saved", solid bookmark for "in library", spinner for "saving". Consistent with the existing FontAwesome usage in the card.
- **Preload via service, not repository injection in the page:** Keeps `Danbooru.razor` free of repository concerns. The service adds a single `Task<HashSet<int>> GetExistingPostIdsAsync(IEnumerable<int>)` method that delegates to the repository.
- **Snackbar severities:** `Success` / `Info` / `Error` match the pattern in `Pages/Resources.razor`.

---

## 10. Open Clarifications

- **Step 4.2 - Bulk preload API shape:** Proposed: add `GetExistingPostIdsAsync(IEnumerable<int>)` on `IDanbooruLibraryService` (thin wrapper over a new repository method `GetExistingPostIdsAsync`). Alternative: inject `ISavedDanbooruMediaRepository` directly into `Danbooru.razor`. Default to the service wrapper because it keeps UI dependencies minimal.
- **Step 4.1 - Icon choice:** Bookmark is proposed. If the user prefers a download / cloud-arrow metaphor, swap `fa-bookmark` for `fa-floppy-disk` or `fa-cloud-arrow-down`. Resolve during Stage 1 of the step.

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 4.1  | [ ]    | 2          |       |
| 4.2  | [ ]    | 2          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 4.1 complete
- [ ] Step 4.2 complete
- [ ] Phase build green; manual round-trip (save / already-saved / error) confirmed

---

## 14. Phase Summary

_To be filled in after the phase is complete._

---

## 15. Cross-References

- Main plan section: [Phase 4: Save UX (Search tab)](./MAIN_PLAN.md)
- Prior phase: [PHASE_3.md](./PHASE_3.md)
- Next phase: [PHASE_5.md](./PHASE_5.md)
- Related code: `BlazorWebApp/Pages/Resources.razor` (snackbar severity patterns)
