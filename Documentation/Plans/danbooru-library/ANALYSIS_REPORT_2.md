# Danbooru Library - QA Analysis Report (Round 2)

> Follow-up review after the fixes to [ANALYSIS_REPORT.md](./ANALYSIS_REPORT.md).
> Cross-checked against the current working tree (`git diff HEAD` + untracked files).

## TL;DR

About **half** of the issues from round 1 have been fixed. The most impactful runtime
bugs are gone (ratings, EF translation, missing CSS, delete-checkbox, viewer URLs,
lazy-load-on-tab). However, **three P0/P1 items from the previous report were not
addressed**, two **new issues** were introduced by the fixes themselves, and several
"already there" smells remain.

---

## Fix Status Matrix

| #                                                                                                                                   | Issue (prev)                                                 | Status                                                                                                                                                                                              | Verified in                                                                                                                                                                                                |
| ----------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2.A                                                                                                                                 | `DanbooruTagBundleConverter` unused                          | Fixed                                                                                                                                                                                               | [AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs#L181) - now `new Converters.DanbooruTagBundleConverter()`.                                                                                    |
| 2.B                                                                                                                                 | `GetPagedAsync` not EF-translatable                          | Fixed (with caveats)                                                                                                                                                                                | [SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L49-L88). See bug R2-1 below.                                                                    |
| 2.C                                                                                                                                 | `UpdateAsync` only persists `TagsBundle`                     | Not fixed                                                                                                                                                                                           | [SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L92-L99). Still drops scalar changes silently.                                                   |
| 3.A                                                                                                                                 | Rating folder always collapses to `none`                     | Fixed                                                                                                                                                                                               | [DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs#L146-L156) accepts single-letter codes.                                                                               |
| 3.B                                                                                                                                 | Captive `HttpClient` via `AddSingleton` shim                 | Not fixed                                                                                                                                                                                           | [Program.cs](../../../BlazorWebApp/Program.cs#L93-L99) is unchanged. The captive handler is still live.                                                                                                    |
| 3.C                                                                                                                                 | No orphan-file cleanup on DB insert failure                  | Not fixed                                                                                                                                                                                           | [DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs#L76-L84). File is still left behind when `AddAsync` throws.                                                           |
| 3.D                                                                                                                                 | Full in-memory buffer for large videos                       | Not fixed                                                                                                                                                                                           | [DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs#L76-L77) still `GetByteArrayAsync` + `WriteAllBytesAsync`.                                                            |
| 5.A                                                                                                                                 | Missing scoped `.razor.css`                                  | Fixed                                                                                                                                                                                               | [SavedDanbooruMediaCard.razor.css](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css) created with full style set.                                                               |
| 5.B                                                                                                                                 | No `EventService` subscription; direct `LoadLibrary()` calls | Not fixed                                                                                                                                                                                           | [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L350-L362) still calls `await LoadLibrary()` in both `SaveToLibrary` and `DeleteFromLibrary`. **Workspace pub/sub convention still violated.** |
| 5.C                                                                                                                                 | Library never auto-loads                                     | Fixed                                                                                                                                                                                               | [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L161-L174) - new `_activeTabIndex` property triggers lazy load. See bug R2-2 below.                                                            |
| 5.D                                                                                                                                 | Delete dialog ignored the checkbox                           | Fixed                                                                                                                                                                                               | [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L432-L438).                                                                                                                                    |
| 5.E                                                                                                                                 | Viewer pointed at remote CDN                                 | Fixed                                                                                                                                                                                               | [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L402) now uses `/files/danbooru/{FilePath}`.                                                                                                   |
| 5 deviations: `_libraryMinScore int`, `"none"` option, no infinite scroll, `WriteLibraryTags` drops `OnSendTags` + `SaveSettings()` | Not fixed                                                    | [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L180-L182), [#L95](../../../BlazorWebApp/Pages/Danbooru.razor#L95), [#L378-L390](../../../BlazorWebApp/Pages/Danbooru.razor#L378-L390). |
| Startup log for unconfigured `SavedMediaPath`                                                                                       | Not fixed                                                    | [Program.cs](../../../BlazorWebApp/Program.cs#L207-L215) still silent.                                                                                                                              |
| Save button disable during in-flight save                                                                                           | Not fixed                                                    | [DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor).                                                                                                      |
| Whitespace-only reformatting in unrelated hunks                                                                                     | Not fixed                                                    | The monstrous 200+-space attribute indent around `SavedDanbooruMediaCard` callbacks in `Danbooru.razor` is still there.                                                                             |

---

## New Issues Introduced by the Fixes

### R2-1. `GetPagedAsync` tag filter is applied _after_ materialising the entire table

[SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L70-L88):

```csharp
var items = await query
    .OrderByDescending(m => m.DateCreated)
    .ToListAsync(cancellationToken);   // <-- materialises EVERYTHING

if (!string.IsNullOrWhiteSpace(filter.TagSearch))
{
    // ...in-memory filter...
}

return items.Skip(filter.Skip).Take(filter.Take).ToList();
```

Two problems:

1. `Skip/Take` is **always** applied in memory, even when `TagSearch` is null. This
   loses the SQL `LIMIT/OFFSET` that the plan explicitly called out for performance.
   Ship the SQL-side paging for the tag-less path:

   ```csharp
   if (string.IsNullOrWhiteSpace(filter.TagSearch))
   {
       return await query
           .OrderByDescending(m => m.DateCreated)
           .Skip(filter.Skip)
           .Take(filter.Take)
           .ToListAsync(cancellationToken);
   }

   // Tag path: materialise filtered-by-scalars set, then filter + page in memory.
   var items = await query.OrderByDescending(m => m.DateCreated).ToListAsync(cancellationToken);
   var needle = filter.TagSearch.Trim();
   return items
       .Where(m => ContainsTag(m.TagsBundle, needle))
       .Skip(filter.Skip)
       .Take(filter.Take)
       .ToList();
   ```

2. Even on the tag path the whole (scalar-filtered) table is materialised. For the
   plan's "expected library size" that is acceptable, but it should be explicitly
   bounded (e.g., `.Take(5_000)` hard cap + a log when hit) so growth doesn't silently
   produce a memory hog.

Additionally, the rating comparison is written as:

```csharp
query = query.Where(m => EF.Functions.Like(m.Rating ?? "", ratingLower));
```

`LIKE` **without** wildcards is a more expensive equality check, and any stray
`%` / `_` in a caller-supplied rating string would act as a wildcard. The rating values
are hard-coded UI options today, so this is not exploitable, but it is still wrong on
principle. Prefer:

```csharp
var ratingLower = filter.Rating.ToLowerInvariant();
query = query.Where(m => m.Rating != null && m.Rating.ToLower() == ratingLower);
```

(SQLite's `LOWER()` is SQL-translatable in EF Core 6 via the standard `ToLower`
mapping.)

---

### R2-2. Lazy-load path ignores Blazor's render cycle

[Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L161-L174):

```csharp
private int _activeTabIndex
{
    get => State.State.Danbooru.ActiveTabIndex;
    set
    {
        State.State.Danbooru.ActiveTabIndex = value;
        if (value == 1 && !_libraryLoaded)
        {
            _libraryLoaded = true;
            _ = LoadLibrary();       // fire-and-forget
        }
    }
}
```

and `LoadLibrary`:

```csharp
private async Task LoadLibrary()
{
    _libraryLoading = true;
    // ...
    _libraryItems = await LibraryService.GetPagedAsync(filter);
    _libraryLoading = false;
}
```

Problems:

- `_ = LoadLibrary()` detaches the task from the Blazor sync context's render loop.
  When the continuation completes, Blazor does **not** re-render unless something else
  triggers it. On the very first Library-tab activation the user will see the
  "Loading library..." spinner until a subsequent unrelated interaction forces a
  render. Easy to reproduce: open the app, switch to Library - the spinner stays
  indefinitely until you hover a topbar control.
- `_libraryLoading` transitions are not wrapped in `StateHasChanged()`; the
  button-click path works by accident because Blazor re-renders after a bound event
  handler, but the tab-index path does not get that free re-render.
- Setting `_libraryLoaded = true` **before** `LoadLibrary` completes means any
  exception inside `LoadLibrary` permanently locks the tab in the spinner state with
  no retry path.

Fix:

```csharp
set
{
    State.State.Danbooru.ActiveTabIndex = value;
    if (value == 1 && !_libraryLoaded)
    {
        _libraryLoaded = true;
        _ = InvokeAsync(async () =>
        {
            try { await LoadLibrary(); }
            catch (Exception ex)
            {
                _libraryLoaded = false;                // allow retry on failure
                Snackbar.Add($"Failed to load library: {ex.Message}", Severity.Error);
            }
            finally { StateHasChanged(); }
        });
    }
}

private async Task LoadLibrary()
{
    _libraryLoading = true;
    StateHasChanged();
    try
    {
        var filter = /* ... */;
        _libraryItems = await LibraryService.GetPagedAsync(filter);
    }
    finally
    {
        _libraryLoading = false;
    }
}
```

Or, more idiomatically, move the lazy bootstrap into `OnAfterRenderAsync` guarded by
`State.State.Danbooru.ActiveTabIndex == 1` and a `_libraryLoaded` flag - that path is
naturally inside Blazor's render cycle.

---

## Regressions / Still Not Addressed (from prev report)

### P0

- **5.B - Event subscription still missing.** Both the MAIN_PLAN ("All events must
  flow through `EventService`") and Phase 5 Step 5.3 required subscribing to
  `DanbooruMediaSavedEventArgs` / `DanbooruMediaDeletedEventArgs`. Today the service
  still publishes them, but `Danbooru.razor` _never_ calls `EventService.Subscribe<>`
  and instead awaits `LoadLibrary()` directly in `SaveToLibrary` + `DeleteFromLibrary`.
  Result:
  - Saving from the Search tab triggers a full library re-fetch even when the user
    never opens the Library tab (wasted IO).
  - Deletes and saves don't propagate to any other subscriber that might exist later.
  - First-party workspace convention is violated.

  Add in `OnAfterRenderAsync(firstRender: true)`:

  ```csharp
  EventService.Subscribe<DanbooruMediaSavedEventArgs>(OnMediaSaved);
  EventService.Subscribe<DanbooruMediaDeletedEventArgs>(OnMediaDeleted);

  void OnMediaSaved(DanbooruMediaSavedEventArgs e)
      => _ = InvokeAsync(() => { _libraryItems.Insert(0, e.Media); StateHasChanged(); });
  void OnMediaDeleted(DanbooruMediaDeletedEventArgs e)
      => _ = InvokeAsync(() => { _libraryItems.RemoveAll(m => m.Id == e.Id); StateHasChanged(); });
  ```

  Unsubscribe in `DisposeAsync`. Remove the `await LoadLibrary()` calls in save/delete
  paths.

### P1

- **3.B - Captive `HttpClient`.** Unchanged. Still:

  ```csharp
  builder.Services.AddHttpClient<DanbooruLibraryService>(c => c.Timeout = ...);
  builder.Services.AddSingleton<IDanbooruLibraryService>(
      sp => sp.GetRequiredService<DanbooruLibraryService>());
  ```

  This freezes the transient handler for the app's lifetime. Replace with:

  ```csharp
  builder.Services.AddHttpClient<IDanbooruLibraryService, DanbooruLibraryService>(c =>
      c.Timeout = TimeSpan.FromMinutes(5));
  ```

- **2.C - `UpdateAsync` drops scalar changes.** `Attach + IsModified(TagsBundle)` only
  persists the tag bundle. Either rename the method `UpdateTagsAsync`, or:

  ```csharp
  context.Update(entity);
  context.Entry(entity).Property(e => e.TagsBundle).IsModified = true;
  ```

- **`WriteLibraryTags` still diverges from `WriteDanbooruTags`.** It does not raise
  `OnSendTags` (so "Send to Img2Img" from the Library does nothing useful on the
  img2img panel) and skips `Settings.SaveSettings()`. Extract a shared helper that
  takes `IEnumerable<string>`.

- **`_libraryMinScore` still `int` (defaults to 0), "none" still in the rating
  dropdown, no infinite scroll.** Functional parity with Phase 5 step 5.2 not
  reached.

### P2

- **3.C** (orphan file on DB failure), **3.D** (streaming download), **startup
  log for missing `SavedMediaPath`**, **Save-button disable**, **whitespace revert**
  - all still open.

---

## Convention Audit Delta

| Convention                                                  | Round 1  | Round 2           | Notes                                                |
| ----------------------------------------------------------- | -------- | ----------------- | ---------------------------------------------------- |
| All events flow through `EventService`                      | Partial  | **Still Partial** | Publish-only; UI never subscribes. Highest priority. |
| `DanbooruTagBundleConverter` registered                     | Violated | **OK**            | Now used in `OnModelCreating`.                       |
| Blazor CSS isolation per component                          | Violated | **OK**            | Scoped CSS file added.                               |
| Named HTTP client / non-captive registration                | Violated | **Violated**      | Program.cs unchanged.                                |
| `IsModified=true` but still using `Attach` in `UpdateAsync` | Partial  | Partial           | Same state as before.                                |

---

## Verification Suggestions

Before declaring the round-2 fixes shipped, manually verify:

1. **Lazy load on fresh visit.** Open app > Library tab with no prior interaction.
   Expect: spinner briefly, then content. (Today: likely stuck on spinner - R2-2.)
2. **Tag filter with 10+ rows.** Type a substring and hit Filter. Watch the generated
   SQL (`context.Database.Log` or EF logging) to confirm `LIMIT/OFFSET` is still
   emitted when tag search is empty.
3. **Save -> Library refresh.** Save from Search; switch to Library without
   reloading. Expect the new item at the top. (Today: only works because of the
   unconditional `LoadLibrary()` in `SaveToLibrary`, not because of the event bus.)
4. **Delete without disk.** Un-check the "Delete file from disk?" box and delete.
   Confirm DB row gone but file remains on disk. (Now fixed - should work.)
5. **Video save.** Save a large webm (~50 MB). Watch RAM. The whole file is loaded
   into memory before flushing to disk (3.D).

---

## Prioritised Follow-up Plan (Round 2)

### P0

1. Subscribe to `DanbooruMediaSaved/Deleted` via `EventService`, drop the inline
   `LoadLibrary()` calls (bug 5.B).
2. Wrap the lazy-load path in `InvokeAsync` + `StateHasChanged` and reset the
   `_libraryLoaded` flag on error (bug R2-2).
3. Restore SQL-side `Skip/Take` in `GetPagedAsync` when `TagSearch` is empty
   (bug R2-1) and switch rating filter from `EF.Functions.Like` to a normal equality.

### P1

4. Replace captive `HttpClient` registration with
   `AddHttpClient<IDanbooruLibraryService, DanbooruLibraryService>` (bug 3.B).
5. Fix `WriteLibraryTags` to route through `OnSendTags` + call `Settings.SaveSettings()`
   (share a helper with `WriteDanbooruTags`).
6. Tighten `UpdateAsync` (rename or use `context.Update`) (bug 2.C).
7. Change `_libraryMinScore` to `int?`, remove the `"none"` rating option, and add
   `CountAsync` + infinite scroll (or document the scope reduction in the MAIN_PLAN
   changelog).

### P2

8. Orphan-file cleanup on DB failure (3.C).
9. Streaming download (3.D).
10. Startup logging for missing/empty `SavedMediaPath`.
11. Disable Save button while a save is in flight.
12. Revert unrelated whitespace-only diff hunks.
13. Unit tests for `ResolveRatingFolder`, `ResolveScoreBucket`, repository filter
    translation, duplicate-save behaviour.

---

## Appendix - Files Re-Inspected

- [BlazorWebApp/Data/AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs) - converter registered, inline duplicate removed.
- [BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs) - EF translation fixed; new memory-paging bug introduced.
- [BlazorWebApp/Services/DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs) - rating map fixed.
- [BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css) - new.
- [BlazorWebApp/Pages/Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor) - delete checkbox + viewer URL fixed; lazy-load added but not render-safe; event subscription still absent.
- [BlazorWebApp/Program.cs](../../../BlazorWebApp/Program.cs) - unchanged since round 1.
- [BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs) - unchanged; no `CountAsync` added.
