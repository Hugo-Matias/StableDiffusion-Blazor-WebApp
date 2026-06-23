# Danbooru Library - QA Analysis Report

> Scope: Review of the Git working tree against [MAIN_PLAN.md](./MAIN_PLAN.md) and the
> `PHASE_1.md` through `PHASE_7.md` documents. Covers all staged / untracked files that
> implement the plan.

## Summary

The implementation lands roughly 70% of the plan and compiles cleanly, but contains
**several correctness bugs that make the Library tab effectively non-functional at
runtime**, plus multiple deviations from the plan's conventions. The most severe issues
are:

1. [Bug - high] Folder routing always collapses to `none/` because `ResolveRatingFolder`
   compares against full rating names while the DTO surfaces single-letter codes.
2. [Bug - high] `GetPagedAsync` is not translatable by EF Core 6 (JSON-list `Any(...)`
   plus `StringComparison.OrdinalIgnoreCase`). Any filter usage will throw at runtime.
3. [Bug - high] The Library tab never calls `LoadLibrary()` on mount, so it is stuck on
   the spinner until the user manually clicks _Filter_.
4. [Bug - high] `DanbooruTagBundleConverter` is implemented but **not registered**; a
   duplicate inline converter is used instead.
5. [Bug - high] `SavedDanbooruMediaCard` has no scoped `.razor.css`; Blazor CSS isolation
   means it inherits **none** of the styles it visually depends on from
   `DanbooruImageCard.razor.css`.
6. [Bug - medium] Delete dialog checkbox value is ignored - the service is always called
   with `deleteFile: true`.
7. [Bug - medium] Events are published by the service but **never subscribed to** by the
   UI, even though both the plan and the workspace-wide pub/sub convention require it.
8. [Smell - medium] `IDanbooruLibraryService` DI registration is a captive-dependency
   trap that freezes an `HttpClient` for the lifetime of the app (defeats
   `AddHttpClient`'s handler rotation).

The rest of this document walks each phase against the implementation and ends with a
prioritised list of follow-up changes.

---

## Phase-by-Phase Review

### Phase 1 - Config Consolidation

| Plan Step                                                                | Status | Notes                                                                                                                            |
| ------------------------------------------------------------------------ | ------ | -------------------------------------------------------------------------------------------------------------------------------- |
| 1.1 `Models/DanbooruOptions.cs` with `Login`, `ApiKey`, `SavedMediaPath` | OK     | [DanbooruOptions.cs](../../../BlazorWebApp/Models/DanbooruOptions.cs). Added `SectionName` constant, fine.                       |
| 1.2 Nest credentials under `Danbooru` section + bind in `Program.cs`     | OK     | [appsettings.json](../../../BlazorWebApp/appsettings.json) updated; [Program.cs](../../../BlazorWebApp/Program.cs#L28) wires it. |
| 1.3 Refactor `DanbooruService` to `IOptions<DanbooruOptions>`            | OK     | [DanbooruService.cs](../../../BlazorWebApp/Services/DanbooruService.cs). Old config field removed.                               |

**Minor issues**

- Inconsistent qualifier style in `Program.cs`: `using BlazorWebApp.Models;` is added
  but `builder.Services.Configure<BlazorWebApp.Models.DanbooruOptions>(...)` uses the
  fully qualified name. Use the short name for consistency with the rest of the file.

---

### Phase 2 - Persistence Layer

| Plan Step                                                  | Status       | Notes                                                                                                                                  |
| ---------------------------------------------------------- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| 2.1 `SavedDanbooruMedia` entity                            | OK           | [SavedDanbooruMedia.cs](../../../BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs). Good denormalisation, constructor copies from DTO. |
| 2.2 `DanbooruTagBundleConverter` + register + unique index | Partial      | Converter exists but **not used**; AppDbContext registers an inline converter instead.                                                 |
| 2.3 Hand-authored migration + snapshot update              | OK (caveats) | Attributes present; snapshot updated alphabetically.                                                                                   |
| 2.4 `ISavedDanbooruMediaRepository` + implementation       | Partial      | Querying contract is broken (see bug 2 below).                                                                                         |

#### Bug 2.A - `DanbooruTagBundleConverter` defined but unused

[AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs#L174-L176) inlines its own converter:

```csharp
var tagBundleConverter = new ValueConverter<Entities.DanbooruTagBundle, string>(
    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
    v => JsonSerializer.Deserialize<Entities.DanbooruTagBundle>(v, (JsonSerializerOptions)null) ?? new Entities.DanbooruTagBundle());
```

while [DanbooruTagBundleConverter.cs](../../../BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs) is never referenced. The phase plan explicitly said to register the class in `OnModelCreating`.

**Also:** passing `(JsonSerializerOptions)null` triggers `ArgumentNullException` warnings on newer runtimes and differs from the `DefaultIgnoreCondition = WhenWritingNull` behaviour encoded in the dedicated converter (so null tag lists will serialise as `"Tag": null` instead of being omitted). This is a subtle behavioural regression vs. the plan.

**Fix:**

```csharp
// AppDbContext.OnModelCreating
modelBuilder.Entity<SavedDanbooruMedia>()
    .HasIndex(m => m.DanbooruPostId)
    .IsUnique();

modelBuilder.Entity<SavedDanbooruMedia>()
    .Property(m => m.TagsBundle)
    .HasConversion(new DanbooruTagBundleConverter());
```

Also add a `ValueComparer<DanbooruTagBundle>` (mirrors `listStringComparer` already in
the context) so EF change tracking does not see every load as a mutation. Consider:

```csharp
var tagBundleComparer = new ValueComparer<DanbooruTagBundle>(
    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
           == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
    c => JsonSerializer.Deserialize<DanbooruTagBundle>(
             JsonSerializer.Serialize(c, (JsonSerializerOptions?)null),
             (JsonSerializerOptions?)null) ?? new DanbooruTagBundle());

modelBuilder.Entity<SavedDanbooruMedia>()
    .Property(m => m.TagsBundle)
    .HasConversion(new DanbooruTagBundleConverter(), tagBundleComparer);
```

#### Bug 2.B - `GetPagedAsync` is not EF-translatable

[SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L48-L80) uses:

```csharp
m.TagsBundle.General.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
// ...
m.Rating.Equals(filter.Rating, StringComparison.OrdinalIgnoreCase)
```

Both are problematic against SQLite + a JSON-string column:

- `TagsBundle` is stored as a JSON blob via a `ValueConverter`; EF Core 6 cannot
  translate `List<string>.Any(...)` over that materialised column. You will get
  `System.InvalidOperationException: The LINQ expression ... could not be translated.`
- `string.Contains(string, StringComparison)` and `string.Equals(string, StringComparison)`
  are **not** translatable on SQLite (EF Core's well-known limitation, as documented in
  EF warnings `CA1866`/`EF1002`).

Since the JSON column will never be efficiently searchable from SQL, the sane approach
is to (a) keep scalar filters in SQL and (b) apply tag-substring filtering in memory
over the already-filtered page. For very large libraries a dedicated `TagsFlat` text
column + `LIKE` would be appropriate, but the original plan says "client-side filtering
fine for expected library size".

**Fix sketch:**

```csharp
public async Task<List<SavedDanbooruMedia>> GetPagedAsync(
    SavedDanbooruMediaFilter filter,
    CancellationToken cancellationToken = default)
{
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

    var query = context.SavedDanbooruMedia.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(filter.Rating))
    {
        var rating = filter.Rating.ToLowerInvariant();
        query = query.Where(m => m.Rating.ToLower() == rating);
    }

    if (filter.MinScore.HasValue)
        query = query.Where(m => m.Score >= filter.MinScore.Value);

    if (filter.VideoOnly == true)
        query = query.Where(m => m.IsVideo);

    query = query.OrderByDescending(m => m.DateCreated);

    // Tag search is JSON-backed: evaluate in memory. Take a generous window first,
    // then filter + page. Assumes "expected library size" per the plan.
    if (!string.IsNullOrWhiteSpace(filter.TagSearch))
    {
        var search = filter.TagSearch.Trim();
        var buffer = await query.ToListAsync(cancellationToken);
        return buffer
            .Where(m => ContainsTag(m.TagsBundle, search))
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToList();
    }

    return await query
        .Skip(filter.Skip)
        .Take(filter.Take)
        .ToListAsync(cancellationToken);

    static bool ContainsTag(DanbooruTagBundle b, string needle)
        => b.General.Concat(b.Artist).Concat(b.Character)
            .Concat(b.Copyright).Concat(b.Meta)
            .Any(t => t.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
```

You should also add a `CountAsync` overload - Phase 5 mentions it and infinite-scroll
needs it for the `hasMore` flag.

#### Bug 2.C - `UpdateAsync` only persists `TagsBundle`

```csharp
context.Attach(entity);
context.Entry(entity).Property(e => e.TagsBundle).IsModified = true;
await context.SaveChangesAsync(cancellationToken);
```

After `Attach`, state is `Unchanged`. Marking a single property modified persists
exactly that property. Any callers expecting scalar updates (e.g., bulk retagging) will
silently lose them. Either:

- document the method as "tag-only update", rename it `UpdateTagsAsync`, or
- do `context.Update(entity)` + `context.Entry(entity).Property(e => e.TagsBundle).IsModified = true`
  (the latter mirrors `JobRepository.UpdateAsync` and the project's standing convention).

#### Minor - Migration timestamp is synthetic

`20260424000000` looks like a placeholder, not a minute-precise timestamp. Non-blocking,
but the convention recorded in the `copilot-instructions.md` is a real `YYYYMMDDhhmmss`
value. Prefer `20260424120000` or similar at commit time.

---

### Phase 3 - Library Service

| Plan Step                                         | Status     | Notes                                                                                                                                                                                             |
| ------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 3.1 Event args                                    | OK (style) | Implemented but with `get; set;` instead of the immutable `get;` used by siblings like `DownloadCompletedEventArgs`.                                                                              |
| 3.2 Service + path helpers                        | Partial    | Path helpers live on the service class as `internal static` rather than in a dedicated `DanbooruLibraryPaths` helper. Acceptable deviation, but rating resolution is **incorrect** (see bug 3.A). |
| 3.3 DI registration + directory creation + events | Partial    | DI registration is structurally wrong (see bug 3.B); events are raised, but the UI never subscribes.                                                                                              |

#### Bug 3.A - Rating folder is always `none`

[DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs#L143-L153):

```csharp
internal static string ResolveRatingFolder(string? rating)
{
    return rating?.ToLowerInvariant() switch
    {
        "general" => "general",
        "sensitive" => "sensitive",
        "questionable" => "questionable",
        "explicit" => "explicit",
        _ => "none"
    };
}
```

The Danbooru v2 API (and therefore `DanbooruPost.Rating` - see
[DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor) `SetBadgeRating()`)
returns the single-letter code `g`, `s`, `q`, `e`. Full names are never returned, so
every saved post routes to `none/score_*/...`. This is the highest-impact correctness
bug: folder layout and future filtering by rating-folder are both silently broken.

**Fix:**

```csharp
internal static string ResolveRatingFolder(string? rating) => rating?.ToLowerInvariant() switch
{
    "g" or "general" => "general",
    "s" or "sensitive" => "sensitive",
    "q" or "questionable" => "questionable",
    "e" or "explicit" => "explicit",
    _ => "none",
};
```

Add a unit test in `BlazorWebApp.Tests` covering the four known codes + `null`.

#### Bug 3.B - Captive `HttpClient` via DI misuse

[Program.cs](../../../BlazorWebApp/Program.cs#L95-L99):

```csharp
builder.Services.AddHttpClient<DanbooruLibraryService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<IDanbooruLibraryService>(
    sp => sp.GetRequiredService<DanbooruLibraryService>());
```

`AddHttpClient<T>` registers `T` as **transient**. The singleton factory is invoked
**once**, resolves one transient instance, and caches it forever - which is precisely
the captive-dependency problem `AddHttpClient` exists to avoid. DNS rotation and
handler-pool recycling are defeated. Worse, because the service is singleton, calling
any `IDbContextFactory`-backed repository through it is fine, but subscribers that are
scoped (not in this case, but a foot-gun for future consumers) would leak.

**Fix - pick one:**

Option A (idiomatic `AddHttpClient`):

```csharp
builder.Services.AddHttpClient<IDanbooruLibraryService, DanbooruLibraryService>(c =>
{
    c.Timeout = TimeSpan.FromMinutes(5);
});
```

Transient, proper handler rotation. Service has no instance state worth sharing.

Option B (named client + `IHttpClientFactory`):

```csharp
builder.Services.AddHttpClient("danbooru-download", c => c.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddSingleton<IDanbooruLibraryService, DanbooruLibraryService>();
```

and change the service to inject `IHttpClientFactory` and call
`_httpFactory.CreateClient("danbooru-download")` per request. This matches the Phase 3
plan verbatim ("`IHttpClientFactory` (named client `"danbooru-download"`)") and avoids
the captive handler.

#### Bug 3.C - No orphan-file cleanup on DB failure

```csharp
var content = await _httpClient.GetByteArrayAsync(post.Url, cancellationToken);
await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

var entity = new SavedDanbooruMedia(post) { FilePath = relativePath };
await _repository.AddAsync(entity, cancellationToken);
```

If `AddAsync` throws (e.g., unique-index race, DB lock), the file stays on disk and the
user has no indication. Wrap the write+insert in try/catch and delete the file on
failure, or insert first and then download (with a rollback delete on write failure).

#### Bug 3.D - Full in-memory buffer for large files

`GetByteArrayAsync` + `WriteAllBytesAsync` pulls the entire video into memory. The plan
acknowledges "Save both image and video original; size trade-off accepted", but for
multi-hundred-MB videos this becomes a real memory spike. Swap to a streaming copy:

```csharp
using var response = await _httpClient.GetAsync(post.Url,
    HttpCompletionOption.ResponseHeadersRead, cancellationToken);
response.EnsureSuccessStatusCode();
await using var http = await response.Content.ReadAsStreamAsync(cancellationToken);
await using var file = File.Create(fullPath);
await http.CopyToAsync(file, cancellationToken);
```

#### Minor - Empty extension on malformed posts

`$"{post.Id}.{post.Extension}"` produces `12345.` when `post.Extension` is null/empty.
Add a fallback (`?? "bin"`) or reject the save.

---

### Phase 4 - Save UX

| Plan Step                                    | Status | Notes                                                                                                                 |
| -------------------------------------------- | ------ | --------------------------------------------------------------------------------------------------------------------- |
| 4.1 `OnSave` callback on `DanbooruImageCard` | OK     | [DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor) adds a bookmark button. |
| 4.2 Wire `SaveAsync` + snackbars             | OK     | [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L333-L349) handles all three result branches.             |

**Minor issues**

- The page does not disable the Save button during the download ("Disable button
  briefly during download" in the plan). Concurrent clicks on the same card will race -
  the first one wins via the unique index, but the user sees an "already saved" toast
  that is misleading during the first save. Consider a per-card `Saving` flag.
- The diff reformatted several unrelated Razor blocks into single-line / oddly
  word-wrapped attribute lists (see the monstrous attribute spread on
  `SavedDanbooruMediaCard` in the Library panel). Looks like an auto-formatter run on
  the whole file - worth a follow-up _whitespace-only_ revert of unrelated hunks to
  keep the diff reviewable.

---

### Phase 5 - Library Tab UI

| Plan Step                                                       | Status  | Notes                                                                                                                                        |
| --------------------------------------------------------------- | ------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| 5.1 `SavedDanbooruMediaCard` component + scoped CSS             | Partial | Component exists; **no `.razor.css` was created** (bug 5.A).                                                                                 |
| 5.2 Replace placeholder with topbar + masonry + infinite scroll | Partial | Topbar + masonry present; **no infinite scroll**, **load-on-mount missing**, filter controls wired but page-state model mismatches the plan. |
| 5.3 Delete flow + live event subscription                       | Broken  | Checkbox value ignored + **event subscription missing entirely** (bug 5.B).                                                                  |

#### Bug 5.A - Missing scoped CSS for `SavedDanbooruMediaCard`

[SavedDanbooruMediaCard.razor](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor) reuses class names from its sibling - `card-image`, `card-media`, `hover-overlay`, `overlay-top`, `overlay-center`, `overlay-bottom`, `quick-actions`, `action-btn`, `rating-badge`, `rating-indicator`, `info-row`, `info-item`, etc.

But Blazor CSS isolation scopes [DanbooruImageCard.razor.css](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor.css) to `DanbooruImageCard` **only** (via `b-<hash>` attributes). The library card renders visually unstyled: no overlay, no hover animations, no rating badge colours, no size constraints.

**Fix:** create `BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css`
that either (a) copies the relevant rules from the sibling (low-effort, matches Phase 5
step 5.1 verbatim) or (b) promotes the shared rules into a _non-scoped_ file under
`wwwroot/css/danbooru-card.css` and references it from `_Host.cshtml` / `App.razor`. A
quick diff-minimising fix today is option (a):

```css
/* SavedDanbooruMediaCard.razor.css */
@import url("DanbooruImageCard.razor.css"); /* does NOT work - CSS isolation rewrites the selectors per file */
```

That import trick is a trap - Blazor's scoping runs per-file, so importing the sibling
CSS still produces unscoped selectors that won't match either card. You must either
duplicate the styles or hoist them to a non-isolated stylesheet.

#### Bug 5.B - No event subscription + wrong refresh strategy

The plan (5.3) and the workspace convention both require subscribing to
`DanbooruMediaSavedEventArgs` / `DanbooruMediaDeletedEventArgs` via `EventService` so
the Library tab refreshes without direct coupling to the Save button.

Current implementation:

- [`Danbooru.razor` line 8](../../../BlazorWebApp/Pages/Danbooru.razor#L8) injects
  `IEventService EventService` but **never** calls `Subscribe<T>` or `Unsubscribe<T>`.
- `SaveToLibrary` (Search tab) instead calls `await LoadLibrary()` directly after a
  successful save, forcing a DB hit on the **Search** tab even when the user never
  opens the Library tab.
- `DeleteFromLibrary` calls `await LoadLibrary()` in the same way.

**Fix:** follow the plan's Step 5.3 code sketch verbatim. In `OnAfterRenderAsync(firstRender: true)`:

```csharp
EventService.Subscribe<DanbooruMediaSavedEventArgs>(OnMediaSaved);
EventService.Subscribe<DanbooruMediaDeletedEventArgs>(OnMediaDeleted);

async void OnMediaSaved(DanbooruMediaSavedEventArgs args)
    => await InvokeAsync(() =>
    {
        _libraryItems.Insert(0, args.Media);
        StateHasChanged();
    });

async void OnMediaDeleted(DanbooruMediaDeletedEventArgs args)
    => await InvokeAsync(() =>
    {
        _libraryItems.RemoveAll(m => m.Id == args.Id);
        StateHasChanged();
    });
```

and unsubscribe both in `DisposeAsync`. Then drop the unconditional `LoadLibrary()`
calls from `SaveToLibrary` / `DeleteFromLibrary`.

#### Bug 5.C - Library never loads on first tab open

[Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L160):

```csharp
private bool _libraryLoading = true;
private List<SavedDanbooruMedia> _libraryItems = new();
```

No `OnInitializedAsync` call to `LoadLibrary`, no lazy trigger on tab switch. Because
`_libraryLoading` starts at `true`, the Library panel shows the "Loading library..."
spinner indefinitely on first visit until the user clicks _Filter_ or saves an item.

**Fix:** Initialise `_libraryLoading = false`, and either:

- call `LoadLibrary()` in `OnInitializedAsync`, or
- watch `State.State.Danbooru.ActiveTabIndex` for the tab index change and call
  `LoadLibrary` lazily the first time it matches the Library tab.

The plan (Step 5.2 _Initialize the library scroll module only when the Library tab
becomes active_) prefers the lazy approach.

#### Bug 5.D - Delete dialog ignores the "Delete file from disk?" checkbox

[Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L404-L418):

```csharp
var result = await dialog.Result;
if (result is not null && !result.Cancelled)
{
    await LibraryService.DeleteAsync(media.Id, true); // <-- hardcoded
    Snackbar.Add("Deleted from library", Severity.Info);
    await LoadLibrary();
}
```

`ConfirmationDialog` returns `DialogResult.Ok(<bool>)` when `CheckboxEnabled == true`
(see `Pages/Resources.razor` lines 185-205). Here the result is ignored and **every**
delete removes the file from disk. This contradicts both Step 5.3 ("Read
`bool deleteFile = (bool)result.Data;`") and the main-plan decision ("Delete files from
disk? checkbox").

**Fix:**

```csharp
var res = await dialog.Result;
if (res is null || res.Cancelled) return;
var deleteFile = res.Data is bool b && b;
await LibraryService.DeleteAsync(media.Id, deleteFile);
Snackbar.Add(deleteFile
    ? "Item removed from library and disk."
    : "Item removed from library.",
    Severity.Success);
```

#### Bug 5.E - `ShowLibraryViewer` points the viewer at remote Danbooru URLs

```csharp
_viewerImages = _libraryItems.Select(m => new Image
{
    Path = m.SampleUrl, // Danbooru CDN URL
    ...
}).ToList();
```

The entire point of saving is local playback/browsing. The viewer should load from
`/files/danbooru/{FilePath}` so the library works offline and doesn't re-hit
Danbooru's CDN. Change to:

```csharp
Path = $"/files/danbooru/{m.FilePath}",
```

(Confirm `ImageViewer` with `IsExternal="true"` treats that as a web-accessible URL;
if it does not, add a new flag or reuse whatever scheme `ResourcesService` uses for the
`/files/...` preview mappings.)

#### Other Phase 5 deviations

- **No infinite scroll.** The plan called for reusing `DanbooruMasonry.js`; the
  implementation hardcodes `Take = 100` and stops. No `LoadLibraryNextPageAsync`, no
  `[JSInvokable]` hook. Either document the scope reduction in the MAIN_PLAN changelog
  or land it in Phase 7.
- **`_libraryMinScore` is `int`, not `int?`.** Defaults to `0`, so `MinScore` is always
  applied as a no-op filter. Should be `int?` so an empty field means "no filter".
- **Rating filter options** list `"none"` as a value - but in the DB `Rating` is `"g"`,
  `"s"`, `"q"`, `"e"`, or possibly empty. There is no `"none"` value to match; the
  option is dead.
- **`WriteLibraryTags` diverges from `WriteDanbooruTags`.** It drops the `OnSendTags`
  external callback plumbing, ignores the Img2Img prompt fragment, and skips the
  `Settings.SaveSettings()` call. Plan said "reuse the existing `WriteDanbooruTags`
  helper, adapted to accept a `DanbooruTagBundle`". Prefer an overload:

  ```csharp
  private void WriteDanbooruTags(IEnumerable<string> tags, bool isImg2Img) { /* shared body */ }

  private void WriteDanbooruTags(DanbooruPost post, bool isImg2Img)
      => WriteDanbooruTags(post.Tags ?? Enumerable.Empty<string>(), isImg2Img);

  private void WriteDanbooruTags(SavedDanbooruMedia media, bool isImg2Img)
      => WriteDanbooruTags(
             media.TagsBundle.Artist
                 .Concat(media.TagsBundle.Character)
                 .Concat(media.TagsBundle.General),
             isImg2Img);
  ```

  Today the library "Send to Img2Img" button does nothing useful because the Img2Img
  branch of `WriteDanbooruTags` (`OnSendTags.InvokeAsync((tagsString, isImg2Img))`) is
  missing from `WriteLibraryTags`.

---

### Phase 6 - Static File Serving

| Plan Step                                 | Status  | Notes                                                                                         |
| ----------------------------------------- | ------- | --------------------------------------------------------------------------------------------- |
| 6.1 `UseStaticFiles` at `/files/danbooru` | OK      | [Program.cs](../../../BlazorWebApp/Program.cs#L207-L216) guards on empty/missing path.        |
| 6.2 Cards render from mapped path         | Partial | Library cards use `/files/danbooru/@Media.FilePath`, but `ImageViewer` doesn't (see bug 5.E). |

**Minor issues**

- The guard logs nothing when `SavedMediaPath` is empty/missing. The success criterion
  says "logs a clear warning if unconfigured". Add a single `app.Logger.LogWarning(...)`
  branch:

  ```csharp
  if (string.IsNullOrWhiteSpace(danbooruOptions.SavedMediaPath))
  {
      app.Logger.LogWarning("DanbooruOptions.SavedMediaPath is not configured; /files/danbooru will not be served.");
  }
  else if (!Directory.Exists(danbooruOptions.SavedMediaPath))
  {
      app.Logger.LogWarning("DanbooruOptions.SavedMediaPath '{Path}' does not exist; /files/danbooru will not be served.", danbooruOptions.SavedMediaPath);
  }
  else
  {
      app.UseStaticFiles(new StaticFileOptions { ... });
  }
  ```

---

### Phase 7 - Polish & Edge Cases

| Plan Step                              | Status   | Notes                                                               |
| -------------------------------------- | -------- | ------------------------------------------------------------------- |
| 7.1 Video hover-play in Library cards  | OK       | `SavedDanbooruMediaCard` imports `VideoCard.js` like the sibling.   |
| 7.2 Missing URL / oversized video path | Partial  | `Url` is null-checked in `SaveAsync`; no oversized handling.        |
| 7.3 Performance pass                   | Not done | Hardcoded `Take = 100`, no `CountAsync`, no server-side pagination. |

---

## Convention Audit

| Convention                                                          | Status   | Notes                                                                                                                                                                                                |
| ------------------------------------------------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| All events flow through `EventService`                              | Partial  | Service publishes correctly; **UI never subscribes** - bug 5.B.                                                                                                                                      |
| `IsModified = true` on JSON columns for in-place mutations          | Partial  | Applied on `TagsBundle` inside `UpdateAsync`, but `UpdateAsync` also drops scalar updates (bug 2.C).                                                                                                 |
| Hand-authored migration attributes                                  | OK       | `[DbContext]` + `[Migration]` both present.                                                                                                                                                          |
| Snapshot updated alphabetically                                     | OK       | `SavedDanbooruMedia` inserted between `SchedulerDraft` and `Selection`.                                                                                                                              |
| Event-arg classes live under `BlazorWebApp/Events/`                 | OK       | Both classes added there.                                                                                                                                                                            |
| Event-arg properties `get;` (immutable) like neighbours             | Drifted  | Siblings (`DownloadCompletedEventArgs`, `DanbooruMediaSavedEventArgs`) mix styles - low-priority cleanup.                                                                                            |
| Blazor CSS isolation for per-component styles                       | Violated | `SavedDanbooruMediaCard` has no `.razor.css` - bug 5.A.                                                                                                                                              |
| Plan's `DanbooruTagBundleConverter` registered in `OnModelCreating` | Violated | Inline duplicate used instead - bug 2.A.                                                                                                                                                             |
| Named HTTP client `"danbooru-download"`                             | Violated | Typed client with `AddSingleton` shim used instead - bug 3.B.                                                                                                                                        |
| `OnSendTags`/`OnShowImage` callback pattern on new cards            | Drifted  | `SavedDanbooruMediaCard` splits `OnSendTags` into `OnCopyTags` + `OnSendImg2Img`, losing parity with the plan's proposed `EventCallback<bool>` signature. Non-blocking, but harder to share helpers. |

---

## Prioritised Follow-up Plan

### P0 - runtime correctness (blocker for merge)

1. Fix `ResolveRatingFolder` to accept single-letter codes (bug 3.A).
2. Replace the non-translatable `GetPagedAsync` LINQ with SQL-safe filters + in-memory
   tag matching (bug 2.B). Add `CountAsync`.
3. Call `LoadLibrary()` on mount (or on first tab activation) and flip `_libraryLoading`
   default to `false` (bug 5.C).
4. Honour the delete-dialog checkbox (bug 5.D).
5. Register `DanbooruTagBundleConverter` + a `ValueComparer`, delete the inline
   duplicate (bug 2.A).
6. Ship `SavedDanbooruMediaCard.razor.css` (or hoist shared styles) so the Library
   cards render (bug 5.A).

### P1 - plan adherence & architecture

7. Subscribe to `DanbooruMediaSaved/Deleted` via `EventService` and remove direct
   `LoadLibrary()` calls from Save/Delete handlers (bug 5.B, workspace convention).
8. Replace the captive-`HttpClient` registration with either a typed interface overload
   or an `IHttpClientFactory` named client (bug 3.B).
9. Point `ShowLibraryViewer` at `/files/danbooru/{FilePath}` (bug 5.E).
10. Fix `WriteLibraryTags` to route through `OnSendTags` / `Settings.SaveSettings()`
    exactly like `WriteDanbooruTags`.
11. Tighten `UpdateAsync` (rename, or use `context.Update(entity)` so scalar updates
    persist).

### P2 - robustness & polish

12. Stream downloads instead of buffering (bug 3.D).
13. Clean up orphan files on DB insert failure (bug 3.C).
14. Disable the Search-tab Save button while a save is in flight.
15. Add startup logging for missing/empty `SavedMediaPath`.
16. Implement infinite scroll in the Library tab (`Take` / `Skip` cursor + masonry JS
    integration).
17. Revert whitespace-only reformatting in `DanbooruImageCard.razor` and the Danbooru
    Search panel so the PR diff stays scoped to behavioural changes.
18. Add unit tests for: rating resolution, score buckets, filter translation, duplicate
    save behaviour.

### P3 - post-merge hardening

19. Consider a `TagsFlat` shadow column (space-separated) with a plain `LIKE` to make
    tag search scalable if the library grows beyond a few thousand rows.
20. Audit the `ImageViewer` contract to ensure local `/files/...` URLs are first-class
    (may need a small extension to the viewer or a new `LocalPath` property on the
    model it consumes).

---

## Appendix - Files Reviewed

Modified (6):

- [BlazorWebApp/Components/Resources/DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor)
- [BlazorWebApp/Data/AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs)
- [BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs](../../../BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs)
- [BlazorWebApp/Pages/Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor)
- [BlazorWebApp/Program.cs](../../../BlazorWebApp/Program.cs)
- [BlazorWebApp/Services/DanbooruService.cs](../../../BlazorWebApp/Services/DanbooruService.cs)

Added (12):

- [BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor)
- [BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs](../../../BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs)
- [BlazorWebApp/Data/Entities/DanbooruTagBundle.cs](../../../BlazorWebApp/Data/Entities/DanbooruTagBundle.cs)
- [BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs](../../../BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs)
- [BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs)
- [BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs)
- [BlazorWebApp/Events/DanbooruMediaDeletedEventArgs.cs](../../../BlazorWebApp/Events/DanbooruMediaDeletedEventArgs.cs)
- [BlazorWebApp/Events/DanbooruMediaSavedEventArgs.cs](../../../BlazorWebApp/Events/DanbooruMediaSavedEventArgs.cs)
- [BlazorWebApp/Migrations/20260424000000_Add_SavedDanbooruMedia.cs](../../../BlazorWebApp/Migrations/20260424000000_Add_SavedDanbooruMedia.cs)
- [BlazorWebApp/Models/DanbooruOptions.cs](../../../BlazorWebApp/Models/DanbooruOptions.cs)
- [BlazorWebApp/Services/DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs)
- [BlazorWebApp/Services/IDanbooruLibraryService.cs](../../../BlazorWebApp/Services/IDanbooruLibraryService.cs)

Missing (expected by the plan):

- `BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css`
- `BlazorWebApp/Services/DanbooruLibraryPaths.cs` (or equivalent - helper methods were
  inlined on the service instead)
