# Phase 5: Library Tab UI

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 8 points
> **Depends on:**
>
> - `ISavedDanbooruMediaRepository.GetPagedAsync` / `CountAsync` (Phase 2)
> - `IDanbooruLibraryService.DeleteAsync` + save/delete events (Phase 3)
> - `DanbooruMediaSavedEventArgs`, `DanbooruMediaDeletedEventArgs` (Phase 3)
> - Static file mapping at `/files/danbooru` (Phase 6) - the cards render from this URL. Library UI can be built and tested with a temporary `UseStaticFiles` mapping stubbed in by the executor if Phase 6 is not yet done, or simply developed in parallel.
>   **Unblocks:** Phase 7 (polish and edge cases).

---

## 1. Objective

Replace the "Coming soon" placeholder inside the Library `MudTabPanel` of `BlazorWebApp/Pages/Danbooru.razor` with a fully functional grid: masonry of saved Danbooru media sourced from the local DB, topbar filters (tag contains, rating, min score, video-only, favorites), infinite scroll using the existing `DanbooruMasonry.js`, a reusable `SavedDanbooruMediaCard` component with per-item actions (Copy Tags, Send to Img2Img, Open Fullscreen, Open Source, Delete), and delete confirmation via the existing `ConfirmationDialog` pattern. Live-update the grid when users save from the Search tab by subscribing to `DanbooruMediaSavedEventArgs` through `EventService`.

---

## 2. Context & Background

The Library tab's target behaviour is structurally similar to the Search tab:

- `TopbarLayout` + `Topbar` / `Content` slots (already used in the Search tab).
- Masonry via `BlazorWebApp/wwwroot/js/DanbooruMasonry.js` + CSS in `BlazorWebApp/Pages/Danbooru.razor.css`.
- Infinite scroll callback invoked from JS.
- Per-card hover overlay.

Differences from Search:

- Source is the local DB (`ISavedDanbooruMediaRepository.GetPagedAsync`), not the remote API.
- Media URLs are `/files/danbooru/{relativePath}` (served by Phase 6) instead of Danbooru CDN URLs.
- Cards have an additional Delete action and no Save action (already saved).
- Filtering is fully client/server hybrid as defined by `SavedDanbooruMediaFilter` (server scalar filters + tag substring on JSON).

Delete flow (verbatim main-plan decision):

- "Delete via existing `ConfirmationDialog` pattern with 'Delete files from disk?' checkbox. Mirrors `Pages/Resources.razor` delete flow."

See `BlazorWebApp/Pages/Resources.razor` lines 185-205 for the canonical dialog invocation:

```csharp
confirmParam.Add(nameof(ConfirmationDialog.CheckboxEnabled), true);
confirmParam.Add(nameof(ConfirmationDialog.CheckboxContent), "Delete files from disk?");
confirmParam.Add(nameof(ConfirmationDialog.CheckboxColor), Color.Error);
// dialog result.Data is (bool)deleteFiles
```

`ConfirmationDialog` (at `BlazorWebApp/Components/Shared/ConfirmationDialog.razor`) returns `DialogResult.Ok(bool)` when `CheckboxEnabled` is true.

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs`, `DanbooruTagBundle.cs`
  - `BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs` + `SavedDanbooruMediaFilter`
  - `BlazorWebApp/Services/IDanbooruLibraryService.cs` (`DeleteAsync`)
  - `BlazorWebApp/Events/DanbooruMediaSavedEventArgs.cs`, `DanbooruMediaDeletedEventArgs.cs`
  - `DanbooruOptions.SavedMediaPath` (Phase 1) - not read directly by the UI; file access goes through `/files/danbooru`.
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Pages/Danbooru.razor` (Library `MudTabPanel` around line 82)
  - `BlazorWebApp/Pages/Danbooru.razor.css` (masonry classes already declared)
  - `BlazorWebApp/wwwroot/js/DanbooruMasonry.js` - infinite-scroll JS API (`attachWindowScroll`, `setHasMore`, `resetLayout`, `dispose`)
  - `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` (+ css) - card visual baseline
  - `BlazorWebApp/Pages/Resources.razor` - `ConfirmationDialog` usage reference
  - `BlazorWebApp/Components/Shared/ConfirmationDialog.razor` - dialog parameter contract
  - `BlazorWebApp/Services/EventService.cs` - `Subscribe<T>` / `Unsubscribe<T>` contract
- **External references:** MudBlazor grid / chip / select components already in use across the codebase.

---

## 4. Files Inventory

### To Create

| Path                                                                 | Purpose                                                                              |
| -------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor`     | Card component for a saved library item                                              |
| `BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css` | Scoped card styles (mirror `DanbooruImageCard.razor.css`, add delete button styling) |

### To Modify

| Path                                    | Change                                                                                                                                                                                   |
| --------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Pages/Danbooru.razor`     | Replace Library placeholder with topbar + masonry grid; inject repository, library service, event service, dialog service; implement load/filter/delete; subscribe to save/delete events |
| `BlazorWebApp/Pages/Danbooru.razor.css` | Add Library-specific modifier classes only if needed (masonry is already styled)                                                                                                         |

### To Leave Untouched (but referenced)

| Path                                                        | Why it matters                                         |
| ----------------------------------------------------------- | ------------------------------------------------------ |
| `BlazorWebApp/wwwroot/js/DanbooruMasonry.js`                | Reused as-is for the second grid instance              |
| `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` | Parallel card; do not over-abstract into a shared base |

---

## 5. Step-by-Step Execution

### Step 5.1: `SavedDanbooruMediaCard` component

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `SavedDanbooruMediaCard.razor` with `[Parameter] public SavedDanbooruMedia Media { get; set; }` and four callbacks:
  - `OnSendTags` (same `EventCallback<bool>` semantics as `DanbooruImageCard`: `true` = Send to Img2Img)
  - `OnShowImage` (`EventCallback`)
  - `OnOpenSource` (`EventCallback`) - opens `https://danbooru.donmai.us/posts/{PostId}` in a new tab
  - `OnDelete` (`EventCallback`)
- [ ] Compose the media URL from `Media.RelativePath`: `$"/files/danbooru/{Media.RelativePath}"`.
- [ ] Overlay layout mirrors `DanbooruImageCard`: top badges (rating + video), centre quick actions, bottom info row.
- [ ] Delete action uses `fa-solid fa-trash`, red hover state.
- [ ] Deferred: video hover-to-play (Phase 7 Step 7.1).

#### Code Sketch

```razor
@* BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor *@
@using BlazorWebApp.Data.Entities

<div class="@GetCardClasses()" @onmouseenter="() => _isHovered = true" @onmouseleave="() => _isHovered = false">
    <div class="image-wrapper">
        @if (Media.IsVideo)
        {
            <video src="@MediaUrl" class="card-media" muted loop playsinline preload="metadata" />
        }
        else
        {
            <img src="@MediaUrl" alt="Saved Danbooru post" class="card-image" loading="lazy" />
        }

        <div class="hover-overlay @(_isHovered ? "visible" : "")">
            <div class="overlay-top @(_isHovered ? "visible" : "")">
                <div class="rating-badge @GetRatingClass()">@RatingLabel()</div>
                @if (Media.IsVideo) { <div class="video-badge"><i class="fa-solid fa-video"></i></div> }
            </div>

            <div class="overlay-center @(_isHovered ? "visible" : "")">
                <div class="quick-actions">
                    <button class="action-btn" @onclick="() => OnSendTags.InvokeAsync(false)" @onclick:stopPropagation title="Copy Tags">
                        <i class="fa-solid fa-file-lines"></i>
                    </button>
                    <button class="action-btn" @onclick="() => OnSendTags.InvokeAsync(true)" @onclick:stopPropagation title="Send to Img2Img">
                        <i class="fa-solid fa-file-image"></i>
                    </button>
                    <button class="action-btn" @onclick="OnOpenSource" @onclick:stopPropagation title="Open on Danbooru">
                        <i class="fa-solid fa-up-right-from-square"></i>
                    </button>
                    <button class="action-btn primary" @onclick="OnShowImage" @onclick:stopPropagation title="View Fullscreen">
                        <i class="fa-solid fa-expand"></i>
                    </button>
                    <button class="action-btn danger" @onclick="OnDelete" @onclick:stopPropagation title="Delete">
                        <i class="fa-solid fa-trash"></i>
                    </button>
                </div>
            </div>

            <div class="overlay-bottom @(_isHovered ? "visible" : "")">
                <div class="info-row">
                    <span class="info-item"><i class="fa-solid fa-star"></i> @Media.Score</span>
                    <span class="info-item"><i class="fa-solid fa-expand-alt"></i> @($"{Media.Width}x{Media.Height}")</span>
                    <span class="info-item">@Media.Extension.ToUpper()</span>
                </div>
            </div>
        </div>

        @if (!_isHovered)
        {
            <div class="rating-indicator @GetRatingClass()">@RatingLabel()[0]</div>
        }
    </div>
</div>

@code {
    [Parameter] public SavedDanbooruMedia Media { get; set; } = default!;
    [Parameter] public EventCallback<bool> OnSendTags { get; set; }
    [Parameter] public EventCallback OnShowImage { get; set; }
    [Parameter] public EventCallback OnOpenSource { get; set; }
    [Parameter] public EventCallback OnDelete { get; set; }

    private bool _isHovered;
    private string MediaUrl => $"/files/danbooru/{Media.RelativePath}";
    private string GetCardClasses() => "danbooru-card" + (_isHovered ? " hovered" : "");
    private string RatingLabel() => Media.Rating switch
    {
        "g" => "General", "s" => "Sensitive", "q" => "Questionable", "e" => "Explicit", _ => "None"
    };
    private string GetRatingClass() => Media.Rating switch
    {
        "g" => "rating-general", "s" => "rating-sensitive", "q" => "rating-questionable", "e" => "rating-explicit", _ => "rating-none"
    };
}
```

```css
/* BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css */
/* Reuse the palette from DanbooruImageCard.razor.css; add danger variant: */
.action-btn.danger {
  color: var(--mud-palette-error);
}
.action-btn.danger:hover {
  background: rgba(255, 0, 0, 0.15);
}
```

#### Conventions to Respect

- Card does not know about the service or the repository. Only data in, callbacks out.
- `@onclick:stopPropagation` on every overlay button (established pattern).
- Forward-slash URLs (matches `RelativePath` storage from Phase 3).

#### Validation

- Renders an image card from a seeded DB row.
- Renders a video card with poster frame.
- All five action buttons fire the correct callback.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 5.2: Library tab topbar + masonry grid + load/filter/infinite-scroll

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] In `BlazorWebApp/Pages/Danbooru.razor`, add injections:
  - `@inject ISavedDanbooruMediaRepository LibraryRepo`
  - `@inject IDanbooruLibraryService Library`
  - `@inject IEventService Events`
  - (Already injected: `IDialogService DialogService`, `IJSRuntime JS`)
- [ ] Replace the Library `MudTabPanel` body with a `TopbarLayout`:
  - **Topbar:** a row of filter controls bound to a `_libraryFilter` backing model.
    - `MudTextField` (Tag contains)
    - `MudSelect<string>` (Rating: All, General, Sensitive, Questionable, Explicit)
    - `MudNumericField<int?>` (Min score)
    - `MudSwitch` (Video only)
    - `MudSwitch` (Favorites only)
    - Apply / Clear buttons
  - **Content:** masonry grid of `SavedDanbooruMediaCard` with end-of-results footer.
- [ ] Page-level state:
  - `List<SavedDanbooruMedia> _library = new();`
  - `SavedDanbooruMediaFilter _libraryFilter = new();`
  - `int _libraryPage = 0;` (offset = `_libraryPage * PageSize`, `PageSize = 30`)
  - `bool _libraryLoading, _libraryLoadingMore, _libraryHasMore = true;`
  - Separate `IJSObjectReference? _libraryScrollModule;` and `DotNetObjectReference<Danbooru>? _libraryDotNetRef;`
- [ ] Implement `LoadLibraryInitialAsync`, `LoadLibraryNextPageAsync`, `ApplyLibraryFilter` (resets offset + calls initial load + JS `resetLayout`).
- [ ] Hook `[JSInvokable] LoadLibraryNextPage` following the existing `LoadNextPage` pattern.
- [ ] Initialize the library scroll module only when the Library tab becomes active (watch `State.State.Danbooru.ActiveTabIndex`). The simplest approach: initialize lazily in `OnAfterRenderAsync` when the tab is selected and data is present.
- [ ] For "Send Tags": reuse the existing `WriteDanbooruTags` helper, adapted to accept a `DanbooruTagBundle` instead of a `DanbooruPost`. Either add an overload or build a minimal adapter inline.

#### Implementation Notes

- The `DanbooruMasonry.js` module is self-contained and can be imported twice (Search + Library). Use a second `IJSObjectReference` with a unique DOM selector or re-call `attachWindowScroll` bound to a different container if the current JS module supports it. Inspect `DanbooruMasonry.js` during Stage 1 of this step; if it uses fixed selectors that clash, scope the Library container by class name `.danbooru-masonry.library` and pass it into the JS call. (See Open Clarifications.)
- Do **not** subscribe event handlers in `OnInitialized` for a page that uses the `TabbedPageShell` - subscribe in `OnAfterRenderAsync(firstRender: true)` and unsubscribe in `DisposeAsync` to avoid memory leaks across navigations.

#### Code Sketch (page excerpt)

```razor
<MudTabPanel Text="Library" Icon="@Icons.Material.Filled.CollectionsBookmark">
    <TopbarLayout>
        <Topbar>
            <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
                <MudTextField T="string" Label="Tag contains" @bind-Value="_libraryFilter.Tag" Clearable />
                <MudSelect T="string" Label="Rating" @bind-Value="_libraryFilter.Rating" Clearable>
                    <MudSelectItem Value=@("g")>General</MudSelectItem>
                    <MudSelectItem Value=@("s")>Sensitive</MudSelectItem>
                    <MudSelectItem Value=@("q")>Questionable</MudSelectItem>
                    <MudSelectItem Value=@("e")>Explicit</MudSelectItem>
                </MudSelect>
                <MudNumericField T="int?" Label="Min score" @bind-Value="_libraryFilter.MinScore" Min="0" Step="50" />
                <MudSwitch T="bool" Label="Video only" Checked="@(_libraryFilter.VideoOnly == true)" CheckedChanged="@(v => _libraryFilter = _libraryFilter with { VideoOnly = v ? true : (bool?)null })" />
                <MudSwitch T="bool" Label="Favorites" Checked="@(_libraryFilter.FavoriteOnly == true)" CheckedChanged="@(v => _libraryFilter = _libraryFilter with { FavoriteOnly = v ? true : (bool?)null })" />
                <MudButton OnClick="ApplyLibraryFilter" Color="Color.Primary">Apply</MudButton>
            </MudStack>
        </Topbar>
        <Content>
            @if (_libraryLoading)
            {
                <MudStack AlignItems="AlignItems.Center" Class="mt-10"><MudProgressCircular Indeterminate /></MudStack>
            }
            else if (_library.Count > 0)
            {
                <div class="danbooru-masonry-wrapper">
                    <div class="danbooru-masonry library">
                        @foreach (var media in _library)
                        {
                            <div class="danbooru-item">
                                <SavedDanbooruMediaCard Media="media"
                                                        OnSendTags="@(isImg2Img => WriteTagsFromLibrary(media, isImg2Img))"
                                                        OnShowImage="@(() => ShowLibraryImageViewer(media))"
                                                        OnOpenSource="@(() => OpenSource(media))"
                                                        OnDelete="@(() => DeleteLibraryItemAsync(media))" />
                            </div>
                        }
                    </div>
                    @if (_libraryLoadingMore) { <div class="load-more-spinner"><MudProgressCircular Indeterminate /></div> }
                    else if (!_libraryHasMore) { <div class="end-of-results"><MudText Typo="Typo.caption">End of library</MudText></div> }
                </div>
            }
            else
            {
                <MudStack AlignItems="AlignItems.Center" Class="mt-10">
                    <MudText Typo="Typo.body1" Style="opacity: 0.6;">Library is empty</MudText>
                </MudStack>
            }
        </Content>
    </TopbarLayout>
</MudTabPanel>
```

#### Conventions to Respect

- All events flow through `EventService`. Any refresh triggered by saves elsewhere in the app comes in via subscriptions, not direct calls.
- Filter changes reset the page offset before reloading.

#### Validation

- Library tab displays all saved media from Phase 4 saves.
- Filters apply server-side for scalar fields; tag substring works as a coarse filter.
- Infinite scroll loads additional pages until `End of library`.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 5.3: Delete flow + live event subscription

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Implement `DeleteLibraryItemAsync(SavedDanbooruMedia media)` following the `Pages/Resources.razor` `ShowDeleteDialog` pattern - `ConfirmationDialog` with `CheckboxContent = "Delete file from disk?"`.
- [ ] On confirmed delete:
  - Read `bool deleteFile = (bool)result.Data;`
  - Call `await Library.DeleteAsync(media.Id, deleteFile);`
  - Snackbar: `"Item removed from library" + (deleteFile ? " and disk" : "")`.
  - Remove the item from `_library` in memory and call `StateHasChanged` + `resetLayout` on the masonry module.
- [ ] Subscribe to events in `OnAfterRenderAsync(firstRender: true)`:
  - `Events.Subscribe<DanbooruMediaSavedEventArgs>(OnMediaSaved)`
  - `Events.Subscribe<DanbooruMediaDeletedEventArgs>(OnMediaDeleted)`
  - Handlers `InvokeAsync(() => { ... })` to marshal onto the Blazor sync context, then `StateHasChanged`.
- [ ] `OnMediaSaved`: prepend the new `SavedDanbooruMedia` to `_library` if it matches the current filter (simplest: if the user has no filter active, always prepend; otherwise require them to click Apply). Default behaviour: always prepend - Phase 7 may refine.
- [ ] `OnMediaDeleted`: remove by id.
- [ ] Unsubscribe in `DisposeAsync`.

#### Code Sketch

```csharp
private async Task DeleteLibraryItemAsync(SavedDanbooruMedia media)
{
    var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small };
    var p = new DialogParameters();
    p.Add(nameof(ConfirmationDialog.CancelButtonText), "No");
    p.Add(nameof(ConfirmationDialog.OkButtonText), "Yes");
    p.Add(nameof(ConfirmationDialog.OkButtonColor), Color.Error);
    p.Add(nameof(ConfirmationDialog.Content),
          new MarkupString(@"<span class='lead'>Remove this item from your library?</span>"));
    p.Add(nameof(ConfirmationDialog.CheckboxEnabled), true);
    p.Add(nameof(ConfirmationDialog.CheckboxContent), "Delete file from disk?");
    p.Add(nameof(ConfirmationDialog.CheckboxColor), Color.Error);

    var dlg = await DialogService.ShowAsync<ConfirmationDialog>("Delete Library Item", p, options);
    var res = await dlg.Result;
    if (res.Canceled) return;

    var deleteFile = res.Data is bool b && b;
    await Library.DeleteAsync(media.Id, deleteFile);
    Snackbar.Add(deleteFile ? "Item removed from library and disk." : "Item removed from library.", Severity.Success);
    // The DanbooruMediaDeletedEventArgs subscription handles in-memory removal + relayout.
}

private async Task OnMediaSaved(DanbooruMediaSavedEventArgs args)
{
    await InvokeAsync(() =>
    {
        _library.Insert(0, args.Media);
        StateHasChanged();
    });
    if (_libraryScrollModule != null)
        try { await _libraryScrollModule.InvokeVoidAsync("resetLayout"); } catch { }
}

private async Task OnMediaDeleted(DanbooruMediaDeletedEventArgs args)
{
    await InvokeAsync(() =>
    {
        _library.RemoveAll(m => m.Id == args.Id);
        StateHasChanged();
    });
    if (_libraryScrollModule != null)
        try { await _libraryScrollModule.InvokeVoidAsync("resetLayout"); } catch { }
}
```

#### Conventions to Respect

- Event-arg types live in `BlazorWebApp/Events/` and inherit `EventArgs`.
- `Subscribe` / `Unsubscribe` pairs must match. Unsubscribe before disposing the page.
- Marshal event handlers through `InvokeAsync` since `EventService` fires on the publishing thread.

#### Validation

- Delete with file removal: row + file gone, snackbar confirms.
- Delete without file removal: row gone, file remains.
- Save on Search tab while Library tab is open: new item appears at the top without refresh.
- Navigate away from `/danbooru` and back: event handlers do not double-fire (no leak).

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:** None new in this phase.
- **Events to subscribe:**
  - `DanbooruMediaSavedEventArgs` -> prepend to `_library`
  - `DanbooruMediaDeletedEventArgs` -> remove by id
- **Configuration bindings:** None consumed directly (URLs go through the static mapping from Phase 6).
- **Startup side-effects:** None.

---

## 7. Testing Strategy

- **Automated tests to add/update:** Out of repo pattern for interactive page tests; rely on manual coverage.
- **Manual verification checklist:**
  1. Library tab lists all saved items (most recent first).
  2. Tag filter `"blue_hair"` returns only matching rows; clearing restores all.
  3. Rating filter scopes correctly.
  4. Infinite scroll loads further pages until end indicator.
  5. Save on Search tab auto-inserts new card at the top of the Library grid.
  6. Delete with "Delete files from disk" checked: DB row gone, file gone.
  7. Delete with checkbox unchecked: DB row gone, file remains on disk.
  8. Send Tags from Library card injects tags into the prompt (same integration point as Search tab).
- **Regression watch-list:** Search tab infinite scroll unaffected by the second scroll module; Danbooru overlay styles unchanged.

---

## 8. Stress Points Specific to This Phase

| Risk                                                                          | Mitigation                                                                                                                            |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `DanbooruMasonry.js` not designed for two simultaneous grids on the same page | Inspect the module in Stage 1; scope by `.library` modifier if necessary, or instantiate two module imports with distinct DotNetRefs. |
| Event subscriptions leak across navigation                                    | `Subscribe` in `OnAfterRenderAsync(firstRender)`; `Unsubscribe` in `DisposeAsync`.                                                    |
| Prepending a save while an active filter hides it                             | Acceptable v1 behaviour - the user can clear filters to see it. Phase 7 may refine.                                                   |
| Tag substring collides across tag categories (e.g., "eye" matches artists)    | Coarse by design; main plan accepts it.                                                                                               |

---

## 9. Resolved Assumptions

- **Keep `SavedDanbooruMediaCard` separate from `DanbooruImageCard`:** Resolves the planning-time micro-decision. Extracting a shared base would bloat scope and is explicitly out of phase.
- **Open-source URL format:** `https://danbooru.donmai.us/posts/{Media.DanbooruPostId}` - canonical public URL for a post.
- **Snackbar copy:** "Item removed from library[ and disk]." mirrors `Pages/Resources.razor` pattern.
- **Live insert on save ignores current filter:** Simplest behaviour; recorded here so Phase 7 can decide whether to refine.

---

## 10. Open Clarifications

- **Step 5.2 - `DanbooruMasonry.js` dual-grid support:** The existing module was authored for the Search grid. During Stage 1 of this step, inspect whether it can attach to a second container. If it cannot, proposed fix: add a second container selector argument to `attachWindowScroll` (small JS change). Resolve before implementing.
- **Step 5.2 - Rating filter UX:** Using `MudSelect<string>` with `"g" | "s" | "q" | "e"`. Alternative: use five `MudChip` toggles like the existing rating badges. Default is the select.

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 5.1  | [ ]    | 3          |       |
| 5.2  | [ ]    | 3          |       |
| 5.3  | [ ]    | 2          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 5.1 complete
- [ ] Step 5.2 complete
- [ ] Step 5.3 complete
- [ ] Phase build green; all manual verification items pass

---

## 14. Phase Summary

_To be filled in after the phase is complete._

---

## 15. Cross-References

- Main plan section: [Phase 5: Library Tab UI](./MAIN_PLAN.md)
- Prior phase: [PHASE_4.md](./PHASE_4.md)
- Next phase: [PHASE_6.md](./PHASE_6.md)
- Related code: `BlazorWebApp/Pages/Resources.razor`, `BlazorWebApp/Components/Shared/ConfirmationDialog.razor`
